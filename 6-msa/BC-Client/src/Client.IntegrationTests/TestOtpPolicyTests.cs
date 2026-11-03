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
        public IConfigurationSection GetSection(string key) => new FakeSection(_v);
    }
    private sealed class FakeSection : IConfigurationSection
    {
        private readonly Dictionary<string, string?> _v;
        public FakeSection(Dictionary<string, string?> v) => _v = v;
        public string? this[string key] { get => _v.TryGetValue(key, out var x) ? x : null; set => _v[key] = value; }
        public string Key => string.Empty;
        public string Path => string.Empty;
        public string? Value { get => null; set { } }
        public IEnumerable<IConfigurationSection> GetChildren() => Array.Empty<IConfigurationSection>();
        public IChangeToken GetReloadToken() => null!;
        public IConfigurationSection GetSection(string key) => new FakeSection(_v);
    }

    private static IConfiguration Cfg(bool allow) => new FakeConfig(new Dictionary<string, string?>
    { ["Clients:TestOtp"] = "000000", ["Clients:AllowTestOtpOutsideDev"] = allow ? "true" : "false" });

    [Fact]
    public void Production_WithFlag_ReturnsTestOtp() => Assert.Equal("000000", TestOtpPolicy.Resolve(Cfg(true), "Production"));
    [Fact]
    public void Production_WithoutFlag_ReturnsNull() => Assert.Null(TestOtpPolicy.Resolve(Cfg(false), "Production"));
    [Fact]
    public void Development_WithoutFlag_ReturnsTestOtp() => Assert.Equal("000000", TestOtpPolicy.Resolve(Cfg(false), "Development"));
}