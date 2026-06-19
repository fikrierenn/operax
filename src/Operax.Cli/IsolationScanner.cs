using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Operax.Cli;

/// <summary>
/// Plan 12 — Multi-Company Veri İzolasyon Guard'ı.
/// Features/ altındaki Dapper SQL'lerini tarar; company-kapsamlı bir tabloya
/// dokunup `CompanyId` predikatı taşımayan ham sorguları (TVF/SP hariç) raporlar.
/// Statik analiz emniyet ağıdır (Desen 3) — DB'deki TVF/SP izolasyonunun üstüne çalışır.
/// </summary>
public static class IsolationScanner
{
    // Doğrudan CompanyId kolonu taşıyan company-kapsamlı tablolar (envanter: plan 12, 2026-05-31).
    private static readonly HashSet<string> DirectScoped = new(StringComparer.OrdinalIgnoreCase)
    {
        "DictionaryType", "DictionaryValue", "Parameter", "CompanyModule", "AuditLog",
        "StatusTransition", "Category", "Item", "Warehouse", "Partner", "PriceList",
        "StockMovement", "PurchaseOrderHeader", "ReceivingHeader", "SalesOrderHeader",
        "ShippingHeader", "PickTask", "StockTransfer", "ItemBinConfig", "CycleCount",
        "LPN", "ItemLot", "ItemSerial", "ProductionOrder", "WorkCenter", "ProductRoute",
        "ProductModel", "ProductionConsumption", "DefectCode", "ProductionInspection",
        "ProductionRework", "CostCenter", "ExpenseType", "ExpenseInvoice", "Budget",
        "CashFlowForecast", "FinancialAccount", "FinancialTransaction", "Cheque",
        "PromissoryNote", "Loan", "CreditCard", "PaymentPlan", "AccountMovement",
        "NumberSeries", "UserCompany", "ItemCost", "PriceVariance", "SalesInvoice",
        "EBelgeProvider", "InvoiceEnvelope",
    };

    // Dolaylı kapsamlı tablolar (CompanyId yok, parent FK üzerinden izole olmalı).
    // Güvenli sayılması için sorguda parent JOIN + CompanyId predikatı beklenir.
    private static readonly HashSet<string> IndirectScoped = new(StringComparer.OrdinalIgnoreCase)
    {
        "Bin", "ItemUOM", "ItemBarcode", "ItemBOM", "PriceListLine", "PurchaseOrderLine",
        "ReceivingLine", "SalesOrderLine", "ShippingLine", "PickTaskLine", "StockTransferLine",
        "CycleCountLine", "ProductionOrderLine", "ProductionActivity", "ProductRouteStep",
        "ProductModelParameter", "ProductModelBOM", "ProductionOrderConfig",
        "ProductionReworkMaterial", "ExpenseInvoiceLine", "BudgetLine", "LoanPayment",
        "CreditCardStatement", "CreditCardTransaction", "SalesInvoiceLine",
        "InvoiceSubmissionLog", "EBelgeQueue",
    };

    // Bilinçli olarak kapsam dışı (global/sistem) tablolar — raporlanmaz.
    // Company, Module, City, AspNet*, RoleModuleAccess, EBelgeMukellef.

    private const string SuppressMarker = "isolation-guard:ignore";

    // SQL DML anahtar kelimeleri — string literal'in SQL olduğunu anlamak için.
    private static readonly Regex DmlKeyword = new(
        @"\b(SELECT|INSERT\s+INTO|UPDATE|DELETE)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public sealed record Violation(string File, int Line, string Table, string Kind, string Snippet);

    /// <summary>
    /// Taramayı çalıştırır; ihlal listesi döner. featuresDir verilmezse otomatik bulunur.
    /// </summary>
    public static List<Violation> Scan(string featuresDir)
    {
        var violations = new List<Violation>();
        var files = Directory.EnumerateFiles(featuresDir, "*.cs", SearchOption.AllDirectories).ToList();
        bool dbg = Environment.GetEnvironmentVariable("ISO_DEBUG") == "1";
        int litCount = 0;

        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            foreach (var (sql, charIndex) in ExtractSqlLiterals(text))
            {
                litCount++;
                // İş kuralı: yalnızca DML içeren string'ler SQL sayılır (SP adı/yorum değil).
                if (!DmlKeyword.IsMatch(sql)) continue;
                // Guard bastırma yorumu varsa atla.
                if (sql.Contains(SuppressMarker, StringComparison.OrdinalIgnoreCase)) continue;
                // TVF kullanımı zaten @CompanyId sargılıdır — CompanyId token'ı yakalar, ek kontrol gerekmez.

                var hasCompanyId = sql.Contains("CompanyId", StringComparison.OrdinalIgnoreCase);
                if (hasCompanyId) continue;

                var hit = FindScopedTable(sql);
                if (hit == null) continue;

                int line = text.Take(charIndex).Count(c => c == '\n') + 1;
                var snippet = Snip(sql);
                violations.Add(new Violation(file, line, hit.Value.table, hit.Value.kind, snippet));
            }
        }

        if (dbg) Console.WriteLine($"[DBG] dosya:{files.Count} sql-literal:{litCount} ihlal:{violations.Count}");
        return violations.OrderBy(v => v.File).ThenBy(v => v.Line).ToList();
    }

    // SQL'de geçen ilk company-kapsamlı tabloyu (FROM/JOIN/INTO/UPDATE/DELETE sonrası) bulur.
    private static (string table, string kind)? FindScopedTable(string sql)
    {
        var m = Regex.Matches(sql,
            @"\b(?:FROM|JOIN|INTO|UPDATE)\s+(?:\[?dbo\]?\.)?\[?(?<t>[A-Za-z_][A-Za-z0-9_]*)\]?",
            RegexOptions.IgnoreCase);
        foreach (Match match in m)
        {
            var t = match.Groups["t"].Value;
            if (DirectScoped.Contains(t)) return (t, "DIRECT");
            if (IndirectScoped.Contains(t)) return (t, "INDIRECT");
        }
        // DELETE FROM zaten yukarıdaki FROM ile yakalanır.
        return null;
    }

    // C# kaynak metninden SQL string literal'lerini çıkarır (verbatim @"..." ve düz "...").
    private static IEnumerable<(string sql, int index)> ExtractSqlLiterals(string text)
    {
        // Verbatim string: @"....." — içinde "" kaçışı olabilir.
        foreach (Match m in Regex.Matches(text, "@\"(?<s>(?:[^\"]|\"\")*)\"", RegexOptions.Singleline))
            yield return (m.Groups["s"].Value.Replace("\"\"", "\""), m.Index);

        // Düz tek satır string: "....." — kaçışlı tırnak içermeyen basit hal.
        foreach (Match m in Regex.Matches(text, "(?<!@)\"(?<s>(?:\\\\.|[^\"\\\\])*)\""))
            yield return (m.Groups["s"].Value, m.Index);
    }

    private static string Snip(string sql)
    {
        var flat = Regex.Replace(sql, @"\s+", " ").Trim();
        return flat.Length <= 90 ? flat : flat[..90] + "…";
    }

    /// <summary>Konsola rapor basar; ihlal varsa 1, yoksa 0 exit kodu önerir.</summary>
    public static int Report(string featuresDir)
    {
        var violations = Scan(featuresDir);
        if (violations.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n[OK] İzolasyon ihlali bulunamadı — tüm company-kapsamlı sorgular CompanyId taşıyor.");
            Console.ResetColor();
            return 0;
        }

        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"\n[FAIL] {violations.Count} izolasyon ihlali (CompanyId predikatı eksik):\n");
        Console.ResetColor();

        foreach (var v in violations)
        {
            var rel = v.File.Replace(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar, "");
            Console.ForegroundColor = v.Kind == "DIRECT" ? ConsoleColor.Red : ConsoleColor.Yellow;
            Console.Write($"  [{v.Kind,-8}] ");
            Console.ResetColor();
            Console.WriteLine($"{rel}:{v.Line}  →  {v.Table}");
            Console.WriteLine($"             {v.Snippet}");
        }

        Console.WriteLine($"\nBastırmak için sorgu içine yorum ekle: /* {SuppressMarker}: <gerekçe> */");
        return 1;
    }
}
