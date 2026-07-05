using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MintADB.Wpf.Models;

public sealed class AppPreset : INotifyPropertyChanged
{
    private bool _selected;
    private bool _installed;

    public string Name { get; init; } = "";
    public string Package { get; init; } = "";
    public string[] Processes { get; init; } = [];
    public string[] Services { get; init; } = [];

    public bool Selected
    {
        get => _selected;
        set { if (_selected == value) return; _selected = value; OnPropertyChanged(); }
    }

    public bool Installed
    {
        get => _installed;
        set
        {
            if (_installed == value) return;
            _installed = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(IsAvailable));
        }
    }

    public bool IsAvailable => _installed;

    public string StatusText => _installed ? "Đã cài" : "Chưa cài";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public static IReadOnlyList<AppPreset> Defaults { get; } =
    [
        new() { Name = "Telegram", Package = "org.telegram.messenger",
            Processes = ["org.telegram.messenger", "org.telegram.messenger:push"],
            Services = ["org.telegram.messenger/com.google.firebase.messaging.FirebaseMessagingService"] },
        new() { Name = "WhatsApp", Package = "com.whatsapp",
            Processes = ["com.whatsapp", "com.whatsapp:push"],
            Services = ["com.whatsapp/com.google.firebase.messaging.FirebaseMessagingService"] },
        new() { Name = "Gmail", Package = "com.google.android.gm",
            Processes = ["com.google.android.gm", "com.google.android.gm:background"],
            Services = ["com.google.android.gm/com.google.android.gm.provider.MailSyncAdapterService"] },
        new() { Name = "Zalo", Package = "com.zing.zalo",
            Processes = ["com.zing.zalo", "com.zing.zalo:push", "com.zing.zalo:service", "com.zing.zalo:background"],
            Services = [
                "com.zing.zalo/.service.ZaloFirebaseMessagingService",
                "com.zing.zalo/com.google.firebase.messaging.FirebaseMessagingService",
            ] },
        new() { Name = "Messenger", Package = "com.facebook.orca",
            Processes = ["com.facebook.orca", "com.facebook.orca:push", "com.facebook.orca:service"],
            Services = [
                "com.facebook.orca/com.facebook.push.fcm.FcmListenerService",
                "com.facebook.push.fcm.FcmListenerService",
            ] },
        new() { Name = "GMS", Package = "com.google.android.gms",
            Processes = ["com.google.android.gms", "com.google.android.gms:persistent"],
            Services = ["com.google.android.gms/.chimera.PersistentIntentOperationService"] },
        new() { Name = "Play Store", Package = "com.android.vending" },
        new() { Name = "Discord", Package = "com.discord" },
        new() { Name = "Outlook", Package = "com.microsoft.office.outlook" },
        new() { Name = "Slack", Package = "com.Slack" },
        new() { Name = "LINE", Package = "jp.naver.line.android" },
    ];
}