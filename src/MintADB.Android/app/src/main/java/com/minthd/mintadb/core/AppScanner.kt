package com.minthd.mintadb.core

object AppScanner {
    private fun parsePackages(output: String): Set<String> =
        output.lineSequence()
            .mapNotNull { line ->
                val trimmed = line.trim()
                if (!trimmed.startsWith("package:")) return@mapNotNull null
                val rest = trimmed.removePrefix("package:").trim()
                rest.substringBefore(' ').ifBlank { null }
            }
            .toSet()

    suspend fun scanActiveApps(): List<InstalledApp> {
        val detail = ShellRunner.run("pm list packages -i -U")
        val enabled = ShellRunner.run("pm list packages -e")
        val system = ShellRunner.run("pm list packages -s")
        val disabled = ShellRunner.run("pm list packages -d")
        var hidden = ShellRunner.run("pm list packages --hidden")
        if (hidden.output.isBlank()) hidden = ShellRunner.run("pm list packages -h")

        val systemPkgs = parsePackages(system.output)
        val enabledPkgs = parsePackages(enabled.output)
        val disabledPkgs = parsePackages(disabled.output)
        val hiddenPkgs = parsePackages(hidden.output)
        val allPkgs = parsePackages(detail.output)

        val activePkgs = if (enabledPkgs.isNotEmpty()) {
            enabledPkgs - hiddenPkgs
        } else {
            allPkgs - disabledPkgs - hiddenPkgs
        }

        return detail.output.lineSequence()
            .mapNotNull { line ->
                val trimmed = line.trim()
                if (!trimmed.startsWith("package:")) return@mapNotNull null
                val pkg = trimmed.removePrefix("package:").substringBefore(' ').trim()
                if (pkg.isEmpty() || pkg !in activePkgs) return@mapNotNull null
                val installer = Regex("installer=(\\S+)").find(trimmed)?.groupValues?.getOrNull(1) ?: ""
                val isSystem = pkg in systemPkgs
                InstalledApp(
                    packageName = pkg,
                    label = AppClassifier.displayName(pkg),
                    category = AppClassifier.classify(pkg, isSystem, installer),
                    installer = installer,
                    isSystem = isSystem,
                )
            }
            .sortedWith(compareBy({ it.category.sortOrder }, { it.label.lowercase() }))
            .toList()
    }

    suspend fun scanInactiveApps(): List<InactiveApp> {
        val installed = parsePackages(ShellRunner.run("pm list packages").output)
        val disabled = parsePackages(ShellRunner.run("pm list packages -d").output)
        val uninstalled = parsePackages(ShellRunner.run("pm list packages -u").output) - installed
        var hidden = parsePackages(ShellRunner.run("pm list packages --hidden").output)
        if (hidden.isEmpty()) hidden = parsePackages(ShellRunner.run("pm list packages -h").output)

        val states = linkedMapOf<String, InactiveAppState>()
        uninstalled.forEach { states[it] = InactiveAppState.Uninstalled }
        disabled.forEach { states[it] = InactiveAppState.Disabled }
        hidden.forEach { if (it !in states) states[it] = InactiveAppState.Hidden }

        return states.map { (pkg, state) ->
            InactiveApp(pkg, AppClassifier.displayName(pkg), state)
        }.sortedWith(compareBy({ it.state.sortOrder }, { it.label.lowercase() }))
    }

    suspend fun disable(packageName: String): ShellResult {
        if (DebloatBlacklist.isProtected(packageName)) {
            return ShellResult(1, DebloatBlacklist.getReason(packageName) ?: "Protected package")
        }
        return ShellRunner.run("pm disable-user --user 0 $packageName")
    }

    suspend fun uninstall(packageName: String): ShellResult {
        if (DebloatBlacklist.isProtected(packageName)) {
            return ShellResult(1, DebloatBlacklist.getReason(packageName) ?: "Protected package")
        }
        return ShellRunner.run("pm uninstall --user 0 $packageName")
    }

    suspend fun enable(packageName: String): ShellResult =
        ShellRunner.run("pm enable $packageName")

    suspend fun installExisting(packageName: String): ShellResult =
        ShellRunner.run("cmd package install-existing $packageName")

    suspend fun isInstalled(packageName: String): Boolean {
        val r = ShellRunner.run("pm path $packageName")
        return r.exitCode == 0 && r.output.contains("package:")
    }
}