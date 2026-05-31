namespace Operax.Web.Lib;

/// <summary>
/// Sistemdeki rol sabitleri. Magic string yasağı gereği rol adları
/// her zaman bu sabitler üzerinden referans edilir (Authorize, Seed, policy).
/// </summary>
public static class Roles
{
    // Tam yetki — tüm modüller + Admin paneli + Hangfire dashboard
    public const string Administrator = "Administrator";

    // Depo operasyonları — Receiving, Shipping, Transfer, Inventory, CycleCount, LPN, Lot, Serial, Picking, Warehouses
    public const string WarehouseManager = "WarehouseManager";

    // Finans — Payments, Loans, CreditCards, Cheques, Accounts, Aging, PaymentPlan, Snapshot
    public const string Finance = "Finance";

    // Satınalma — PurchaseOrders, Expenses, Budget
    public const string Purchasing = "Purchasing";

    // Satış — SalesOrders, SalesInvoices
    public const string Sales = "Sales";

    // Üretim — Manufacturing (BOM, WorkOrders, WorkCenters), Production
    public const string Manufacturing = "Manufacturing";

    // Salt-okuma izleyici — Dashboard + MasterData referans verileri
    public const string Viewer = "Viewer";

    /// <summary>Seed ve doğrulama için tüm rollerin listesi.</summary>
    public static readonly string[] All =
    [
        Administrator, WarehouseManager, Finance, Purchasing, Sales, Manufacturing, Viewer
    ];
}
