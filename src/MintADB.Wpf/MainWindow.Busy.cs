namespace MintADB.Wpf;

public partial class MainWindow
{
    private int _busyDepth;

    private void EnterBusy()
    {
        if (_busyDepth++ == 0)
            SetActionButtonsEnabled(false);
    }

    private void ExitBusy()
    {
        if (_busyDepth > 0 && --_busyDepth == 0)
            SetActionButtonsEnabled(true);
    }

    private async Task RunWithBusyAsync(Func<Task> action)
    {
        EnterBusy();
        try { await action(); }
        finally { ExitBusy(); }
    }
}