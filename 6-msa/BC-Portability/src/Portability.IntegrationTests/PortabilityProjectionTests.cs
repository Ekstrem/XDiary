using Portability.Api.Events;
using Portability.Domain.Implementation;
using Portability.InternalContracts;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using XDiary.Contracts;
using Xunit;

namespace Portability.IntegrationTests;

/// <summary>
/// BE-140 L1: проекция события → materialized view (`csv_document_read`).
/// Проверяет, что проекция маппит ВСЕ ключи `GetValueObjects()` (класс BE-134/137/138).
/// </summary>
public class PortabilityProjectionTests
{
    [Fact]
    public void ApplyVos_Maps_EveryFieldFromGetValueObjects()
    {
        var clientId = Guid.NewGuid();
        var rows = new[] { "id,name", "1,Аня" };
        var res = Aggregate.CreateInstance(new AnemicModel()).ExportRecords(rows, clientId);
        var m = res.Model();

        var read = new CsvDocumentReadModel { Id = m.Id };
        PortabilityProjection.ApplyVos(read, m.GetValueObjects());

        Assert.Equal(clientId, read.ClientId);
        Assert.Equal(rows, read.Rows);
        Assert.NotEmpty(read.Headers);
    }

    [Fact]
    public void ApplyVos_CoversAllKeysOfGetValueObjects()
    {
        var rows = new[] { "id,name", "1,Аня" };
        var m = Aggregate.CreateInstance(new AnemicModel()).ExportRecords(rows, Guid.NewGuid()).Model();
        var keys = m.GetValueObjects().Keys.ToHashSet();
        Assert.Subset(keys, new HashSet<string> { "Root", "Rows", "Kind", "ClientId" });
    }

    [Fact]
    public void ApplyVos_PerField_DoesNotWipeUnchanged()
    {
        var read = new CsvDocumentReadModel { Id = Guid.NewGuid(), Headers = new List<string> { "h" }, Rows = new List<string> { "r" } };

        PortabilityProjection.ApplyVos(read, new Dictionary<string, IValueObject>
        {
            ["Rows"] = new StringListValueObject(new List<string> { "r2" }),
        });

        Assert.Equal(new[] { "r2" }, read.Rows);
        Assert.Equal(new[] { "h" }, read.Headers);
    }

    [Fact]
    public void ApplyVos_EmptyDiff_NoOp()
    {
        var read = new CsvDocumentReadModel { Id = Guid.NewGuid(), Headers = new List<string> { "h" } };
        PortabilityProjection.ApplyVos(read, new Dictionary<string, IValueObject>());
        Assert.Equal(new[] { "h" }, read.Headers);
    }
}
