# MintADB

**Developer / Nhà phát triển: [MINT_HD](https://github.com/MintKtc)**

<p align="center">
  <img src="exe.ico" alt="MintADB" width="96" height="96">
</p>

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)
![Platform](https://img.shields.io/badge/platform-Windows%20x64-0078D6)
![License](https://img.shields.io/badge/license-MIT-green)

Desktop ADB toolkit for **Xiaomi / HyperOS** — optimize, debloat, lock refresh rate, device spoof, Shizuku, fastboot, and more.

Công cụ ADB desktop cho **Xiaomi / HyperOS** — tối ưu, debloat, khóa Hz, spoof thiết bị, Shizuku, fastboot và nhiều tiện ích khác.

---

## English

### Features

**Optimize (Xiaomi China → Global):**
- Fix notifications with 7-phase approach (unfreeze, enable, permissions, whitelists, deviceidle, appops, autostart)
- Safe debloat — remove bloatware while keeping system apps
- Change region CN → Global
- Disable MIUI analytics & tracking
- Backup/Restore app lists
- Undo system for batch operations

**Tools:**
- ADB/Fastboot/Driver management
- Screenshot & Screen record
- Push/Pull files
- File explorer
- WiFi, Bluetooth, Airplane mode, Hotspot
- Network status display
- Locale change (9 languages)
- Ad blocking (MIUI + Google + DNS)

**System Info:**
- Battery (level, health, capacity, temperature)
- Display (resolution, Hz, panel type, DPI)
- Storage (internal + SD card)
- RAM (total, used, available, swap)
- CPU (SoC model, cores, frequency)
- GPU (renderer, frequency)
- Touch (sampling rate)

**Tweaks:**
- DPI adjustment (preset + custom)
- Animation speed
- Refresh rate lock (60/90/120/144 Hz)
- Font size
- Screen timeout
- Developer options (Stay awake, Show taps, Pointer location)
- Status bar customization
- Battery percentage display

**Fastboot:**
- Flash image (boot, recovery, vbmeta, etc.)
- Flash ROM (MiFlash-style) with auto-detect
- Flash current slot (A/B devices)
- Flash + Lock bootloader
- Cancel flash operation
- Factory reset (wipe)
- Set active slot (A/B)
- Boot temporary (no flash)
- OEM EDL (Xiaomi)

**Advanced:**
- Device spoof (fake ro.product.*)
- Unlock FPS / refresh rate
- Shizuku integration
- AppOps management
- 87 permissions supported

### Requirements

| | |
|---|---|
| OS | Windows 10/11 **64-bit** |
| Runtime | No .NET install needed (self-contained build) |
| Phone | Xiaomi / Redmi / POCO with **USB Debugging** enabled |
| Install | **Administrator** rights (installer) |

### Download

[Releases](https://github.com/MintKtc/MintADB/releases):

- `MintADB-Setup-v1.0.0-win-x64.exe` — installer (recommended)
- `MintADB-v1.0.0-win-x64.zip` — portable

### Development

**Prerequisites:** .NET 8 SDK, Git. Optional: VS 2022, Inno Setup 6.

```bash
git clone https://github.com/MintKtc/MintADB.git
cd MintADB
```

Copy **PlatformTools**, **Drivers**, **Miui** — see [docs/BUNDLED-ASSETS.md](docs/BUNDLED-ASSETS.md).

```bat
run-wpf.bat
```

```powershell
powershell -ExecutionPolicy Bypass -File scripts\publish.ps1 -Profile installer
```

### User data (runtime)

| Path | Content |
|------|---------|
| `%LOCALAPPDATA%\MintADB\` | Deployed ADB, `install-state.json` |
| `Desktop\MintADB\` | Screenshots, Hz/spoof backups, recordings |

### Contribute

Fork → branch → PR. Report issues on [GitHub Issues](https://github.com/MintKtc/MintADB/issues).

---

## Tiếng Việt

### Tính năng

**Tối ưu (Xiaomi China → Global):**
- Fix thông báo 7 giai đoạn (unfreeze, enable, permissions, whitelists, deviceidle, appops, autostart)
- Debloat an toàn — gỡ rác ROM giữ app hệ thống
- Đổi region CN → Global
- Tắt MIUI analytics & theo dõi
- Backup/Restore danh sách app
- Hệ thống hoàn tác (Undo)

**Công cụ:**
- Quản lý ADB/Fastboot/Driver
- Chụp ảnh & Quay màn hình
- Push/Pull file
- File explorer
- WiFi, Bluetooth, Chế độ máy bay, Hotspot
- Hiển thị trạng thái mạng
- Đổi ngôn ngữ (9 ngôn ngữ)
- Chặn quảng cáo (MIUI + Google + DNS)

**Thông tin hệ thống:**
- Pin (mức, sức khỏe, dung lượng, nhiệt độ)
- Màn hình (độ phân giải, Hz, tấm nền, DPI)
- Bộ nhớ trong (internal + SD card)
- RAM (tổng, đã dùng, khả dụng, swap)
- CPU (SoC, nhân, xung nhịp)
- GPU (renderer, xung nhịp)
- Touch (tốc độ quét)

**Tinh chỉnh:**
- Điều chỉnh DPI (preset + tùy chỉnh)
- Tốc độ animation
- Khóa tần số quét (60/90/120/144 Hz)
- Kích thước font
- Thời gian tắt màn hình
- Tùy chọn Nhà phát triển (Stay awake, Show taps, Pointer location)
- Tùy chỉnh thanh trạng thái
- Hiển thị phần trăm pin

**Fastboot:**
- Flash image (boot, recovery, vbmeta, v.v.)
- Flash ROM (kiểu MiFlash) với tự động tìm
- Flash slot hiện tại (thiết bị A/B)
- Flash + Lock bootloader
- Hủy flash
- Factory reset (wipe)
- Đặt slot active (A/B)
- Boot tạm thời (không flash)
- OEM EDL (Xiaomi)

**Nâng cao:**
- Giả mạo thiết bị (fake ro.product.*)
- Unlock FPS / tần số quét
- Tích hợp Shizuku
- Quản lý AppOps
- Hỗ trợ 87 quyền

### Yêu cầu

| | |
|---|---|
| OS | Windows 10/11 **64-bit** |
| Runtime | Không cần cài .NET (self-contained) |
| Điện thoại | Xiaomi / Redmi / POCO, bật **USB Debugging** |
| Cài đặt | Quyền **Administrator** (installer) |

### Tải bản build

[Releases](https://github.com/MintKtc/MintADB/releases):

- `MintADB-Setup-v1.0.0-win-x64.exe` — installer (khuyến nghị)
- `MintADB-v1.0.0-win-x64.zip` — portable

### Phát triển

**Yêu cầu:** .NET 8 SDK, Git. Tùy chọn: VS 2022, Inno Setup 6.

```bash
git clone https://github.com/MintKtc/MintADB.git
cd MintADB
```

Copy **PlatformTools**, **Drivers**, **Miui** — xem [docs/BUNDLED-ASSETS.md](docs/BUNDLED-ASSETS.md).

```bat
setup-dev.bat
run-wpf.bat
```

```powershell
powershell -ExecutionPolicy Bypass -File scripts\publish.ps1 -Profile installer
```

### Cấu trúc mã nguồn

```
MintADB/
├── src/MintADB.Wpf/           # Bản PC (Windows) — bản chính
│   ├── MainWindow.xaml        # Giao diện chính
│   ├── Services/              # ADB, Hardware, Network, System, Fastboot
│   ├── Models/                # Data models
│   ├── Windows/               # Welcome popup
│   └── Helpers/               # Utility classes
├── scripts/                   # publish.ps1, MintADB.iss (installer)
├── docs/
└── exe.ico
```

### Dữ liệu người dùng

| Vị trí | Nội dung |
|--------|----------|
| `%LOCALAPPDATA%\MintADB\` | ADB triển khai, `install-state.json` |
| `Desktop\MintADB\` | Screenshot, backup, recording |

---

## License / Giấy phép

[MIT](LICENSE) — Copyright (c) 2026 **MINT_HD**

### Third-party / Bên thứ ba

- **Platform-Tools** — [Android SDK License](https://developer.android.com/studio/terms)
- **Google USB Driver** — Google / Android Open Source
- APK in `Miui/` — respective owners (Shizuku, Xiaomi…)
