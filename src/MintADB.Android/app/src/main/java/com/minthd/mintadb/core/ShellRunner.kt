package com.minthd.mintadb.core

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import rikka.shizuku.Shizuku
import java.io.BufferedReader
import java.io.InputStreamReader

data class ShellResult(val exitCode: Int, val output: String)

object ShellRunner {
    suspend fun run(command: String): ShellResult = withContext(Dispatchers.IO) {
        if (!Shizuku.pingBinder()) {
            return@withContext ShellResult(1, "Shizuku not running — open Shizuku app first.")
        }
        try {
            val process = Shizuku.newProcess(arrayOf("sh", "-c", command), null, null)
            val stdout = BufferedReader(InputStreamReader(process.inputStream)).readText()
            val stderr = BufferedReader(InputStreamReader(process.errorStream)).readText()
            val code = process.waitFor()
            ShellResult(code, (stdout + stderr).trim())
        } catch (e: Exception) {
            ShellResult(1, e.message ?: "Shell error")
        }
    }

    suspend fun getProp(name: String): String =
        run("getprop $name").output.trim().removeSurrounding("\"")

    suspend fun settingsGet(ns: String, key: String): String =
        run("settings get $ns $key").output.trim()

    suspend fun settingsPut(ns: String, key: String, value: String): ShellResult =
        run("settings put $ns $key $value")
}