using XDiary.Contracts;
using DigiTFactory.Libraries.SeedWorks.Result;
using Portability.Domain.Implementation;
using Xunit;

public class CsvDocumentAggregateTest
{
    [Fact]
    public void ExportRecords_Valid_Succeeds()
    { var r = Aggregate.CreateInstance(new AnemicModel()).ExportRecords(new[] { "row1", "row2" }); Assert.Equal(DomainOperationResultEnum.Success, r.Result); Assert.Equal(2, r.Model().Rows.Count); }
    [Fact]
    public void ImportRecords_Valid_Succeeds()
    { var r = Aggregate.CreateInstance(new AnemicModel()).ImportRecords(new[] { "row1" }); Assert.Equal(DomainOperationResultEnum.Success, r.Result); }
    [Fact]
    public void ImportSomatic_Valid_Succeeds()
    { var r = Aggregate.CreateInstance(new AnemicModel()).ImportSomatic(new[] { "row1" }); Assert.Equal(DomainOperationResultEnum.Success, r.Result); }
}