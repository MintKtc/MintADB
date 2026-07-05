package com.minthd.mintadb.core

object DisplayPerformance {
    suspend fun readStatus(): String {
        val keys = listOf(
            Triple("system", "peak_refresh_rate", "peak (system)"),
            Triple("system", "min_refresh_rate", "min (system)"),
            Triple("global", "peak_refresh_rate", "peak (global)"),
            Triple("secure", "refresh_rate_mode", "mode"),
            Triple("secure", "adaptive_refresh_rate", "adaptive"),
            Triple("system", "miui_refresh_rate", "miui_hz"),
        )
        val lines = keys.mapNotNull { (ns, key, label) ->
            val v = ShellRunner.settingsGet(ns, key)
            if (v.isNotBlank() && v != "null") "$label=$v" else null
        }.toMutableList()
        val hw = ShellRunner.run("dumpsys display | grep -i refresh")
        Regex("(\\d+(?:\\.\\d+)?)\\s*Hz", RegexOption.IGNORE_CASE)
            .find(hw.output)?.groupValues?.getOrNull(1)?.let { lines += "active≈${it}Hz" }
        return if (lines.isEmpty()) "(không đọc được — thử Khóa Hz)" else lines.joinToString("\n")
    }

    suspend fun applyLockHz(options: HzLockOptions): List<OptimizeStep> {
        val hz = options.targetHz.toString()
        val commands = mutableListOf(
            Triple("system", "peak_refresh_rate", hz),
            Triple("system", "min_refresh_rate", hz),
            Triple("global", "peak_refresh_rate", hz),
            Triple("global", "min_refresh_rate", hz),
            Triple("secure", "peak_refresh_rate", hz),
            Triple("secure", "min_refresh_rate", hz),
            Triple("system", "screen_refresh_rate", hz),
            Triple("secure", "refresh_rate", hz),
            Triple("secure", "refresh_rate_mode", "1"),
            Triple("secure", "adaptive_refresh_rate", "0"),
            Triple("system", "adaptive_refresh_rate", "0"),
            Triple("global", "game_driver_all_apps", "1"),
            Triple("global", "game_mode_intervention_enabled", "0"),
        )
        if (options.miuiTweaks) {
            commands += listOf(
                Triple("system", "miui_refresh_rate", hz),
                Triple("system", "user_refresh_rate", hz),
                Triple("secure", "user_refresh_rate", hz),
                Triple("global", "miui_smart_fps_mode", "0"),
                Triple("global", "power_save_120hz_mode", "0"),
                Triple("global", "miui_game_booster", "0"),
                Triple("global", "enhanced_mode", "1"),
            )
        }
        return commands.map { (ns, key, value) ->
            val r = ShellRunner.settingsPut(ns, key, value)
            OptimizeStep("settings $ns $key=$value", r.exitCode == 0, r.output)
        }
    }

    suspend fun applySmoothUi(boostGpu: Boolean): List<OptimizeStep> {
        val commands = mutableListOf(
            Triple("global", "window_animation_scale", "1.0"),
            Triple("global", "transition_animation_scale", "1.0"),
            Triple("global", "animator_duration_scale", "1.0"),
            Triple("global", "disable_animations", "0"),
            Triple("system", "touch_response", "1"),
            Triple("secure", "show_refresh_rate", "0"),
        )
        if (boostGpu) {
            commands += listOf(
                Triple("global", "force_gpu_rendering", "1"),
                Triple("global", "hwui.renderer", "skiagl"),
                Triple("global", "game_driver_all_apps", "1"),
            )
        }
        return commands.map { (ns, key, value) ->
            val r = ShellRunner.settingsPut(ns, key, value)
            OptimizeStep("settings $ns $key=$value", r.exitCode == 0, r.output)
        }
    }

    suspend fun restoreAdaptive(): List<OptimizeStep> = listOf(
        Triple("secure", "adaptive_refresh_rate", "1"),
        Triple("system", "adaptive_refresh_rate", "1"),
        Triple("secure", "refresh_rate_mode", "0"),
    ).map { (ns, key, value) ->
        val r = ShellRunner.settingsPut(ns, key, value)
        OptimizeStep("restore $ns $key=$value", r.exitCode == 0, r.output)
    }
}