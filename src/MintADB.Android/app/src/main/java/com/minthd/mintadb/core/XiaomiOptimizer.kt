package com.minthd.mintadb.core

data class OptimizeStep(val label: String, val ok: Boolean, val detail: String)

object XiaomiOptimizer {
    private val globalRelax = listOf(
        Triple("global", "app_standby_enabled", "0"),
        Triple("secure", "adaptive_battery_management_enabled", "0"),
        Triple("global", "power_supersave_mode_enabled", "0"),
        Triple("global", "notification_listener_timeout", "0"),
        Triple("global", "miui_restricted_mode_enabled", "0"),
        Triple("global", "cached_apps_freezer_enabled", "0"),
        Triple("global", "app_hibernation_enabled", "0"),
        Triple("global", "settings_enable_monitor_phantom_procs", "false"),
    )

    private val chinaUnlock = listOf(
        Triple("global", "tn_disable_cloud_strategy", "1"),
        Triple("system", "POWER_CLOUD_INTERCEPT_ENABLE", "1"),
        Triple("global", "app_auto_startup_switch", "1"),
        Triple("global", "app_force_stop_behavior", "0"),
        Triple("secure", "forced_app_standby_enabled", "0"),
        Triple("global", "app_auto_revive_enabled", "1"),
        Triple("global", "app_kill_protection_enabled", "1"),
        Triple("global", "miui_optimization_whitelist_enabled", "1"),
        Triple("global", "miui_app_control_enabled", "0"),
    )

    suspend fun applyGlobalRelax(): List<OptimizeStep> =
        globalRelax.map { (ns, key, value) ->
            val r = ShellRunner.settingsPut(ns, key, value)
            OptimizeStep("settings $ns $key=$value", r.exitCode == 0, r.output)
        }

    suspend fun applyChinaUnlock(): List<OptimizeStep> =
        chinaUnlock.map { (ns, key, value) ->
            val r = ShellRunner.settingsPut(ns, key, value)
            OptimizeStep("settings $ns $key=$value", r.exitCode == 0, r.output)
        }
}