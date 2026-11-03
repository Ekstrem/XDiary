using Client.Domain;
using Client.Domain.Abstraction;
using Client.Domain.Implementation;
using DigiTFactory.Libraries.SeedWorks.Result;
using Xunit;
using XDiary.Contracts;

public class UpdateProfileTests
{
    private static AggregateResult<IClients, IClientAnemicModel> Start(string name, string gender)
        => Aggregate.CreateInstance(new AnemicModel()).UpdateProfile(name, gender, null);

    [Fact]
    public void Male_CanonicalizesToLowercase()
    {
        var res = Start("Евгений", "Male");
        Assert.True(res.IsSuccess());
        Assert.Equal("male", res.Model().Gender);
    }

    [Fact]
    public void Female_TrimsAndCanonicalizes()
    {
        var res = Start("Евгений", " female ");
        Assert.True(res.IsSuccess());
        Assert.Equal("female", res.Model().Gender);
    }

    [Fact]
    public void Other_Fails()
    {
        Assert.False(Start("Евгений", "Other").IsSuccess());
    }

    [Fact]
    public void EmptyGender_Fails()
    {
        Assert.False(Start("Евгений", "").IsSuccess());
    }

    [Fact]
    public void EmptyDisplayName_Fails()
    {
        Assert.False(Start("", "male").IsSuccess());
    }
}
