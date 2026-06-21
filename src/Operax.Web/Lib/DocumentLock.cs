using System.Data;
using Dapper;

namespace Operax.Web.Lib;

/// <summary>
/// Evrak bütünlüğü (document-immutability §3/§5) — bir belge başka bir belgeden beslendiyse
/// (alt/child kayıt varsa) kaynak belge düzenlenemez. Bu yardımcı, mutasyon handler'larında
/// (edit/satır ekle) çağrılır; child varsa düzenleme reddedilir. SP-level guard + UI readonly ile
/// birlikte üç katmanlı savunma (defense in depth).
/// </summary>
public static class DocumentLock
{
    // PurchaseOrder → ReceivingHeader (bu siparişe mal kabul yapılmış mı)
    public static async Task<bool> PoHasReceivingAsync(IDbConnection conn, Guid poId, Guid companyId, CancellationToken ct = default)
        => await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM ReceivingHeader WHERE PurchaseOrderId = @id AND CompanyId = @cid AND IsDeleted = 0",
            new { id = poId, cid = companyId },
            cancellationToken: ct)) > 0;

    // ReceivingHeader → PurchaseInvoice (bu mal kabule alış faturası kesilmiş mi; iptaller hariç).
    // Canlı bağ: PurchaseInvoiceLine.SourceReceivingLineId → ReceivingLine.HeaderId (ExpenseInvoice.ReceivingId
    // ölü — hiç beslenmiyor; koddan doğrulandı 2026-06-19). PurchaseInvoice'ta IsDeleted kolonu YOK.
    public static async Task<bool> ReceivingHasInvoiceAsync(IDbConnection conn, Guid receivingId, Guid companyId, CancellationToken ct = default)
        => await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            @"SELECT COUNT(1)
              FROM PurchaseInvoice pi
              JOIN PurchaseInvoiceLine pil ON pil.InvoiceId = pi.Id
              JOIN ReceivingLine rl ON rl.Id = pil.SourceReceivingLineId
              WHERE rl.HeaderId = @id AND pi.CompanyId = @cid AND pi.Status <> 'CANCELLED'",
            new { id = receivingId, cid = companyId },
            cancellationToken: ct)) > 0;

    // SalesOrder → ShippingHeader (bu siparişe sevkiyat yapılmış mı)
    public static async Task<bool> SoHasShippingAsync(IDbConnection conn, Guid soId, Guid companyId, CancellationToken ct = default)
        => await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM ShippingHeader WHERE SalesOrderId = @id AND CompanyId = @cid AND IsDeleted = 0",
            new { id = soId, cid = companyId },
            cancellationToken: ct)) > 0;

    // ShippingHeader → SalesInvoice (bu sevkiyata satış faturası kesilmiş mi; iptaller hariç)
    public static async Task<bool> ShippingHasInvoiceAsync(IDbConnection conn, Guid shippingId, Guid companyId, CancellationToken ct = default)
        => await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM SalesInvoice WHERE ShippingId = @id AND CompanyId = @cid AND IsDeleted = 0 AND Status <> 'CANCELLED'",
            new { id = shippingId, cid = companyId },
            cancellationToken: ct)) > 0;
}
