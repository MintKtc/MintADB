# Tài nguyên đi kèm (Bundled Assets)

Các thư mục sau **không** nằm trong Git (dung lượng lớn / bản quyền bên thứ ba). Bạn cần chuẩn bị thủ công trước khi build hoặc publish.

## PlatformTools (~17 MB)

Tải [Android SDK Platform-Tools](https://developer.android.com/tools/releases/platform-tools) và giải nén vào:

```
src/MintADB.Wpf/PlatformTools/
```

Cần có ít nhất: `adb.exe`, `fastboot.exe`, `AdbWinApi.dll`, `AdbWinUsbApi.dll`.

## Drivers (~9 MB)

Tải [Google USB Driver](https://developer.android.com/studio/run/win-usb) và đặt vào:

```
src/MintADB.Wpf/Drivers/usb_driver/
```

Cần có: `android_winusb.inf` và thư mục `amd64/`, `i386/`.

## Miui (APK, ~700 MB)

Đặt các APK tùy chọn vào:

```
src/MintADB.Wpf/Miui/
```

Ví dụ: Shizuku, Settings việt hóa, Gboard, Play Store… App vẫn chạy nếu thiếu Miui; chỉ mất tính năng cài APK đi kèm.