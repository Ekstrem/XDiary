namespace Journal.Api.Events;
using System.Net.Http.Json;
using Journal.Application;
using Journal.Domain.Abstraction;

public sealed class HttpJournalEventPublisher : IJournalEventPublisher
{
    private readonly IHttpClientFactory _http;
    private readonly string _analyticsUrl;
    public HttpJournalEventPublisher(IHttpClientFactory http, IConfiguration config)
    { _http = http; _analyticsUrl = config["Services:Analytics"] ?? "http://analytics:8080"; }

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
        try
        {
            var client = _http.CreateClient();
            await client.PostAsJsonAsync($"{_analyticsUrl}/api/v1/analytics/events", new { EventType = eventType, Id = id, Record = record }, ct);
        }
        catch { /* best-effort: Analytics MV восстановится fallback-расписанием */ }
    }
}