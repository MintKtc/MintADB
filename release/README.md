# MintADB — Release builds

Thư mục **mặc định** chứa file cài đặt sau khi build:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\publish.ps1 -Profile installer -Version 1.0.2
```

## Output

| File | Mô tả |
|------|--------|
| `MintADB-Setup-v1.0.2-win-x64.exe` | Installer Windows x64 (self-contained) + scrcpy 4.0 |

**Tải bản phát hành:** https://github.com/MintKtc/MintADB/releases/tag/v1.0.2

**Cài mặc định trên PC:** `C:\Program Files\MintADB`

## Ghi chú

- File `.exe` lớn được track bằng **Git LFS** (`release/*.exe`).
- Chỉ giữ bản **v1.0.2** (các bản 1.0.0 / 1.0.1 đã gỡ vì lỗi thiếu scrcpy / locator).
- Bản portable (thư mục chạy trực tiếp): `..\dist\MintADB\`
- scrcpy nằm trong `PlatformTools\scrcpy\` sau khi cài.
