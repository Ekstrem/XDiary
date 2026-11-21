using Client.Domain.Abstraction;
using Client.Domain.Implementation;
using DigiTFactory.Libraries.SeedWorks.Result;
using XDiary.Contracts;
using Xunit;

namespace Client.Domain.Tests;

/// <summary>BE-144: базовые инварианты Client — регистрация, OTP и профиль.</summary>
public class ClientAggregateTests
{
    private static AnemicModel RegisteredModel(string code = "123456")
        => (AnemicModel)Aggregate.CreateInstance(new AnemicModel()).Register("+79000000000", code).Model();

    [Fact]
    public void Register_WhenPhoneValid_ShouldSetOtpAndExpiry()
    {
        var result = Aggregate.CreateInstance(new AnemicModel()).Register("+79000000000", "123456");

        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
        Assert.Equal("123456", result.Model().OtpCode);
        Assert.NotNull(result.Model().OtpExpiresAt);
    }

    [Fact]
    public void Register_WhenPhoneEmpty_ShouldFail()
    {
        var result = Aggregate.CreateInstance(new AnemicModel()).Register("", "123456");

        Assert.NotEqual(DomainOperationResultEnum.Success, result.Result);
    }

    [Fact]
    public void VerifyOtp_WhenCodeCorrect_ShouldVerifyAndClearOtp()
    {
        var result = Aggregate.CreateInstance(RegisteredModel()).VerifyOtp("123456");

        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
        Assert.True(result.Model().Verified);
        Assert.Equal(string.Empty, result.Model().OtpCode);
        Assert.Null(result.Model().OtpExpiresAt);
    }

    [Fact]
    public void VerifyOtp_WhenCodeWrong_ShouldFail()
    {
        var result = Aggregate.CreateInstance(RegisteredModel()).VerifyOtp("000000");

        Assert.NotEqual(DomainOperationResultEnum.Success, result.Result);
    }

    [Fact]
    public void VerifyOtp_WhenExpired_ShouldFail()
    {
        var model = RegisteredModel() with { OtpExpiresAt = DateTime.UtcNow.AddMinutes(-1) };

        var result = Aggregate.CreateInstance(model).VerifyOtp("123456");

        Assert.NotEqual(DomainOperationResultEnum.Success, result.Result);
    }

    [Fact]
    public void VerifyOtp_WhenAttemptsExceeded_ShouldFail()
    {
        var model = RegisteredModel() with { OtpAttempts = 5 };

        var result = Aggregate.CreateInstance(model).VerifyOtp("123456");

        Assert.NotEqual(DomainOperationResultEnum.Success, result.Result);
    }

    [Fact]
    public void RecordFailedAttempt_WhenCalled_ShouldIncrementAttempts()
    {
        var result = Aggregate.CreateInstance(RegisteredModel()).RecordFailedAttempt();

        Assert.Equal(1, result.Model().OtpAttempts);
    }

    [Fact]
    public void UpdateProfile_WhenNameEmpty_ShouldFail()
    {
        var result = Aggregate.CreateInstance(new AnemicModel()).UpdateProfile("", "female", null);

        Assert.NotEqual(DomainOperationResultEnum.Success, result.Result);
    }

    [Fact]
    public void UpdateProfile_WhenGenderInvalid_ShouldFail()
    {
        var result = Aggregate.CreateInstance(new AnemicModel()).UpdateProfile("Alice", "other", null);

        Assert.NotEqual(DomainOperationResultEnum.Success, result.Result);
    }

    [Fact]
    public void UpdateProfile_WhenValid_ShouldNormalizeGender()
    {
        var result = Aggregate.CreateInstance(new AnemicModel()).UpdateProfile("Alice", "FEMALE", "photo");

        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
        Assert.Equal("female", result.Model().Gender);
        Assert.Equal("Alice", result.Model().DisplayName);
    }
}
