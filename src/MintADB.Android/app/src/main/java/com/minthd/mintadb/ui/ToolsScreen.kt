package com.minthd.mintadb.ui

import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.Checkbox
import androidx.compose.material3.FilterChip
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.getValue
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.unit.dp
import com.minthd.mintadb.MintAdbViewModel
import com.minthd.mintadb.MintUiState
import com.minthd.mintadb.ToolTab

@Composable
fun ToolsScreen(state: MintUiState, vm: MintAdbViewModel) {
    Column(Modifier.fillMaxSize()) {
        Row(
            Modifier.fillMaxWidth().horizontalScroll(rememberScrollState()).padding(horizontal = 12.dp, vertical = 6.dp),
            horizontalArrangement = Arrangement.spacedBy(6.dp),
        ) {
            ToolTab.entries.forEach { tab ->
                FilterChip(
                    selected = state.toolTab == tab,
                    onClick = { vm.setToolTab(tab) },
                    label = { Text(tab.label) },
                )
            }
        }
        Column(
            Modifier.weight(1f).verticalScroll(rememberScrollState()).padding(horizontal = 16.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp),
        ) {
            when (state.toolTab) {
                ToolTab.Basic -> BasicTools(state, vm)
                ToolTab.Apps -> AppTools(state, vm)
                ToolTab.System -> SystemToolsPanel(state, vm)
                ToolTab.Advanced -> AdvancedTools(state, vm)
                ToolTab.PcOnly -> PcOnlyInfo()
            }
        }
    }
}

private val ToolTab.label: String
    get() = when (this) {
        ToolTab.Basic -> "Cơ bản"
        ToolTab.Apps -> "Ứng dụng"
        ToolTab.System -> "Hệ thống"
        ToolTab.Advanced -> "Nâng cao"
        ToolTab.PcOnly -> "PC only"
    }

@Composable
private fun BasicTools(state: MintUiState, vm: MintAdbViewModel) {
    SectionTitle("Công cụ ADB", "Chạy trên thiết bị qua Shizuku")
    GroupLabel("Khởi động lại", Color(0xFFFF8A82))
    Row(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
        OutlinedButton(onClick = { vm.reboot("normal") }, enabled = state.shizukuReady && !state.busy) { Text("Reboot") }
        OutlinedButton(onClick = { vm.reboot("recovery") }, enabled = state.shizukuReady && !state.busy) { Text("Recovery") }
        OutlinedButton(onClick = { vm.reboot("bootloader") }, enabled = state.shizukuReady && !state.busy) { Text("Bootloader") }
    }
    GroupLabel("Shizuku", Color(0xFF64B5F6))
    Text("MintADB Android dùng Shizuku thay ADB từ PC.", style = androidx.compose.material3.MaterialTheme.typography.bodySmall)
}

@Composable
private fun AppTools(state: MintUiState, vm: MintAdbViewModel) {
    SectionTitle("App user & Play Store", "Chọn app ở tab Tối ưu hoặc nhập package")
    OutlinedTextField(
        value = state.customPackage,
        onValueChange = vm::setCustomPackage,
        modifier = Modifier.fillMaxWidth(),
        label = { Text("Package (vd: com.zing.zalo)") },
        singleLine = true,
    )
    Row(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
        OutlinedButton(onClick = vm::toolForceStop, enabled = state.shizukuReady && !state.busy) { Text("Force stop") }
        OutlinedButton(onClick = vm::toolClearData, enabled = state.shizukuReady && !state.busy) { Text("Xóa data") }
        Button(onClick = vm::toolLaunchApp, enabled = state.shizukuReady && !state.busy) { Text("Mở app") }
    }
}

@Composable
private fun SystemToolsPanel(state: MintUiState, vm: MintAdbViewModel) {
    var sysTab by remember { mutableIntStateOf(0) }
    ChipRow(
        chips = listOf("Màn hình" to (sysTab == 0), "Máy" to (sysTab == 1)),
        onSelect = { sysTab = it },
    )

    if (sysTab == 0) {
        SectionTitle("Thông tin màn hình")
        state.displayInfo?.let {
            InfoCard("Peak Hz", it.peakHz)
            InfoCard("Min Hz", it.minHz)
            InfoCard("Active mode", it.currentMode)
            if (it.statusText.isNotBlank()) InfoCard("Chi tiết", it.statusText)
        }
        OutlinedButton(onClick = vm::readHzStatus, enabled = state.shizukuReady && !state.busy) { Text("Đọc Hz") }

        GroupLabel("Lock Hz & UI mượt", Color(0xFF5DD68A))
        val hzPresets = listOf(60, 90, 120, 144)
        Row(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
            hzPresets.forEach { hz ->
                FilterChip(
                    selected = state.hzOptions.targetHz == hz,
                    onClick = { vm.setHzOption { copy(targetHz = hz) } },
                    label = { Text("$hz Hz") },
                )
            }
        }
        ToggleCheck("Tối ưu MIUI/HyperOS", state.hzOptions.miuiTweaks) { vm.setHzOption { copy(miuiTweaks = !miuiTweaks) } }
        ToggleCheck("UI mượt", state.hzOptions.smoothUi) { vm.setHzOption { copy(smoothUi = !smoothUi) } }
        ToggleCheck("Tăng GPU", state.hzOptions.boostGpu) { vm.setHzOption { copy(boostGpu = !boostGpu) } }
        Row(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
            Button(onClick = vm::applyHzLock, enabled = state.shizukuReady && !state.busy) { Text("Khóa Hz") }
            OutlinedButton(onClick = vm::applySmoothUi, enabled = state.shizukuReady && !state.busy) { Text("UI mượt") }
            Button(onClick = vm::applyHzFull, enabled = state.shizukuReady && !state.busy) { Text("Khóa + UI") }
            OutlinedButton(onClick = vm::restoreAdaptiveHz, enabled = state.shizukuReady && !state.busy) { Text("Adaptive") }
        }
    } else {
        SectionTitle("Thông tin máy")
        state.romInfo?.let { RomSummary(it) }
            ?: Text("Bấm Làm mới ở thanh trên")
        OutlinedButton(onClick = vm::refreshAll, enabled = state.shizukuReady && !state.busy) { Text("Đọc thông tin máy") }
    }
}

@Composable
private fun AdvancedTools(state: MintUiState, vm: MintAdbViewModel) {
    SectionTitle("Nâng cao", "Chạy lệnh shell qua Shizuku")
    OutlinedTextField(
        value = state.shellCommand,
        onValueChange = vm::setShellCommand,
        modifier = Modifier.fillMaxWidth(),
        label = { Text("Lệnh shell") },
        placeholder = { Text("settings get secure peak_refresh_rate") },
        minLines = 2,
    )
    Button(onClick = vm::runShellCommand, enabled = state.shizukuReady && !state.busy) { Text("Chạy") }
}

@Composable
private fun PcOnlyInfo() {
    SectionTitle("Chỉ có trên bản PC")
    listOf(
        "Scrcpy — mirror màn hình lên PC",
        "Mạng — AdBlock DNS, hosts",
        "Fastboot — flash, unlock bootloader",
        "Push/Pull file qua ADB",
        "Kho APK Miui offline + cài qua ADB",
        "Driver USB Windows",
    ).forEach { item ->
        Card(Modifier.fillMaxWidth()) {
            Text(item, Modifier.padding(12.dp))
        }
    }
}

@Composable
private fun ToggleCheck(label: String, checked: Boolean, onToggle: () -> Unit) {
    Row(verticalAlignment = Alignment.CenterVertically) {
        Checkbox(checked = checked, onCheckedChange = { onToggle() })
        Text(label)
    }
}