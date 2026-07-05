package com.minthd.mintadb.ui

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Apps
import androidx.compose.material.icons.filled.DisplaySettings
import androidx.compose.material.icons.filled.PhoneAndroid
import androidx.compose.material.icons.filled.Speed
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.NavigationBar
import androidx.compose.material3.NavigationBarItem
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.minthd.mintadb.MintAdbViewModel
import com.minthd.mintadb.MintUiState
import com.minthd.mintadb.core.DebloatBlacklist

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun MintAdbApp(viewModel: MintAdbViewModel, onRequestShizuku: () -> Unit) {
    val state by viewModel.state.collectAsState()
    var tab by remember { mutableIntStateOf(0) }

    Scaffold(
        topBar = {
            TopAppBar(
                title = {
                    Column {
                        Text("MintADB", fontWeight = FontWeight.Bold)
                        Text("MINT_HD · On-device", style = MaterialTheme.typography.labelSmall)
                    }
                },
                actions = {
                    if (!state.shizukuReady) {
                        OutlinedButton(onClick = onRequestShizuku, modifier = Modifier.padding(end = 8.dp)) {
                            Text("Shizuku")
                        }
                    }
                }
            )
        },
        bottomBar = {
            NavigationBar {
                NavigationBarItem(selected = tab == 0, onClick = { tab = 0 },
                    icon = { Icon(Icons.Default.PhoneAndroid, null) }, label = { Text("Device") })
                NavigationBarItem(selected = tab == 1, onClick = { tab = 1 },
                    icon = { Icon(Icons.Default.Speed, null) }, label = { Text("Optimize") })
                NavigationBarItem(selected = tab == 2, onClick = { tab = 2 },
                    icon = { Icon(Icons.Default.Apps, null) }, label = { Text("Apps") })
                NavigationBarItem(selected = tab == 3, onClick = { tab = 3 },
                    icon = { Icon(Icons.Default.DisplaySettings, null) }, label = { Text("Display") })
            }
        }
    ) { padding ->
        Column(Modifier.fillMaxSize().padding(padding)) {
            Box(Modifier.weight(1f).fillMaxWidth()) {
                when (tab) {
                    0 -> DeviceTab(state, onRefresh = viewModel::refreshAll, onRequestShizuku)
                    1 -> OptimizeTab(state, viewModel::applyGlobalRelax, viewModel::applyChinaUnlock)
                    2 -> AppsTab(state, viewModel::disableApp)
                    3 -> DisplayTab(state, viewModel::refreshAll)
                }
            }
            LogPanel(state)
        }
    }
}

@Composable
private fun DeviceTab(state: MintUiState, onRefresh: () -> Unit, onRequestShizuku: () -> Unit) {
    Column(
        Modifier.padding(16.dp).verticalScroll(rememberScrollState()),
        verticalArrangement = Arrangement.spacedBy(8.dp),
    ) {
        ShizukuCard(state, onRequestShizuku)
        Button(onClick = onRefresh, enabled = !state.busy && state.shizukuReady) { Text("Refresh / Làm mới") }
        state.romInfo?.let { rom ->
            InfoCard("Model", "${rom.brand} ${rom.model}")
            InfoCard("Android", rom.androidVersion)
            InfoCard("OS", "${rom.osVersion} ${if (rom.isHyperOs) "(HyperOS)" else "(MIUI)"}")
            InfoCard("Region", rom.region.ifBlank { "?" })
            InfoCard("Build", rom.buildId)
            InfoCard("ROM", if (rom.isChinaRom) "China CN" else "Global / other")
        } ?: Text("Tap Refresh after granting Shizuku")
    }
}

@Composable
private fun OptimizeTab(state: MintUiState, onGlobal: () -> Unit, onChina: () -> Unit) {
    Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
        Text("Ported from MintADB PC — settings via Shizuku shell")
        Button(onClick = onGlobal, enabled = state.shizukuReady && !state.busy) {
            Text("Global relax")
        }
        Button(onClick = onChina, enabled = state.shizukuReady && !state.busy) {
            Text("China ROM unlock tweaks")
        }
        if (state.busy) CircularProgressIndicator()
    }
}

@Composable
private fun AppsTab(state: MintUiState, onDisable: (String) -> Unit) {
    LazyColumn(Modifier.padding(horizontal = 16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
        items(state.apps, key = { it.packageName }) { app ->
            Card(Modifier.fillMaxWidth()) {
                Row(Modifier.padding(12.dp).fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
                    Column(Modifier.weight(1f)) {
                        Text(app.packageName, style = MaterialTheme.typography.bodySmall)
                        if (DebloatBlacklist.isProtected(app.packageName)) {
                            Text("Protected", color = MaterialTheme.colorScheme.secondary,
                                style = MaterialTheme.typography.labelSmall)
                        }
                    }
                    OutlinedButton(
                        onClick = { onDisable(app.packageName) },
                        enabled = state.shizukuReady && !state.busy && !DebloatBlacklist.isProtected(app.packageName)
                    ) { Text("Disable") }
                }
            }
        }
    }
}

@Composable
private fun DisplayTab(state: MintUiState, onRefresh: () -> Unit) {
    Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
        Button(onClick = onRefresh, enabled = state.shizukuReady && !state.busy) { Text("Read Hz") }
        state.displayInfo?.let { d ->
            InfoCard("Peak Hz", d.peakHz)
            InfoCard("Min Hz", d.minHz)
            InfoCard("Active mode", d.currentMode)
        }
        Text("Lock Hz like PC app needs WRITE_SECURE_SETTINGS + OEM support.", style = MaterialTheme.typography.bodySmall)
    }
}

@Composable
private fun ShizukuCard(state: MintUiState, onRequest: () -> Unit) {
    Card(Modifier.fillMaxWidth()) {
        Column(Modifier.padding(12.dp)) {
            Text("Shizuku", fontWeight = FontWeight.Bold)
            Text(
                if (state.shizukuReady) "Running · v${state.shizukuVersion}"
                else "Required — install moe.shizuku.privileged.api"
            )
            if (!state.shizukuReady) {
                Spacer(Modifier.height(8.dp))
                Button(onClick = onRequest) { Text("Grant permission") }
            }
        }
    }
}

@Composable
private fun InfoCard(label: String, value: String) {
    Card(Modifier.fillMaxWidth()) {
        Column(Modifier.padding(12.dp)) {
            Text(label, style = MaterialTheme.typography.labelMedium, color = MaterialTheme.colorScheme.secondary)
            Text(value)
        }
    }
}

@Composable
private fun LogPanel(state: MintUiState) {
    Card(Modifier.fillMaxWidth().padding(16.dp)) {
        LazyColumn(Modifier.padding(12.dp).height(140.dp)) {
            items(state.logs) { line ->
                Text(line, style = MaterialTheme.typography.bodySmall)
            }
        }
    }
}