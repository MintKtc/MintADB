package com.minthd.mintadb.ui

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
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.unit.dp
import com.minthd.mintadb.AppListMode
import com.minthd.mintadb.MintAdbViewModel
import com.minthd.mintadb.MintUiState
import com.minthd.mintadb.core.DebloatBlacklist

@Composable
fun OptimizeScreen(state: MintUiState, vm: MintAdbViewModel) {
    Column(
        Modifier.fillMaxSize().verticalScroll(rememberScrollState()).padding(horizontal = 16.dp, vertical = 8.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        SectionTitle("Tối ưu", "Xiaomi China — trải nghiệm Global-like")

        state.romInfo?.let { RomSummary(it) } ?: Text("Bấm «Quét ROM» hoặc «Làm mới» ở trên")

        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            OutlinedButton(onClick = vm::refreshAll, enabled = state.shizukuReady && !state.busy) { Text("Quét ROM") }
        }

        GroupLabel("Tùy chọn hệ thống", Color(0xFFFFD060))
        Card(Modifier.fillMaxWidth()) {
            Column(Modifier.padding(8.dp)) {
                ToggleRow("Tắt hạn chế Android", state.optimizeOptions.globalRelax) {
                    vm.setOptimizeOption { copy(globalRelax = !globalRelax) }
                }
                ToggleRow("Mở khóa kiểm soát Xiaomi China", state.optimizeOptions.chinaUnlock) {
                    vm.setOptimizeOption { copy(chinaUnlock = !chinaUnlock) }
                }
                ToggleRow("Cấp quyền tự động", state.optimizeOptions.grantPerms) {
                    vm.setOptimizeOption { copy(grantPerms = !grantPerms) }
                }
                ToggleRow("Tắt MIUI Optimization (cần reboot)", state.optimizeOptions.disableMiuiOpt) {
                    vm.setOptimizeOption { copy(disableMiuiOpt = !disableMiuiOpt) }
                }
            }
        }

        GroupLabel("Ứng dụng", Color(0xFF5DD68A))
        ChipRow(
            chips = listOf(
                "Đang cài" to (state.appListMode == AppListMode.Active),
                "Đã tắt / gỡ" to (state.appListMode == AppListMode.Inactive),
            ),
            onSelect = { vm.setAppListMode(if (it == 0) AppListMode.Active else AppListMode.Inactive) },
        )

        SearchField(state.searchQuery, vm::setSearchQuery, "Tìm tên hoặc package...")

        Row(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
            OutlinedButton(
                onClick = {
                    if (state.appListMode == AppListMode.Active) vm.scanApps() else vm.scanInactiveApps()
                },
                enabled = state.shizukuReady && !state.busy,
            ) { Text("Quét app") }
            OutlinedButton(onClick = vm::selectAllVisible, enabled = !state.busy) { Text("Chọn cả") }
            OutlinedButton(onClick = vm::clearSelection, enabled = !state.busy) { Text("Bỏ chọn") }
        }

        Text("● Play Store  ·  ● User  ·  ● Rác ROM  ·  ● Hệ thống", style = androidx.compose.material3.MaterialTheme.typography.labelSmall)

        if (state.appListMode == AppListMode.Active) {
            ActiveAppList(state, vm)
            GroupLabel("Gỡ & tắt app", Color(0xFFFF8A82))
            Row(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                Button(onClick = vm::uninstallSelected, enabled = state.shizukuReady && !state.busy) { Text("Gỡ (đã chọn)") }
                OutlinedButton(onClick = vm::disableSelected, enabled = state.shizukuReady && !state.busy) { Text("Tắt (đã chọn)") }
                OutlinedButton(onClick = vm::uninstallAllBloat, enabled = state.shizukuReady && !state.busy) { Text("Gỡ rác ROM") }
            }
        } else {
            InactiveAppList(state, vm)
            GroupLabel("Khôi phục app", Color(0xFF5DD68A))
            Button(onClick = vm::restoreSelectedInactive, enabled = state.shizukuReady && !state.busy) {
                Text("Khôi phục (đã chọn)")
            }
        }

        GroupLabel("Hành động nhanh", Color(0xFFFFD060))
        Row(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
            Button(onClick = vm::fixNotifications, enabled = state.shizukuReady && !state.busy) { Text("Fix thông báo") }
            OutlinedButton(onClick = vm::grantPermissionsSelected, enabled = state.shizukuReady && !state.busy) { Text("Cấp quyền") }
            Button(onClick = vm::fullOptimize, enabled = state.shizukuReady && !state.busy) { Text("Full Optimize") }
        }
    }
}

@Composable
private fun ToggleRow(label: String, checked: Boolean, onToggle: () -> Unit) {
    Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
        Checkbox(checked = checked, onCheckedChange = { onToggle() })
        Text(label, modifier = Modifier.padding(start = 4.dp))
    }
}

@Composable
private fun ActiveAppList(state: MintUiState, vm: MintAdbViewModel) {
    val apps = MintAdbViewModel.visiblePackages(state).take(80)
    Column(Modifier.fillMaxWidth().padding(vertical = 4.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
        apps.forEach { app ->
            Card(Modifier.fillMaxWidth()) {
                Row(Modifier.padding(10.dp).fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
                    Checkbox(
                        checked = app.packageName in state.selectedPackages,
                        onCheckedChange = { vm.toggleSelection(app.packageName) },
                    )
                    Column(Modifier.weight(1f).padding(horizontal = 6.dp)) {
                        Text(app.label)
                        Text(app.packageName, style = androidx.compose.material3.MaterialTheme.typography.labelSmall)
                    }
                    CategoryBadge(app.category)
                    if (!DebloatBlacklist.isProtected(app.packageName)) {
                        OutlinedButton(
                            onClick = { vm.disableApp(app.packageName) },
                            enabled = state.shizukuReady && !state.busy,
                            modifier = Modifier.padding(start = 4.dp),
                        ) { Text("Tắt") }
                    }
                }
            }
        }
    }
}

@Composable
private fun InactiveAppList(state: MintUiState, vm: MintAdbViewModel) {
    val apps = MintAdbViewModel.visibleInactive(state).take(80)
    Column(Modifier.fillMaxWidth(), verticalArrangement = Arrangement.spacedBy(6.dp)) {
        apps.forEach { app ->
            Card(Modifier.fillMaxWidth()) {
                Row(Modifier.padding(10.dp), verticalAlignment = Alignment.CenterVertically) {
                    Checkbox(
                        checked = app.packageName in state.selectedPackages,
                        onCheckedChange = { vm.toggleSelection(app.packageName) },
                    )
                    Column(Modifier.weight(1f).padding(horizontal = 6.dp)) {
                        Text(app.label)
                        Text(app.packageName, style = androidx.compose.material3.MaterialTheme.typography.labelSmall)
                    }
                    Text(app.state.labelVi, style = androidx.compose.material3.MaterialTheme.typography.labelSmall, color = Color(0xFF7EC8FF))
                }
            }
        }
    }
}