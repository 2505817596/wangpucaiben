namespace wangpucaiben.Services;

public sealed class ScanFocusService
{
    public event Action? Changed;

    public string CurrentTarget { get; private set; } = "当前没有接收目标";

    public bool HasTarget { get; private set; }

    public void SetTarget(string target)
    {
        CurrentTarget = string.IsNullOrWhiteSpace(target) ? "当前没有接收目标" : target.Trim();
        HasTarget = !string.IsNullOrWhiteSpace(target);
        Changed?.Invoke();
    }

    public void ClearTarget()
    {
        CurrentTarget = "当前没有接收目标";
        HasTarget = false;
        Changed?.Invoke();
    }
}
