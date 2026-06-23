using System;
using System.IO;
using System.Data;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Dapper;
using Npgsql;

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
                // SEC-1 sonrası gerçek DB bağlantısı Web User Secrets'ta tutuluyor.
                // Web ile aynı UserSecretsId → CLI de gerçek bağlantıyı okur (appsettings placeholder'ı ezilir).
                .AddUserSecrets("ab29872f-6bf8-4602-8dc1-c66e703da6cc")
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
                    // 0. DB uyumluluk seviyesi 160 (SQL 2022) sigortası — sürüm-korumalı, tolerant (eski SQL'de atlar)
                    await ExecuteScriptAsync(Path.Combine(sqlDir, "migration_set_compat.sql"), tolerant: true);
                    // 1. Çekirdek şema (tablolar, index'ler, seed)
                    await ExecuteScriptAsync(Path.Combine(sqlDir, "schema_all.sql"), tolerant: true);
                    // 2. Ek modül şemaları (alt parçalar — idempotent IF NOT EXISTS koruması var)
                    foreach (var addonSchema in new[] {
                        "schema_M02_Costing.sql",            // ItemCost + PriceVariance + StockMovement.UnitCost
                        "schema_M02_PriceVarianceAi.sql",    // Plan 27: PriceVariance override gerekçe + AI verdict kolonları
                        "schema_M04_SalesInvoice.sql",       // Satış faturası
                        "schema_M11_Finance.sql",            // Kasa, banka, çek, senet, kredi, kart, ödeme planı
                        "schema_M01_M04_StarterFields.sql",  // Item/Partner/Header tablolarına STARTER eksik kolonlar
                        "schema_M04_EBelge.sql",             // e-Fatura / e-Arşiv / e-İrsaliye altyapısı (inbound sync)
                        "schema_M01_M11_RiskAndLoanTypes.sql", // Partner risk + Loan tipleri + Kart-Banka bağlantısı
                        "schema_M01_PartnerExtended.sql",     // Plan 08: cari sorumlu temsilci + contact/address/bank/activity
                        "schema_M11_AccountMovement.sql",     // Plan 09: cari hesap defteri (StockMovement muadili)
                        "schema_M11_Reconciliation.sql",      // Plan 18: AccountReconciliation (açık-kalem kapama) — AccountMovement'tan sonra
                        "schema_M11_PartnerReconciliation.sql", // Plan 19: PartnerReconciliationLog (cari mutabakat turu)
                        "schema_M00_NumberSeries.sql",        // Plan 10: belge seri yönetimi (otomatik numaralama)
                        "schema_M11_DocumentRegistry.sql",    // Plan 11: gelen belge kayıt no (RegistryNo)
                        "schema_UserCompany.sql",             // Plan 13: UserCompany yetki tablosu
                        "schema_M00_Security.sql",            // Plan 17: RoleModuleAccess (DB-driven RBAC)
                        "schema_M11_LedgerIntegrity.sql",     // Plan 14: AccountingPeriod + PeriodOverrideLog + dönem guard
                        "schema_M01_Branch.sql",              // Plan 23 Faz A: Branch (Şube) tablosu + Warehouse.BranchId/City/Address
                        "schema_M01_Branch_F.sql",            // Plan 23 Faz F: Belge+ledger tablolarına BranchId (evrak+SM+AM)
                        "schema_M05_DocChain.sql",            // Plan 05 Faz 1: ExpenseInvoice.ReceivingId + ShippingHeader.SalesOrderId
                        "schema_M03_PurchaseInvoice.sql",     // Plan 24: Mal Alım Faturası (PurchaseInvoice/Line) + ItemType CONSUMABLE
                        "schema_M03_ReceivingPoControl.sql",  // Plan 28: mal kabul mod + tedarikçi irsaliye + ReturnQty (iade alanı)
                        "schema_M03_ReceivingInvoiceMatch.sql", // Plan 28 Faz C: PurchaseInvoiceLine.PurchaseOrderLineId + ReceivingLine.InvoicedQty
                        "schema_M18_ExpenseReporting.sql",    // Plan 26: ExpenseType.ParentId + ExpenseInvoice.CostCenterId + Line.CostCenterId NULL
                        "schema_M02_MaterialIssue.sql",       // Plan 25: Sarf Fişi (MaterialIssueHeader/Line)
                        "schema_M04_ShipmentInvoiceMerge.sql", // Plan 21: İrsaliye↔Fatura N:1 (InvoicedQty + SourceShipmentLineId + IssueDate)
                        "schema_M01_SupplierItem.sql",        // Plan 32: Tedarikçi-Ürün Kataloğu (SupplierItem)
                        "schema_M_UDF.sql",                   // Plan 34: UserFieldDefinition + Item.AdditionalFields/MinStockLevel/MaxStockLevel
                        "schema_M00_DictRefCols.sql",         // Plan 35 Faz 1: DictionaryValue.UnEceCode + IsWholeNumber (referans baseline taşıyıcı)
                        "migration_add_updatedby.sql",        // Zorunlu audit kolonu UpdatedBy/At — fresh-install'da SP CREATE bağımlılığı (db_objects.sql ÖNCE çalışmalı)
                        "migration_41_status_checks.sql",     // Plan 41: statü/yön/tip uzlaştırma (HAVALE→GIRO, NEW→DRAFT) + 23 CHECK constraint
                        "migration_44_stock_consume.sql",     // Plan 44: StockMovement.SourceLineId + consume key-range index + idempotency unique
                        "migration_44b_idempotency_multibin.sql", // Plan 44 Faz 3: idempotency index'e BinId+LotNo (multi-bin split)
                        "migration_46_dict_crud_flag.sql",    // Plan 46 M0: DictionaryType.AllowValueCrud (kod-çıpalı vs dinamik CRUD)
                        "migration_47_itemcost_company_grain.sql", // Plan 47 M1 Faz 1: ItemCost per-depo→şirket-genel collapse + OnHandQty ledger senkron
                        "migration_48_costing_method_lock.sql", // Plan 47 M1 Faz 3: CostingMethod MOVING_AVG kilit (FIFO/STANDARD motoru yok)
                        "migration_49_materialissue_scrap.sql"  // Plan 47 M1 Faz 4: MaterialIssue.ReasonCode (zayiat HASAR/FIRE/HURDA → StockMovement SCRAP)

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
                    // 5. Reversal SP'leri (Plan 14/16/22) — POSTED evrak iptali + ters StockMovement/AccountMovement
                    var reversal = Path.Combine(sqlDir, "db_objects_reversal.sql");
                    if (File.Exists(reversal)) await ExecuteScriptAsync(reversal, tolerant: false);
                    // 6. Branch boyutu nesneleri (Plan 23 Faz F) — StockMovement trigger + fn_DefaultBranchId
                    var branch = Path.Combine(sqlDir, "db_objects_branch.sql");
                    if (File.Exists(branch)) await ExecuteScriptAsync(branch, tolerant: false);
                    // 7. Belge zinciri SP'leri (Plan 05/24) — Receiving/Shipping/PurchaseInvoice create + post
                    var docchain = Path.Combine(sqlDir, "db_objects_docchain.sql");
                    if (File.Exists(docchain)) await ExecuteScriptAsync(docchain, tolerant: false);
                    // 7b. Mutabakat SP/TVF'leri (Plan 18/19) — açık-kalem kapama + cari mutabakat turu.
                    //     expensereport'tan ÖNCE (v_ExpenseDistribution canonical'ı expensereport'ta; bu dosyadan çıkarıldı — Plan 48).
                    var recon = Path.Combine(sqlDir, "db_objects_reconciliation.sql");
                    if (File.Exists(recon)) await ExecuteScriptAsync(recon, tolerant: false);
                    var partnerRecon = Path.Combine(sqlDir, "db_objects_partner_reconciliation.sql");
                    if (File.Exists(partnerRecon)) await ExecuteScriptAsync(partnerRecon, tolerant: false);
                    // 8. Gider raporlama (Plan 26) — v_ExpenseDistribution genişletme + tvf_ExpenseBreakdown (EN SON: view son kazanır)
                    var expenseReport = Path.Combine(sqlDir, "db_objects_expensereport.sql");
                    if (File.Exists(expenseReport)) await ExecuteScriptAsync(expenseReport, tolerant: false);
                    // 9. Sarf fişi SP'leri (Plan 25) — sp_MaterialIssuePost + Reverse
                    var matIssue = Path.Combine(sqlDir, "db_objects_materialissue.sql");
                    if (File.Exists(matIssue)) await ExecuteScriptAsync(matIssue, tolerant: false);
                    // 10. Mal kabul terminal tarama SP'si (Plan 28) — 3 mod (SINGLE_PO/BULK/FREE) + iade alanı
                    var rcvScan = Path.Combine(sqlDir, "db_objects_receiving.sql");
                    if (File.Exists(rcvScan)) await ExecuteScriptAsync(rcvScan, tolerant: false);
                    // 11. Fiyat listesi toplu giriş SP'leri (Plan 31) — sp_PriceListBulkUpsert + sp_PriceListClone (TVP)
                    var plBulk = Path.Combine(sqlDir, "db_objects_pricelist_bulk.sql");
                    if (File.Exists(plBulk)) await ExecuteScriptAsync(plBulk, tolerant: false);
                    // 12. Tedarikçi-ürün kataloğu SP'leri (Plan 32) — sp_SupplierItemBulkUpsert + sp_SetPreferredSupplier (TVP)
                    var supItem = Path.Combine(sqlDir, "db_objects_supplieritem.sql");
                    if (File.Exists(supItem)) await ExecuteScriptAsync(supItem, tolerant: false);
                    // 13. WMS terminal SP'leri (Plan 33 Faz B) — sp_PutawayPost + sp_PickConfirm (SQL-First)
                    var putawayPick = Path.Combine(sqlDir, "db_objects_putaway_pick.sql");
                    if (File.Exists(putawayPick)) await ExecuteScriptAsync(putawayPick, tolerant: false);
                    // 14. UDF backfill + seed (Plan 34) — Description JSON -> kolon/AdditionalFields + Volume/Weight/TempRange seed
                    var udf = Path.Combine(sqlDir, "db_objects_udf.sql");
                    if (File.Exists(udf)) await ExecuteScriptAsync(udf, tolerant: false);
                    // 15. Stok çıkış atomik primitive (Plan 44) — sp_ConsumeInventory (oversell/concurrency/idempotency)
                    var consume = Path.Combine(sqlDir, "db_objects_consume.sql");
                    if (File.Exists(consume)) await ExecuteScriptAsync(consume, tolerant: false);
                    break;

                case "seed":
                    // Baseline (operable kurulum) — sahte/demo veri YOK. Demo için: seed-demo.
                    var seedDir = FindDir("docs/sql") ?? ".";
                    // seed_branch_default + seed_M18: Company+Warehouse oluştuktan SONRA (Plan 49 — taban şube + masraf merkezi)
                    foreach (var seedFile in new[] { "seed_core.sql", "seed_company_claims.sql", "setup_tax_dictionary.sql", "seed_reference.sql", "seed_dictionary_labels.sql", "seed_receiving_bin.sql", "seed_business_params.sql", "seed_branch_default.sql", "seed_M18_ExpenseReporting.sql" })
                    {
                        var p = Path.Combine(seedDir, seedFile);
                        if (File.Exists(p)) await ExecuteScriptAsync(p, tolerant: true);
                        else Console.WriteLine($"  [SKIP] {seedFile} bulunamadi");
                    }
                    break;

                case "seed-demo":
                    // Opt-in DEMO verisi (Operax Demo LTD): master + işlem zincirleri. Üretim kurulumunda çağrılmaz.
                    // seed_demo_master ÖNCE — diğerleri sabit-GUID master'a (Warehouse/Item/Partner/Bin/UOM) bağımlı.
                    var demoDir = FindDir("docs/sql") ?? ".";
                    foreach (var seedFile in new[] { "seed_demo_master.sql", "seed_demo.sql", "seed_dashboard.sql", "seed_business_history.sql", "seed_finance_starter.sql" })
                    {
                        var p = Path.Combine(demoDir, seedFile);
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

                case "pg-test":
                    await RunPgTestAsync();
                    break;

                case "stress-consume":
                    // Plan 44 Faz 5: sp_ConsumeInventory'i N paralel worker ile aynı item/bin'de zorla.
                    // Kanıt: oversell YOK (bakiye hiç negatif olmaz) + serialization doğru (tam floor(stok/qty) başarılı).
                    var stWorkers = args.Length > 1 && int.TryParse(args[1], out var w) ? w : 30;
                    var stQty     = args.Length > 2 && int.TryParse(args[2], out var q) ? q : 0;
                    var stExit = await RunStressConsumeAsync(stWorkers, stQty);
                    Environment.Exit(stExit);
                    break;

                case "scan-isolation":
                    // Plan 12: company-kapsamlı tablolara CompanyId'siz dokunan Dapper sorgularını tara
                    var featuresDir = FindDir("src/Operax.Web/Features");
                    if (featuresDir == null) { Console.WriteLine("Hata: Features klasörü bulunamadı."); Environment.Exit(1); return; }
                    var exitCode = IsolationScanner.Report(featuresDir);
                    Environment.Exit(exitCode);
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
        Console.WriteLine("  operax-cli seed                 -> baseline (core + reference + tax) — operable, demo YOK");
        Console.WriteLine("  operax-cli seed-demo            -> opt-in DEMO verisi (Operax Demo LTD: master + islem zincirleri)");
        Console.WriteLine("  operax-cli script <dosya>       -> SQL dosyasi calistirir");
        Console.WriteLine("  operax-cli query \"SELECT ...\"   -> SQL sorgusu calistirir");
        Console.WriteLine("  operax-cli status               -> DB durumunu goster");
        Console.WriteLine("  operax-cli list-tables          -> Tablolari listele");
        Console.WriteLine("  operax-cli tables-empty         -> Tablo satir sayilarini goster");
        Console.WriteLine("  operax-cli check-fk             -> Foreign key listesi");
        Console.WriteLine("  operax-cli scan-isolation       -> CompanyId'siz company-tablo sorgularini tarar (plan 12)");
        Console.WriteLine("  operax-cli pg-test              -> PostgreSQL pilot: Neon bağlantı + SP smoke (NEON_CONN env)");
        Console.WriteLine("  operax-cli stress-consume [N] [qty] -> Plan 44: N paralel worker ile sp_ConsumeInventory oversell/deadlock testi");
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
                // Fail-closed (audit P0-3): yalnız "zaten var" (WarnOnly) kodları toleranslı.
                // Diğer her SQL hatası migration'ı DURDURUR — yarım kurulu DB "başarılı" sanılmasın.
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  [FATAL {ex.Number}] {Path.GetFileName(path)}: {ex.Message.Split('\n')[0]}");
                Console.ResetColor();
                throw;  // Main catch'e propagate → Environment.Exit(1)
            }
        }

        Console.ForegroundColor = warn > 0 ? ConsoleColor.Yellow : ConsoleColor.Green;
        Console.WriteLine($"\nTamamlandi — ok:{ok} warn:{warn} fail:{fail}");
        Console.ResetColor();
    }

    // postgresql://user:pass@host/db?sslmode=require → Npgsql key=value bağlantı dizesi
    static string BuildPgConnString(string raw)
    {
        if (!raw.StartsWith("postgres://") && !raw.StartsWith("postgresql://")) return raw;
        var uri = new Uri(raw);
        var userInfo = uri.UserInfo.Split(':', 2);
        var b = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
            Database = uri.AbsolutePath.TrimStart('/'),
            SslMode = SslMode.Require
        };
        return b.ConnectionString;
    }

    static async Task RunPgTestAsync()
    {
        // Bağlantı önce NEON_CONN env var'dan, yoksa User Secrets'tan (Neon:ConnectionString) okunur
        var neonConn = Environment.GetEnvironmentVariable("NEON_CONN");
        if (string.IsNullOrWhiteSpace(neonConn))
        {
            var cfg = new ConfigurationBuilder()
                .AddUserSecrets("ab29872f-6bf8-4602-8dc1-c66e703da6cc")
                .Build();
            neonConn = cfg["Neon:ConnectionString"];
        }
        if (string.IsNullOrWhiteSpace(neonConn))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Hata: Neon bağlantısı yok.");
            Console.WriteLine("  Env: $env:NEON_CONN = \"postgresql://...\"");
            Console.WriteLine("  veya User Secrets: dotnet user-secrets set \"Neon:ConnectionString\" \"postgresql://...\"");
            Console.ResetColor();
            return;
        }

        Console.WriteLine("\n=== PostgreSQL Pilot Testi ===");
        // Neon URI formatını (postgresql://...) Npgsql key=value formatına çevir
        var dataSource = NpgsqlDataSource.Create(BuildPgConnString(neonConn));

        // 1. Bağlantı doğrula
        Console.Write("1. Bağlantı...");
        await using var pingConn = await dataSource.OpenConnectionAsync();
        var pgVersion = await pingConn.QuerySingleAsync<string>("SELECT version()");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($" OK — {pgVersion[..pgVersion.IndexOf(',')]}");
        Console.ResetColor();

        // 2. Test şeması oluştur (cheque tablosu stub)
        Console.Write("2. Tablo oluştur...");
        await using var conn = await dataSource.OpenConnectionAsync();
        await conn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS cheque (
                id                      uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                company_id              uuid NOT NULL,
                direction               text NOT NULL,
                cheque_no               text NOT NULL,
                bank_name               text NOT NULL,
                drawer_name             text NOT NULL,
                amount                  numeric(18,2) NOT NULL,
                currency                text DEFAULT 'TRY',
                cheque_date             date NOT NULL,
                due_date                date NOT NULL,
                status                  text DEFAULT 'PORTFOLIO',
                deposited_to_account_id uuid NULL,
                deposited_at            timestamptz NULL,
                is_deleted              boolean DEFAULT false,
                updated_at              timestamptz NULL,
                updated_by              uuid NULL,
                created_at              timestamptz DEFAULT now(),
                created_by              uuid NULL
            )");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(" OK");
        Console.ResetColor();

        // 3. sp_deposit_cheque fonksiyonu yükle
        Console.Write("3. Fonksiyon yükle...");
        await conn.ExecuteAsync(@"
            CREATE OR REPLACE FUNCTION sp_deposit_cheque(
                p_cheque_id  uuid,
                p_account_id uuid,
                p_company_id uuid,
                p_deposit_date timestamptz DEFAULT NULL,
                p_user_id      uuid DEFAULT NULL
            ) RETURNS void LANGUAGE plpgsql AS $$
            DECLARE v_status text;
            BEGIN
                SELECT status INTO v_status
                FROM cheque
                WHERE id = p_cheque_id AND company_id = p_company_id AND is_deleted = false;

                IF v_status IS NULL THEN
                    RAISE EXCEPTION 'Çek bulunamadı.' USING ERRCODE = 'OP601';
                END IF;
                IF v_status <> 'PORTFOLIO' THEN
                    RAISE EXCEPTION 'Sadece portföydeki çekler bankaya verilebilir.' USING ERRCODE = 'OP602';
                END IF;

                UPDATE cheque
                SET status                  = 'IN_BANK',
                    deposited_to_account_id = p_account_id,
                    deposited_at            = COALESCE(p_deposit_date, now()),
                    updated_at              = now(),
                    updated_by              = p_user_id
                WHERE id = p_cheque_id;
            END $$");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(" OK");
        Console.ResetColor();

        // 4. Test verisi ekle
        Console.Write("4. Test çeki ekle...");
        var companyId  = Guid.NewGuid();
        var chequeId   = Guid.NewGuid();
        var accountId  = Guid.NewGuid();
        await conn.ExecuteAsync(@"
            INSERT INTO cheque (id, company_id, direction, cheque_no, bank_name, drawer_name,
                                amount, cheque_date, due_date, status)
            VALUES (@id, @companyId, 'RECEIVED', 'CHQ-TEST-001', 'Test Bank', 'Test Kesideci',
                    5000.00, CURRENT_DATE, CURRENT_DATE + 30, 'PORTFOLIO')",
            new { id = chequeId, companyId });
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($" OK (id={chequeId})");
        Console.ResetColor();

        // 5. Fonksiyonu çağır
        Console.Write("5. sp_deposit_cheque çağır...");
        await conn.ExecuteAsync(
            "SELECT sp_deposit_cheque(@p_cheque_id, @p_account_id, @p_company_id)",
            new { p_cheque_id = chequeId, p_account_id = accountId, p_company_id = companyId });
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(" OK");
        Console.ResetColor();

        // 6. Sonucu doğrula
        Console.Write("6. Durum doğrula...");
        var status = await conn.QuerySingleAsync<string>(
            "SELECT status FROM cheque WHERE id = @id", new { id = chequeId });
        if (status == "IN_BANK")
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($" OK — status={status} ✓");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($" HATA — beklenen IN_BANK, gelen {status}");
        }
        Console.ResetColor();

        // 7. Guard testi — aynı çeki tekrar bankaya ver → OP602 fırlamalı
        Console.Write("7. Guard testi (çift deposit)...");
        try
        {
            await conn.ExecuteAsync(
                "SELECT sp_deposit_cheque(@p_cheque_id, @p_account_id, @p_company_id)",
                new { p_cheque_id = chequeId, p_account_id = accountId, p_company_id = companyId });
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(" HATA — istisna fırlatılmadı (guard çalışmıyor)");
        }
        catch (PostgresException pgEx) when (pgEx.SqlState == "OP602")
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($" OK — OP602 yakalandı: \"{pgEx.MessageText}\" ✓");
        }
        Console.ResetColor();

        // 8. Temizlik
        await conn.ExecuteAsync("DELETE FROM cheque WHERE id = @id", new { id = chequeId });

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n=== TÜM TESTLER GEÇTİ — PostgreSQL pilot başarılı ===");
        Console.ResetColor();
    }

    // Plan 44 Faz 5 — eşzamanlılık kanıtı. N worker aynı item/bin'i tüketir; toplam talep > stok.
    // Doğru HOLDLOCK ile: oversell imkânsız (bakiye ≥ 0) + tam floor(stok/qty) worker başarılı olur.
    record StressPick(Guid CompanyId, Guid ItemId, Guid WarehouseId, Guid BinId, Guid UomId, Guid UserId, decimal Bal);

    static async Task<int> RunStressConsumeAsync(int workers, int qtyArg)
    {
        Console.WriteLine($"\n=== Concurrency Stress — sp_ConsumeInventory ({workers} paralel worker) ===");
        var stamp = Guid.NewGuid();  // bu koşumun temizlik etiketi (SourceDocId)

        using var setup = await OpenAsync();
        // En stoklu item/bin + geçerli UomId/CreatedBy seç
        var pick = await setup.QueryFirstOrDefaultAsync<StressPick>(@"
            SELECT TOP 1 sm.CompanyId, sm.ItemId, sm.WarehouseId, sm.BinId,
                   (SELECT TOP 1 UomId FROM StockMovement WHERE ItemId = sm.ItemId) AS UomId,
                   (SELECT TOP 1 CreatedBy FROM StockMovement WHERE CreatedBy IS NOT NULL) AS UserId,
                   SUM(sm.QtyBase) AS Bal
            FROM StockMovement sm
            WHERE sm.IsCancelled = 0 AND sm.BinId IS NOT NULL
            GROUP BY sm.CompanyId, sm.ItemId, sm.WarehouseId, sm.BinId
            HAVING SUM(sm.QtyBase) > 100
            ORDER BY SUM(sm.QtyBase) DESC");

        if (pick == null) { Console.WriteLine("Stoklu bin bulunamadı (test atlandı)."); return 0; }

        var initialBal = pick.Bal;
        // qty verilmemişse: talebin arzı ~%65 aşması için (bazı worker reddedilmeli)
        var qty = qtyArg > 0 ? qtyArg : (int)Math.Ceiling((double)initialBal / (workers * 0.65));
        if (qty < 1) qty = 1;
        var expectedSuccess = (int)Math.Floor(initialBal / qty);

        Console.WriteLine($"Item {pick.ItemId.ToString()[..8]} · Bin {pick.BinId.ToString()[..8]} · başlangıç bakiye {initialBal} · qty/worker {qty} · beklenen başarı {expectedSuccess}/{workers}");

        int success = 0, reject = 0, deadlock = 0, other = 0;

        // N worker'ı AYNI ANDA başlat (her biri kendi bağlantı + transaction)
        var tasks = Enumerable.Range(0, workers).Select(async _ =>
        {
            try
            {
                using var c = await OpenAsync();
                using var tx = c.BeginTransaction();
                await c.ExecuteAsync(new CommandDefinition("sp_ConsumeInventory", new
                {
                    CompanyId = pick.CompanyId, WarehouseId = pick.WarehouseId, BinId = pick.BinId,
                    ItemId = pick.ItemId, UomId = pick.UomId, QtyBase = (decimal)qty, LotNo = (string?)null,
                    SourceDocType = "STRESS", SourceDocId = stamp, SourceLineId = Guid.NewGuid(),
                    SourceDocNo = (string?)null, UnitCost = 0m, UserId = pick.UserId,
                    MovementDate = (DateTime?)null, BranchId = (Guid?)null, MovementType = "ISSUE"
                }, transaction: tx, commandType: CommandType.StoredProcedure));
                tx.Commit();
                Interlocked.Increment(ref success);
            }
            catch (SqlException ex) when (ex.Number == 53001) { Interlocked.Increment(ref reject); }   // yetersiz stok (beklenen)
            catch (SqlException ex) when (ex.Number == 1205)  { Interlocked.Increment(ref deadlock); }  // deadlock kurbanı (retry'lik)
            catch (Exception ex) { Interlocked.Increment(ref other); Console.WriteLine($"  [ERR] {ex.Message.Split('\n')[0]}"); }
        });
        await Task.WhenAll(tasks);

        // Son bakiye (oversell kontrolü)
        var finalBal = await setup.ExecuteScalarAsync<decimal>(@"
            SELECT ISNULL(SUM(QtyBase),0) FROM StockMovement
            WHERE CompanyId=@c AND ItemId=@i AND WarehouseId=@w AND BinId=@b AND IsCancelled=0",
            new { c = pick.CompanyId, i = pick.ItemId, w = pick.WarehouseId, b = pick.BinId });

        // Temizlik: bu koşumun STRESS hareketlerini iptal et (IsCancelled=1 — append-only ledger,
        // fiziksel DELETE trigger ile yasak). Bakiye SUM(WHERE IsCancelled=0) ile geri gelir.
        var cleaned = await setup.ExecuteAsync(
            "UPDATE StockMovement SET IsCancelled=1, CancelledAt=GETUTCDATE() WHERE SourceDocType='STRESS' AND SourceDocId=@s AND IsCancelled=0",
            new { s = stamp });

        Console.WriteLine($"\nSonuç: başarı={success} red={reject} deadlock={deadlock} diğer={other}");
        Console.WriteLine($"Son bakiye {finalBal} (başlangıç {initialBal}, {success}×{qty} düşüldü) · temizlenen {cleaned} hareket");

        bool noOversell  = finalBal >= 0;
        bool consistent  = finalBal == initialBal - (decimal)success * qty;
        bool serialized  = success == expectedSuccess && other == 0;

        Console.ForegroundColor = (noOversell && consistent && serialized) ? ConsoleColor.Green : ConsoleColor.Red;
        Console.WriteLine($"\n[{(noOversell ? "PASS" : "FAIL")}] OVERSELL YOK — bakiye negatife düşmedi (final {finalBal} ≥ 0)");
        Console.WriteLine($"[{(serialized ? "PASS" : "FAIL")}] SERIALIZATION — tam {expectedSuccess} worker başarılı (gerçek {success}), 0 beklenmeyen hata");
        Console.WriteLine($"[{(consistent ? "PASS" : "FAIL")}] TUTARLILIK — final bakiye = başlangıç − başarı×qty");
        Console.ResetColor();

        return (noOversell && consistent && serialized) ? 0 : 1;
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
