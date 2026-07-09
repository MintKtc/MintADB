# MintADB — Release builds

Thư mục **mặc định** chứa file cài đặt sau khi build:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\publish.ps1 -Profile installer -Version 1.0.0
```

## Output

| File | Mô tả |
|------|--------|
| `MintADB-Setup-v1.0.0-win-x64.exe` | Installer Windows x64 (self-contained) |

**Cài mặc định trên PC:** `C:\Program Files\MintADB`

## Ghi chú

- File `.exe` **không** commit lên Git (dung lượng lớn) — tải từ [GitHub Releases](https://github.com/MintKtc/MintADB/releases).
- Bản portable (thư mục chạy trực tiếp): `..\dist\MintADB\`
