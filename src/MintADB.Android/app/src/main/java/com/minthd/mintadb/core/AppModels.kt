package com.minthd.mintadb.core

enum class AppCategory(val labelVi: String, val sortOrder: Int) {
    PlayStore("Play Store", 0),
    UserInstalled("User", 1),
    RomBloat("Rác ROM", 2),
    System("Hệ thống", 3),
}

enum class InactiveAppState(val labelVi: String, val sortOrder: Int) {
    Disabled("Đã tắt", 0),
    Hidden("Đã ẩn", 1),
    Uninstalled("Đã gỡ", 2),
}

data class InstalledApp(
    val packageName: String,
    val label: String,
    val category: AppCategory,
    val installer: String = "",
    val isSystem: Boolean = false,
)

data class InactiveApp(
    val packageName: String,
    val label: String,
    val state: InactiveAppState,
)

data class OptimizeOptions(
    val globalRelax: Boolean = true,
    val chinaUnlock: Boolean = true,
    val grantPerms: Boolean = true,
    val disableMiuiOpt: Boolean = false,
)

data class HzLockOptions(
    val targetHz: Int = 120,
    val miuiTweaks: Boolean = true,
    val smoothUi: Boolean = true,
    val boostGpu: Boolean = true,
)