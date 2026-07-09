# MintADB — Release builds

Thư mục **mặc định** chứa file cài đặt sau khi build:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\publish.ps1 -Profile installer -Version 1.0.2
```

## Output

| File | Mô tả |
|------|--------|
| `MintADB-Setup-v1.0.2-win-x64.exe` | **Khuyến nghị** — scrcpy 4.0 + locator/bootstrap sửa lỗi gỡ/cài lại |
| `MintADB-Setup-v1.0.1-win-x64.exe` | Có scrcpy, locator cũ |
| `MintADB-Setup-v1.0.0-win-x64.exe` | Bản cũ (**không** kèm scrcpy) — đừng dùng nếu cần mirror |

**Cài mặc định trên PC:** `C:\Program Files\MintADB`

## Ghi chú

- File `.exe` lớn được track bằng **Git LFS** (`release/*.exe`).
- Bản portable (thư mục chạy trực tiếp): `..\dist\MintADB\`
- scrcpy nằm trong `PlatformTools\scrcpy\` sau khi cài (publish.ps1 tự bundle).