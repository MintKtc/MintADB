# Bundled Assets / Tài nguyên đi kèm

These folders are **not** in Git (large size / third-party licenses). Prepare them locally before build or publish.

Các thư mục sau **không** nằm trong Git (dung lượng lớn / bản quyền bên thứ ba). Chuẩn bị thủ công trước khi build.

---

## PlatformTools (~17 MB)

**EN:** Download [Android SDK Platform-Tools](https://developer.android.com/tools/releases/platform-tools) and extract to:

**VI:** Tải [Android SDK Platform-Tools](https://developer.android.com/tools/releases/platform-tools) và giải nén vào:

```
src/MintADB.Wpf/PlatformTools/
```

Required: `adb.exe`, `fastboot.exe`, `AdbWinApi.dll`, `AdbWinUsbApi.dll`.

---

## Drivers (~9 MB)

**EN:** Download [Google USB Driver](https://developer.android.com/studio/run/win-usb) and place in:

**VI:** Tải [Google USB Driver](https://developer.android.com/studio/run/win-usb) và đặt vào:

```
src/MintADB.Wpf/Drivers/usb_driver/
```

Required: `android_winusb.inf`, `amd64/`, `i386/`.

---

## Miui APKs (~700 MB, optional)

**EN:** Place optional APKs in:

**VI:** Đặt APK tùy chọn vào:

```
src/MintADB.Wpf/Miui/
```

Examples: Shizuku, localized Settings, Gboard, Play Store. The app runs without Miui; only bundled APK features are missing.