using System.Threading.RateLimiting;
using Dapper;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using NetEscapades.AspNetCore.SecurityHeaders;
using Operax.Web.Lib;
using Operax.Web.Lib.Authz;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// F0.2/Güvenlik: Kestrel "Server: Kestrel" başlığını gizle (sunucu parmak izi sızıntısı önlenir)
// NetEscapades RemoveServerHeader üst katmanda; Kestrel kendi başlığını burada kapatır.
builder.WebHost.ConfigureKestrel(o => o.AddServerHeader = false);

// Bağlantı dizesini al ve doğrula
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Connection string 'Default' not found.");

// F0.2: Yapılandırılmış loglama — IP + UserAgent enrich + günlük dosya rotasyonu
builder.Host.UseSerilog((ctx, cfg) => cfg
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithClientIp()
    .Enrich.WithRequestHeader("User-Agent", "ClientAgent")
    .WriteTo.Console()
    .WriteTo.File("logs/operax-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] IP:{ClientIp} UA:{ClientAgent} {Message:lj}{NewLine}{Exception}"));

builder.Services.AddDistributedMemoryCache();
builder.Services.AddMemoryCache(); // F0.2: rol-modül erişim haritası cache'i

// Identity — EF Core yerine Dapper tabanlı store kullanır
// AspNetUsers / AspNetRoles tabloları Dapper ile yönetilir
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
    // Yanlış şifre: 5 denemede 5 dakika kilitle
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
})
.AddDefaultTokenProviders();

// Dapper tabanlı Identity store kayıtları
builder.Services.AddScoped<IUserStore<IdentityUser>, DapperUserStore>();
builder.Services.AddScoped<IRoleStore<IdentityRole>, DapperRoleStore>();

// Plan 13 (Model 3): firma-bağlamlı rol — oturum principal'inde rol claim'i aktif firmanın
// UserCompany.Role'undan üretilir (global AspNetUserRoles yerine)
builder.Services.AddScoped<IUserClaimsPrincipalFactory<IdentityUser>, CompanyAwareClaimsPrincipalFactory>();

// Cookie yapılandırması — özel login sayfamıza yönlendir
builder.Services.ConfigureApplicationCookie(opts =>
{
    // Login sayfası @page "/login" rotasında; LoginPath bununla eşleşmeli (aksi halde redirect 404)
    opts.LoginPath = "/login";
    opts.LogoutPath = "/Auth/Logout";
    opts.AccessDeniedPath = "/Auth/AccessDenied";
    opts.SlidingExpiration = true;
    opts.ExpireTimeSpan = TimeSpan.FromHours(8);
    // SEC-3: Güvenli cookie flag'leri
    opts.Cookie.HttpOnly = true;          // JS üzerinden cookie erişimi engellenir (XSS koruması)
    // F0.2/TLS: Prod'da yalnızca HTTPS; dev'de http geliştirme akışı bozulmasın
    opts.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest
        : Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
    opts.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict; // CSRF koruması
});

// F0.2/HSTS: production'da 1 yıl + alt domainler + preload
builder.Services.AddHsts(o =>
{
    o.MaxAge = TimeSpan.FromDays(365);
    o.IncludeSubDomains = true;
    o.Preload = true;
});

// F0.2: Rate Limiting — brute-force/DDoS koruması
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Global fallback: IP başına dakikada 200 istek
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions { PermitLimit = 200, Window = TimeSpan.FromMinutes(1) }));

    // Login: IP başına dakikada 10 istek (Identity kilidini IP atlatamasın)
    options.AddPolicy("login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));

    // Şirket değiştirme: kullanıcı başına dakikada 10 istek
    options.AddPolicy("switch-company", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.Identity?.Name
                ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});

// F0.2: DB-driven RBAC — rol-modül yetki handler'ı
builder.Services.AddScoped<IAuthorizationHandler, ModuleAccessHandler>();

// Yetki politikaları: Admin sabit; modül policy'leri ince sarmalayıcı (allow/deny DB'den handler ile)
var authz = builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdministratorOnly", p => p.RequireRole(Roles.Administrator));
foreach (var key in ModuleKeys.All)
    authz.AddPolicy($"mod:{key}", p => p.AddRequirements(new ModuleAccessRequirement(key)));

// Lib Services
builder.Services.AddSingleton<Db>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<ICurrentCompany, CurrentCompany>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<INumberSeriesService, NumberSeriesService>();
// Plan 27: Yerel AI istemcisi (qwen2.5, LLamaSharp süreç-içi) — fiyat farkı gerekçe denetimi.
// Singleton: model bir kez yüklenir (tembel, Ai:Enabled=false ise hiç yüklenmez). Bulut yok.
builder.Services.AddSingleton<Operax.Web.Lib.Ai.IOperaxAiClient, Operax.Web.Lib.Ai.OperaxAiClient>();

// Hangfire — arka plan görev kuyruğu
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(connectionString));
builder.Services.AddHangfireServer();
builder.Services.AddScoped<Operax.Web.Lib.Jobs.ReconciliationExpiryJob>();

// Razor Pages — Feature-based yapı + klasör bazlı yetkilendirme (RBAC)
builder.Services.AddRazorPages()
    .WithRazorPagesRoot("/Features")
    .AddRazorPagesOptions(o =>
    {
        // Admin klasörü sadece Administrator
        o.Conventions.AuthorizeFolder("/Admin", "AdministratorOnly");
        // Her modül klasörü kendi policy'sine bağlanır; karar RoleModuleAccess'ten gelir
        foreach (var key in ModuleKeys.All)
            o.Conventions.AuthorizeFolder("/" + key, $"mod:{key}");
    });
builder.Services.AddSignalR();

var app = builder.Build();

// F0.2: Güvenlik header'ları — pipeline'ın en başında (statik dosyalar dahil tüm yanıtlara uygulanır)
var headerPolicies = new HeaderPolicyCollection()
    .AddFrameOptionsDeny()                              // Clickjacking
    .AddContentTypeOptionsNoSniff()                     // MIME sniffing
    .AddXssProtectionBlock()                            // Eski tarayıcı XSS filtresi
    .AddReferrerPolicyStrictOriginWhenCrossOrigin()     // URL sızıntısı önleme
    .RemoveServerHeader()
    .AddContentSecurityPolicy(csp =>
    {
        csp.AddDefaultSrc().Self();
        csp.AddScriptSrc().Self().UnsafeInline();       // inline/Alpine scriptleri
        csp.AddStyleSrc().Self().UnsafeInline().From("https://fonts.googleapis.com");
        csp.AddFontSrc().Self().From("https://fonts.gstatic.com");
        csp.AddImgSrc().Self().Data();
        csp.AddConnectSrc().Self();
        csp.AddFrameAncestors().None();                 // Clickjacking (CSP seviyesi)
    })
    .AddPermissionsPolicy(pp =>
    {
        pp.AddCamera().None();
        pp.AddMicrophone().None();
        pp.AddGeolocation().None();
    });
app.UseSecurityHeaders(headerPolicies);

// Arayüz tamamen Türkçe (turkish-ui.md kuralı) — tek desteklenen kültür tr-TR
var supportedCultures = new[] { "tr-TR" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("tr-TR")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);
app.UseRequestLocalization(localizationOptions);

// Reverse proxy (Nginx / Hostinger VPS) arkasında gerçek şema (https) ve istemci IP'sini al.
// X-Forwarded-Proto olmadan Kestrel isteği http görür → UseHttpsRedirection sonsuz döngü +
// CookieSecurePolicy.Always nedeniyle giriş cookie'si set edilemez (login kırılır).
// KnownProxies/Networks temizlenir: önümüzdeki tek ters-proxy (aynı host Nginx) güvenilir kabul edilir.
var forwardedOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwardedOptions.KnownNetworks.Clear();
forwardedOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedOptions);

// HTTP pipeline yapılandırması
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();

// F0.2: Rate limiter routing'den sonra, endpoint'lerden önce
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages();
// F0.2: Hangfire dashboard sadece Administrator
app.MapHangfireDashboard("/admin/jobs").RequireAuthorization("AdministratorOnly");

// Plan 19: cari mutabakat sessiz onay job'ı — her gün 02:00 (TTK md.94 1 ay deadline)
RecurringJob.AddOrUpdate<Operax.Web.Lib.Jobs.ReconciliationExpiryJob>(
    "reconciliation-expiry", j => j.RunAsync(), "0 2 * * *");

// Root URL: giriş yapılmışsa Dashboard'a, yapılmamışsa Login'e yönlendir
app.MapGet("/", (HttpContext ctx) =>
    ctx.User.Identity?.IsAuthenticated == true
        ? Results.Redirect("/Dashboard")
        : Results.Redirect("/login"));

// Aktif Şirket Değiştirme (Company Switcher) API Endpoint
// SEC-2: Kullanıcının hedef şirkete erişim yetkisi UserCompany tablosundan doğrulanır
app.MapPost("/api/switch-company", async (
    HttpContext ctx,
    Microsoft.AspNetCore.Identity.UserManager<Microsoft.AspNetCore.Identity.IdentityUser> userManager,
    Microsoft.AspNetCore.Identity.SignInManager<Microsoft.AspNetCore.Identity.IdentityUser> signInManager,
    Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery,
    IAuditService audit,
    Db db) =>
{
    // CSRF koruması: sidebar formu @Html.AntiForgeryToken() üretir; token doğrulanmazsa
    // kötü niyetli sayfa kullanıcının firmasını değiştirebilir (AR-003). Önce token doğrula.
    try
    {
        await antiforgery.ValidateRequestAsync(ctx);
    }
    catch (Microsoft.AspNetCore.Antiforgery.AntiforgeryValidationException)
    {
        return Results.Forbid();
    }

    var companyIdStr = ctx.Request.Form["companyId"].ToString();
    if (!System.Guid.TryParse(companyIdStr, out var companyId))
        return Results.Redirect("/Dashboard");

    var user = await userManager.GetUserAsync(ctx.User);
    if (user == null)
        return Results.Unauthorized();

    // İş kuralı: kullanıcı yalnızca UserCompany'de aktif erişimi olan firmaya geçebilir (IDOR koruması)
    using var conn = db.Open();
    var hasAccess = await conn.ExecuteScalarAsync<int>(
        "SELECT COUNT(1) FROM UserCompany WHERE UserId = @UserId AND CompanyId = @CompanyId AND IsActive = 1",
        new { UserId = user.Id, CompanyId = companyId });

    if (hasAccess == 0)
    {
        var logger = ctx.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogWarning("SEC-2: {User} yetkisiz şirket değiştirme girişimi. CompanyId: {CompanyId}", user.UserName, companyId);
        return Results.Forbid();
    }

    // Aktif firma claim'ini güncelle: eski "company" claim'leri silinip yenisi yazılır
    var claims = await userManager.GetClaimsAsync(user);
    var oldCompanyClaims = claims.Where(c => c.Type == "company").ToList();
    foreach (var oldClaim in oldCompanyClaims)
        await userManager.RemoveClaimAsync(user, oldClaim);

    await userManager.AddClaimAsync(user, new System.Security.Claims.Claim("company", companyId.ToString()));
    // RefreshSignInAsync, CompanyAwareClaimsPrincipalFactory'i yeniden çalıştırır →
    // rol claim'i otomatik olarak yeni firmanın UserCompany.Role'una göre güncellenir (rol-aware geçiş)
    await signInManager.RefreshSignInAsync(user);

    // Denetim izi: kim, hangi firmaya geçti (güvenlik kuralı — firma değiştirme loglanır)
    await audit.LogAsync("COMPANY_SWITCH", "Company", companyId, $"Kullanıcı {user.UserName} aktif firmayı değiştirdi.");

    return Results.Redirect("/Dashboard");
}).RequireAuthorization().RequireRateLimiting("switch-company");

// Başlangıç seed: Admin kullanıcısı + şirket yoksa oluşturur (geliştirme ve ilk kurulum için)
// Tablolar mevcut değilse hata loglanır, uygulama çalışmaya devam eder
try
{
    await SeedData.EnsureAdminAsync(app.Services);
}
catch (Exception ex)
{
    var log = app.Services.GetRequiredService<ILogger<Program>>();
    log.LogWarning(ex, "Seed data oluşturulamadı. Tablolar mevcut olmayabilir — önce schema çalıştırın.");
}

app.Run();
