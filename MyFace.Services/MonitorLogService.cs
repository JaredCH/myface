using System;
using System.Collections.Concurrent;

namespace MyFace.Services;

public record MonitorLogEntry(DateTime Timestamp, string Message);

public class MonitorLogService
{
    private const int MaxEntries = 1000;
    private readonly ConcurrentQueue<MonitorLogEntry> _entries = new();

    public void Append(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var entry = new MonitorLogEntry(DateTime.UtcNow, message.Trim());
        _entries.Enqueue(entry);
        TrimExcess();
    }

    public IReadOnlyList<MonitorLogEntry> GetEntries()
    {
        return _entries.ToArray();
    }

    private void TrimExcess()
    {
        while (_entries.Count > MaxEntries && _entries.TryDequeue(out _))
        {
        }
    }
}
