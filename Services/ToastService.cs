namespace SalesApp.Services;

public enum ToastKind { Info, Success, Warning, Error }

public record Toast(Guid Id, string Message, ToastKind Kind);

public class ToastService
{
    public event Action<Toast>? OnShow;
    public event Action<Guid>? OnDismiss;

    public void Show(string message, ToastKind kind = ToastKind.Info, int autoDismissMs = 4000)
    {
        var t = new Toast(Guid.NewGuid(), message, kind);
        OnShow?.Invoke(t);
        if (autoDismissMs > 0)
            _ = DelayDismiss(t.Id, autoDismissMs);
    }

    public void Success(string m) => Show(m, ToastKind.Success);
    public void Error(string m) => Show(m, ToastKind.Error, 6000);
    public void Warning(string m) => Show(m, ToastKind.Warning);

    public void Dismiss(Guid id) => OnDismiss?.Invoke(id);

    private async Task DelayDismiss(Guid id, int ms)
    {
        await Task.Delay(ms);
        OnDismiss?.Invoke(id);
    }
}
