package com.minthd.mintadb

import android.content.pm.PackageManager
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.minthd.mintadb.core.AppScanner
import com.minthd.mintadb.core.DeviceInfoReader
import com.minthd.mintadb.core.DisplayInfoReader
import com.minthd.mintadb.core.InstalledApp
import com.minthd.mintadb.core.OptimizeStep
import com.minthd.mintadb.core.RomInfo
import com.minthd.mintadb.core.ShellRunner
import com.minthd.mintadb.core.DisplayHzInfo
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import rikka.shizuku.Shizuku

data class MintUiState(
    val shizukuReady: Boolean = false,
    val shizukuVersion: Int = -1,
    val romInfo: RomInfo? = null,
    val displayInfo: DisplayHzInfo? = null,
    val apps: List<InstalledApp> = emptyList(),
    val logs: List<String> = emptyList(),
    val busy: Boolean = false,
)

class MintAdbViewModel : ViewModel() {
    private val _state = MutableStateFlow(MintUiState())
    val state: StateFlow<MintUiState> = _state.asStateFlow()

    private val shizukuListener = Shizuku.OnBinderReceivedListener { refreshShizuku() }
    private val shizukuDeadListener = Shizuku.OnBinderDeadListener { refreshShizuku() }

    init {
        Shizuku.addBinderReceivedListener(shizukuListener)
        Shizuku.addBinderDeadListener(shizukuDeadListener)
        refreshShizuku()
    }

    override fun onCleared() {
        Shizuku.removeBinderReceivedListener(shizukuListener)
        Shizuku.removeBinderDeadListener(shizukuDeadListener)
    }

    fun refreshShizuku() {
        val ready = Shizuku.pingBinder()
        val ver = if (ready) Shizuku.getVersion() else -1
        _state.update { it.copy(shizukuReady = ready, shizukuVersion = ver) }
        log(if (ready) "[OK] Shizuku v$ver" else "[WARN] Shizuku not ready")
    }

    fun requestShizukuPermission(code: Int) {
        if (Shizuku.isPreV11() || Shizuku.checkSelfPermission() == PackageManager.PERMISSION_GRANTED) {
            refreshShizuku()
            return
        }
        Shizuku.requestPermission(code)
    }

    fun onShizukuPermissionResult(granted: Boolean) {
        log(if (granted) "[OK] Shizuku permission granted" else "[FAIL] Shizuku permission denied")
        refreshShizuku()
        if (granted) refreshAll()
    }

    fun refreshAll() = viewModelScope.launch {
        if (!_state.value.shizukuReady) {
            log("[WARN] Enable Shizuku first")
            return@launch
        }
        _state.update { it.copy(busy = true) }
        try {
            _state.update { it.copy(romInfo = DeviceInfoReader.read()) }
            _state.update { it.copy(displayInfo = DisplayInfoReader.read()) }
            _state.update { it.copy(apps = AppScanner.listUserApps()) }
            log("[OK] Refreshed device data")
        } catch (e: Exception) {
            log("[FAIL] ${e.message}")
        } finally {
            _state.update { it.copy(busy = false) }
        }
    }

    fun applyGlobalRelax() = runOptimize("Global relax") {
        com.minthd.mintadb.core.XiaomiOptimizer.applyGlobalRelax()
    }

    fun applyChinaUnlock() = runOptimize("China unlock") {
        com.minthd.mintadb.core.XiaomiOptimizer.applyChinaUnlock()
    }

    fun disableApp(packageName: String) = viewModelScope.launch {
        _state.update { it.copy(busy = true) }
        val r = AppScanner.disable(packageName)
        log(if (r.exitCode == 0) "[OK] Disabled $packageName" else "[FAIL] $packageName — ${r.output}")
        _state.update { it.copy(apps = AppScanner.listUserApps(), busy = false) }
    }

    private fun runOptimize(title: String, block: suspend () -> List<OptimizeStep>) =
        viewModelScope.launch {
            _state.update { it.copy(busy = true) }
            log("== $title ==")
            try {
                block().forEach { step ->
                    log("${if (step.ok) "[OK]" else "[FAIL]"} ${step.label}")
                }
            } catch (e: Exception) {
                log("[FAIL] $title — ${e.message}")
            } finally {
                _state.update { it.copy(busy = false) }
            }
        }

    private fun log(line: String) {
        _state.update { it.copy(logs = (listOf(line) + it.logs).take(80)) }
    }
}