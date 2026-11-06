using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using DailyMetrics.Api.Controllers;
using DailyMetrics.Application.Queries;
using Xunit;

/// <summary>BE-084: серия метрик — sub из токена передаётся в запрос (не из тела).</summary>
public class DailyMetricsControllerIsolationTests
{
    private sealed class StubMediator : IMediator
    {
        public object? Captured;
        public object? Result;
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default) { Captured = request; return Task.FromResult(Result is null ? default! : (TResponse)Result); }
        public Task<object?> Send(object request, CancellationToken ct = default) { Captured = request; return Task.FromResult(Result); }
        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest { Captured = request; return Task.CompletedTask; }
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct = default) => throw new NotImplementedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default) => throw new NotImplementedException();
        public Task Publish(object notification, CancellationToken ct = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default) where TNotification : INotification => Task.CompletedTask;
    }

    private static DailyMetricsController MakeController(Guid sub, StubMediator mediator)
    {
        var c = new DailyMetricsController(mediator);
        c.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", sub.ToString()) }, "test")) } };
        return c;
    }

    [Fact]
    public async Task Series_PassesClientIdFromSub()
    {
        var sub = Guid.NewGuid();
        var mediator = new StubMediator { Result = null };
        await MakeController(sub, mediator).Series("2026-01-01", "2026-01-02", CancellationToken.None);
        var q = Assert.IsType<GetDailySeriesQuery>(mediator.Captured);
        Assert.Equal(sub, q.ClientId);
    }
}
