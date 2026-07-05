package com.minthd.mintadb.core

data class DisplayHzInfo(
    val peakHz: String,
    val minHz: String,
    val currentMode: String,
)

object DisplayInfoReader {
    suspend fun read(): DisplayHzInfo {
        val peak = ShellRunner.settingsGet("secure", "peak_refresh_rate")
        val min = ShellRunner.settingsGet("secure", "min_refresh_rate")
        val mode = ShellRunner.run("dumpsys display | grep -m1 mActiveMode").output
        return DisplayHzInfo(
            peakHz = if (peak == "null") "n/a" else peak,
            minHz = if (min == "null") "n/a" else min,
            currentMode = mode.ifBlank { "n/a" },
        )
    }
}