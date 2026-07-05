package com.minthd.mintadb.ui.theme

import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color

private val MacDark = darkColorScheme(
    primary = Color(0xFF0A84FF),
    onPrimary = Color.White,
    secondary = Color(0xFF8E8E93),
    background = Color(0xFF1C1C1E),
    surface = Color(0xFF2C2C2E),
    onBackground = Color(0xFFF2F2F7),
    onSurface = Color(0xFFF2F2F7),
    surfaceVariant = Color(0xFF3A3A3C),
)

@Composable
fun MintAdbTheme(content: @Composable () -> Unit) {
    MaterialTheme(colorScheme = MacDark, content = content)
}