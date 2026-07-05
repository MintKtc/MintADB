package com.minthd.mintadb.core

object AppClassifier {
    private val playStoreInstallers = setOf("com.android.vending", "com.google.android.packageinstaller")

    private val essentialPrefixes = listOf(
        "android", "com.android.systemui", "com.android.settings", "com.android.phone",
        "com.google.android.gms", "com.miui.home", "com.miui.securitycenter",
        "com.xiaomi.account", "com.xiaomi.finddevice",
    )

    private val bloatPrefixes = listOf(
        "com.miui.cleaner", "com.miui.analytics", "com.miui.hybrid", "com.miui.videoplayer",
        "com.miui.yellowpage", "com.miui.weather", "com.xiaomi.market", "com.xiaomi.mipicks",
        "com.baidu.", "com.tencent.", "com.sina.", "com.taobao.",
    )

    private val knownBloat = setOf(
        "com.miui.systemAdSolution", "com.miui.android.fashiongallery", "com.xiaomi.discover",
        "com.lbe.security.miui", "com.android.soundrecorder",
    )

    fun classify(packageName: String, isSystem: Boolean, installer: String?): AppCategory {
        if (essentialPrefixes.any { packageName.startsWith(it) || packageName == it }) {
            return AppCategory.System
        }
        if (isRomBloat(packageName, isSystem, installer)) return AppCategory.RomBloat
        if (isSystem) return AppCategory.System
        if (!installer.isNullOrBlank() && installer in playStoreInstallers) return AppCategory.PlayStore
        return AppCategory.UserInstalled
    }

    fun displayName(packageName: String): String {
        AppPreset.defaults.firstOrNull { it.packageName == packageName }?.let { return it.name }
        val last = packageName.substringAfterLast('.')
        if (last.length <= 2) return packageName
        return last.replaceFirstChar { it.uppercase() }
    }

    private fun isRomBloat(packageName: String, isSystem: Boolean, installer: String?): Boolean {
        if (packageName in knownBloat) return true
        if (isSystem && bloatPrefixes.any { packageName.startsWith(it) }) return true
        val noInstaller = installer.isNullOrBlank() || installer == "null"
        if (noInstaller && (packageName.startsWith("com.miui.") || packageName.startsWith("com.xiaomi."))) {
            return true
        }
        if (installer == "com.miui.packageinstaller" || installer == "com.xiaomi.market") return true
        return false
    }
}