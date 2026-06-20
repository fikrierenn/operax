using Operax.Web.Features.Help;
using Xunit;

namespace Operax.Tests.Security;

/// <summary>
/// P0-6 güvenlik regresyon testi: Help RouteToSlug path-traversal whitelist'i (^[a-z0-9-]+$).
/// Bu test, traversal düzeltmesinin (Help/Index.cshtml.cs) geri alınmasını yakalar.
/// </summary>
public class HelpRouteToSlugTests
{
    [Theory]
    [InlineData("..\\..\\appsettings")]   // Windows dizin çıkışı
    [InlineData("../../secret")]           // POSIX dizin çıkışı
    [InlineData("C:\\Windows\\system")]    // mutlak yol
    [InlineData("\\\\server\\share")]      // UNC yol
    [InlineData("foo:bar")]                // sürücü/stream ayırıcı
    [InlineData("foo.md")]                 // nokta (uzantı enjeksiyonu)
    public void RouteToSlug_GuvensizGirisleriReddeder(string ret)
    {
        Assert.Null(IndexModel.RouteToSlug(ret));
    }

    [Theory]
    [InlineData("/PurchaseOrders/Details/00000000-0000-0000-0000-000000000001", "purchaseorders-details")]
    [InlineData("/Dashboard", "dashboard")]
    [InlineData("/MasterData/Items", "masterdata-items")]
    public void RouteToSlug_NormalRotalariKabulEder(string ret, string expected)
    {
        Assert.Equal(expected, IndexModel.RouteToSlug(ret));
    }

    [Fact]
    public void RouteToSlug_BosGirisNullDoner()
    {
        Assert.Null(IndexModel.RouteToSlug(null));
        Assert.Null(IndexModel.RouteToSlug(""));
        Assert.Null(IndexModel.RouteToSlug("   "));
    }
}
