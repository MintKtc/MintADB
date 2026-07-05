"""MintADB GUI — pure ADB power."""

from __future__ import annotations

import os
import tempfile
import threading
import tkinter as tk
from datetime import datetime
from tkinter import filedialog, messagebox
from typing import Optional

import customtkinter as ctk

from mintadb.adb_engine import AdbEngine, Device, DeviceInfo
from mintadb.xiaomi_cn import APP_PRESETS, AppPreset, XiaomiCnOptimizer

ctk.set_appearance_mode("dark")
ctk.set_default_color_theme("blue")

ACCENT = "#00C9A7"
BG = "#1a1a2e"
CARD = "#16213e"


class MintAdbApp(ctk.CTk):
    def __init__(self) -> None:
        super().__init__()
        self.title("MintADB")
        self.geometry("1100x720")
        self.minsize(900, 600)
        self.configure(fg_color=BG)

        self.engine = AdbEngine()
        self.xiaomi = XiaomiCnOptimizer(self.engine)
        self.devices: list[Device] = []
        self.selected_serial: Optional[str] = None
        self.device_info: Optional[DeviceInfo] = None
        self._monitor_running = True
        self._logcat_stop = threading.Event()
        self._logcat_thread: Optional[threading.Thread] = None

        self._build_ui()
        self._start_monitor()

    def _build_ui(self) -> None:
        self.grid_columnconfigure(1, weight=1)
        self.grid_rowconfigure(0, weight=1)

        self._build_sidebar()
        self._build_main()

    def _build_sidebar(self) -> None:
        side = ctk.CTkFrame(self, width=260, fg_color=CARD, corner_radius=0)
        side.grid(row=0, column=0, sticky="nsew")
        side.grid_propagate(False)

        ctk.CTkLabel(
            side, text="MintADB", font=ctk.CTkFont(size=22, weight="bold"), text_color=ACCENT
        ).pack(pady=(20, 4))
        ctk.CTkLabel(
            side, text="Xiaomi China → Global-like", font=ctk.CTkFont(size=11), text_color="#888"
        ).pack(pady=(0, 16))

        ctk.CTkLabel(side, text="Thiết bị", anchor="w", font=ctk.CTkFont(weight="bold")).pack(
            fill="x", padx=16, pady=(8, 4)
        )
        self.device_menu = ctk.CTkOptionMenu(
            side, values=["Không có thiết bị"], command=self._on_device_select, width=220
        )
        self.device_menu.pack(padx=16, pady=4)

        ctk.CTkButton(side, text="Làm mới", width=220, command=self._refresh_devices).pack(
            padx=16, pady=4
        )

        self.info_frame = ctk.CTkFrame(side, fg_color="#0f3460", corner_radius=8)
        self.info_frame.pack(fill="x", padx=16, pady=16)
        self.info_label = ctk.CTkLabel(
            self.info_frame,
            text="Chưa chọn thiết bị",
            justify="left",
            font=ctk.CTkFont(size=12),
            wraplength=210,
        )
        self.info_label.pack(padx=12, pady=12, anchor="w")

        ctk.CTkLabel(side, text="Quick Actions", anchor="w", font=ctk.CTkFont(weight="bold")).pack(
            fill="x", padx=16, pady=(8, 4)
        )
        actions = [
            ("Reboot", lambda: self._reboot("")),
            ("Bootloader", lambda: self._reboot("bootloader")),
            ("Recovery", lambda: self._reboot("recovery")),
            ("Screenshot", self._screenshot),
            ("TCP/IP 5555", self._enable_tcpip),
        ]
        for text, cmd in actions:
            ctk.CTkButton(side, text=text, width=220, fg_color="#0f3460", hover_color="#1a4a7a", command=cmd).pack(
                padx=16, pady=3
            )

        ctk.CTkLabel(side, text="", ).pack(expand=True)
        adb_path = self.engine.adb_path
        ctk.CTkLabel(
            side, text=f"adb: {os.path.basename(adb_path)}", font=ctk.CTkFont(size=10), text_color="#555"
        ).pack(pady=8)

    def _build_main(self) -> None:
        main = ctk.CTkFrame(self, fg_color=BG, corner_radius=0)
        main.grid(row=0, column=1, sticky="nsew", padx=0, pady=0)
        main.grid_rowconfigure(0, weight=1)
        main.grid_columnconfigure(0, weight=1)

        self.tabs = ctk.CTkTabview(main, fg_color=CARD)
        self.tabs.grid(row=0, column=0, sticky="nsew", padx=16, pady=16)

        self._build_xiaomi_tab()
        self._build_shell_tab()
        self._build_files_tab()
        self._build_apps_tab()
        self._build_logcat_tab()
        self._build_tools_tab()

    def _build_xiaomi_tab(self) -> None:
        tab = self.tabs.add("Xiaomi CN")
        tab.grid_rowconfigure(4, weight=1)
        tab.grid_columnconfigure(0, weight=1)

        header = ctk.CTkFrame(tab, fg_color="#0f3460", corner_radius=8)
        header.grid(row=0, column=0, sticky="ew", pady=(0, 6))
        header.grid_columnconfigure(0, weight=1)

        ctk.CTkLabel(
            header,
            text="Tối ưu ROM Xiaomi China — trải nghiệm Global-like",
            font=ctk.CTkFont(size=14, weight="bold"),
        ).grid(row=0, column=0, padx=12, pady=(12, 4), sticky="w")
        self.rom_label = ctk.CTkLabel(
            header, text="Chưa quét ROM", font=ctk.CTkFont(size=12), text_color="#aaa"
        )
        self.rom_label.grid(row=1, column=0, padx=12, pady=(0, 8), sticky="w")
        ctk.CTkButton(header, text="Quét ROM", width=90, command=self._scan_rom).grid(
            row=0, column=1, rowspan=2, padx=12, pady=12
        )

        opts = ctk.CTkFrame(tab, fg_color="transparent")
        opts.grid(row=1, column=0, sticky="ew", pady=(0, 6))

        self.opt_global = ctk.CTkCheckBox(opts, text="Tắt hạn chế Android")
        self.opt_global.select()
        self.opt_global.grid(row=0, column=0, padx=8, pady=2, sticky="w")

        self.opt_china = ctk.CTkCheckBox(opts, text="Mở khóa Xiaomi China")
        self.opt_china.select()
        self.opt_china.grid(row=0, column=1, padx=8, pady=2, sticky="w")

        self.opt_grant = ctk.CTkCheckBox(opts, text="Cấp quyền tự động")
        self.opt_grant.select()
        self.opt_grant.grid(row=0, column=2, padx=8, pady=2, sticky="w")

        self.opt_miui_opt = ctk.CTkCheckBox(opts, text="Tắt MIUI Optimization (reboot)")
        self.opt_miui_opt.grid(row=0, column=3, padx=8, pady=2, sticky="w")

        apps_frame = ctk.CTkFrame(tab, fg_color="#0f3460", corner_radius=8)
        apps_frame.grid(row=2, column=0, sticky="ew", pady=(0, 6))
        ctk.CTkLabel(apps_frame, text="App cần fix thông báo:", anchor="w").pack(
            fill="x", padx=12, pady=(8, 4)
        )

        checks = ctk.CTkFrame(apps_frame, fg_color="transparent")
        checks.pack(fill="x", padx=8, pady=(0, 4))

        self.app_checks: dict[str, ctk.CTkCheckBox] = {}
        defaults_on = {
            "org.telegram.messenger", "com.whatsapp", "com.google.android.gm",
            "com.zing.zalo", "com.facebook.orca", "com.google.android.gms",
        }
        for i, preset in enumerate(APP_PRESETS):
            cb = ctk.CTkCheckBox(checks, text=preset.name, width=110)
            if preset.package in defaults_on:
                cb.select()
            cb.grid(row=i // 5, column=i % 5, padx=6, pady=3, sticky="w")
            self.app_checks[preset.package] = cb

        custom_row = ctk.CTkFrame(apps_frame, fg_color="transparent")
        custom_row.pack(fill="x", padx=12, pady=(4, 10))
        self.custom_pkg = ctk.CTkEntry(custom_row, placeholder_text="Package tùy chỉnh (vd: com.example.app)")
        self.custom_pkg.pack(side="left", fill="x", expand=True)

        btn_row = ctk.CTkFrame(tab, fg_color="transparent")
        btn_row.grid(row=3, column=0, sticky="ew", pady=(0, 6))
        ctk.CTkButton(
            btn_row, text="Fix thông báo", width=140, height=36,
            fg_color=ACCENT, text_color="#000", command=self._fix_notifications,
        ).pack(side="left", padx=8)
        ctk.CTkButton(
            btn_row, text="Cấp quyền", width=120, height=36,
            command=self._grant_permissions,
        ).pack(side="left", padx=8)
        ctk.CTkButton(
            btn_row, text="Full Optimize", width=140, height=36,
            fg_color="#e94560", hover_color="#c73e54", command=self._full_optimize,
        ).pack(side="left", padx=8)

        self.xiaomi_log = ctk.CTkTextbox(tab, font=ctk.CTkFont(family="Consolas", size=11))
        self.xiaomi_log.grid(row=4, column=0, sticky="nsew")

    def _xiaomi_log(self, msg: str) -> None:
        def append() -> None:
            self.xiaomi_log.insert("end", msg + "\n")
            self.xiaomi_log.see("end")

        try:
            self.after(0, append)
        except Exception:
            pass

    def _selected_presets(self) -> list[AppPreset]:
        selected: list[AppPreset] = []
        for preset in APP_PRESETS:
            cb = self.app_checks.get(preset.package)
            if cb and cb.get():
                selected.append(preset)
        custom = self.custom_pkg.get().strip()
        if custom:
            name = custom.rsplit(".", 1)[-1]
            selected.append(AppPreset(name=name, package=custom))
        return selected

    def _scan_rom(self) -> None:
        if not self._require_device():
            return

        def work():
            return self.xiaomi.detect_rom(self.selected_serial)

        def done(rom):
            self.rom_label.configure(text=f"{rom.summary}\n{rom.build}")

        self._run_bg(work, done)

    def _fix_notifications(self) -> None:
        if not self._require_device():
            return
        apps = self._selected_presets()
        if not apps:
            messagebox.showwarning("MintADB", "Chọn ít nhất 1 app.")
            return
        self.xiaomi_log.delete("1.0", "end")
        serial = self.selected_serial

        def work():
            if self.opt_global.get():
                self.xiaomi.apply_global_relax(serial, self._xiaomi_log)
            if self.opt_china.get():
                self.xiaomi.apply_china_unlock(serial, self._xiaomi_log)
            for app in apps:
                self.xiaomi.fix_app_notifications(serial, app, self._xiaomi_log)

        self._run_bg(work)

    def _grant_permissions(self) -> None:
        if not self._require_device():
            return
        apps = self._selected_presets()
        if not apps:
            messagebox.showwarning("MintADB", "Chọn ít nhất 1 app.")
            return
        serial = self.selected_serial

        def work():
            for app in apps:
                self.xiaomi.grant_app_permissions(serial, app.package, self._xiaomi_log)

        self._run_bg(work)

    def _full_optimize(self) -> None:
        if not self._require_device():
            return
        apps = self._selected_presets()
        if not apps:
            messagebox.showwarning("MintADB", "Chọn ít nhất 1 app.")
            return
        if self.opt_miui_opt.get():
            if not messagebox.askyesno(
                "Xác nhận",
                "Tắt MIUI Optimization sẽ thay đổi hành vi hệ thống.\nCần reboot sau khi chạy.\nTiếp tục?",
            ):
                return

        self.xiaomi_log.delete("1.0", "end")
        serial = self.selected_serial

        def work():
            return self.xiaomi.full_optimize(
                serial,
                apps,
                global_relax=bool(self.opt_global.get()),
                china_unlock=bool(self.opt_china.get()),
                disable_optimization=bool(self.opt_miui_opt.get()),
                grant_permissions=bool(self.opt_grant.get()),
                log=self._xiaomi_log,
            )

        def done(summary):
            if summary.get("fail", 0) == 0:
                messagebox.showinfo("MintADB", "Tối ưu hoàn tất!")
            else:
                messagebox.showinfo(
                    "MintADB",
                    f"Hoàn tất: {summary['ok']}/{summary['steps']} OK\n"
                    f"{summary['fail']} bước lỗi/bỏ qua (xem log)",
                )

        self._run_bg(work, done)

    def _build_shell_tab(self) -> None:
        tab = self.tabs.add("Shell")
        tab.grid_rowconfigure(1, weight=1)
        tab.grid_columnconfigure(0, weight=1)

        row = ctk.CTkFrame(tab, fg_color="transparent")
        row.grid(row=0, column=0, sticky="ew", pady=(0, 8))
        row.grid_columnconfigure(0, weight=1)

        self.shell_input = ctk.CTkEntry(row, placeholder_text="adb shell <lệnh>...")
        self.shell_input.grid(row=0, column=0, sticky="ew", padx=(0, 8))
        self.shell_input.bind("<Return>", lambda e: self._run_shell())

        ctk.CTkButton(row, text="Chạy", width=80, fg_color=ACCENT, text_color="#000", command=self._run_shell).grid(
            row=0, column=1
        )

        presets = ctk.CTkFrame(tab, fg_color="transparent")
        presets.grid(row=0, column=0, sticky="ew", pady=(44, 0))
        for i, (label, cmd) in enumerate(
            [
                ("getprop", "getprop"),
                ("dumpsys battery", "dumpsys battery"),
                ("df -h", "df -h"),
                ("ps -A", "ps -A"),
                ("wm size", "wm size"),
            ]
        ):
            ctk.CTkButton(
                presets, text=label, width=100, height=28, fg_color="#0f3460",
                command=lambda c=cmd: self._shell_preset(c),
            ).grid(row=0, column=i, padx=4)

        self.shell_output = ctk.CTkTextbox(tab, font=ctk.CTkFont(family="Consolas", size=12))
        self.shell_output.grid(row=1, column=0, sticky="nsew", pady=(8, 0))

    def _build_files_tab(self) -> None:
        tab = self.tabs.add("Files")
        tab.grid_rowconfigure(1, weight=1)
        tab.grid_columnconfigure(0, weight=1)

        nav = ctk.CTkFrame(tab, fg_color="transparent")
        nav.grid(row=0, column=0, sticky="ew", pady=(0, 8))
        nav.grid_columnconfigure(1, weight=1)

        ctk.CTkButton(nav, text="↑", width=40, command=self._file_up).grid(row=0, column=0, padx=(0, 8))
        self.file_path = ctk.CTkEntry(nav)
        self.file_path.insert(0, "/sdcard")
        self.file_path.grid(row=0, column=1, sticky="ew", padx=(0, 8))
        ctk.CTkButton(nav, text="Mở", width=60, command=self._file_open).grid(row=0, column=2, padx=(0, 8))
        ctk.CTkButton(nav, text="Push", width=60, fg_color=ACCENT, text_color="#000", command=self._file_push).grid(
            row=0, column=3, padx=(0, 8)
        )
        ctk.CTkButton(nav, text="Pull", width=60, command=self._file_pull).grid(row=0, column=4)

        self.file_list = ctk.CTkTextbox(tab, font=ctk.CTkFont(family="Consolas", size=12))
        self.file_list.grid(row=1, column=0, sticky="nsew")
        self.file_list.bind("<Double-Button-1>", lambda e: self._file_enter())

        shortcuts = ctk.CTkFrame(tab, fg_color="transparent")
        shortcuts.grid(row=2, column=0, sticky="ew", pady=(8, 0))
        for label, path in [("/sdcard", "/sdcard"), ("/data/local/tmp", "/data/local/tmp"), ("/system", "/system")]:
            ctk.CTkButton(
                shortcuts, text=label, width=120, height=28, fg_color="#0f3460",
                command=lambda p=path: self._file_goto(p),
            ).pack(side="left", padx=4)

        self._selected_remote_file: Optional[str] = None

    def _build_apps_tab(self) -> None:
        tab = self.tabs.add("Apps")
        tab.grid_rowconfigure(1, weight=1)
        tab.grid_columnconfigure(0, weight=1)

        top = ctk.CTkFrame(tab, fg_color="transparent")
        top.grid(row=0, column=0, sticky="ew", pady=(0, 8))
        top.grid_columnconfigure(0, weight=1)

        self.app_filter = ctk.CTkEntry(top, placeholder_text="Lọc package...")
        self.app_filter.grid(row=0, column=0, sticky="ew", padx=(0, 8))
        ctk.CTkButton(top, text="Tìm", width=60, command=self._list_apps).grid(row=0, column=1, padx=(0, 8))
        ctk.CTkButton(top, text="Cài APK", width=80, fg_color=ACCENT, text_color="#000", command=self._install_apk).grid(
            row=0, column=2, padx=(0, 8)
        )
        ctk.CTkButton(top, text="Gỡ", width=60, command=self._uninstall_app).grid(row=0, column=3, padx=(0, 8))
        ctk.CTkButton(top, text="Clear data", width=80, command=self._clear_app).grid(row=0, column=4)

        self.app_list = ctk.CTkTextbox(tab, font=ctk.CTkFont(family="Consolas", size=12))
        self.app_list.grid(row=1, column=0, sticky="nsew")

    def _build_logcat_tab(self) -> None:
        tab = self.tabs.add("Logcat")
        tab.grid_rowconfigure(1, weight=1)
        tab.grid_columnconfigure(0, weight=1)

        top = ctk.CTkFrame(tab, fg_color="transparent")
        top.grid(row=0, column=0, sticky="ew", pady=(0, 8))
        top.grid_columnconfigure(1, weight=1)

        self.logcat_filter = ctk.CTkEntry(top, placeholder_text="Filter tag:level (vd: ActivityManager:I)")
        self.logcat_filter.grid(row=0, column=0, columnspan=2, sticky="ew", padx=(0, 8))
        ctk.CTkButton(top, text="Live", width=60, fg_color=ACCENT, text_color="#000", command=self._logcat_live).grid(
            row=0, column=2, padx=(0, 8)
        )
        ctk.CTkButton(top, text="Dừng", width=60, command=self._logcat_stop_live).grid(row=0, column=3, padx=(0, 8))
        ctk.CTkButton(top, text="Dump", width=60, command=self._logcat_dump).grid(row=0, column=4, padx=(0, 8))
        ctk.CTkButton(top, text="Xóa", width=60, command=self._logcat_clear).grid(row=0, column=5)

        self.logcat_output = ctk.CTkTextbox(tab, font=ctk.CTkFont(family="Consolas", size=11))
        self.logcat_output.grid(row=1, column=0, sticky="nsew")

    def _build_tools_tab(self) -> None:
        tab = self.tabs.add("Tools")
        tab.grid_columnconfigure(0, weight=1)

        # Wireless
        wifi = ctk.CTkFrame(tab, fg_color="#0f3460", corner_radius=8)
        wifi.grid(row=0, column=0, sticky="ew", pady=8)
        wifi.grid_columnconfigure(1, weight=1)
        ctk.CTkLabel(wifi, text="Wireless ADB", font=ctk.CTkFont(weight="bold")).grid(
            row=0, column=0, columnspan=3, padx=12, pady=(12, 8), sticky="w"
        )
        self.wifi_addr = ctk.CTkEntry(wifi, placeholder_text="192.168.1.100:5555")
        self.wifi_addr.grid(row=1, column=0, columnspan=2, sticky="ew", padx=12, pady=4)
        ctk.CTkButton(wifi, text="Connect", width=80, command=self._wifi_connect).grid(row=1, column=2, padx=12, pady=4)
        ctk.CTkButton(wifi, text="Disconnect all", width=120, command=self._wifi_disconnect).grid(
            row=2, column=0, padx=12, pady=(4, 12)
        )

        # Port forward
        fwd = ctk.CTkFrame(tab, fg_color="#0f3460", corner_radius=8)
        fwd.grid(row=1, column=0, sticky="ew", pady=8)
        fwd.grid_columnconfigure(1, weight=1)
        ctk.CTkLabel(fwd, text="Port Forward", font=ctk.CTkFont(weight="bold")).grid(
            row=0, column=0, columnspan=3, padx=12, pady=(12, 8), sticky="w"
        )
        self.fwd_local = ctk.CTkEntry(fwd, placeholder_text="tcp:8080")
        self.fwd_local.grid(row=1, column=0, padx=12, pady=4)
        self.fwd_remote = ctk.CTkEntry(fwd, placeholder_text="tcp:8080")
        self.fwd_remote.grid(row=1, column=1, sticky="ew", padx=4, pady=4)
        ctk.CTkButton(fwd, text="Forward", width=80, command=self._do_forward).grid(row=1, column=2, padx=12, pady=4)
        ctk.CTkButton(fwd, text="List forwards", width=120, command=self._list_forwards).grid(
            row=2, column=0, padx=12, pady=(4, 12)
        )

        # Input
        inp = ctk.CTkFrame(tab, fg_color="#0f3460", corner_radius=8)
        inp.grid(row=2, column=0, sticky="ew", pady=8)
        inp.grid_columnconfigure(0, weight=1)
        ctk.CTkLabel(inp, text="Input", font=ctk.CTkFont(weight="bold")).grid(
            row=0, column=0, columnspan=3, padx=12, pady=(12, 8), sticky="w"
        )
        self.input_text = ctk.CTkEntry(inp, placeholder_text="Text to send...")
        self.input_text.grid(row=1, column=0, sticky="ew", padx=12, pady=4)
        ctk.CTkButton(inp, text="Send text", width=80, command=self._send_text).grid(row=1, column=1, padx=4, pady=4)
        keys = ctk.CTkFrame(inp, fg_color="transparent")
        keys.grid(row=2, column=0, columnspan=3, padx=12, pady=(4, 12))
        for label, code in [("Home", "3"), ("Back", "4"), ("Power", "26"), ("Vol+", "24"), ("Vol-", "25")]:
            ctk.CTkButton(keys, text=label, width=70, height=28, command=lambda c=code: self._send_key(c)).pack(
                side="left", padx=3
            )

        self.tools_output = ctk.CTkTextbox(tab, height=120, font=ctk.CTkFont(family="Consolas", size=11))
        self.tools_output.grid(row=3, column=0, sticky="ew", pady=8)

    # ── Device monitoring ──────────────────────────────────────────────

    def _start_monitor(self) -> None:
        def loop() -> None:
            while self._monitor_running:
                try:
                    self.devices = self.engine.list_devices()
                    self.after(0, self._update_device_menu)
                except Exception:
                    pass
                threading.Event().wait(2)

        threading.Thread(target=loop, daemon=True).start()
        self._refresh_devices()

    def _update_device_menu(self) -> None:
        online = [d for d in self.devices if d.state == "device"]
        if not online:
            self.device_menu.configure(values=["Không có thiết bị"])
            self.device_menu.set("Không có thiết bị")
            return
        labels = [d.label for d in online]
        self.device_menu.configure(values=labels)
        if self.selected_serial:
            for d in online:
                if d.serial == self.selected_serial:
                    self.device_menu.set(d.label)
                    return
        self.device_menu.set(labels[0])
        self._on_device_select(labels[0])

    def _on_device_select(self, label: str) -> None:
        for d in self.devices:
            if d.label == label:
                self.selected_serial = d.serial
                threading.Thread(target=self._load_device_info, daemon=True).start()
                return
        self.selected_serial = None

    def _load_device_info(self) -> None:
        if not self.selected_serial:
            return
        try:
            info = self.engine.get_device_info(self.selected_serial)
            self.device_info = info
            text = (
                f"{info.manufacturer} {info.model}\n"
                f"Android {info.android_version} (SDK {info.sdk})\n"
                f"Build: {info.build}\n"
                f"Patch: {info.security_patch}\n"
                f"ABI: {info.abi}\n"
                f"Screen: {info.screen_size} @{info.density}dpi\n"
                f"Battery: {info.battery_level} ({info.battery_status})\n"
                f"IP: {info.ip_address or 'N/A'}"
            )
            self.after(0, lambda: self.info_label.configure(text=text))
        except Exception as e:
            self.after(0, lambda: self.info_label.configure(text=f"Lỗi: {e}"))

    def _refresh_devices(self) -> None:
        def work() -> None:
            self.devices = self.engine.list_devices()
            self.after(0, self._update_device_menu)

        threading.Thread(target=work, daemon=True).start()

    def _require_device(self) -> bool:
        if not self.selected_serial:
            messagebox.showwarning("MintADB", "Chưa chọn thiết bị ADB.")
            return False
        return True

    def _run_bg(self, fn, on_done=None) -> None:
        def work() -> None:
            result = fn()
            if on_done:
                self.after(0, lambda: on_done(result))

        threading.Thread(target=work, daemon=True).start()

    # ── Quick actions ──────────────────────────────────────────────────

    def _reboot(self, mode: str) -> None:
        if not self._require_device():
            return

        def work():
            return self.engine.reboot(self.selected_serial, mode)

        def done(result):
            ok, msg = result
            messagebox.showinfo("Reboot", msg or ("OK" if ok else "Failed"))

        self._run_bg(work, done)

    def _screenshot(self) -> None:
        if not self._require_device():
            return
        path = filedialog.asksaveasfilename(
            defaultextension=".png",
            filetypes=[("PNG", "*.png")],
            initialfile=f"screenshot_{datetime.now():%Y%m%d_%H%M%S}.png",
        )
        if not path:
            return

        def work():
            return self.engine.screencap(self.selected_serial, path)

        def done(result):
            ok, msg = result
            if ok:
                messagebox.showinfo("Screenshot", f"Đã lưu: {path}")
            else:
                messagebox.showerror("Screenshot", msg)

        self._run_bg(work, done)

    def _enable_tcpip(self) -> None:
        if not self._require_device():
            return

        def work():
            return self.engine.tcpip(self.selected_serial)

        def done(result):
            ok, msg = result
            messagebox.showinfo("TCP/IP", msg or ("OK" if ok else "Failed"))

        self._run_bg(work, done)

    # ── Shell ──────────────────────────────────────────────────────────

    def _shell_preset(self, cmd: str) -> None:
        self.shell_input.delete(0, "end")
        self.shell_input.insert(0, cmd)
        self._run_shell()

    def _run_shell(self) -> None:
        if not self._require_device():
            return
        cmd = self.shell_input.get().strip()
        if not cmd:
            return
        self.shell_output.insert("end", f"\n$ {cmd}\n")

        def work():
            code, out, err = self.engine.run_shell(cmd, self.selected_serial)
            return code, out, err

        def done(result):
            code, out, err = result
            if out:
                self.shell_output.insert("end", out + "\n")
            if err:
                self.shell_output.insert("end", f"[stderr] {err}\n")
            self.shell_output.insert("end", f"[exit {code}]\n")
            self.shell_output.see("end")

        self._run_bg(work, done)

    # ── Files ──────────────────────────────────────────────────────────

    def _file_goto(self, path: str) -> None:
        self.file_path.delete(0, "end")
        self.file_path.insert(0, path)
        self._file_open()

    def _file_open(self) -> None:
        if not self._require_device():
            return
        path = self.file_path.get().strip()

        def work():
            return self.engine.list_dir(self.selected_serial, path)

        def done(entries):
            self.file_list.delete("1.0", "end")
            for name, kind in entries:
                icon = "📁" if kind == "dir" else "📄"
                self.file_list.insert("end", f"{icon} {name}\n")

        self._run_bg(work, done)

    def _file_up(self) -> None:
        path = self.file_path.get().strip().rstrip("/")
        if path == "/":
            return
        parent = path.rsplit("/", 1)[0] or "/"
        self.file_path.delete(0, "end")
        self.file_path.insert(0, parent)
        self._file_open()

    def _file_enter(self) -> None:
        try:
            line = self.file_list.get("insert linestart", "insert lineend").strip()
            if not line or line[0] != "📁":
                return
            name = line[2:].strip()
            base = self.file_path.get().strip().rstrip("/")
            new_path = f"{base}/{name}"
            self.file_path.delete(0, "end")
            self.file_path.insert(0, new_path)
            self._selected_remote_file = new_path
            self._file_open()
        except Exception:
            pass

    def _file_push(self) -> None:
        if not self._require_device():
            return
        local = filedialog.askopenfilename()
        if not local:
            return
        remote = self.file_path.get().strip().rstrip("/") + "/" + os.path.basename(local)

        def work():
            return self.engine.push(self.selected_serial, local, remote)

        def done(result):
            ok, msg = result
            messagebox.showinfo("Push", msg or ("OK" if ok else "Failed"))
            if ok:
                self._file_open()

        self._run_bg(work, done)

    def _file_pull(self) -> None:
        if not self._require_device():
            return
        try:
            line = self.file_list.get("insert linestart", "insert lineend").strip()
            if line.startswith("📄"):
                name = line[2:].strip()
            else:
                name = line.split()[-1] if line else ""
            if not name:
                messagebox.showwarning("Pull", "Chọn file cần pull.")
                return
            base = self.file_path.get().strip().rstrip("/")
            remote = f"{base}/{name}"
        except Exception:
            messagebox.showwarning("Pull", "Chọn file cần pull.")
            return

        local = filedialog.asksaveasfilename(initialfile=name)
        if not local:
            return

        def work():
            return self.engine.pull(self.selected_serial, remote, local)

        def done(result):
            ok, msg = result
            messagebox.showinfo("Pull", msg or ("OK" if ok else "Failed"))

        self._run_bg(work, done)

    # ── Apps ───────────────────────────────────────────────────────────

    def _list_apps(self) -> None:
        if not self._require_device():
            return
        filt = self.app_filter.get().strip()

        def work():
            return self.engine.list_packages(self.selected_serial, filt)

        def done(packages):
            self.app_list.delete("1.0", "end")
            for pkg in packages:
                self.app_list.insert("end", pkg + "\n")

        self._run_bg(work, done)

    def _get_selected_package(self) -> Optional[str]:
        try:
            line = self.app_list.get("insert linestart", "insert lineend").strip()
            return line if line else None
        except Exception:
            return None

    def _install_apk(self) -> None:
        if not self._require_device():
            return
        path = filedialog.askopenfilename(filetypes=[("APK", "*.apk")])
        if not path:
            return

        def work():
            return self.engine.install_apk(self.selected_serial, path, reinstall=True)

        def done(result):
            ok, msg = result
            messagebox.showinfo("Install", msg)

        self._run_bg(work, done)

    def _uninstall_app(self) -> None:
        if not self._require_device():
            return
        pkg = self._get_selected_package()
        if not pkg:
            messagebox.showwarning("Uninstall", "Chọn package.")
            return

        def work():
            return self.engine.uninstall(self.selected_serial, pkg)

        def done(result):
            ok, msg = result
            messagebox.showinfo("Uninstall", msg)
            if ok:
                self._list_apps()

        self._run_bg(work, done)

    def _clear_app(self) -> None:
        if not self._require_device():
            return
        pkg = self._get_selected_package()
        if not pkg:
            messagebox.showwarning("Clear", "Chọn package.")
            return

        def work():
            return self.engine.clear_app_data(self.selected_serial, pkg)

        def done(result):
            ok, msg = result
            messagebox.showinfo("Clear data", msg)

        self._run_bg(work, done)

    # ── Logcat ─────────────────────────────────────────────────────────

    def _logcat_dump(self) -> None:
        if not self._require_device():
            return
        filt = self.logcat_filter.get().strip()

        def work():
            return self.engine.get_logcat(self.selected_serial, filt)

        def done(text):
            self.logcat_output.delete("1.0", "end")
            self.logcat_output.insert("end", text)

        self._run_bg(work, done)

    def _logcat_live(self) -> None:
        if not self._require_device():
            return
        self._logcat_stop_live()
        self._logcat_stop = threading.Event()
        filt = self.logcat_filter.get().strip()
        serial = self.selected_serial

        def loop() -> None:
            args = ["logcat"]
            if filt:
                args.extend(filt.split())
            try:
                for line in self.engine.stream(args, serial=serial, stop_event=self._logcat_stop):
                    self.after(0, lambda l=line: self._append_logcat(l))
            except Exception:
                pass

        self._logcat_thread = threading.Thread(target=loop, daemon=True)
        self._logcat_thread.start()

    def _append_logcat(self, line: str) -> None:
        self.logcat_output.insert("end", line + "\n")
        self.logcat_output.see("end")
        content = self.logcat_output.get("1.0", "end")
        if content.count("\n") > 2000:
            self.logcat_output.delete("1.0", "500.0")

    def _logcat_stop_live(self) -> None:
        self._logcat_stop.set()

    def _logcat_clear(self) -> None:
        self.logcat_output.delete("1.0", "end")

    # ── Tools ──────────────────────────────────────────────────────────

    def _tools_log(self, msg: str) -> None:
        self.tools_output.insert("end", msg + "\n")
        self.tools_output.see("end")

    def _wifi_connect(self) -> None:
        addr = self.wifi_addr.get().strip()
        if not addr:
            return

        def work():
            return self.engine.connect(addr)

        def done(result):
            ok, msg = result
            self._tools_log(f"connect {addr}: {msg}")
            self._refresh_devices()

        self._run_bg(work, done)

    def _wifi_disconnect(self) -> None:
        def work():
            return self.engine.disconnect()

        def done(result):
            ok, msg = result
            self._tools_log(f"disconnect: {msg}")
            self._refresh_devices()

        self._run_bg(work, done)

    def _do_forward(self) -> None:
        if not self._require_device():
            return
        local = self.fwd_local.get().strip()
        remote = self.fwd_remote.get().strip()
        if not local or not remote:
            return

        def work():
            return self.engine.forward(self.selected_serial, local, remote)

        def done(result):
            ok, msg = result
            self._tools_log(f"forward {local} -> {remote}: {msg}")

        self._run_bg(work, done)

    def _list_forwards(self) -> None:
        if not self._require_device():
            return

        def work():
            return self.engine.list_forwards(self.selected_serial)

        def done(text):
            self._tools_log(text or "(no forwards)")

        self._run_bg(work, done)

    def _send_text(self) -> None:
        if not self._require_device():
            return
        text = self.input_text.get().strip()
        if not text:
            return

        def work():
            return self.engine.input_text(self.selected_serial, text)

        def done(result):
            ok, msg = result
            self._tools_log(f"input text: {msg}")

        self._run_bg(work, done)

    def _send_key(self, keycode: str) -> None:
        if not self._require_device():
            return

        def work():
            return self.engine.input_keyevent(self.selected_serial, keycode)

        def done(result):
            ok, msg = result
            self._tools_log(f"keyevent {keycode}: {msg}")

        self._run_bg(work, done)

    def on_closing(self) -> None:
        self._monitor_running = False
        self._logcat_stop_live()
        self.destroy()


def run() -> None:
    app = MintAdbApp()
    app.protocol("WM_DELETE_WINDOW", app.on_closing)
    app.mainloop()