package com.minthd.mintadb.core

object SystemTools {
    suspend fun reboot(): ShellResult = ShellRunner.run("svc power reboot")
    suspend fun rebootRecovery(): ShellResult = ShellRunner.run("svc power reboot recovery")
    suspend fun rebootBootloader(): ShellResult = ShellRunner.run("svc power reboot bootloader")

    suspend fun forceStop(packageName: String): ShellResult =
        ShellRunner.run("am force-stop $packageName")

    suspend fun clearData(packageName: String): ShellResult =
        ShellRunner.run("pm clear $packageName")

    suspend fun launchApp(packageName: String): ShellResult {
        val resolve = ShellRunner.run("cmd package resolve-activity --brief $packageName | tail -n 1")
        val component = resolve.output.trim()
        return if (component.isNotBlank() && component.contains("/")) {
            ShellRunner.run("am start -n $component")
        } else {
            ShellRunner.run("monkey -p $packageName -c android.intent.category.LAUNCHER 1")
        }
    }

    suspend fun runShell(command: String): ShellResult = ShellRunner.run(command)
}