namespace Catalog.Application;
using DigiTFactory.Libraries.SeedWorks.Invariants;
using DigiTFactory.Libraries.SeedWorks.Result;
using Catalog.Domain;
using Catalog.Domain.Abstraction;
using XDiary.Contracts;
public static class CatalogResults
{
    public static AggregateResult<ICatalog, ICatalogAnemicModel> Fail(string error)
    { var d = BusinessOperationData<ICatalog, ICatalogAnemicModel>.Commit<ICatalog, ICatalogAnemicModel>(default!, default!); return new AggregateResultException<ICatalog, ICatalogAnemicModel>(d, new FailedSpecification<ICatalog, ICatalogAnemicModel>(error)); }
}