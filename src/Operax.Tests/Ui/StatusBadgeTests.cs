using Operax.Web.Lib;
using Xunit;

namespace Operax.Tests.Ui;

/// <summary>
/// UiHelpers.StatusBadge birim testleri — DocStatus eşlemesi + A-8 fallback HTML-encode regresyonu.
/// </summary>
public class StatusBadgeTests
{
    [Theory]
    [InlineData(DocStatus.Draft, "TASLAK", "badge-warn")]
    [InlineData(DocStatus.Posted, "İŞLENDİ", "badge-success")]
    [InlineData(DocStatus.Cancelled, "İPTAL", "badge-danger")]
    public void StatusBadge_BilinenKoduDogruRozeteEsler(string code, string label, string cssClass)
    {
        var html = UiHelpers.StatusBadge(code);
        Assert.Contains(label, html);
        Assert.Contains(cssClass, html);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void StatusBadge_BosKodNotrRozetDoner(string? code)
    {
        var html = UiHelpers.StatusBadge(code);
        Assert.Contains("badge-neutral", html);
        Assert.Contains("—", html);
    }

    [Fact]
    public void StatusBadge_BilinmeyenKoduHtmlEncodeEder()
    {
        // A-8 regresyon: fallback kod doğrudan HTML'e gömülmemeli
        var html = UiHelpers.StatusBadge("<script>alert(1)</script>");
        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;", html);
    }
}
