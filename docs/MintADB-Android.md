# MintADB Android (companion APK)

On-device companion for **MintADB PC** — same optimizer logic, runs **on your Xiaomi phone** via **Shizuku**.

**Developer:** MINT_HD · `com.minthd.mintadb`

## PC app vs Android APK

| Feature | MintADB PC | MintADB APK |
|---------|------------|-------------|
| Control phone from PC (ADB) | Yes | No |
| Multi-device | Yes | No (this device only) |
| Fastboot / flash | Yes | No |
| Global relax / China unlock | Yes | Yes (Shizuku) |
| Debloat user apps | Yes | Yes (disable, blacklist) |
| Device / ROM info | Yes | Yes |
| Display Hz read | Yes | Yes |
| Lock Hz / device spoof | Yes | Limited (needs extra perms / root) |
| Shizuku setup | Via ADB | Native |

## Requirements

- Android 8+ (API 26+)
- **Shizuku** installed (`moe.shizuku.privileged.api`) and started
- Xiaomi / Redmi / POCO recommended

## Build APK

1. Install [Android Studio](https://developer.android.com/studio)
2. Open folder: `src/MintADB.Android`
3. **File → Sync Project with Gradle Files**
4. **Build → Build Bundle(s) / APK(s) → Build APK(s)**
5. Output: `app/build/outputs/apk/debug/app-debug.apk`

Release:

```bash
cd src/MintADB.Android
./gradlew assembleRelease
```

## Icon

Replace launcher icon with project `exe.ico` via Android Studio **Image Asset** (Adaptive Icon).

## Tabs (like PC app)

1. **Device** — ROM, model, Shizuku status
2. **Optimize** — Global relax, China unlock (ported from WPF)
3. **Apps** — User apps, disable (debloat blacklist)
4. **Display** — Peak/min refresh rate

## Tiếng Việt

APK chạy **trên điện thoại**, không thay thế bản PC. Cần **Shizuku** để chạy lệnh shell như app desktop qua ADB. Mở project bằng Android Studio và build APK như trên.