"""Xiaomi China ROM optimizer — global-like experience via ADB."""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Callable, Optional

from mintadb.adb_engine import AdbEngine

LogFn = Callable[[str], None]

# MIUI system settings keys — append package names (comma-separated)
MIUI_PKG_WHITELISTS = [
    "rt_pkg_white_list",
    "power_pkg_white_list",
    "power_alarm_white_list",
    "power_broadcast_white_list",
    "perf_proc_protect_list",
    "frozen_new_whitelist",
    "doze_whitelist_apps",
    "cluster_whitelist",
    "msystem_whitelist",
    "battery_optimization_whitelist_apps",
]

# Global Android + MIUI restrictions to relax
GLOBAL_RELAX_SETTINGS: list[tuple[str, str, str]] = [
    ("global", "app_standby_enabled", "0"),
    ("secure", "adaptive_battery_management_enabled", "0"),
    ("global", "power_supersave_mode_enabled", "0"),
    ("global", "notification_listener_timeout", "0"),
    ("global", "miui_restricted_mode_enabled", "0"),
    ("global", "cached_apps_freezer_enabled", "0"),
    ("global", "app_hibernation_enabled", "0"),
]

# Xiaomi cloud / kill-protection overrides (China ROM)
CHINA_UNLOCK_SETTINGS: list[tuple[str, str, str]] = [
    ("global", "tn_disable_cloud_strategy", "1"),
    ("system", "POWER_CLOUD_INTERCEPT_ENABLE", "1"),
    ("global", "app_auto_startup_switch", "1"),
    ("global", "app_force_stop_behavior", "0"),
    ("secure", "forced_app_standby_enabled", "0"),
    ("global", "app_auto_revive_enabled", "1"),
    ("global", "app_kill_protection_enabled", "1"),
    ("global", "miui_optimization_whitelist_enabled", "1"),
    ("global", "miui_app_control_enabled", "0"),
]

APPOPS_MODES = [
    "RUN_IN_BACKGROUND",
    "RUN_ANY_IN_BACKGROUND",
    "WAKE_LOCK",
    "START_FOREGROUND",
]

GRANT_PERMISSIONS = [
    "android.permission.POST_NOTIFICATIONS",
    "android.permission.RECEIVE_BOOT_COMPLETED",
    "android.permission.VIBRATE",
    "android.permission.ACCESS_NETWORK_STATE",
    "android.permission.ACCESS_WIFI_STATE",
    "android.permission.FOREGROUND_SERVICE",
    "android.permission.FOREGROUND_SERVICE_DATA_SYNC",
    "android.permission.FOREGROUND_SERVICE_MEDIA_PLAYBACK",
    "android.permission.REQUEST_IGNORE_BATTERY_OPTIMIZATIONS",
]


@dataclass
class AppPreset:
    name: str
    package: str
    processes: list[str] = field(default_factory=list)
    services: list[str] = field(default_factory=list)


APP_PRESETS: list[AppPreset] = [
    AppPreset("Telegram", "org.telegram.messenger", processes=[
        "org.telegram.messenger", "org.telegram.messenger:push",
    ], services=[
        "org.telegram.messenger/com.google.firebase.messaging.FirebaseMessagingService",
    ]),
    AppPreset("WhatsApp", "com.whatsapp", processes=[
        "com.whatsapp", "com.whatsapp:push",
    ], services=[
        "com.whatsapp/com.google.firebase.messaging.FirebaseMessagingService",
    ]),
    AppPreset("Gmail", "com.google.android.gm", processes=[
        "com.google.android.gm", "com.google.android.gm:background",
    ], services=[
        "com.google.android.gm/com.google.android.gm.provider.MailSyncAdapterService",
    ]),
    AppPreset("Zalo", "com.zing.zalo", processes=[
        "com.zing.zalo", "com.zing.zalo:push", "com.zing.zalo:service", "com.zing.zalo:background",
    ], services=[
        "com.zing.zalo.service.ZaloFirebaseMessagingService",
        "com.zing.zalo/com.google.firebase.messaging.FirebaseMessagingService",
    ]),
    AppPreset("Messenger", "com.facebook.orca", processes=[
        "com.facebook.orca", "com.facebook.orca:push", "com.facebook.orca:service",
    ], services=[
        "com.facebook.push.fcm.FcmListenerService",
        "com.facebook.orca/com.facebook.push.fcm.FcmListenerService",
    ]),
    AppPreset("Google Play", "com.android.vending"),
    AppPreset("GMS", "com.google.android.gms", processes=[
        "com.google.android.gms", "com.google.android.gms:persistent",
    ], services=[
        "com.google.android.gms/.chimera.PersistentIntentOperationService",
    ]),
    AppPreset("Discord", "com.discord", processes=["com.discord"]),
    AppPreset("Outlook", "com.microsoft.office.outlook"),
    AppPreset("Slack", "com.Slack"),
    AppPreset("LINE", "jp.naver.line.android"),
]


@dataclass
class RomInfo:
    is_xiaomi: bool = False
    is_china: bool = False
    region: str = ""
    build: str = ""
    hyperos: bool = False
    miui_version: str = ""

    @property
    def summary(self) -> str:
        if not self.is_xiaomi:
            return "Không phải Xiaomi/MIUI"
        region = "China" if self.is_china else self.region or "Global/EU"
        os_name = "HyperOS" if self.hyperos else "MIUI"
        return f"{os_name} {self.miui_version} — ROM {region}"


@dataclass
class StepResult:
    ok: bool
    detail: str


class XiaomiCnOptimizer:
    def __init__(self, engine: AdbEngine) -> None:
        self.engine = engine

    def detect_rom(self, serial: str) -> RomInfo:
        info = RomInfo()
        manufacturer = self.engine.get_prop(serial, "ro.product.manufacturer").lower()
        brand = self.engine.get_prop(serial, "ro.product.brand").lower()
        info.is_xiaomi = manufacturer in ("xiaomi", "redmi", "poco") or brand in ("xiaomi", "redmi", "poco")

        info.build = self.engine.get_prop(serial, "ro.build.display.id")
        info.region = self.engine.get_prop(serial, "ro.miui.region") or self.engine.get_prop(serial, "ro.product.locale.region")
        locale = self.engine.get_prop(serial, "ro.product.locale")
        miui = self.engine.get_prop(serial, "ro.miui.ui.version.name")
        hyperos = self.engine.get_prop(serial, "ro.mi.os.version.name")

        info.miui_version = hyperos or miui
        info.hyperos = bool(hyperos)

        cn_markers = ("cn", "china", "zh-cn")
        region_lower = (info.region + locale + info.build).lower()
        info.is_china = any(m in region_lower for m in cn_markers) or region_lower.endswith("cnxm")

        return info

    def _log(self, fn: Optional[LogFn], msg: str) -> None:
        if fn:
            fn(msg)

    def settings_put(self, serial: str, ns: str, key: str, value: str) -> StepResult:
        escaped = value.replace("'", "'\\''")
        code, out, err = self.engine.run_shell(
            f"settings put {ns} {key} '{escaped}'", serial=serial, timeout=15
        )
        msg = (out + err).strip()
        return StepResult(code == 0, msg or "ok")

    def settings_get(self, serial: str, ns: str, key: str) -> str:
        _, out, _ = self.engine.run_shell(f"settings get {ns} {key}", serial=serial, timeout=10)
        val = out.strip()
        return "" if val == "null" else val

    def append_whitelist(self, serial: str, key: str, items: list[str]) -> StepResult:
        current = self.settings_get(serial, "system", key)
        existing = {x.strip() for x in current.split(",") if x.strip()}
        new_items = [i for i in items if i and i not in existing]
        if not new_items:
            return StepResult(True, "đã có trong whitelist")
        merged = ",".join(sorted(existing | set(new_items)))
        return self.settings_put(serial, "system", key, merged)

    def apply_global_relax(self, serial: str, log: Optional[LogFn] = None) -> list[StepResult]:
        results = []
        for ns, key, val in GLOBAL_RELAX_SETTINGS:
            r = self.settings_put(serial, ns, key, val)
            results.append(r)
            self._log(log, f"[{'OK' if r.ok else 'FAIL'}] settings {ns} {key}={val}")
        return results

    def apply_china_unlock(self, serial: str, log: Optional[LogFn] = None) -> list[StepResult]:
        results = []
        for ns, key, val in CHINA_UNLOCK_SETTINGS:
            r = self.settings_put(serial, ns, key, val)
            results.append(r)
            self._log(log, f"[{'OK' if r.ok else 'FAIL'}] settings {ns} {key}={val}")
        return results

    def disable_miui_optimization(self, serial: str, log: Optional[LogFn] = None) -> StepResult:
        r = self.settings_put(serial, "global", "miui_optimization", "false")
        self._log(log, f"[{'OK' if r.ok else 'FAIL'}] Tắt MIUI optimization (cần reboot)")
        return r

    def fix_app_notifications(
        self,
        serial: str,
        preset: AppPreset,
        log: Optional[LogFn] = None,
    ) -> list[StepResult]:
        results: list[StepResult] = []
        pkg = preset.package

        if not self._package_installed(serial, pkg):
            self._log(log, f"[SKIP] {preset.name} ({pkg}) — chưa cài")
            return results

        self._log(log, f"--- Fix thông báo: {preset.name} ({pkg}) ---")

        for key in MIUI_PKG_WHITELISTS:
            r = self.append_whitelist(serial, key, [pkg])
            results.append(r)

        if preset.processes:
            r = self.append_whitelist(serial, "power_proc_white_list", preset.processes)
            results.append(r)

        if preset.services:
            r = self.append_whitelist(serial, "power_service_white_list", preset.services)
            results.append(r)

        code, out, err = self.engine.run_shell(
            f"dumpsys deviceidle whitelist +{pkg}", serial=serial, timeout=15
        )
        r = StepResult(code == 0, (out + err).strip())
        results.append(r)
        self._log(log, f"[{'OK' if r.ok else 'FAIL'}] deviceidle whitelist +{pkg}")

        code, out, err = self.engine.run_shell(
            f"am set-standby-bucket {pkg} active", serial=serial, timeout=15
        )
        r = StepResult(code == 0, (out + err).strip())
        results.append(r)
        self._log(log, f"[{'OK' if r.ok else 'FAIL'}] standby-bucket active")

        for mode in APPOPS_MODES:
            code, out, err = self.engine.run_shell(
                f"cmd appops set {pkg} {mode} allow", serial=serial, timeout=15
            )
            r = StepResult(code == 0, (out + err).strip())
            results.append(r)
            self._log(log, f"[{'OK' if r.ok else 'WARN'}] appops {mode}")

        code, out, err = self.engine.run_shell(f"pm enable {pkg}", serial=serial, timeout=15)
        results.append(StepResult(code == 0, (out + err).strip()))

        return results

    def grant_app_permissions(
        self,
        serial: str,
        package: str,
        log: Optional[LogFn] = None,
    ) -> list[StepResult]:
        results: list[StepResult] = []
        if not self._package_installed(serial, package):
            self._log(log, f"[SKIP] {package} — chưa cài")
            return results

        self._log(log, f"--- Cấp quyền: {package} ---")

        for perm in GRANT_PERMISSIONS:
            code, out, err = self.engine.run_shell(
                f"pm grant {package} {perm}", serial=serial, timeout=10
            )
            msg = (out + err).strip()
            ok = code == 0 or "already" in msg.lower()
            results.append(StepResult(ok, msg))
            if not ok and "Unknown permission" not in msg:
                self._log(log, f"[{'OK' if ok else 'SKIP'}] {perm.split('.')[-1]}")

        for mode in ("SYSTEM_ALERT_WINDOW",):
            code, out, err = self.engine.run_shell(
                f"cmd appops set {package} {mode} allow", serial=serial, timeout=10
            )
            results.append(StepResult(code == 0, (out + err).strip()))
            self._log(log, f"[OK] appops {mode}")

        return results

    def full_optimize(
        self,
        serial: str,
        apps: list[AppPreset],
        *,
        global_relax: bool = True,
        china_unlock: bool = True,
        disable_optimization: bool = False,
        grant_permissions: bool = True,
        log: Optional[LogFn] = None,
    ) -> dict:
        rom = self.detect_rom(serial)
        self._log(log, f"ROM: {rom.summary}")
        self._log(log, f"Build: {rom.build}")
        self._log(log, "")

        summary = {"rom": rom, "steps": 0, "ok": 0, "fail": 0}

        def count(results: list[StepResult]) -> None:
            for r in results:
                summary["steps"] += 1
                if r.ok:
                    summary["ok"] += 1
                else:
                    summary["fail"] += 1

        if global_relax:
            self._log(log, "== Tối ưu Android Global ==")
            count(self.apply_global_relax(serial, log))
            self._log(log, "")

        if china_unlock:
            self._log(log, "== Mở khóa kiểm soát Xiaomi China ==")
            count(self.apply_china_unlock(serial, log))
            self._log(log, "")

        if disable_optimization:
            self._log(log, "== Tắt MIUI Optimization ==")
            r = self.disable_miui_optimization(serial, log)
            summary["steps"] += 1
            summary["ok" if r.ok else "fail"] += 1
            self._log(log, "")

        for app in apps:
            count(self.fix_app_notifications(serial, app, log))
            if grant_permissions:
                count(self.grant_app_permissions(serial, app.package, log))
            self._log(log, "")

        self._log(log, f"Hoàn tất: {summary['ok']}/{summary['steps']} OK, {summary['fail']} lỗi/bỏ qua")
        if disable_optimization:
            self._log(log, "Nhớ REBOOT máy để MIUI optimization có hiệu lực.")

        return summary

    def _package_installed(self, serial: str, package: str) -> bool:
        _, out, _ = self.engine.run_shell(
            f"pm path {package}", serial=serial, timeout=10
        )
        return "package:" in out