using DigiTFactory.Libraries.SeedWorks.Result;
using Partners.Domain;
using Partners.Domain.Abstraction;
using Partners.Domain.Implementation;
using Partners.InternalContracts;
using Partners.Storage;
using Xunit;

namespace XDiary.Tests.PartnersCatalog;

/// <summary>TE-002: partners — CRUD + пол (gender), Command → Query → Assert.</summary>
public class PartnerTests
{
    private static IPartnerAnemicModel Register(string name)
    {
        var r = Aggregate.CreateInstance(new AnemicModel()).Register(name);
        Assert.Equal(DomainOperationResultEnum.Success, r.Result);
        return r.Model();
    }

    private static PartnersReadModel ToRead(IPartnerAnemicModel m)
    {
        var e = new PartnerEntity();
        PartnerMapper.MapToEntity(m, e);
        return PartnerMapper.MapToReadModel(e);
    }

    [Fact]
    public void Register_EmptyName_Fails()
    {
        var r = Aggregate.CreateInstance(new AnemicModel()).Register("   ");
        Assert.NotEqual(DomainOperationResultEnum.Success, r.Result);
    }

    [Fact]
    public void Register_ValidName_Query_Returns()
    {
        var read = ToRead(Register("Аня"));
        Assert.Equal("Аня", read.Name);
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
        var model = Register("Аня");
        model = Aggregate.CreateInstance(model)
            .UpdateProfile("data:image/png;base64,AAA", "female", "любит нежность").Model();

        var read = ToRead(model);
        Assert.Equal("Аня", read.Name);
        Assert.Equal("female", read.Gender);
        Assert.Equal("любит нежность", read.Notes);
        Assert.Equal("data:image/png;base64,AAA", read.Photo);
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
        Assert.Equal("Анюта", ToRead(model).Name);
    }

    [Fact]
    public void Remove_Succeeds()
    {
        var r = Aggregate.CreateInstance(Register("Аня")).Remove();
        Assert.Equal(DomainOperationResultEnum.Success, r.Result);
    }
}
