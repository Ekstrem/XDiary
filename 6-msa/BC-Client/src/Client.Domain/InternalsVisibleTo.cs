using System.Runtime.CompilerServices;

// QG-020: реализация модели — internal (наружу только абстракции). Свои сборки BC — friend-сборки.
[assembly: InternalsVisibleTo("Client.Storage")]
[assembly: InternalsVisibleTo("Client.Api")]
[assembly: InternalsVisibleTo("Client.Application")]
[assembly: InternalsVisibleTo("Client.DomainServices")]
[assembly: InternalsVisibleTo("Client.IntegrationTests")]
[assembly: InternalsVisibleTo("Client.Domain.Tests")]
