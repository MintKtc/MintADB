package com.minthd.mintadb.core

data class AppPreset(
    val name: String,
    val packageName: String,
    val processes: List<String> = emptyList(),
    val services: List<String> = emptyList(),
) {
    companion object {
        val defaults = listOf(
            AppPreset("Telegram", "org.telegram.messenger",
                processes = listOf("org.telegram.messenger", "org.telegram.messenger:push"),
                services = listOf("org.telegram.messenger/com.google.firebase.messaging.FirebaseMessagingService")),
            AppPreset("WhatsApp", "com.whatsapp",
                processes = listOf("com.whatsapp", "com.whatsapp:push"),
                services = listOf("com.whatsapp/com.google.firebase.messaging.FirebaseMessagingService")),
            AppPreset("Gmail", "com.google.android.gm",
                processes = listOf("com.google.android.gm", "com.google.android.gm:background")),
            AppPreset("Zalo", "com.zing.zalo",
                processes = listOf("com.zing.zalo", "com.zing.zalo:push", "com.zing.zalo:service")),
            AppPreset("Messenger", "com.facebook.orca",
                processes = listOf("com.facebook.orca", "com.facebook.orca:push")),
            AppPreset("GMS", "com.google.android.gms",
                processes = listOf("com.google.android.gms", "com.google.android.gms:persistent")),
            AppPreset("Play Store", "com.android.vending"),
            AppPreset("Discord", "com.discord"),
            AppPreset("Outlook", "com.microsoft.office.outlook"),
            AppPreset("Slack", "com.Slack"),
            AppPreset("LINE", "jp.naver.line.android"),
        )
    }
}