using AssettoServer.Server;

namespace CarSwapPlugin;

/// <summary>
/// Short-lived slot reservations so nobody else can take a slot that was promised to a swapping player.
/// </summary>
public class SlotReservations
{
    private readonly object _lock = new();
    private readonly Dictionary<byte, (ulong Guid, DateTime Expires)> _bySession = new();

    public bool TryReserve(EntryCar car, ulong guid, TimeSpan duration)
    {
        lock (_lock)
        {
            Purge();
            if (_bySession.ContainsKey(car.SessionId)) return false;

            // one reservation per player
            foreach (var key in _bySession.Where(kv => kv.Value.Guid == guid).Select(kv => kv.Key).ToList())
                _bySession.Remove(key);

            _bySession[car.SessionId] = (guid, DateTime.UtcNow + duration);
            return true;
        }
    }

    public bool IsReserved(EntryCar car)
    {
        lock (_lock)
        {
            Purge();
            return _bySession.ContainsKey(car.SessionId);
        }
    }

    public bool IsReservedForOther(EntryCar car, ulong guid)
    {
        lock (_lock)
        {
            Purge();
            return _bySession.TryGetValue(car.SessionId, out var r) && r.Guid != guid;
        }
    }

    public void ReleaseForGuid(ulong guid)
    {
        lock (_lock)
        {
            foreach (var key in _bySession.Where(kv => kv.Value.Guid == guid).Select(kv => kv.Key).ToList())
                _bySession.Remove(key);
        }
    }

    private void Purge()
    {
        var now = DateTime.UtcNow;
        foreach (var key in _bySession.Where(kv => kv.Value.Expires <= now).Select(kv => kv.Key).ToList())
            _bySession.Remove(key);
    }
}
