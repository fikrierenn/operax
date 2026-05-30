using System;
using System.IO;
using System.Data;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Dapper;

namespace Operax.Cli;

class Program
{
    static string? _connStr;

    static string ConnStr => _connStr ??= ResolveConnectionString();

    static string ResolveConnectionString()
    {
        // 1) Env variable
        var env = Environment.GetEnvironmentVariable("OPERAX_CONN");
        if (!string.IsNullOrWhiteSpace(env)) return env;

        // 2) appsettings.json in docs/sql sibling web project
        var webDir = FindDir("src/Operax.Web");
        if (webDir != null)
        {
            var cfg = new ConfigurationBuilder()
                .SetBasePath(webDir)
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .Build();
            var cs = cfg.GetConnectionString("Default");
            if (!string.IsNullOrWhiteSpace(cs)) return cs;
        }

        // 3) Fallback
        return "Server=.\\SQLEXPRESS;Database=Operax;Integrated Security=True;TrustServerCertificate=True";
    }

    static string? FindDir(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return null;
    }

    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Operax DB Management Tool ===");

        if (args.Length == 0)
        {
            ShowHelp();
            return;
        }

        var command = args[0].ToLower();

        try
        {
            switch (command)
            {
                case "migrate":
                    var sqlDir = FindDir("docs/sql") ?? ".";
                    // 1. Çekirdek şema (tablolar, index'ler, seed)
                    await ExecuteScriptAsync(Path.Combine(sqlDir, "schema_all.sql"), tolerant: true);
                    // 2. Ek modül şemaları (alt parçalar — idempotent IF NOT EXISTS koruması var)
                    foreach (var addonSchema in new[] {
                        "schema_M02_Costing.sql",            // ItemCost + PriceVariance + StockMovement.UnitCost
                        "schema_M04_SalesInvoice.sql",       // Satış faturası
                        "schema_M11_Finance.sql",            // Kasa, banka, çek, senet, kredi, kart, ödeme planı
                        "schema_M01_M04_StarterFields.sql",  // Item/Partner/Header tablolarına STARTER eksik kolonlar
                        "schema_M04_EBelge.sql",             // e-Fatura / e-Arşiv / e-İrsaliye altyapısı (inbound sync)
                        "schema_M01_M11_RiskAndLoanTypes.sql", // Partner risk + Loan tipleri + Kart-Banka bağlantısı
                        "schema_M01_PartnerExtended.sql",     // Plan 08: cari sorumlu temsilci + contact/address/bank/activity
                        "schema_M11_AccountMovement.sql",     // Plan 09: cari hesap defteri (StockMovement muadili)
                        "schema_M00_NumberSeries.sql",        // Plan 10: belge seri yönetimi (otomatik numaralama)
                        "schema_M11_DocumentRegistry.sql",    // Plan 11: gelen belge kayıt no (RegistryNo)
                        "schema_UserCompany.sql"              // Plan 13: UserCompany yetki tablosu
                    })
                    {
                        var addon = Path.Combine(sqlDir, addonSchema);
                        if (File.Exists(addon)) await ExecuteScriptAsync(addon, tolerant: true);
                        else Console.WriteLine($"  [SKIP] {addonSchema} bulunamadi");
                    }
                    // 3. DB nesneleri (SP, FN, View) — CREATE OR ALTER
                    await ExecuteScriptAsync(Path.Combine(sqlDir, "db_objects.sql"), tolerant: false);
                    // 4. STARTER paketi için ek SP'ler (M02 maliyet, M03 fiyat farkı, M04 fatura, M11 finans)
                    var starter = Path.Combine(sqlDir, "db_objects_starter.sql");
                    if (File.Exists(starter)) await ExecuteScriptAsync(starter, tolerant: false);
                    break;

                case "seed":
                    var seedDir = FindDir("docs/sql") ?? ".";
                    foreach (var seedFile in new[] { "seed_core.sql", "seed_company_claims.sql", "setup_tax_dictionary.sql", "seed_demo.sql", "seed_dashboard.sql", "seed_business_history.sql", "seed_finance_starter.sql" })
                    {
                        var p = Path.Combine(seedDir, seedFile);
                        if (File.Exists(p)) await ExecuteScriptAsync(p, tolerant: true);
                        else Console.WriteLine($"  [SKIP] {seedFile} bulunamadi");
                    }
                    break;

                case "script":
                    if (args.Length < 2) { Console.WriteLine("Hata: Dosya yolu belirtilmedi."); return; }
                    await ExecuteScriptAsync(args[1], tolerant: false);
                    break;

                case "query":
                    if (args.Length < 2) { Console.WriteLine("Hata: Sorgu belirtilmedi."); return; }
                    await ExecuteQueryAsync(args[1]);
                    break;

                case "status":
                    await ExecuteQueryAsync("SELECT name, state_desc FROM sys.databases WHERE name = DB_NAME()");
                    break;

                case "list-tables":
                    await ExecuteQueryAsync(
                        "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES " +
                        "WHERE TABLE_TYPE='BASE TABLE' ORDER BY TABLE_NAME");
                    break;

                case "tables-empty":
                    await ExecuteQueryAsync(
                        "SELECT t.TABLE_NAME, p.rows AS RowCount " +
                        "FROM INFORMATION_SCHEMA.TABLES t " +
                        "JOIN sys.partitions p ON p.object_id = OBJECT_ID(t.TABLE_NAME) " +
                        "WHERE t.TABLE_TYPE='BASE TABLE' AND p.index_id IN (0,1) " +
                        "ORDER BY t.TABLE_NAME");
                    break;

                case "check-fk":
                    await ExecuteQueryAsync(
                        "SELECT fk.name AS FK, tp.name AS ParentTable, tr.name AS RefTable, " +
                        "fk.is_disabled AS Disabled " +
                        "FROM sys.foreign_keys fk " +
                        "JOIN sys.tables tp ON tp.object_id = fk.parent_object_id " +
                        "JOIN sys.tables tr ON tr.object_id = fk.referenced_object_id " +
                        "ORDER BY tp.name, fk.name");
                    break;

                default:
                    Console.WriteLine($"Bilinmeyen komut: {command}");
                    ShowHelp();
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nHATA: {ex.Message}");
            if (ex.InnerException != null) Console.WriteLine($"DETAY: {ex.InnerException.Message}");
            Console.ResetColor();
            Environment.Exit(1);
        }
    }

    static void ShowHelp()
    {
        Console.WriteLine("\nKullanim:");
        Console.WriteLine("  operax-cli migrate              -> schema_all.sql uygular (idempotent)");
        Console.WriteLine("  operax-cli seed                 -> seed_core + company_claims + tax_dictionary");
        Console.WriteLine("  operax-cli script <dosya>       -> SQL dosyasi calistirir");
        Console.WriteLine("  operax-cli query \"SELECT ...\"   -> SQL sorgusu calistirir");
        Console.WriteLine("  operax-cli status               -> DB durumunu goster");
        Console.WriteLine("  operax-cli list-tables          -> Tablolari listele");
        Console.WriteLine("  operax-cli tables-empty         -> Tablo satir sayilarini goster");
        Console.WriteLine("  operax-cli check-fk             -> Foreign key listesi");
        Console.WriteLine("\nBaglanti:");
        Console.WriteLine("  Env: OPERAX_CONN");
        Console.WriteLine("  appsettings.json > ConnectionStrings:Default");
    }

    static async Task ExecuteQueryAsync(string sql)
    {
        Console.WriteLine($"\nSorgu: {sql}");
        using var conn = await OpenAsync();

        var reader = await conn.ExecuteReaderAsync(sql);
        var table = new DataTable();
        table.Load(reader);

        if (table.Rows.Count == 0)
        {
            Console.WriteLine("(sonuc yok)");
            return;
        }

        var widths = new int[table.Columns.Count];
        for (int i = 0; i < table.Columns.Count; i++)
            widths[i] = Math.Max(table.Columns[i].ColumnName.Length,
                table.Rows.Cast<DataRow>().Max(r => r[i]?.ToString()?.Length ?? 0));

        void PrintRow(Func<int, string?> cell)
        {
            for (int i = 0; i < table.Columns.Count; i++)
                Console.Write((cell(i) ?? "").PadRight(widths[i] + 2));
            Console.WriteLine();
        }

        PrintRow(i => table.Columns[i].ColumnName);
        Console.WriteLine(new string('-', widths.Sum() + widths.Length * 2));
        foreach (DataRow row in table.Rows)
            PrintRow(i => row[i]?.ToString());

        Console.WriteLine($"\n{table.Rows.Count} satir");
    }

    // Non-fatal SQL error numbers (object/index already exists, duplicate key)
    static readonly HashSet<int> WarnOnly = new() { 2714, 1913, 2627, 2601, 1505, 2705 };

    static async Task ExecuteScriptAsync(string path, bool tolerant)
    {
        if (!File.Exists(path))
        {
            Console.WriteLine($"Hata: Dosya bulunamadi: {path}");
            return;
        }

        Console.WriteLine($"\nScript: {Path.GetFileName(path)}");
        var script = await File.ReadAllTextAsync(path);

        // Proper GO splitter: line that is just GO (case-insensitive)
        var batches = Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);

        using var conn = await OpenAsync();

        int ok = 0, warn = 0, fail = 0;
        foreach (var batch in batches)
        {
            if (string.IsNullOrWhiteSpace(batch)) continue;
            try
            {
                await conn.ExecuteAsync(batch);
                ok++;
            }
            catch (SqlException ex) when (tolerant && WarnOnly.Contains(ex.Number))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  [WARN {ex.Number}] {ex.Message.Split('\n')[0]}");
                Console.ResetColor();
                warn++;
            }
            catch (SqlException ex) when (tolerant)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  [ERR  {ex.Number}] {ex.Message.Split('\n')[0]}");
                Console.ResetColor();
                fail++;
            }
        }

        Console.ForegroundColor = fail > 0 ? ConsoleColor.Yellow : ConsoleColor.Green;
        Console.WriteLine($"\nTamamlandi — ok:{ok} warn:{warn} fail:{fail}");
        Console.ResetColor();
    }

    static async Task<SqlConnection> OpenAsync()
    {
        var conn = new SqlConnection(ConnStr);
        await conn.OpenAsync();
        // Required for filtered indexes and views
        await conn.ExecuteAsync("SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON;");
        return conn;
    }
}
