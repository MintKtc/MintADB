package com.minthd.mintadb.core

object DebloatBlacklist {
    private val reasons = mapOf(
        "com.lbe.security.miui" to "Permission manager — bootloop risk",
        "com.android.updater" to "System updater — bootloop risk",
        "com.miui.securitycenter" to "Mi Security — bootloop risk",
        "com.miui.securityadd" to "Mi Security add-on",
        "com.xiaomi.finddevice" to "Find device — bootloop risk",
        "com.miui.home" to "System launcher — bootloop risk",
        "com.miui.guardprovider" to "MIUI security component",
        "com.xiaomi.market" to "GetApps / Xiaomi store",
        "com.xiaomi.account" to "Xiaomi account",
        "com.miui.packageinstaller" to "MIUI package installer",
    )

    fun isProtected(packageName: String): Boolean = packageName in reasons

    fun getReason(packageName: String): String? = reasons[packageName]
}