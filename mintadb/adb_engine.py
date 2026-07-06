"""ADB engine — subprocess wrapper with device monitoring."""

from __future__ import annotations

import os
import re
import shlex
import shutil
import subprocess
import threading
from dataclasses import dataclass, field
from typing import Callable, Iterator, Optional

# Android package/component names only ever contain these characters.
# Anything else is rejected to prevent shell command injection when values
# are interpolated into `adb shell` command strings.
_PACKAGE_RE = re.compile(r"^[A-Za-z][A-Za-z0-9_.]*(/[A-Za-z0-9_.$]+)?$")  # pkg or pkg/component


def is_valid_package(package: str) -> bool:
    """Return True if *package* is a safe Android package/component name."""
    return bool(_PACKAGE_RE.match(package))


@dataclass
class Device:
    serial: str
    state: str
    product: str = ""
    model: str = ""
    device: str = ""
    transport_id: str = ""

    @property
    def label(self) -> str:
        name = self.model or self.product or self.device or self.serial
        return f"{name} ({self.serial})"

    @property
    def online(self) -> str:
        return self.state == "device"


@dataclass
class DeviceInfo:
    serial: str
    model: str = ""
    manufacturer: str = ""
    android_version: str = ""
    sdk: str = ""
    build: str = ""
    battery_level: str = ""
    battery_status: str = ""
    ip_address: str = ""
    screen_size: str = ""
    density: str = ""
    abi: str = ""
    security_patch: str = ""


def find_adb() -> str:
    env = os.environ.get("ADB_PATH") or os.environ.get("ANDROID_ADB")
    if env and os.path.isfile(env):
        return env
    found = shutil.which("adb")
    if found:
        return found
    candidates = [
        r"D:\Miui\platform-tools\adb.exe",
        os.path.expandvars(r"%LOCALAPPDATA%\Android\Sdk\platform-tools\adb.exe"),
        os.path.expandvars(r"%USERPROFILE%\AppData\Local\Android\Sdk\platform-tools\adb.exe"),
    ]
    for path in candidates:
        if os.path.isfile(path):
            return path
    return "adb"


class AdbEngine:
    def __init__(self, adb_path: Optional[str] = None) -> None:
        self.adb_path = adb_path or find_adb()
        self._lock = threading.Lock()

    def run(
        self,
        args: list[str],
        serial: Optional[str] = None,
        timeout: Optional[float] = 120,
        text: bool = True,
    ) -> subprocess.CompletedProcess:
        cmd = [self.adb_path]
        if serial:
            cmd.extend(["-s", serial])
        cmd.extend(args)
        with self._lock:
            return subprocess.run(
                cmd,
                capture_output=True,
                text=text,
                timeout=timeout,
                creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0,
            )

    def run_shell(
        self,
        command: str,
        serial: Optional[str] = None,
        timeout: Optional[float] = 120,
    ) -> tuple[int, str, str]:
        proc = self.run(["shell", command], serial=serial, timeout=timeout)
        return proc.returncode, proc.stdout or "", proc.stderr or ""

    def stream(
        self,
        args: list[str],
        serial: Optional[str] = None,
        on_line: Optional[Callable[[str], None]] = None,
        stop_event: Optional[threading.Event] = None,
    ) -> Iterator[str]:
        cmd = [self.adb_path]
        if serial:
            cmd.extend(["-s", serial])
        cmd.extend(args)
        proc = subprocess.Popen(
            cmd,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            text=True,
            bufsize=1,
            creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0,
        )
        try:
            assert proc.stdout is not None
            for line in proc.stdout:
                if stop_event and stop_event.is_set():
                    break
                line = line.rstrip("\n\r")
                if on_line:
                    on_line(line)
                yield line
        finally:
            proc.terminate()
            try:
                proc.wait(timeout=2)
            except subprocess.TimeoutExpired:
                proc.kill()

    def list_devices(self) -> list[Device]:
        proc = self.run(["devices", "-l"])
        devices: list[Device] = []
        for line in (proc.stdout or "").splitlines():
            line = line.strip()
            if not line or line.startswith("List of"):
                continue
            parts = line.split()
            if len(parts) < 2:
                continue
            serial, state = parts[0], parts[1]
            extras = {}
            for token in parts[2:]:
                if ":" in token:
                    k, v = token.split(":", 1)
                    extras[k] = v
            devices.append(
                Device(
                    serial=serial,
                    state=state,
                    product=extras.get("product", ""),
                    model=extras.get("model", ""),
                    device=extras.get("device", ""),
                    transport_id=extras.get("transport_id", ""),
                )
            )
        return devices

    def get_prop(self, serial: str, prop: str) -> str:
        _, out, _ = self.run_shell(f"getprop {prop}", serial=serial, timeout=10)
        return out.strip()

    def get_device_info(self, serial: str) -> DeviceInfo:
        info = DeviceInfo(serial=serial)
        info.model = self.get_prop(serial, "ro.product.model")
        info.manufacturer = self.get_prop(serial, "ro.product.manufacturer")
        info.android_version = self.get_prop(serial, "ro.build.version.release")
        info.sdk = self.get_prop(serial, "ro.build.version.sdk")
        info.build = self.get_prop(serial, "ro.build.display.id")
        info.security_patch = self.get_prop(serial, "ro.build.version.security_patch")
        info.abi = self.get_prop(serial, "ro.product.cpu.abi")

        _, battery, _ = self.run_shell("dumpsys battery", serial=serial, timeout=10)
        for line in battery.splitlines():
            line = line.strip()
            if line.startswith("level:"):
                info.battery_level = line.split(":", 1)[1].strip() + "%"
            elif line.startswith("status:"):
                codes = {"2": "Charging", "3": "Discharging", "5": "Full"}
                code = line.split(":", 1)[1].strip()
                info.battery_status = codes.get(code, code)

        _, ip_out, _ = self.run_shell(
            "ip -f inet addr show wlan0 2>/dev/null | grep inet | awk '{print $2}' | cut -d/ -f1",
            serial=serial,
            timeout=10,
        )
        info.ip_address = ip_out.strip()

        _, wm, _ = self.run_shell("wm size", serial=serial, timeout=10)
        m = re.search(r"Physical size:\s*(\S+)", wm)
        if m:
            info.screen_size = m.group(1)

        _, density, _ = self.run_shell("wm density", serial=serial, timeout=10)
        m = re.search(r"Physical density:\s*(\d+)", density)
        if m:
            info.density = m.group(1)

        return info

    def reboot(self, serial: str, mode: str = "") -> tuple[bool, str]:
        args = ["reboot"] + ([mode] if mode else [])
        proc = self.run(args, serial=serial, timeout=15)
        msg = (proc.stdout or "") + (proc.stderr or "")
        return proc.returncode == 0, msg.strip()

    def install_apk(self, serial: str, path: str, reinstall: bool = False) -> tuple[bool, str]:
        args = ["install"]
        if reinstall:
            args.append("-r")
        args.append(path)
        proc = self.run(args, serial=serial, timeout=300)
        out = ((proc.stdout or "") + (proc.stderr or "")).strip()
        return "Success" in out, out

    def uninstall(self, serial: str, package: str, keep_data: bool = False) -> tuple[bool, str]:
        args = ["uninstall"]
        if keep_data:
            args.append("-k")
        args.append(package)
        proc = self.run(args, serial=serial, timeout=60)
        out = ((proc.stdout or "") + (proc.stderr or "")).strip()
        return "Success" in out, out

    def push(self, serial: str, local: str, remote: str) -> tuple[bool, str]:
        proc = self.run(["push", local, remote], serial=serial, timeout=600)
        out = ((proc.stdout or "") + (proc.stderr or "")).strip()
        return proc.returncode == 0, out

    def pull(self, serial: str, remote: str, local: str) -> tuple[bool, str]:
        proc = self.run(["pull", remote, local], serial=serial, timeout=600)
        out = ((proc.stdout or "") + (proc.stderr or "")).strip()
        return proc.returncode == 0, out

    def list_dir(self, serial: str, path: str) -> list[tuple[str, str]]:
        _, out, _ = self.run_shell(f'ls -la "{path}"', serial=serial, timeout=30)
        entries: list[tuple[str, str]] = []
        for line in out.splitlines():
            if line.startswith("total") or not line.strip():
                continue
            parts = line.split(None, 8)
            if len(parts) < 9:
                continue
            perm, name = parts[0], parts[8]
            kind = "dir" if perm.startswith("d") else "file"
            if name in (".", ".."):
                continue
            entries.append((name, kind))
        return entries

    def list_packages(self, serial: str, filter_text: str = "") -> list[str]:
        _, out, _ = self.run_shell("pm list packages", serial=serial, timeout=60)
        packages = []
        for line in out.splitlines():
            if line.startswith("package:"):
                packages.append(line.split(":", 1)[1].strip())
        needle = filter_text.strip().lower()
        if needle:
            packages = [p for p in packages if needle in p.lower()]
        return sorted(packages)

    def screencap(self, serial: str, local_path: str) -> tuple[bool, str]:
        remote = "/sdcard/mintadb_cap.png"
        code, _, err = self.run_shell(f"screencap -p {remote}", serial=serial, timeout=30)
        if code != 0:
            return False, err
        ok, msg = self.pull(serial, remote, local_path)
        self.run_shell(f"rm {remote}", serial=serial, timeout=10)
        return ok, msg

    def tcpip(self, serial: str, port: int = 5555) -> tuple[bool, str]:
        proc = self.run(["tcpip", str(port)], serial=serial, timeout=15)
        out = ((proc.stdout or "") + (proc.stderr or "")).strip()
        return proc.returncode == 0, out

    def connect(self, address: str) -> tuple[bool, str]:
        proc = self.run(["connect", address], timeout=15)
        out = ((proc.stdout or "") + (proc.stderr or "")).strip()
        return "connected" in out.lower() or "already" in out.lower(), out

    def disconnect(self, address: str = "") -> tuple[bool, str]:
        args = ["disconnect"] + ([address] if address else [])
        proc = self.run(args, timeout=15)
        out = ((proc.stdout or "") + (proc.stderr or "")).strip()
        return proc.returncode == 0, out

    def forward(self, serial: str, local: str, remote: str) -> tuple[bool, str]:
        proc = self.run(["forward", local, remote], serial=serial, timeout=15)
        out = ((proc.stdout or "") + (proc.stderr or "")).strip()
        return proc.returncode == 0, out

    def list_forwards(self, serial: str) -> str:
        proc = self.run(["forward", "--list"], serial=serial, timeout=15)
        return (proc.stdout or "").strip()

    def clear_app_data(self, serial: str, package: str) -> tuple[bool, str]:
        _, out, err = self.run_shell(f"pm clear {package}", serial=serial, timeout=30)
        msg = (out + err).strip()
        return "Success" in msg, msg

    def force_stop(self, serial: str, package: str) -> tuple[bool, str]:
        _, out, err = self.run_shell(f"am force-stop {package}", serial=serial, timeout=15)
        return True, (out + err).strip()

    def start_activity(self, serial: str, component: str) -> tuple[bool, str]:
        _, out, err = self.run_shell(f"am start -n {component}", serial=serial, timeout=15)
        msg = (out + err).strip()
        return "Error" not in msg, msg

    def input_text(self, serial: str, text: str) -> tuple[bool, str]:
        # `input text` interprets %s as a space; shlex.quote then makes the
        # argument safe against shell metacharacters.
        arg = shlex.quote(text.replace(" ", "%s"))
        _, out, err = self.run_shell(f"input text {arg}", serial=serial, timeout=15)
        return True, (out + err).strip()

    def input_keyevent(self, serial: str, keycode: str) -> tuple[bool, str]:
        if not re.fullmatch(r"\d+|KEYCODE_[A-Z0-9_]+", keycode.strip()):
            return False, f"invalid keycode: {keycode}"
        _, out, err = self.run_shell(
            f"input keyevent {keycode.strip()}", serial=serial, timeout=15
        )
        return True, (out + err).strip()

    def get_logcat(self, serial: str, args: str = "") -> str:
        proc = self.run(["logcat", "-d", "-t", "200"] + (args.split() if args else []), serial=serial, timeout=30)
        return (proc.stdout or "").strip()