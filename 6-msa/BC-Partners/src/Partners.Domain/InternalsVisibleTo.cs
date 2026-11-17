using System.Runtime.CompilerServices;

// QG-020: реализация модели — internal (наружу только абстракции). Свои сборки BC — friend-сборки.
[assembly: InternalsVisibleTo("Partners.Storage")]
[assembly: InternalsVisibleTo("Partners.Api")]
[assembly: InternalsVisibleTo("Partners.Application")]
[assembly: InternalsVisibleTo("Partners.DomainServices")]
[assembly: InternalsVisibleTo("Partners.IntegrationTests")]
[assembly: InternalsVisibleTo("Partners.Domain.Tests")]
