using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Markdig;

namespace Operax.Web.Features.Help;

/// <summary>
/// Ekran-içi yardım / kullanıcı kitapçığı görüntüleyici. ?ret=&lt;route&gt; parametresiyle
/// gelen ekranın yardım Markdown'unu (App_Data/manual/screens/&lt;slug&gt;.md) HTML olarak render eder.
/// İçerik yoksa nazik "hazırlanıyor" mesajı + kitapçık dizini gösterilir.
/// </summary>
[Authorize]
public class IndexModel(IWebHostEnvironment env, ILogger<IndexModel> logger) : PageModel
{
    public string Title { get; set; } = "Yardım";
    public string ContentHtml { get; set; } = "";
    public bool Found { get; set; }
    public string? ReturnPath { get; set; }
    public List<(string Slug, string Title)> AllTopics { get; set; } = [];

    // Markdown render hattı — güvenli HTML (raw inline HTML devre dışı, XSS yüzeyi yok)
    private static readonly MarkdownPipeline Pipeline =
        new MarkdownPipelineBuilder().UseAdvancedExtensions().DisableHtml().Build();

    public void OnGet(string? ret)
    {
        ReturnPath = string.IsNullOrWhiteSpace(ret) ? null : ret;
        var dir = Path.Combine(env.ContentRootPath, "App_Data", "manual", "screens");
        LoadTopics(dir);

        // ret rotasından slug üret: /PurchaseOrders/Details/{guid} → purchaseorders-details
        var slug = RouteToSlug(ret);
        if (slug is null) { Title = "Kullanıcı Kitapçığı"; Found = false; return; }

        var path = Path.Combine(dir, slug + ".md");
        if (!System.IO.File.Exists(path))
        {
            // Slug birebir yoksa modül kökü (ilk segment) yardımına düş
            var moduleSlug = slug.Split('-')[0];
            var modulePath = Path.Combine(dir, moduleSlug + ".md");
            if (System.IO.File.Exists(modulePath)) path = modulePath;
        }

        if (System.IO.File.Exists(path))
        {
            try
            {
                var md = System.IO.File.ReadAllText(path);
                ContentHtml = Markdown.ToHtml(md, Pipeline);
                Found = true;
                Title = FirstHeading(md) ?? "Yardım";
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Yardım içeriği okunamadı: {Path}", path);
                Found = false;
            }
        }
        else
        {
            Found = false;
        }
    }

    // Dizindeki tüm yardım başlıklarını listele (kitapçık içindekiler)
    private void LoadTopics(string dir)
    {
        if (!Directory.Exists(dir)) return;
        foreach (var f in Directory.EnumerateFiles(dir, "*.md").OrderBy(x => x))
        {
            var slug = Path.GetFileNameWithoutExtension(f);
            string title;
            try { title = FirstHeading(System.IO.File.ReadAllText(f)) ?? slug; }
            catch { title = slug; }
            AllTopics.Add((slug, title));
        }
    }

    // Route → dosya slug: küçük harf, segment'leri '-' ile birleştir, guid/sayı segmentleri at
    private static string? RouteToSlug(string? ret)
    {
        if (string.IsNullOrWhiteSpace(ret)) return null;
        var pathOnly = ret.Split('?')[0].Trim('/');
        if (pathOnly.Length == 0) return null;
        var segments = pathOnly.Split('/')
            .Where(s => !string.IsNullOrEmpty(s) && !IsIdSegment(s))
            .Select(s => s.ToLowerInvariant());
        var slug = string.Join('-', segments);
        return string.IsNullOrEmpty(slug) ? null : slug;
    }

    // Guid veya sayı segmenti (kayıt id'si) yardım slug'ına dahil edilmez
    private static bool IsIdSegment(string s)
        => Guid.TryParse(s, out _) || long.TryParse(s, out _);

    private static string? FirstHeading(string md)
    {
        foreach (var line in md.Split('\n'))
        {
            var t = line.TrimStart();
            if (t.StartsWith('#')) return t.TrimStart('#').Trim();
        }
        return null;
    }
}
