using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Operax.Web.Lib;
using Xunit;

namespace Operax.Tests.Security;

/// <summary>
/// P0-1 güvenlik regresyon testi: SeedData.ResolveAdminPassword env/fail-fast mantığı.
/// ADMIN_PASSWORD env önceliklidir; üretimde env yoksa fail-fast, Development'ta fallback.
/// </summary>
public class ResolveAdminPasswordTests
{
    [Fact]
    public void EnvVarVarsa_OnuDoner()
    {
        Environment.SetEnvironmentVariable("ADMIN_PASSWORD", "EnvP@ssw0rd!");
        try
        {
            var pwd = SeedData.ResolveAdminPassword(new FakeEnv("Production"));
            Assert.Equal("EnvP@ssw0rd!", pwd);
        }
        finally { Environment.SetEnvironmentVariable("ADMIN_PASSWORD", null); }
    }

    [Fact]
    public void EnvYok_Development_VarsayilanDoner()
    {
        Environment.SetEnvironmentVariable("ADMIN_PASSWORD", null);
        var pwd = SeedData.ResolveAdminPassword(new FakeEnv("Development"));
        Assert.False(string.IsNullOrWhiteSpace(pwd));
    }

    [Fact]
    public void EnvYok_Production_FailFast()
    {
        Environment.SetEnvironmentVariable("ADMIN_PASSWORD", null);
        Assert.Throws<InvalidOperationException>(
            () => SeedData.ResolveAdminPassword(new FakeEnv("Production")));
    }

    // Minimum IWebHostEnvironment sahtesi — yalnız EnvironmentName (IsDevelopment) önemli
    private sealed class FakeEnv(string envName) : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = envName;
        public string ApplicationName { get; set; } = "Operax.Tests";
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
