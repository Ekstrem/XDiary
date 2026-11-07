using XDiary.Contracts;
using DigiTFactory.Libraries.SeedWorks.Result;
using Journal.Domain;
using Journal.Domain.Abstraction;
using Journal.Domain.Implementation;
using Xunit;

namespace XDiary.Tests.Journal;

/// <summary>TE-001: инварианты агрегата Record (WhenRequired, DurationPositive, RatingRange).</summary>
public class RecordInvariantTests
{
    private static IJournalAnemicModel StartManual()
        => Aggregate.CreateInstance(new AnemicModel()).StartManualEntry(Guid.NewGuid()).Model();

    // ---- WhenRequired: Complete невозможен без startedAt ----
    [Fact]
    public void WhenRequired_Complete_WithoutStartedAt_Fails()
    {
        var result = Aggregate.CreateInstance(StartManual()).Complete();
        Assert.NotEqual(DomainOperationResultEnum.Success, result.Result);
    }

    [Fact]
    public void WhenRequired_Complete_WithStartedAt_Succeeds()
    {
        var model = Aggregate.CreateInstance(StartManual()).SpecifyWhen("2026-10-29T21:00").Model();
        var result = Aggregate.CreateInstance(model).Complete();
        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
    }

    // ---- WhenIso (TE-101): startedAt обязан быть ISO 8601, мусор не сохраняется ----
    [Theory]
    [InlineData("2026-10-29T21:00")]
    [InlineData("2026-11-11T10:00:00Z")]
    public void SpecifyWhen_Iso_Accepted(string startedAt)
    {
        var result = Aggregate.CreateInstance(StartManual()).SpecifyWhen(startedAt);
        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
    }

    [Theory]
    [InlineData("не дата")]
    [InlineData("1234567890")]
    [InlineData("")]
    [InlineData("   ")]
    public void SpecifyWhen_NonIso_Rejected(string startedAt)
    {
        var result = Aggregate.CreateInstance(StartManual()).SpecifyWhen(startedAt);
        Assert.NotEqual(DomainOperationResultEnum.Success, result.Result);
    }

    [Fact]
    public void SpecifyWhen_NormalizesToIso()
    {
        var result = Aggregate.CreateInstance(StartManual()).SpecifyWhen("2026-10-29T21:00");
        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
        var stored = result.Model().Root.StartedAt;
        Assert.NotEqual("2026-10-29T21:00", stored); // TE-101: нормализованный ISO, а не дословная строка
        Assert.True(System.DateTimeOffset.TryParse(stored, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out _));
    }

    // ---- DurationPositive: durationMin >= 1 ----
    [Theory]
    [InlineData(0, false)]
    [InlineData(-5, false)]
    [InlineData(1, true)]
    [InlineData(60, true)]
    public void DurationPositive_Validated(int durationMin, bool expectedSuccess)
    {
        var result = Aggregate.CreateInstance(StartManual()).SpecifyDuration(durationMin);
        Assert.Equal(expectedSuccess, result.Result == DomainOperationResultEnum.Success);
    }

    // ---- RatingRange: 0..5 для каждого оргазма ----
    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(3, true)]
    [InlineData(5, true)]
    [InlineData(6, false)]
    public void RatingRange_ValidatedForSingleOrgasm(int rating, bool expectedSuccess)
    {
        var result = Aggregate.CreateInstance(StartManual())
            .RecordOrgasms(new[] { Orgasms.CreateInstance("me", 1, new[] { "обычный" }, rating) });
        Assert.Equal(expectedSuccess, result.Result == DomainOperationResultEnum.Success);
    }

    [Fact]
    public void RatingRange_FailsIfAnyOrgasmOutOfRange()
    {
        var result = Aggregate.CreateInstance(StartManual()).RecordOrgasms(new[]
        {
            Orgasms.CreateInstance("me", 1, new[] { "обычный" }, 5),
            Orgasms.CreateInstance("partner-1", 1, new[] { "клиторальный" }, 9)
        });
        Assert.NotEqual(DomainOperationResultEnum.Success, result.Result);
    }

    // ---- Документируем расхождение с TE-001: «PartnerRequired» в домене НЕ реализован ----
    [Fact]
    public void PartnerRequired_IsNotAnInvariant_SoloRecordCompletes()
    {
        var model = Aggregate.CreateInstance(StartManual()).SpecifyWhen("2026-10-29T21:00").Model();
        var result = Aggregate.CreateInstance(model).Complete();

        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
        Assert.Empty(result.Model().Root.Partners);
    }
}
