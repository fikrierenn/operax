using System.Diagnostics;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Operax.Tests.Integration;

/// <summary>
/// DB integration testleri için izole "Operax_Test" veritabanını bir kez kurar (Plan 37 Faz 3).
/// Gerçek dev DB'ye DOKUNMAZ: bağlantı, Web ile aynı sunucuda fakat ayrı katalog (Operax_Test).
/// Kurulum: drop+create → operax-cli migrate → operax-cli seed (baseline). Tek kaynak migrate sırası
/// CLI'da kaldığı için testler şema listesini kopyalamaz (drift yok).
/// </summary>
public sealed class DatabaseFixture : IDisposable
{
    public const string TestDbName = "Operax_Test";
    public string ConnectionString { get; }

    private readonly string _repoRoot;

    public DatabaseFixture()
    {
        _repoRoot = FindRepoRoot();
        var baseConn = ResolveBaseConnection(_repoRoot);

        // İş kuralı: gerçek DB adını Operax_Test ile değiştir — aynı sunucu, ayrı katalog (izolasyon)
        var builder = new SqlConnectionStringBuilder(baseConn) { InitialCatalog = TestDbName };
        ConnectionString = builder.ConnectionString;

        RecreateDatabase(builder);
        RunCli("migrate");
        RunCli("seed");
    }

    /// <summary>Test DB'sini sunucu üzerinde sıfırdan oluşturur (varsa düşürür).</summary>
    private static void RecreateDatabase(SqlConnectionStringBuilder testBuilder)
    {
        var master = new SqlConnectionStringBuilder(testBuilder.ConnectionString) { InitialCatalog = "master" };
        using var conn = new SqlConnection(master.ConnectionString);
        conn.Open();
        conn.Execute($@"
            IF DB_ID('{TestDbName}') IS NOT NULL
            BEGIN
                ALTER DATABASE [{TestDbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{TestDbName}];
            END
            CREATE DATABASE [{TestDbName}];");
    }

    /// <summary>operax-cli komutunu test DB bağlantısıyla (OPERAX_CONN env) çalıştırır.</summary>
    private void RunCli(string command)
    {
        var cliProj = Path.Combine(_repoRoot, "src", "Operax.Cli", "Operax.Cli.csproj");
        var psi = new ProcessStartInfo("dotnet", $"run --project \"{cliProj}\" -- {command}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            WorkingDirectory       = _repoRoot
        };
        psi.Environment["OPERAX_CONN"] = ConnectionString;

        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("operax-cli başlatılamadı.");
        var stdout = proc.StandardOutput.ReadToEnd();
        var stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit();
        if (proc.ExitCode != 0)
            throw new InvalidOperationException($"operax-cli {command} başarısız (exit {proc.ExitCode}).\n{stdout}\n{stderr}");
    }

    /// <summary>Bağlantıyı env OPERAX_TEST_CONN'dan ya da Web config'inden (appsettings + User Secrets) çözer.</summary>
    private static string ResolveBaseConnection(string repoRoot)
    {
        var env = Environment.GetEnvironmentVariable("OPERAX_TEST_CONN");
        if (!string.IsNullOrWhiteSpace(env)) return env;

        var webDir = Path.Combine(repoRoot, "src", "Operax.Web");
        var cfg = new ConfigurationBuilder()
            .SetBasePath(webDir)
            .AddJsonFile("appsettings.json", optional: true)
            // Web ile aynı UserSecretsId → gerçek bağlantı (SEC-1: secrets'ta tutulur)
            .AddUserSecrets("ab29872f-6bf8-4602-8dc1-c66e703da6cc")
            .Build();
        return cfg.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Test için bağlantı bulunamadı (OPERAX_TEST_CONN veya Web 'Default').");
    }

    /// <summary>AppContext.BaseDirectory'den yukarı çıkarak repo kökünü (src/Operax.Cli içeren) bulur.</summary>
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "src", "Operax.Cli"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Repo kökü bulunamadı (src/Operax.Cli aranıyor).");
    }

    public void Dispose() { /* Test DB bilerek bırakılır — sonraki koşum drop+create eder, hata ayıklama için kalır */ }
}

/// <summary>DB integration testleri tek fixture'ı paylaşır (DB bir kez kurulur).</summary>
[CollectionDefinition("Database")]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseFixture> { }
