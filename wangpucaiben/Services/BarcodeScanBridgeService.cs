using System.Collections.Concurrent;

namespace wangpucaiben.Services;

public sealed class BarcodeScanBridgeService
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, Func<string, Task>>> _listeners = new();

    public string CreateSessionId()
    {
        return Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
    }

    public Guid RegisterListener(string sessionId, Func<string, Task> listener)
    {
        var listenerId = Guid.NewGuid();
        var sessionListeners = _listeners.GetOrAdd(sessionId, _ => new ConcurrentDictionary<Guid, Func<string, Task>>());
        sessionListeners[listenerId] = listener;
        return listenerId;
    }

    public void RemoveListener(string sessionId, Guid listenerId)
    {
        if (!_listeners.TryGetValue(sessionId, out var sessionListeners))
        {
            return;
        }

        sessionListeners.TryRemove(listenerId, out _);
        if (sessionListeners.IsEmpty)
        {
            _listeners.TryRemove(sessionId, out _);
        }
    }

    public async Task<bool> PublishAsync(string sessionId, string? barcode)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(barcode))
        {
            return false;
        }

        if (!_listeners.TryGetValue(sessionId.Trim(), out var sessionListeners) || sessionListeners.IsEmpty)
        {
            return false;
        }

        var normalizedBarcode = barcode.Trim();
        foreach (var listener in sessionListeners.Values)
        {
            await listener(normalizedBarcode);
        }

        return true;
    }
}
