using Client.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using Xunit;

public class RateLimitPolicyTests
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
    private static IConfiguration Cfg(bool disable) => new FakeConfig(new Dictionary<string, string?>
    { ["Clients:DisableOtpRateLimit"] = disable ? "true" : "false" });

    [Fact]
    public void Production_NoFlag_RateLimitEnabled()
    { Assert.False(RateLimitPolicy.IsDisabled(Cfg(false), "Production")); }

    [Fact]
    public void Development_NoFlag_RateLimitDisabled()
    { Assert.True(RateLimitPolicy.IsDisabled(Cfg(false), "Development")); }

    [Fact]
    public void Production_WithFlag_RateLimitDisabled()
    { Assert.True(RateLimitPolicy.IsDisabled(Cfg(true), "Production")); }
}
