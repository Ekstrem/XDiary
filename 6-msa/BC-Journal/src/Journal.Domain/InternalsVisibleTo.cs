using System.Runtime.CompilerServices;

// QG-020: реализация модели — internal (наружу только абстракции). Свои сборки BC — friend-сборки,
// чтобы Storage/Api/Application/тесты могли строить модели, не выставляя их наружу публично.
[assembly: InternalsVisibleTo("Journal.Storage")]
[assembly: InternalsVisibleTo("Journal.Api")]
[assembly: InternalsVisibleTo("Journal.Application")]
[assembly: InternalsVisibleTo("Journal.DomainServices")]
[assembly: InternalsVisibleTo("Journal.IntegrationTests")]
