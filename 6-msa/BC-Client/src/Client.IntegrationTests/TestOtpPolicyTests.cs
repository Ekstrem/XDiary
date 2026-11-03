using Client.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using Xunit;

public class TestOtpPolicyTests
{
    private sealed class FakeConfig : IConfiguration
    {
        private readonly Dictionary<string, string?> _v;
        public FakeConfig(Dictionary<string, string?> v) => _v = v;
        public string? this[string key] { get => _v.TryGetValue(key, out var x) ? x : null; set => _v[key] = value; }
        public IEnumerable<IConfigurationSection> GetChildren() => Array.Empty<IConfigurationSection>();
        public IChangeToken GetReloadToken() => null!;
        public IConfigurationSection GetSection(string key) => null!;
    }
    private static IConfiguration Cfg(bool allow) => new FakeConfig(new Dictionary<string, string?>
    { ["Clients:TestOtp"] = "000000", ["Clients:AllowTestOtpOutsideDev"] = allow ? "true" : "false" });

    [Fact]
    public void Production_WithFlag_ReturnsTestOtp_AndWarns()
    { var code = TestOtpPolicy.Resolve(Cfg(true), "Production", out var warn); Assert.Equal("000000", code); Assert.True(warn); }
    [Fact]
    public void Production_WithoutFlag_ReturnsNull_NoWarn()
    { var code = TestOtpPolicy.Resolve(Cfg(false), "Production", out var warn); Assert.Null(code); Assert.False(warn); }
    [Fact]
    public void Development_WithoutFlag_ReturnsTestOtp_NoWarn()
    { var code = TestOtpPolicy.Resolve(Cfg(false), "Development", out var warn); Assert.Equal("000000", code); Assert.False(warn); }
    [Fact]
    public void Flag_WithoutTestOtp_Null()
    { var cfg = new FakeConfig(new Dictionary<string, string?> { ["Clients:AllowTestOtpOutsideDev"] = "true" }); var code = TestOtpPolicy.Resolve(cfg, "Production", out var warn); Assert.Null(code); Assert.False(warn); }
}