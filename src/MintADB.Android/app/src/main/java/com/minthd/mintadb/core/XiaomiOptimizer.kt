package com.minthd.mintadb.core

data class OptimizeStep(val label: String, val ok: Boolean, val detail: String)

object XiaomiOptimizer {
    private val miuiPkgWhitelists = listOf(
        "rt_pkg_white_list", "power_pkg_white_list", "power_alarm_white_list",
        "power_broadcast_white_list", "perf_proc_protect_list", "frozen_new_whitelist",
        "doze_whitelist_apps", "cluster_whitelist", "msystem_whitelist",
        "battery_optimization_whitelist_apps", "cloud_lowlatency_whitelist",
    )

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

    private val appOpsModes = listOf(
        "RUN_IN_BACKGROUND", "RUN_ANY_IN_BACKGROUND", "WAKE_LOCK", "START_FOREGROUND",
        "POST_NOTIFICATION", "IGNORE_BATTERY_OPTIMIZATIONS",
    )

    private val grantPermissions = listOf(
        "android.permission.POST_NOTIFICATIONS",
        "android.permission.RECEIVE_BOOT_COMPLETED",
        "android.permission.VIBRATE",
        "android.permission.ACCESS_NETWORK_STATE",
        "android.permission.ACCESS_WIFI_STATE",
        "android.permission.FOREGROUND_SERVICE",
        "android.permission.REQUEST_IGNORE_BATTERY_OPTIMIZATIONS",
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

    suspend fun disableMiuiOptimization(): OptimizeStep {
        val r = ShellRunner.settingsPut("global", "miui_optimization", "false")
        return OptimizeStep("Tắt MIUI optimization (cần reboot)", r.exitCode == 0, r.output)
    }

    suspend fun grantPermissions(packageName: String): List<OptimizeStep> {
        if (!AppScanner.isInstalled(packageName)) {
            return listOf(OptimizeStep("$packageName — chưa cài", false, "skip"))
        }
        val steps = grantPermissions.map { perm ->
            val r = ShellRunner.run("pm grant $packageName $perm")
            val ok = r.exitCode == 0 || r.output.contains("already", true)
            OptimizeStep(perm.substringAfterLast('.'), ok, r.output)
        }.toMutableList()
        val overlay = ShellRunner.run("cmd appops set $packageName SYSTEM_ALERT_WINDOW allow")
        steps += OptimizeStep("SYSTEM_ALERT_WINDOW", overlay.exitCode == 0, overlay.output)
        return steps
    }

    suspend fun fixAppNotifications(preset: AppPreset): List<OptimizeStep> {
        if (!AppScanner.isInstalled(preset.packageName)) {
            return listOf(OptimizeStep("${preset.name} — chưa cài", false, "skip"))
        }
        val steps = mutableListOf<OptimizeStep>()
        for (key in miuiPkgWhitelists) {
            steps += appendWhitelist(key, listOf(preset.packageName))
        }
        if (preset.processes.isNotEmpty()) steps += appendWhitelist("power_proc_white_list", preset.processes)
        if (preset.services.isNotEmpty()) steps += appendWhitelist("power_service_white_list", preset.services)

        val idle = ShellRunner.run("dumpsys deviceidle whitelist +${preset.packageName}")
        steps += OptimizeStep("deviceidle whitelist", idle.exitCode == 0, idle.output)
        val bucket = ShellRunner.run("am set-standby-bucket ${preset.packageName} active")
        steps += OptimizeStep("standby-bucket active", bucket.exitCode == 0, bucket.output)
        for (mode in appOpsModes) {
            val r = ShellRunner.run("cmd appops set ${preset.packageName} $mode allow")
            steps += OptimizeStep("appops $mode", r.exitCode == 0, r.output)
        }
        val autostart = ShellRunner.run(
            "am broadcast -a miui.intent.action.POWER_HIDE_MODE_APP_LIST " +
                "--es package_name ${preset.packageName} --ez enable true"
        )
        steps += OptimizeStep("MIUI autostart", autostart.exitCode == 0, autostart.output)
        return steps
    }

    suspend fun fullOptimize(options: OptimizeOptions): List<OptimizeStep> {
        val steps = mutableListOf<OptimizeStep>()
        val rom = DeviceInfoReader.read()
        steps += OptimizeStep("ROM", true, "${rom.brand} ${rom.model} · ${if (rom.isChinaRom) "China" else "Global"}")

        if (options.globalRelax) steps += applyGlobalRelax()
        if (options.chinaUnlock) steps += applyChinaUnlock()
        if (options.disableMiuiOpt) steps += disableMiuiOptimization()

        val installedPresets = AppPreset.defaults.filter { AppScanner.isInstalled(it.packageName) }
        for (preset in installedPresets) {
            steps += fixAppNotifications(preset)
            if (options.grantPerms) steps += grantPermissions(preset.packageName)
        }
        steps += OptimizeStep("Hoàn tất", true, "${installedPresets.size} app đã xử lý")
        return steps
    }

    private suspend fun appendWhitelist(key: String, items: List<String>): OptimizeStep {
        val current = ShellRunner.settingsGet("system", key)
        val set = current.split(',').map { it.trim() }.filter { it.isNotEmpty() }.toMutableSet()
        val before = set.size
        items.forEach { set.add(it) }
        if (set.size == before) return OptimizeStep("whitelist $key", true, "no change")
        val merged = set.sorted().joinToString(",")
        val r = ShellRunner.settingsPut("system", key, merged)
        return OptimizeStep("whitelist $key (+${set.size - before})", r.exitCode == 0, r.output)
    }
}