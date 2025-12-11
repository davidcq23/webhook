using System.Collections.Concurrent;
using DidiWebhookReceiver.Models;

namespace DidiWebhookReceiver.Services;

public class WebhookLogEntry
{
    public DateTime ReceivedAt { get; set; }
    public string RawBody { get; set; } = string.Empty;
    public WebhookNotification? Parsed { get; set; }
}

public class WebhookLogService
{
    private readonly ConcurrentQueue<WebhookLogEntry> _entries = new();
    private const int MaxEntries = 100;

    public void Add(WebhookLogEntry entry)
    {
        _entries.Enqueue(entry);

        while (_entries.Count > MaxEntries && _entries.TryDequeue(out _))
        {
            // Descarta los más antiguos
        }
    }

    public IReadOnlyCollection<WebhookLogEntry> GetAll()
    {
        return _entries.ToArray()
            .OrderByDescending(e => e.ReceivedAt)
            .ToArray();
    }
}
