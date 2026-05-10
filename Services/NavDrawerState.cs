namespace SalesApp.Services;

public class NavDrawerState
{
    public bool IsOpen { get; private set; }
    public event Action? Changed;
    public void Toggle() { IsOpen = !IsOpen; Changed?.Invoke(); }
    public void Close() { if (IsOpen) { IsOpen = false; Changed?.Invoke(); } }
}
