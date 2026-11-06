using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SomaticSeries.Api.Controllers;
using SomaticSeries.Application.Queries;
using Xunit;

/// <summary>BE-084: срез соматики — sub из токена передаётся в запрос (не из тела).</summary>
public class SomaticSeriesControllerIsolationTests
{
    private sealed class StubMediator : IMediator
    {
        public object? Captured;
        public object? Result;
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default) { Captured = request; return Task.FromResult((TResponse)Result!); }
        public Task<object?> Send(object request, CancellationToken ct = default) { Captured = request; return Task.FromResult(Result); }
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct = default) => throw new NotImplementedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default) => throw new NotImplementedException();
        public Task Publish(object notification, CancellationToken ct = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default) where TNotification : INotification => Task.CompletedTask;
    }

    private static SomaticSeriesController MakeController(Guid sub, StubMediator mediator)
    {
        var c = new SomaticSeriesController(mediator);
        c.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", sub.ToString()) }, "test")) } };
        return c;
    }

    [Fact]
    public async Task Slice_PassesClientIdFromSub()
    {
        var sub = Guid.NewGuid();
        var mediator = new StubMediator { Result = new List<object>() };
        await MakeController(sub, mediator).Slice("2026-01-01", "2026-01-02", CancellationToken.None);
        var q = Assert.IsType<GetSomaticSliceQuery>(mediator.Captured);
        Assert.Equal(sub, q.ClientId);
    }
}
