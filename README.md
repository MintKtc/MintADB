# MintADB

Công cụ ADB desktop cho điện thoại **Xiaomi / HyperOS** — tối ưu hệ thống, debloat, khóa Hz, spoof thiết bị, Shizuku, fastboot và nhiều tiện ích khác.

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)
![Platform](https://img.shields.io/badge/platform-Windows%20x64-0078D6)
![License](https://img.shields.io/badge/license-MIT-green)

<p align="center">
  <img src="exe.ico" alt="MintADB" width="96" height="96">
</p>

## Tính năng

- Quản lý thiết bị ADB (kể cả `unauthorized` / `offline` + gợi ý xử lý)
- Tối ưu HyperOS / MIUI (debloat có blacklist an toàn)
- Khóa tần số quét (Hz) + backup theo serial
- Device spoof, ad-block hosts, app permissions
- Fastboot, cài APK đi kèm (Shizuku, Settings…)
- Giao diện macOS-style (WPF)
- Cài đặt tự động: triển khai ADB/Fastboot, driver USB, bootstrap headless

## Yêu cầu (người dùng)

| | |
|---|---|
| OS | Windows 10/11 **64-bit** |
| Runtime | Không cần cài .NET (bản publish self-contained) |
| Điện thoại | Xiaomi / Redmi / POCO, bật **USB Debugging** |
| Cài đặt | Quyền **Administrator** (installer) |

## Tải bản build

Phát hành tại [Releases](https://github.com/MintKtc/MintADB/releases):

- `MintADB-Setup-v1.0.0-win-x64.exe` — installer (khuyến nghị)
- `MintADB-v1.0.0-win-x64.zip` — portable

## Phát triển

### Yêu cầu môi trường

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (Desktop / WPF)
- [Git](https://git-scm.com/)
- *(Tùy chọn)* [Visual Studio 2022](https://visualstudio.microsoft.com/) hoặc VS Code
- *(Build installer)* [Inno Setup 6](https://jrsoftware.org/isinfo.php)

Kiểm tra nhanh:

```bat
setup-dev.bat
```

### Clone & chuẩn bị tài nguyên

```bash
git clone https://github.com/MintKtc/MintADB.git
cd MintADB
```

Copy **PlatformTools**, **Drivers**, **Miui** theo hướng dẫn: [docs/BUNDLED-ASSETS.md](docs/BUNDLED-ASSETS.md).

### Chạy dev

```bat
run-wpf.bat
```

hoặc:

```bash
cd src/MintADB.Wpf
dotnet run
```

### Build & publish

```powershell
# Self-contained (~910 MB)
powershell -ExecutionPolicy Bypass -File scripts\publish.ps1 -Profile standalone

# ZIP phát hành
powershell -ExecutionPolicy Bypass -File scripts\publish.ps1 -Profile zip

# Installer (cần Inno Setup)
powershell -ExecutionPolicy Bypass -File scripts\publish.ps1 -Profile installer
```

Output: `dist/MintADB/`, `dist/MintADB-Setup-*.exe`

### Cấu trúc mã nguồn

```
MintADB/
├── src/MintADB.Wpf/          # Ứng dụng WPF chính
│   ├── Services/             # ADB, Shizuku, driver, bootstrap…
│   ├── MainWindow.*.cs       # Partial classes theo tab/tính năng
│   ├── Themes/MacOS.xaml     # Giao diện
│   ├── PlatformTools/        # (local) adb/fastboot
│   ├── Drivers/              # (local) USB driver
│   └── Miui/                 # (local) APK đi kèm
├── scripts/
│   ├── publish.ps1           # Build profiles
│   └── MintADB.iss           # Inno Setup
├── docs/
└── exe.ico                   # Icon app & installer
```

### Dữ liệu người dùng (runtime)

| Vị trí | Nội dung |
|--------|----------|
| `%LOCALAPPDATA%\MintADB\` | ADB triển khai, `install-state.json` |
| `Desktop\MintADB\` | Screenshot, backup Hz/spoof, recording |

## Đóng góp

1. Fork repository
2. Tạo branch: `git checkout -b feature/ten-tinh-nang`
3. Commit & push
4. Mở Pull Request

Báo lỗi / đề xuất: [Issues](https://github.com/MintKtc/MintADB/issues)

## Giấy phép

[MIT](LICENSE) — Copyright (c) 2026 MintADB

### Bên thứ ba

- **Platform-Tools** — [Android SDK License](https://developer.android.com/studio/terms)
- **Google USB Driver** — Google / Android Open Source
- APK trong `Miui/` — thuộc chủ sở hữu tương ứng (Shizuku, Xiaomi…)

## English (short)

MintADB is a Windows desktop ADB toolkit for Xiaomi / HyperOS devices. Built with **C# / WPF / .NET 8**. See [docs/BUNDLED-ASSETS.md](docs/BUNDLED-ASSETS.md) for offline assets required to build locally.