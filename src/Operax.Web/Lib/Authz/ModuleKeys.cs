namespace Operax.Web.Lib.Authz;

/// <summary>
/// RoleModuleAccess yetki birimleri = üst-seviye Feature klasörleri.
/// Bu liste hem Program.cs policy üretimi hem de AuthorizeFolder eşlemesi için tek kaynaktır.
/// Not: 'Admin' (sadece Administrator) ve 'Auth' (anonim) bu listeye dahil DEĞİLDİR.
/// </summary>
public static class ModuleKeys
{
    public static readonly string[] All =
    [
        "Budget", "CycleCount", "Dashboard", "Expenses", "Finance", "Inventory",
        "LPN", "Lot", "Manufacturing", "MasterData", "Picking", "Production",
        "PurchaseOrders", "Receiving", "SalesInvoices", "SalesOrders", "Serial",
        "Shipping", "Transfer", "Warehouses"
    ];

    // Erişim seviyeleri
    public const byte AccessView = 1; // sadece GET (görüntüleme)
    public const byte AccessEdit = 2; // GET + POST (düzenleme)
}
