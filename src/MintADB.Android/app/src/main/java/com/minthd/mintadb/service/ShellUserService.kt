package com.minthd.mintadb.service

import com.minthd.mintadb.IShellService
import java.io.BufferedReader
import java.io.InputStreamReader

class ShellUserService : IShellService.Stub() {
    override fun destroy() {
        System.exit(0)
    }

    override fun exit() {
        destroy()
    }

    override fun run(command: String): String {
        val process = Runtime.getRuntime().exec(arrayOf("sh", "-c", command))
        val stdout = BufferedReader(InputStreamReader(process.inputStream)).readText()
        val stderr = BufferedReader(InputStreamReader(process.errorStream)).readText()
        val code = process.waitFor()
        return "$code\n${(stdout + stderr).trim()}"
    }
}