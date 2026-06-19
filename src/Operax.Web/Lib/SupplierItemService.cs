using System.Data;
using Dapper;

namespace Operax.Web.Lib;

/// <summary>
/// Tedarikçi-ürün kataloğu işlemleri (Plan 32) — ürüne göre tedarikçi listele,
/// Item kartı grid'inden toplu upsert, tercih edilen tedarikçi ata. SQL-first:
/// sp_SupplierItemBulkUpsert (TVP) / sp_SetPreferredSupplier üzerinden tek transaction.
/// LastPrice yalnız bilgilendirici — fiyat çözümü PriceList tek-kaynağında kalır (Plan 30).
/// </summary>
public sealed class SupplierItemService(Db db)
{
    /// <summary>Bir ürünün tedarikçi satırı (Item kartı grid + bulk girdi).</summary>
    public sealed record SupplierRow(
        int RowNo, Guid PartnerId, string? SupplierItemCode, string? SupplierItemName,
        int? LeadTimeDays, decimal MinOrderQty, decimal? LastPrice, string Currency, bool IsPreferred);

    /// <summary>Grid görüntüleme satırı (tedarikçi adı/kodu dahil).</summary>
    public sealed record SupplierView(
        Guid PartnerId, string PartnerName, string? PartnerCode, string? SupplierItemCode,
        string? SupplierItemName, int? LeadTimeDays, decimal MinOrderQty, decimal? LastPrice,
        string Currency, bool IsPreferred);

    // Ürünün tedarikçilerini okur (grid yükleme).
    public async Task<IEnumerable<SupplierView>> ListByItemAsync(Guid companyId, Guid itemId,
        CancellationToken ct = default)
    {
        using var conn = db.Open();
        return await conn.QueryAsync<SupplierView>(new CommandDefinition(
            @"SELECT si.PartnerId, p.Name AS PartnerName, p.Code AS PartnerCode,
                     si.SupplierItemCode, si.SupplierItemName, si.LeadTimeDays,
                     si.MinOrderQty, si.LastPrice, si.Currency, si.IsPreferred
              FROM SupplierItem si
              JOIN Partner p ON p.Id = si.PartnerId
              WHERE si.CompanyId = @CompanyId AND si.ItemId = @ItemId AND si.IsDeleted = 0
              ORDER BY si.IsPreferred DESC, p.Name",
            new { CompanyId = companyId, ItemId = itemId }, cancellationToken: ct));
    }

    // Item grid "Kaydet": görünen satırlar TAM durum (SyncDelete=1 → eksikler soft-delete).
    public async Task<int> BulkSaveAsync(Guid companyId, Guid itemId, Guid userId,
        IEnumerable<SupplierRow> rows, CancellationToken ct = default)
    {
        var tvp = BuildTvp(rows);
        using var conn = db.Open();
        var p = new DynamicParameters();
        p.Add("@CompanyId", companyId);
        p.Add("@ItemId", itemId);
        p.Add("@UserId", userId);
        p.Add("@Lines", tvp.AsTableValuedParameter("dbo.SupplierItemTVP"));
        p.Add("@SyncDelete", true);
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "sp_SupplierItemBulkUpsert", p, commandType: CommandType.StoredProcedure, cancellationToken: ct));
    }

    // Ürün için tercih edilen tedarikçiyi tekil ayarlar (exclusive).
    public async Task SetPreferredAsync(Guid companyId, Guid itemId, Guid partnerId, Guid userId,
        CancellationToken ct = default)
    {
        using var conn = db.Open();
        await conn.ExecuteAsync(new CommandDefinition(
            "sp_SetPreferredSupplier",
            new { CompanyId = companyId, ItemId = itemId, PartnerId = partnerId, UserId = userId },
            commandType: CommandType.StoredProcedure, cancellationToken: ct));
    }

    // Satır demetini dbo.SupplierItemTVP yapısına (kolon sırası birebir) çevirir.
    private static DataTable BuildTvp(IEnumerable<SupplierRow> rows)
    {
        var tvp = new DataTable();
        tvp.Columns.Add("RowNo", typeof(int));
        tvp.Columns.Add("PartnerId", typeof(Guid));
        tvp.Columns.Add("SupplierItemCode", typeof(string));
        tvp.Columns.Add("SupplierItemName", typeof(string));
        tvp.Columns.Add("LeadTimeDays", typeof(int));
        tvp.Columns.Add("MinOrderQty", typeof(decimal));
        tvp.Columns.Add("LastPrice", typeof(decimal));
        tvp.Columns.Add("Currency", typeof(string));
        tvp.Columns.Add("IsPreferred", typeof(bool));
        foreach (var r in rows)
        {
            tvp.Rows.Add(
                r.RowNo,
                r.PartnerId,
                (object?)r.SupplierItemCode ?? DBNull.Value,
                (object?)r.SupplierItemName ?? DBNull.Value,
                (object?)r.LeadTimeDays ?? DBNull.Value,
                r.MinOrderQty,
                (object?)r.LastPrice ?? DBNull.Value,
                string.IsNullOrWhiteSpace(r.Currency) ? "TRY" : r.Currency,
                r.IsPreferred);
        }
        return tvp;
    }
}
