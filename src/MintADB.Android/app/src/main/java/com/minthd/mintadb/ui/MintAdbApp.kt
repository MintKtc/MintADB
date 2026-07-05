package com.minthd.mintadb.ui

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Build
import androidx.compose.material.icons.filled.Speed
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
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.minthd.mintadb.MainSection
import com.minthd.mintadb.MintAdbViewModel

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun MintAdbApp(viewModel: MintAdbViewModel, onRequestShizuku: () -> Unit) {
    val state by viewModel.state.collectAsState()

    Scaffold(
        topBar = {
            TopAppBar(
                title = {
                    Column {
                        Text("MintADB", fontWeight = FontWeight.Bold)
                        Text("MINT_HD · Xiaomi Tools", style = MaterialTheme.typography.labelSmall)
                    }
                },
                actions = {
                    OutlinedButton(
                        onClick = viewModel::refreshAll,
                        enabled = state.shizukuReady && !state.busy,
                        modifier = Modifier.padding(end = 4.dp),
                    ) { Text("Làm mới") }
                    if (!state.shizukuReady) {
                        OutlinedButton(onClick = onRequestShizuku, modifier = Modifier.padding(end = 8.dp)) {
                            Text("Shizuku")
                        }
                    }
                },
            )
        },
        bottomBar = {
            NavigationBar {
                NavigationBarItem(
                    selected = state.mainSection == MainSection.Optimize,
                    onClick = { viewModel.setMainSection(MainSection.Optimize) },
                    icon = { Icon(Icons.Default.Speed, null) },
                    label = { Text("Tối ưu") },
                )
                NavigationBarItem(
                    selected = state.mainSection == MainSection.Tools,
                    onClick = { viewModel.setMainSection(MainSection.Tools) },
                    icon = { Icon(Icons.Default.Build, null) },
                    label = { Text("Công cụ") },
                )
            }
        },
    ) { padding ->
        Column(Modifier.fillMaxSize().padding(padding)) {
            DeviceSidebarCard(state, viewModel::refreshAll, onRequestShizuku)
            Column(Modifier.weight(1f).fillMaxWidth()) {
                when (state.mainSection) {
                    MainSection.Optimize -> OptimizeScreen(state, viewModel)
                    MainSection.Tools -> ToolsScreen(state, viewModel)
                }
            }
            LogPanel(state)
        }
    }
}