using System.Runtime.CompilerServices;

// QG-020: реализация модели — internal (наружу только абстракции). Свои сборки BC — friend-сборки.
[assembly: InternalsVisibleTo("Catalog.Storage")]
[assembly: InternalsVisibleTo("Catalog.Api")]
[assembly: InternalsVisibleTo("Catalog.Application")]
[assembly: InternalsVisibleTo("Catalog.DomainServices")]
[assembly: InternalsVisibleTo("Catalog.IntegrationTests")]
[assembly: InternalsVisibleTo("Catalog.Domain.Tests")]
