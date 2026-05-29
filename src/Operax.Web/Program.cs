using Hangfire;
using Microsoft.AspNetCore.Identity;
using Operax.Web.Lib;

var builder = WebApplication.CreateBuilder(args);

// Bağlantı dizesini al ve doğrula
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Connection string 'Default' not found.");

builder.Services.AddDistributedMemoryCache();

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

// Cookie yapılandırması — özel login sayfamıza yönlendir
builder.Services.ConfigureApplicationCookie(opts =>
{
    opts.LoginPath = "/Auth/Login";
    opts.LogoutPath = "/Auth/Logout";
    opts.AccessDeniedPath = "/Auth/AccessDenied";
    opts.SlidingExpiration = true;
    opts.ExpireTimeSpan = TimeSpan.FromHours(8);
});

// Lib Services
builder.Services.AddSingleton<Db>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<ICurrentCompany, CurrentCompany>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<INumberSeriesService, NumberSeriesService>();

// Hangfire — arka plan görev kuyruğu
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(connectionString));
builder.Services.AddHangfireServer();

// Razor Pages — Feature-based yapı
builder.Services.AddRazorPages().WithRazorPagesRoot("/Features");
builder.Services.AddSignalR();

var app = builder.Build();

// Arayüz tamamen Türkçe (turkish-ui.md kuralı) — tek desteklenen kültür tr-TR
var supportedCultures = new[] { "tr-TR" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("tr-TR")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);
app.UseRequestLocalization(localizationOptions);

// HTTP pipeline yapılandırması
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages();
app.MapHangfireDashboard("/admin/jobs").RequireAuthorization();

// Root URL: giriş yapılmışsa Dashboard'a, yapılmamışsa Login'e yönlendir
app.MapGet("/", (HttpContext ctx) =>
    ctx.User.Identity?.IsAuthenticated == true
        ? Results.Redirect("/Dashboard")
        : Results.Redirect("/login"));

// Aktif Şirket Değiştirme (Company Switcher) API Endpoint
app.MapPost("/api/switch-company", async (HttpContext ctx, Microsoft.AspNetCore.Identity.UserManager<Microsoft.AspNetCore.Identity.IdentityUser> userManager, Microsoft.AspNetCore.Identity.SignInManager<Microsoft.AspNetCore.Identity.IdentityUser> signInManager) =>
{
    var companyIdStr = ctx.Request.Form["companyId"].ToString();
    if (System.Guid.TryParse(companyIdStr, out var companyId))
    {
        var user = await userManager.GetUserAsync(ctx.User);
        if (user != null)
        {
            var claims = await userManager.GetClaimsAsync(user);
            var oldCompanyClaims = claims.Where(c => c.Type == "company").ToList();
            foreach (var oldClaim in oldCompanyClaims)
            {
                await userManager.RemoveClaimAsync(user, oldClaim);
            }
            await userManager.AddClaimAsync(user, new System.Security.Claims.Claim("company", companyId.ToString()));
            await signInManager.RefreshSignInAsync(user);
        }
    }
    return Results.Redirect("/Dashboard");
}).DisableAntiforgery();

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
