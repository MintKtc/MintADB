package com.minthd.mintadb

import android.content.pm.PackageManager
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.minthd.mintadb.core.AppScanner
import com.minthd.mintadb.core.DeviceInfoReader
import com.minthd.mintadb.core.DisplayInfoReader
import com.minthd.mintadb.core.DisplayPerformance
import com.minthd.mintadb.core.HzLockOptions
import com.minthd.mintadb.core.InactiveApp
import com.minthd.mintadb.core.InstalledApp
import com.minthd.mintadb.core.OptimizeOptions
import com.minthd.mintadb.core.OptimizeStep
import com.minthd.mintadb.core.RomInfo
import com.minthd.mintadb.core.SystemTools
import com.minthd.mintadb.core.DisplayHzInfo
import com.minthd.mintadb.core.XiaomiOptimizer
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import rikka.shizuku.Shizuku

enum class MainSection { Optimize, Tools }
enum class AppListMode { Active, Inactive }
enum class ToolTab { Basic, Apps, System, Advanced, PcOnly }

data class MintUiState(
    val shizukuReady: Boolean = false,
    val shizukuVersion: Int = -1,
    val mainSection: MainSection = MainSection.Optimize,
    val toolTab: ToolTab = ToolTab.Basic,
    val appListMode: AppListMode = AppListMode.Active,
    val romInfo: RomInfo? = null,
    val displayInfo: DisplayHzInfo? = null,
    val apps: List<InstalledApp> = emptyList(),
    val inactiveApps: List<InactiveApp> = emptyList(),
    val selectedPackages: Set<String> = emptySet(),
    val searchQuery: String = "",
    val optimizeOptions: OptimizeOptions = OptimizeOptions(),
    val hzOptions: HzLockOptions = HzLockOptions(),
    val customPackage: String = "",
    val shellCommand: String = "",
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

    fun setMainSection(section: MainSection) = _state.update { it.copy(mainSection = section) }
    fun setToolTab(tab: ToolTab) = _state.update { it.copy(toolTab = tab) }
    fun setAppListMode(mode: AppListMode) = _state.update { it.copy(appListMode = mode, selectedPackages = emptySet()) }
    fun setSearchQuery(q: String) = _state.update { it.copy(searchQuery = q) }
    fun setCustomPackage(p: String) = _state.update { it.copy(customPackage = p) }
    fun setShellCommand(c: String) = _state.update { it.copy(shellCommand = c) }

    fun setOptimizeOption(update: OptimizeOptions.() -> OptimizeOptions) =
        _state.update { it.copy(optimizeOptions = it.optimizeOptions.update()) }

    fun setHzOption(update: HzLockOptions.() -> HzLockOptions) =
        _state.update { it.copy(hzOptions = it.hzOptions.update()) }

    fun toggleSelection(packageName: String) = _state.update { s ->
        val next = s.selectedPackages.toMutableSet()
        if (packageName in next) next.remove(packageName) else next.add(packageName)
        s.copy(selectedPackages = next)
    }

    fun selectAllVisible() = _state.update { s ->
        val visible = visiblePackages(s)
        s.copy(selectedPackages = visible.map { it.packageName }.toSet())
    }

    fun clearSelection() = _state.update { it.copy(selectedPackages = emptySet()) }

    fun refreshShizuku() {
        val ready = Shizuku.pingBinder()
        val ver = if (ready) Shizuku.getVersion() else -1
        _state.update { it.copy(shizukuReady = ready, shizukuVersion = ver) }
        log(if (ready) "[OK] Shizuku v$ver" else "[WARN] Shizuku chưa sẵn sàng")
    }

    fun requestShizukuPermission(code: Int) {
        if (Shizuku.isPreV11() || Shizuku.checkSelfPermission() == PackageManager.PERMISSION_GRANTED) {
            refreshShizuku()
            return
        }
        Shizuku.requestPermission(code)
    }

    fun onShizukuPermissionResult(granted: Boolean) {
        log(if (granted) "[OK] Đã cấp quyền Shizuku" else "[FAIL] Từ chối quyền Shizuku")
        refreshShizuku()
        if (granted) refreshAll()
    }

    fun refreshAll() = viewModelScope.launch {
        if (!_state.value.shizukuReady) {
            log("[WARN] Bật Shizuku trước")
            return@launch
        }
        _state.update { it.copy(busy = true) }
        try {
            _state.update {
                it.copy(
                    romInfo = DeviceInfoReader.read(),
                    displayInfo = DisplayInfoReader.read(),
                    apps = AppScanner.scanActiveApps(),
                    inactiveApps = AppScanner.scanInactiveApps(),
                )
            }
            log("[OK] Đã quét thiết bị · ${ _state.value.apps.size} app · ${ _state.value.inactiveApps.size} inactive")
        } catch (e: Exception) {
            log("[FAIL] ${e.message}")
        } finally {
            _state.update { it.copy(busy = false) }
        }
    }

    fun scanApps() = viewModelScope.launch {
        if (!requireShizuku()) return@launch
        _state.update { it.copy(busy = true) }
        try {
            val apps = AppScanner.scanActiveApps()
            _state.update { it.copy(apps = apps) }
            log("[OK] Quét ${apps.size} app đang cài")
        } finally {
            _state.update { it.copy(busy = false) }
        }
    }

    fun scanInactiveApps() = viewModelScope.launch {
        if (!requireShizuku()) return@launch
        _state.update { it.copy(busy = true) }
        try {
            val list = AppScanner.scanInactiveApps()
            _state.update { it.copy(inactiveApps = list) }
            log("[OK] Quét ${list.size} app đã tắt/gỡ")
        } finally {
            _state.update { it.copy(busy = false) }
        }
    }

    fun applyGlobalRelax() = runSteps("Tối ưu Android Global") { XiaomiOptimizer.applyGlobalRelax() }
    fun applyChinaUnlock() = runSteps("Mở khóa Xiaomi China") { XiaomiOptimizer.applyChinaUnlock() }
    fun fullOptimize() = runSteps("Full Optimize") { XiaomiOptimizer.fullOptimize(_state.value.optimizeOptions) }

    fun fixNotifications() = viewModelScope.launch {
        if (!requireShizuku()) return@launch
        _state.update { it.copy(busy = true) }
        log("== Fix thông báo ==")
        try {
            com.minthd.mintadb.core.AppPreset.defaults
                .filter { AppScanner.isInstalled(it.packageName) }
                .forEach { preset ->
                    XiaomiOptimizer.fixAppNotifications(preset).forEach { step ->
                        log("${if (step.ok) "[OK]" else "[WARN]"} ${preset.name}: ${step.label}")
                    }
                }
        } finally {
            _state.update { it.copy(busy = false) }
        }
    }

    fun grantPermissionsSelected() = viewModelScope.launch {
        val pkgs = _state.value.selectedPackages.ifEmpty {
            com.minthd.mintadb.core.AppPreset.defaults.map { it.packageName }.toSet()
        }
        if (!requireShizuku()) return@launch
        _state.update { it.copy(busy = true) }
        log("== Cấp quyền ==")
        try {
            pkgs.forEach { pkg ->
                XiaomiOptimizer.grantPermissions(pkg).forEach { step ->
                    log("${if (step.ok) "[OK]" else "[WARN]"} $pkg · ${step.label}")
                }
            }
        } finally {
            _state.update { it.copy(busy = false) }
        }
    }

    fun disableSelected() = bulkAppAction("Tắt app") { AppScanner.disable(it) }
    fun uninstallSelected() = bulkAppAction("Gỡ app") { AppScanner.uninstall(it) }
    fun uninstallAllBloat() = viewModelScope.launch {
        val bloat = _state.value.apps.filter { it.category == com.minthd.mintadb.core.AppCategory.RomBloat }
        if (!requireShizuku()) return@launch
        _state.update { it.copy(busy = true) }
        log("== Gỡ rác ROM (${bloat.size}) ==")
        try {
            bloat.forEach { app ->
                val r = AppScanner.uninstall(app.packageName)
                log("${if (r.exitCode == 0) "[OK]" else "[FAIL]"} ${app.packageName}")
            }
            refreshAll()
        } finally {
            _state.update { it.copy(busy = false) }
        }
    }

    fun restoreSelectedInactive() = viewModelScope.launch {
        val selected = _state.value.selectedPackages
        if (!requireShizuku()) return@launch
        _state.update { it.copy(busy = true) }
        try {
            selected.forEach { pkg ->
                val app = _state.value.inactiveApps.find { it.packageName == pkg }
                val r = when (app?.state) {
                    com.minthd.mintadb.core.InactiveAppState.Uninstalled -> AppScanner.installExisting(pkg)
                    else -> AppScanner.enable(pkg)
                }
                log("${if (r.exitCode == 0) "[OK]" else "[FAIL]"} Khôi phục $pkg")
            }
            refreshAll()
        } finally {
            _state.update { it.copy(busy = false) }
        }
    }

    fun disableApp(packageName: String) = viewModelScope.launch {
        _state.update { it.copy(busy = true) }
        val r = AppScanner.disable(packageName)
        log(if (r.exitCode == 0) "[OK] Tắt $packageName" else "[FAIL] $packageName — ${r.output}")
        _state.update { it.copy(apps = AppScanner.scanActiveApps(), busy = false) }
    }

    fun applyHzLock() = runSteps("Khóa Hz") {
        DisplayPerformance.applyLockHz(_state.value.hzOptions)
    }

    fun applySmoothUi() = runSteps("UI mượt") {
        DisplayPerformance.applySmoothUi(_state.value.hzOptions.boostGpu)
    }

    fun applyHzFull() = viewModelScope.launch {
        if (!requireShizuku()) return@launch
        _state.update { it.copy(busy = true) }
        log("== Khóa Hz + UI mượt ==")
        try {
            DisplayPerformance.applyLockHz(_state.value.hzOptions).forEach { logStep(it) }
            if (_state.value.hzOptions.smoothUi) {
                DisplayPerformance.applySmoothUi(_state.value.hzOptions.boostGpu).forEach { logStep(it) }
            }
        } finally {
            _state.update { it.copy(busy = false, displayInfo = DisplayInfoReader.read()) }
        }
    }

    fun restoreAdaptiveHz() = runSteps("Khôi phục adaptive Hz") { DisplayPerformance.restoreAdaptive() }

    fun readHzStatus() = viewModelScope.launch {
        if (!requireShizuku()) return@launch
        val status = DisplayPerformance.readStatus()
        _state.update { it.copy(displayInfo = DisplayInfoReader.read().copy(statusText = status)) }
        log("[OK] Đọc Hz\n$status")
    }

    fun reboot(mode: String) = viewModelScope.launch {
        if (!requireShizuku()) return@launch
        val r = when (mode) {
            "recovery" -> SystemTools.rebootRecovery()
            "bootloader" -> SystemTools.rebootBootloader()
            else -> SystemTools.reboot()
        }
        log(if (r.exitCode == 0) "[OK] Reboot $mode" else "[FAIL] ${r.output}")
    }

    fun toolForceStop() = toolOnPackage("Force stop") { SystemTools.forceStop(it) }
    fun toolClearData() = toolOnPackage("Xóa data") { SystemTools.clearData(it) }
    fun toolLaunchApp() = toolOnPackage("Mở app") { SystemTools.launchApp(it) }

    fun runShellCommand() = viewModelScope.launch {
        val cmd = _state.value.shellCommand.trim()
        if (cmd.isEmpty() || !requireShizuku()) return@launch
        _state.update { it.copy(busy = true) }
        val r = SystemTools.runShell(cmd)
        log(if (r.exitCode == 0) "[OK] $cmd\n${r.output}" else "[FAIL] $cmd\n${r.output}")
        _state.update { it.copy(busy = false) }
    }

    private fun toolOnPackage(label: String, block: suspend (String) -> com.minthd.mintadb.core.ShellResult) =
        viewModelScope.launch {
            val pkg = _state.value.customPackage.trim().ifBlank { _state.value.selectedPackages.firstOrNull() }
            if (pkg.isNullOrBlank()) {
                log("[WARN] Nhập package hoặc chọn app")
                return@launch
            }
            if (!requireShizuku()) return@launch
            _state.update { it.copy(busy = true) }
            val r = block(pkg)
            log(if (r.exitCode == 0) "[OK] $label $pkg" else "[FAIL] $label — ${r.output}")
            _state.update { it.copy(busy = false) }
        }

    private fun bulkAppAction(title: String, block: suspend (String) -> com.minthd.mintadb.core.ShellResult) =
        viewModelScope.launch {
            val pkgs = _state.value.selectedPackages
            if (pkgs.isEmpty()) {
                log("[WARN] Chọn app trước")
                return@launch
            }
            if (!requireShizuku()) return@launch
            _state.update { it.copy(busy = true) }
            log("== $title (${pkgs.size}) ==")
            try {
                pkgs.forEach { pkg ->
                    val r = block(pkg)
                    log("${if (r.exitCode == 0) "[OK]" else "[FAIL]"} $pkg")
                }
                refreshAll()
            } finally {
                _state.update { it.copy(busy = false, selectedPackages = emptySet()) }
            }
        }

    private fun runSteps(title: String, block: suspend () -> List<OptimizeStep>) =
        viewModelScope.launch {
            if (!requireShizuku()) return@launch
            _state.update { it.copy(busy = true) }
            log("== $title ==")
            try {
                block().forEach { logStep(it) }
            } catch (e: Exception) {
                log("[FAIL] $title — ${e.message}")
            } finally {
                _state.update { it.copy(busy = false) }
            }
        }

    private fun logStep(step: OptimizeStep) {
        log("${if (step.ok) "[OK]" else "[FAIL]"} ${step.label}")
    }

    private fun requireShizuku(): Boolean {
        if (_state.value.shizukuReady) return true
        log("[WARN] Cần Shizuku — bấm Grant permission")
        return false
    }

    private fun log(line: String) {
        _state.update { it.copy(logs = (listOf(line) + it.logs).take(100)) }
    }

    companion object {
        fun visiblePackages(state: MintUiState): List<InstalledApp> {
            val q = state.searchQuery.trim().lowercase()
            return state.apps.filter {
                q.isEmpty() || it.packageName.lowercase().contains(q) || it.label.lowercase().contains(q)
            }
        }

        fun visibleInactive(state: MintUiState): List<InactiveApp> {
            val q = state.searchQuery.trim().lowercase()
            return state.inactiveApps.filter {
                q.isEmpty() || it.packageName.lowercase().contains(q) || it.label.lowercase().contains(q)
            }
        }
    }
}