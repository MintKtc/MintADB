package com.minthd.mintadb.ui

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.FilterChip
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.minthd.mintadb.MintUiState
import com.minthd.mintadb.core.AppCategory
import com.minthd.mintadb.core.RomInfo

@Composable
fun DeviceSidebarCard(state: MintUiState, @Suppress("UNUSED_PARAMETER") onRefresh: () -> Unit, onRequestShizuku: () -> Unit) {
    Card(Modifier.fillMaxWidth(), colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceVariant)) {
        Column(Modifier.padding(12.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
            Text("Thiết bị", fontWeight = FontWeight.Bold, style = MaterialTheme.typography.labelLarge)
            Text(
                if (state.shizukuReady) "● Đã kết nối Shizuku v${state.shizukuVersion}"
                else "○ Chưa có Shizuku",
                color = if (state.shizukuReady) Color(0xFF5DD68A) else Color(0xFFFFD060),
                style = MaterialTheme.typography.bodySmall,
            )
            state.romInfo?.let { InfoRow("Model", "${it.brand} ${it.model}") }
                ?: Text("Chưa quét — bấm Làm mới", style = MaterialTheme.typography.bodySmall)
            state.romInfo?.let { InfoRow("Android", it.androidVersion) }
            if (!state.shizukuReady) {
                OutlinedButton(onClick = onRequestShizuku, modifier = Modifier.fillMaxWidth()) {
                    Text("Cấp quyền Shizuku")
                }
            }
        }
    }
}

@Composable
fun InfoRow(label: String, value: String) {
    Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
        Text(label, style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.secondary)
        Text(value, style = MaterialTheme.typography.bodySmall, fontWeight = FontWeight.SemiBold)
    }
}

@Composable
fun InfoCard(label: String, value: String) {
    Card(Modifier.fillMaxWidth()) {
        Column(Modifier.padding(12.dp)) {
            Text(label, style = MaterialTheme.typography.labelMedium, color = MaterialTheme.colorScheme.secondary)
            Text(value)
        }
    }
}

@Composable
fun SectionTitle(title: String, subtitle: String? = null) {
    Text(title, style = MaterialTheme.typography.titleLarge, fontWeight = FontWeight.Bold)
    subtitle?.let {
        Text(it, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.secondary)
    }
}

@Composable
fun GroupLabel(text: String, color: Color = MaterialTheme.colorScheme.primary) {
    Text(text, fontWeight = FontWeight.Bold, color = color, style = MaterialTheme.typography.labelLarge)
}

@Composable
fun ChipRow(chips: List<Pair<String, Boolean>>, onSelect: (Int) -> Unit) {
    Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(6.dp)) {
        chips.forEachIndexed { i, (label, selected) ->
            FilterChip(selected = selected, onClick = { onSelect(i) }, label = { Text(label, style = MaterialTheme.typography.labelSmall) })
        }
    }
}

@Composable
fun SearchField(value: String, onValueChange: (String) -> Unit, placeholder: String) {
    OutlinedTextField(
        value = value,
        onValueChange = onValueChange,
        modifier = Modifier.fillMaxWidth(),
        placeholder = { Text(placeholder) },
        singleLine = true,
    )
}

@Composable
fun CategoryBadge(category: AppCategory) {
    val (bg, fg) = when (category) {
        AppCategory.PlayStore -> Color(0xFF1C2A22) to Color(0xFF5DD68A)
        AppCategory.UserInstalled -> Color(0xFF1A3050) to Color(0xFF7EC8FF)
        AppCategory.RomBloat -> Color(0xFF3D1A1A) to Color(0xFFFF8A82)
        AppCategory.System -> Color(0xFF3D3018) to Color(0xFFFFD060)
    }
    Card(colors = CardDefaults.cardColors(containerColor = bg)) {
        Text(category.labelVi, Modifier.padding(horizontal = 8.dp, vertical = 2.dp), color = fg, style = MaterialTheme.typography.labelSmall)
    }
}

@Composable
fun LogPanel(state: MintUiState) {
    Card(Modifier.fillMaxWidth().padding(12.dp)) {
        Column(Modifier.padding(10.dp)) {
            Text("Nhật ký", fontWeight = FontWeight.Bold, style = MaterialTheme.typography.labelMedium)
            if (state.busy) CircularProgressIndicator(Modifier.padding(vertical = 4.dp))
            LazyColumn(Modifier.height(120.dp)) {
                items(state.logs) { line ->
                    Text(line, style = MaterialTheme.typography.bodySmall)
                }
            }
        }
    }
}

@Composable
fun RomSummary(rom: RomInfo) {
    InfoCard("ROM", buildString {
        append(if (rom.isChinaRom) "China CN" else "Global")
        append(" · ")
        append(rom.osVersion)
        if (rom.isHyperOs) append(" (HyperOS)")
    })
    InfoCard("Region", rom.region.ifBlank { "?" })
    InfoCard("Build", rom.buildId)
}