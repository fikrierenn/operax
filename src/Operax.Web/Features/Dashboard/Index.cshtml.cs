using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dapper;
using Operax.Web.Lib;

namespace Operax.Web.Features.Dashboard;

public class IndexModel(Db db, ICurrentCompany company) : PageModel
{
    public int PendingShipments { get; set; }
    public int ActiveReceivings { get; set; }
    public int LowStockItems { get; set; }
    public int ActiveProductionOrders { get; set; }
    public List<ActivityDto> RecentActivities { get; set; } = [];

    public async Task OnGetAsync()
    {
        using var conn = db.Open();

        PendingShipments = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM ShippingHeader WHERE CompanyId = @CompanyId AND Status IN ('NEW', 'PENDING', 'APPROVED')", 
            new { CompanyId = company.Id });

        ActiveReceivings = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM PurchaseOrderHeader WHERE CompanyId = @CompanyId AND Status IN ('NEW', 'PENDING', 'APPROVED')", 
            new { CompanyId = company.Id });

        ActiveProductionOrders = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM ProductionOrder WHERE CompanyId = @CompanyId AND Status IN ('NEW', 'IN_PROGRESS')", 
            new { CompanyId = company.Id });

        // Dummy Low Stock check (Balance < 10 for any item with movement)
        LowStockItems = 0; // Simplified for now

        // Get recent stock movements as activities
        const string activitySql = @"
            SELECT TOP 5 m.CreatedAt, i.Name as ItemName, m.MovementType, m.QtyBase as Qty
            FROM StockMovement m
            JOIN Item i ON i.Id = m.ItemId
            WHERE i.CompanyId = @CompanyId
            ORDER BY m.CreatedAt DESC";
        
        RecentActivities = (await conn.QueryAsync<ActivityDto>(activitySql, new { CompanyId = company.Id })).ToList();
    }

    public record ActivityDto(DateTime CreatedAt, string ItemName, string MovementType, decimal Qty);
}
