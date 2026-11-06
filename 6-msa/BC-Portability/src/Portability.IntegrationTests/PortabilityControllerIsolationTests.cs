using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Portability.Api.Controllers;
using Portability.Application.Queries;
using Xunit;

/// <summary>BE-084: владелец документа — sub из токена передаётся в запрос (не из тела).</summary>
public class PortabilityControllerIsolationTests
{
    private sealed class StubMediator : IMediator
    {
        public object? Captured;
        public object? Result;
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default) { Captured = request; return Task.FromResult((TResponse)Result!); }
        public Task<object?> Send(object request, CancellationToken ct = default) { Captured = request; return Task.FromResult(Result); }
        public Task<object?> Send<TRequest>(TRequest request, CancellationToken ct = default) { Captured = request; return Task.FromResult<object?>(Result); }
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct = default) => throw new NotImplementedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default) => throw new NotImplementedException();
        public Task Publish(object notification, CancellationToken ct = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default) where TNotification : INotification => Task.CompletedTask;
    }

    private static PortabilityController MakeController(Guid sub, StubMediator mediator)
    {
        var c = new PortabilityController(mediator);
        c.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", sub.ToString()) }, "test")) } };
        return c;
    }

    [Fact]
    public async Task Document_PassesClientIdFromSub()
    {
        var sub = Guid.NewGuid();
        var mediator = new StubMediator { Result = null };
        await MakeController(sub, mediator).Document(CancellationToken.None);
        var q = Assert.IsType<GetDocumentQuery>(mediator.Captured);
        Assert.Equal(sub, q.ClientId);
    }
}
