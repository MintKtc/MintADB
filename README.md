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

- ADB device management (`unauthorized` / `offline` hints included)
- HyperOS / MIUI optimization (safe debloat blacklist)
- Lock display refresh rate (Hz) + per-serial backup
- Device spoof, ad-block hosts, app permissions
- Fastboot, bundled APK install (Shizuku, Settings…)
- macOS-style WPF UI
- Auto setup: deploy ADB/Fastboot, USB driver, headless bootstrap

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

### Android companion APK

On-device app (Shizuku) with similar optimize/apps/display tabs — see [docs/MintADB-Android.md](docs/MintADB-Android.md).

### Contribute

Fork → branch → PR. Report issues on [GitHub Issues](https://github.com/MintKtc/MintADB/issues).

---

## Tiếng Việt

### Tính năng

- Quản lý thiết bị ADB (kể cả `unauthorized` / `offline` + gợi ý xử lý)
- Tối ưu HyperOS / MIUI (debloat có blacklist an toàn)
- Khóa tần số quét (Hz) + backup theo serial
- Device spoof, ad-block hosts, app permissions
- Fastboot, cài APK đi kèm (Shizuku, Settings…)
- Giao diện macOS-style (WPF)
- Cài đặt tự động: triển khai ADB/Fastboot, driver USB, bootstrap headless

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

### APK Android (companion)

App chạy trên máy, cần Shizuku — xem [docs/MintADB-Android.md](docs/MintADB-Android.md). Build bằng Android Studio trong `src/MintADB.Android`.

### Phát triển

**Yêu cầu:** .NET 8 SDK, Git. Tùy chọn: VS 2022, Inno Setup 6, Android Studio (APK).

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
├── src/MintADB.Wpf/       # WPF app
├── scripts/               # publish.ps1, MintADB.iss
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