package com.minthd.mintadb.core

import android.content.ComponentName
import android.content.ServiceConnection
import android.os.IBinder
import com.minthd.mintadb.IShellService
import com.minthd.mintadb.service.ShellUserService
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlinx.coroutines.suspendCancellableCoroutine
import kotlinx.coroutines.withContext
import rikka.shizuku.Shizuku
import kotlin.coroutines.resume

data class ShellResult(val exitCode: Int, val output: String)

object ShellRunner {
    private var shellService: IShellService? = null
    private val bindLock = Mutex()

    private val serviceArgs =
        Shizuku.UserServiceArgs(
            ComponentName("com.minthd.mintadb", ShellUserService::class.java.name)
        )
            .daemon(true)
            .version(1)

    private val serviceConnection = object : ServiceConnection {
        override fun onServiceConnected(name: ComponentName, binder: IBinder) {
            shellService = IShellService.Stub.asInterface(binder)
        }

        override fun onServiceDisconnected(name: ComponentName) {
            shellService = null
        }
    }

    private suspend fun getService(): IShellService? {
        shellService?.let { return it }
        if (!Shizuku.pingBinder()) return null

        return bindLock.withLock {
            shellService?.let { return it }
            suspendCancellableCoroutine { cont ->
                try {
                    Shizuku.bindUserService(
                        serviceArgs,
                        object : ServiceConnection {
                            override fun onServiceConnected(name: ComponentName, binder: IBinder) {
                                val service = IShellService.Stub.asInterface(binder)
                                shellService = service
                                cont.resume(service)
                            }

                            override fun onServiceDisconnected(name: ComponentName) {
                                shellService = null
                            }
                        }
                    )
                } catch (e: Exception) {
                    cont.resume(null)
                }
            }
        }
    }

    suspend fun run(command: String): ShellResult = withContext(Dispatchers.IO) {
        if (!Shizuku.pingBinder()) {
            return@withContext ShellResult(1, "Shizuku not running — open Shizuku app first.")
        }
        try {
            val service = getService()
                ?: return@withContext ShellResult(1, "Shizuku service unavailable — grant permission first.")
            val raw = service.run(command)
            val newline = raw.indexOf('\n')
            val code = if (newline >= 0) raw.substring(0, newline).toIntOrNull() ?: 1 else 1
            val output = if (newline >= 0) raw.substring(newline + 1) else raw
            ShellResult(code, output.trim())
        } catch (e: Exception) {
            shellService = null
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