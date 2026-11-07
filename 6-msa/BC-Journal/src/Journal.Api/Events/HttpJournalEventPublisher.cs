namespace Journal.Api.Events;
using System.Diagnostics.Metrics;
using System.Net.Http.Json;
using Journal.Application;
using Journal.Domain.Abstraction;
using Microsoft.Extensions.Logging;

public sealed class HttpJournalEventPublisher : IJournalEventPublisher
{
    private readonly IHttpClientFactory _http;
    private readonly string _analyticsUrl;
    private readonly ILogger<HttpJournalEventPublisher> _log;
    private static readonly Meter _meter = new("Journal.Events");
    private static readonly Counter<long> _failures = _meter.CreateCounter<long>("publisher.failures", description: "не-2xx/исключение при публикации события в Analytics");
    public HttpJournalEventPublisher(IHttpClientFactory http, IConfiguration config, ILogger<HttpJournalEventPublisher> log)
    { _http = http; _analyticsUrl = config["Services:Analytics"] ?? "http://analytics:8080"; _log = log; }

    public Task PublishRecordCompletedAsync(IJournalAnemicModel m, CancellationToken ct = default)
        => PublishAsync("RecordCompleted", m.Id, new
        {
            Id = m.Id,
            StartedAt = m.Root.StartedAt,
            DurationMin = m.DurationMin,
            Orgasms = m.Orgasms.Select(o => new { Participant = o.Participant, Count = o.Count, Rating = o.Rating }),
            Positions = m.Positions,
            Fantasies = m.Fantasies,
            Place = m.Place,
            Partners = m.Root.Partners
        }, ct);

    public Task PublishRecordDeletedAsync(IJournalAnemicModel m, CancellationToken ct = default)
        => PublishAsync("RecordDeleted", m.Id, null, ct);

    private async Task PublishAsync(string eventType, Guid id, object? record, CancellationToken ct)
    {
        // @TL 09:38: write-путь обязан не глотать не-2xx — LogWarning + счётчик, а не пустой catch.
        try
        {
            var client = _http.CreateClient(); client.Timeout = TimeSpan.FromSeconds(2);
            var resp = await client.PostAsJsonAsync($"{_analyticsUrl}/api/v1/analytics/events", new { EventType = eventType, Id = id, Record = record }, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _failures.Add(1);
                _log.LogWarning("Analytics publish {EventType}: HTTP {Status} (не-2xx)", eventType, (int)resp.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _failures.Add(1);
            _log.LogWarning(ex, "Analytics publish {EventType}: канал недоступен (best-effort, fallback-расписание восстановит)", eventType);
        }
    }
}
