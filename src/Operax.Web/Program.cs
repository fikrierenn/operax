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

// HTTP pipeline yapılandırması
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
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
