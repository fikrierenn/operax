using System.Data;
using Dapper;

namespace Operax.Web.Lib;

/// <summary>
/// Belge seri yönetimi — tüm otomatik numaralama (cari kod, fatura/sipariş/çek no)
/// tek yerden, ayarlardan (NumberSeries tablosu) okunur. Atomik (sp_NextNumber).
/// </summary>
public interface INumberSeriesService
{
    /// <summary>Verilen belge tipi için sıradaki biçimlenmiş kodu üretir (örn. CUS-001).</summary>
    Task<string> NextAsync(Guid companyId, string docType, CancellationToken ct = default);
}

/// <summary>NumberSeries tablosundan atomik sıradaki numarayı çeker (sp_NextNumber).</summary>
public sealed class NumberSeriesService(Db db) : INumberSeriesService
{
    public async Task<string> NextAsync(Guid companyId, string docType, CancellationToken ct = default)
    {
        using var conn = db.Open();
        var p = new DynamicParameters();
        p.Add("@CompanyId", companyId);
        p.Add("@DocType", docType);
        p.Add("@Code", dbType: DbType.String, size: 40, direction: ParameterDirection.Output);
        // Atomik artış + biçimlenmiş kod SP içinde üretilir
        await conn.ExecuteAsync(new CommandDefinition(
            "sp_NextNumber", p, commandType: CommandType.StoredProcedure, cancellationToken: ct));
        return p.Get<string>("@Code");
    }
}

/// <summary>Belge seri tipleri (NumberSeries.DocType) — magic string yasağı.</summary>
public static class NumberSeriesType
{
    public const string PartnerCustomer = "PARTNER_CUST";
    public const string PartnerVendor   = "PARTNER_VEND";
    public const string PartnerBoth     = "PARTNER_BOTH";
    public const string SalesInvoice    = "SALES_INVOICE";
    public const string PurchaseInvoice = "PURCHASE_INVOICE";
    public const string SalesOrder      = "SALES_ORDER";
    public const string PurchaseOrder   = "PURCHASE_ORDER";
    public const string Cheque          = "CHEQUE";
    public const string PromissoryNote  = "PROMISSORY_NOTE";
}
