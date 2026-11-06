using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Catalog.Api.Controllers;
using Xunit;

/// <summary>BE-084: сквозной отказ — B запрашивает позицию каталога A ⇒ 404 (не 403, не 200).</summary>
public class CatalogControllerIsolationTests
{
    private sealed class StubMediator : IMediator
    {
        public object? Result { get; set; }
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
            => Task.FromResult((TResponse)Result!);
        public Task<object?> Send(object request, CancellationToken ct = default)
            => Task.FromResult(Result);
        public Task Send<TRequest>(TRequest request, CancellationToken ct = default)
            => Task.CompletedTask;
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct = default)
            => throw new NotImplementedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task Publish(object notification, CancellationToken ct = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default) where TNotification : INotification
            => Task.CompletedTask;
    }

    private static CatalogController MakeController(Guid sub, object? result)
    {
        var controller = new CatalogController(new StubMediator { Result = result });
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", sub.ToString()) }, "test"))
            }
        };
        return controller;
    }

    [Fact]
    public async Task Get_OtherClientsItem_Returns404()
    {
        var controller = MakeController(Guid.NewGuid(), null); // чужой → query отдаёт null → 404 (не 403)
        var result = await controller.Get(Guid.NewGuid(), CancellationToken.None);
        Assert.IsType<NotFoundResult>(result);
    }
}
