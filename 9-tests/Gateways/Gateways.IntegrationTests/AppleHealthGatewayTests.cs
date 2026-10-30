using AppleHealthGateway.Domain;
using AppleHealthGateway.Domain.Abstraction;
using AppleHealthGateway.Domain.Implementation;
using DigiTFactory.Libraries.SeedWorks.Result;
using Xunit;

namespace XDiary.Tests.Gateways;

/// <summary>TE-004: apple-health-gateway — импорт + идемпотентность по имени файла.</summary>
public class AppleHealthGatewayTests
{
    [Fact]
    public void Configure_SetsMetrics()
    {
        var r = Aggregate.CreateInstance(new AnemicModel()).Configure(new[] { "hr", "sleep" });
        Assert.Equal(DomainOperationResultEnum.Success, r.Result);
        Assert.Equal(2, r.Model().ImportConfig.Metrics.Count);
    }

    [Fact]
    public void ImportFile_IsIdempotent_ByFileName()
    {
        var model = Aggregate.CreateInstance(new AnemicModel()).Configure(new[] { "hr" }).Model();
        model = Aggregate.CreateInstance(model).ImportFile("export.xml").Model();

        Assert.Single(model.ProcessedFiles);
        Assert.False(string.IsNullOrEmpty(model.LastImport));

        // Повторный импорт того же файла — не дублирует.
        model = Aggregate.CreateInstance(model).ImportFile("export.xml").Model();
        Assert.Single(model.ProcessedFiles);
    }

    [Fact]
    public void ImportFile_DifferentFiles_AreBothTracked()
    {
        var model = Aggregate.CreateInstance(new AnemicModel()).Configure(new[] { "hr" }).Model();
        model = Aggregate.CreateInstance(model).ImportFile("export-1.xml").Model();
        model = Aggregate.CreateInstance(model).ImportFile("export-2.xml").Model();

        Assert.Equal(2, model.ProcessedFiles.Count);
    }
}
