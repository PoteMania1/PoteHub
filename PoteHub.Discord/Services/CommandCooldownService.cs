namespace PoteHub.Discord.Services;

public class CommandCooldownService
{
    private readonly Dictionary<string, DateTime>
        _lastExecutions = [];

    private readonly object _lock = new();

    public bool TryAcquire(
        ulong userId,
        string operation,
        TimeSpan cooldown,
        out TimeSpan remaining)
    {
        string key =
            $"{userId}:{operation}";

        DateTime now =
            DateTime.UtcNow;

        lock (_lock)
        {
            if (_lastExecutions.TryGetValue(
                    key,
                    out DateTime lastExecution))
            {
                DateTime availableAt =
                    lastExecution + cooldown;

                if (availableAt > now)
                {
                    remaining =
                        availableAt - now;

                    return false;
                }
            }

            _lastExecutions[key] = now;
            remaining = TimeSpan.Zero;

            RemoveExpiredEntries(now);

            return true;
        }
    }

    private void RemoveExpiredEntries(
        DateTime now)
    {
        if (_lastExecutions.Count < 1000)
        {
            return;
        }

        DateTime threshold =
            now.AddMinutes(-10);

        List<string> expiredKeys =
            _lastExecutions
                .Where(item =>
                    item.Value < threshold)
                .Select(item => item.Key)
                .ToList();

        foreach (string key in expiredKeys)
        {
            _lastExecutions.Remove(key);
        }
    }
}