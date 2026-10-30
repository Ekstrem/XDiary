using XDiary.Contracts;
using DigiTFactory.Libraries.SeedWorks.Result;
using Journal.Domain.Implementation;
using Xunit;

public class RecordAggregateTest
{
    [Fact]
    public void Complete_WithoutStartedAt_Fails()
    {
        var start = Aggregate.CreateInstance(new AnemicModel()).StartManualEntry();
        var result = Aggregate.CreateInstance(start.Model()).Complete();
        Assert.NotEqual(DomainOperationResultEnum.Success, result.Result);
    }

    [Fact]
    public void Wizard_Then_Complete_Succeeds()
    {
        var start = Aggregate.CreateInstance(new AnemicModel()).StartManualEntry();
        var when = Aggregate.CreateInstance(start.Model()).SpecifyWhen("2026-10-29T21:00");
        var partner = Aggregate.CreateInstance(when.Model()).AddPartner("partner-1");
        var complete = Aggregate.CreateInstance(partner.Model()).Complete();
        Assert.Equal(DomainOperationResultEnum.Success, complete.Result);
    }

    [Fact]
    public void Solo_Complete_Succeeds()
    {
        var start = Aggregate.CreateInstance(new AnemicModel()).StartManualEntry();
        var when = Aggregate.CreateInstance(start.Model()).SpecifyWhen("2026-10-29T21:00");
        var complete = Aggregate.CreateInstance(when.Model()).Complete();
        Assert.Equal(DomainOperationResultEnum.Success, complete.Result);
    }
}