using XDiary.Contracts;
using DigiTFactory.Libraries.SeedWorks.Result;
using Partners.Domain;
using Partners.Domain.Abstraction;
using Partners.Domain.Implementation;
using Xunit;

/// <summary>TE-002: partners — CRUD + пол (gender), Command → Aggregate → Assert.</summary>
public class PartnerTests
{
    private static IPartnerAnemicModel Register(string name)
    {
        var r = Aggregate.CreateInstance(new AnemicModel()).Register(name, Guid.NewGuid());
        Assert.Equal(DomainOperationResultEnum.Success, r.Result);
        return r.Model();
    }

    [Fact]
    public void Register_EmptyName_Fails()
    {
        var r = Aggregate.CreateInstance(new AnemicModel()).Register("   ", Guid.NewGuid());
        Assert.NotEqual(DomainOperationResultEnum.Success, r.Result);
    }

    [Fact]
    public void Register_ValidName_Query_Returns()
    {
        var model = Register("Аня");
        Assert.Equal("Аня", model.Root.Name);
    }

    [Theory]
    [InlineData("male", true)]
    [InlineData("female", true)]
    [InlineData("other", false)]
    [InlineData("", false)]
    public void UpdateProfile_Gender_Validated(string gender, bool expectedSuccess)
    {
        var r = Aggregate.CreateInstance(Register("Аня")).UpdateProfile("", gender, "");
        Assert.Equal(expectedSuccess, r.Result == DomainOperationResultEnum.Success);
    }

    [Fact]
    public void Register_UpdateProfile_Query_ReturnsAllFields()
    {
        var model = Aggregate.CreateInstance(Register("Аня"))
            .UpdateProfile("data:image/png;base64,AAA", "female", "любит нежность").Model();

        Assert.Equal("Аня", model.Root.Name);
        Assert.Equal("female", model.Gender);
        Assert.Equal("любит нежность", model.Notes);
        Assert.Equal("data:image/png;base64,AAA", model.Photo);
    }

    [Fact]
    public void Rename_EmptyName_Fails()
    {
        var r = Aggregate.CreateInstance(Register("Аня")).Rename(" ");
        Assert.NotEqual(DomainOperationResultEnum.Success, r.Result);
    }

    [Fact]
    public void Rename_ValidName_Updates()
    {
        var model = Aggregate.CreateInstance(Register("Аня")).Rename("Анюта").Model();
        Assert.Equal("Анюта", model.Root.Name);
    }

    [Fact]
    public void Remove_Succeeds()
    {
        var r = Aggregate.CreateInstance(Register("Аня")).Remove();
        Assert.Equal(DomainOperationResultEnum.Success, r.Result);
    }
}
