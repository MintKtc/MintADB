package com.minthd.mintadb.core

data class RomInfo(
    val manufacturer: String,
    val brand: String,
    val model: String,
    val androidVersion: String,
    val buildId: String,
    val region: String,
    val osVersion: String,
    val isXiaomi: Boolean,
    val isChinaRom: Boolean,
    val isHyperOs: Boolean,
)

object DeviceInfoReader {
    suspend fun read(): RomInfo {
        val manufacturer = ShellRunner.getProp("ro.product.manufacturer")
        val brand = ShellRunner.getProp("ro.product.brand")
        val model = ShellRunner.getProp("ro.product.model")
        val android = ShellRunner.getProp("ro.build.version.release")
        val build = ShellRunner.getProp("ro.build.display.id")
        var region = ShellRunner.getProp("ro.miui.region")
        if (region.isBlank()) region = ShellRunner.getProp("ro.product.locale.region")
        val locale = ShellRunner.getProp("ro.product.locale")
        val hyper = ShellRunner.getProp("ro.mi.os.version.name")
        val miui = ShellRunner.getProp("ro.miui.ui.version.name")
        val blob = (region + locale + build).lowercase()
        val isChina = blob.contains("cn") || blob.contains("china") || build.endsWith("CNXM", true)
        val m = manufacturer.lowercase()
        val b = brand.lowercase()
        val isXiaomi = m in listOf("xiaomi", "redmi", "poco") || b in listOf("xiaomi", "redmi", "poco")
        return RomInfo(
            manufacturer = manufacturer,
            brand = brand,
            model = model,
            androidVersion = android,
            buildId = build,
            region = region,
            osVersion = hyper.ifBlank { miui },
            isXiaomi = isXiaomi,
            isChinaRom = isChina,
            isHyperOs = hyper.isNotBlank(),
        )
    }
}