package com.minthd.mintadb.core

data class InstalledApp(val packageName: String, val label: String, val isSystem: Boolean)

object AppScanner {
    suspend fun listUserApps(): List<InstalledApp> {
        val r = ShellRunner.run("pm list packages -3")
        if (r.exitCode != 0) return emptyList()
        return r.output.lines()
            .mapNotNull { line ->
                val pkg = line.removePrefix("package:").trim()
                if (pkg.isEmpty()) null else InstalledApp(pkg, pkg, isSystem = false)
            }
            .sortedBy { it.packageName }
    }

    suspend fun disable(packageName: String): ShellResult {
        if (DebloatBlacklist.isProtected(packageName)) {
            return ShellResult(1, DebloatBlacklist.getReason(packageName) ?: "Protected package")
        }
        return ShellRunner.run("pm disable-user --user 0 $packageName")
    }
}