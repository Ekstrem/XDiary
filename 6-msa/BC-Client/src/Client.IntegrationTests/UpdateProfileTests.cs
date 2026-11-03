using Client.Domain;
using Client.Domain.Abstraction;
using Client.Domain.Implementation;
using DigiTFactory.Libraries.SeedWorks.Result;
using XDiary.Contracts;
using Xunit;

public class UpdateProfileTests
{
    [Fact]
    public void UpdateProfile_SetsDisplayNameAndGender()
    {
        var start = Aggregate.CreateInstance(new AnemicModel()).Register("+79990000000", "000000");
        var res = Aggregate.CreateInstance(start.Model()).UpdateProfile("Евгений", "male", null);
        Assert.True(res.IsSuccess());
        Assert.Equal("Евгений", res.Model().DisplayName);
        Assert.Equal("male", res.Model().Gender);
    }

    [Fact]
    public void UpdateProfile_EmptyDisplayName_Fails()
    {
        var start = Aggregate.CreateInstance(new AnemicModel()).Register("+79990000000", "000000");
        var res = Aggregate.CreateInstance(start.Model()).UpdateProfile("", "male", null);
        Assert.False(res.IsSuccess());
    }

    [Fact]
    public void UpdateProfile_EmptyGender_Fails()
    {
        var start = Aggregate.CreateInstance(new AnemicModel()).Register("+79990000000", "000000");
        var res = Aggregate.CreateInstance(start.Model()).UpdateProfile("Евгений", "", null);
        Assert.False(res.IsSuccess());
    }
}
