using Dapper;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Operax.Tests.Integration;

/// <summary>
/// Mal kabul onayı (sp_ReceivingPost) ledger doğruluğu: posting stok bakiyesini artırır,
/// çift-post status guard ile reddedilir. Plan 37 Faz 3b — gerçek SP + tvf_InventoryBalance.
/// </summary>
[Collection("Database")]
public sealed class ReceivingPostingTests(DatabaseFixture fixture)
{
    // SYSTEM şirketi baseline'da StatusTransition (RECEIVING DRAFT→POSTED) + UOM taşır
    private static readonly Guid SystemCompany = new("00000000-0000-0000-0000-000000000000");

    private SqlConnection Open()
    {
        var conn = new SqlConnection(fixture.ConnectionString);
        conn.Open();
        return conn;
    }

    [Fact]
    public void ReceivingPost_Increases_Inventory_Balance()
    {
        using var conn = Open();
        var s = SetupDraftReceiving(conn, qty: 100m);

        conn.Execute("dbo.sp_ReceivingPost",
            new { HeaderId = s.HeaderId, CompanyId = SystemCompany, UserId = s.UserId },
            commandType: System.Data.CommandType.StoredProcedure);

        // İş kuralı: onaylanan mal kabul, kalemin fiziksel stok bakiyesini artırmalı
        var balance = conn.ExecuteScalar<decimal?>(
            @"SELECT SUM(QtyBalance) FROM dbo.tvf_InventoryBalance(@CompanyId) WHERE ItemId = @ItemId",
            new { CompanyId = SystemCompany, s.ItemId });

        Assert.Equal(100m, balance);
    }

    [Fact]
    public void ReceivingPost_Twice_Is_Rejected()
    {
        using var conn = Open();
        var s = SetupDraftReceiving(conn, qty: 50m);

        conn.Execute("dbo.sp_ReceivingPost",
            new { HeaderId = s.HeaderId, CompanyId = SystemCompany, UserId = s.UserId },
            commandType: System.Data.CommandType.StoredProcedure);

        // İş kuralı: zaten POSTED belge ikinci kez onaylanamaz (status guard → THROW)
        var ex = Assert.Throws<SqlException>(() =>
            conn.Execute("dbo.sp_ReceivingPost",
                new { HeaderId = s.HeaderId, CompanyId = SystemCompany, UserId = s.UserId },
                commandType: System.Data.CommandType.StoredProcedure));

        Assert.True(ex.Number is >= 50000 and < 60000, $"İş-hatası bandı (50000-59999) beklenir, gelen {ex.Number}");

        // Çift-post reddedildiği için bakiye tek post kadar (50) kalmalı
        var balance = conn.ExecuteScalar<decimal?>(
            @"SELECT SUM(QtyBalance) FROM dbo.tvf_InventoryBalance(@CompanyId) WHERE ItemId = @ItemId",
            new { CompanyId = SystemCompany, s.ItemId });
        Assert.Equal(50m, balance);
    }

    [Fact]
    public void ReceivingReverse_Nets_Balance_To_Zero()
    {
        using var conn = Open();
        var s = SetupDraftReceiving(conn, qty: 75m);

        conn.Execute("dbo.sp_ReceivingPost",
            new { HeaderId = s.HeaderId, CompanyId = SystemCompany, UserId = s.UserId },
            commandType: System.Data.CommandType.StoredProcedure);

        conn.Execute("dbo.sp_ReceivingReverse",
            new { HeaderId = s.HeaderId, CompanyId = SystemCompany, UserId = s.UserId },
            commandType: System.Data.CommandType.StoredProcedure);

        // İş kuralı: iptal sonrası net bakiye 0 (IsCancelled=1 flag, ters satır yok → çift-sayım yok)
        var balance = conn.ExecuteScalar<decimal?>(
            @"SELECT SUM(QtyBalance) FROM dbo.tvf_InventoryBalance(@CompanyId) WHERE ItemId = @ItemId",
            new { CompanyId = SystemCompany, s.ItemId });

        Assert.True(balance is null or 0m, $"İptal sonrası net 0 beklenir, gelen {balance}");
    }

    [Fact]
    public void InventoryBalance_Is_Company_Isolated()
    {
        using var conn = Open();
        var s = SetupDraftReceiving(conn, qty: 30m);

        conn.Execute("dbo.sp_ReceivingPost",
            new { HeaderId = s.HeaderId, CompanyId = SystemCompany, UserId = s.UserId },
            commandType: System.Data.CommandType.StoredProcedure);

        // İş kuralı: tvf_InventoryBalance(@CompanyId) yalnız o şirketin hareketini görür — başka şirket sızmaz
        var demoCompany = new Guid("d1e1b1a5-0000-0000-0000-000000000001");
        var leaked = conn.ExecuteScalar<decimal?>(
            @"SELECT SUM(QtyBalance) FROM dbo.tvf_InventoryBalance(@CompanyId) WHERE ItemId = @ItemId",
            new { CompanyId = demoCompany, s.ItemId });

        Assert.True(leaked is null or 0m, $"Başka şirkette bakiye görünmemeli, gelen {leaked}");
    }

    /// <summary>SYSTEM şirketinde izole (taze GUID) bir DRAFT mal kabul senaryosu kurar.</summary>
    private static Scenario SetupDraftReceiving(SqlConnection conn, decimal qty)
    {
        var userId = conn.ExecuteScalar<string>("SELECT TOP 1 Id FROM AspNetUsers")
            ?? throw new InvalidOperationException("Test için kullanıcı yok (seed_core).");
        var uomId = conn.ExecuteScalar<Guid>(
            @"SELECT TOP 1 dv.Id FROM DictionaryValue dv
              JOIN DictionaryType dt ON dt.Id = dv.TypeId AND dt.Code = 'UOM'
              WHERE dt.CompanyId = @C AND dv.Code = 'EACH'", new { C = SystemCompany });

        var catId = Guid.NewGuid(); var partnerId = Guid.NewGuid(); var itemId = Guid.NewGuid();
        var whId = Guid.NewGuid(); var binId = Guid.NewGuid(); var headerId = Guid.NewGuid();
        var suffix = headerId.ToString("N")[..8];

        conn.Execute(@"
            INSERT INTO Category (Id, CompanyId, Code, Name, IsActive, IsDeleted)
                VALUES (@catId, @C, 'TCAT-' + @sfx, N'Test Kategori', 1, 0);
            INSERT INTO Partner (Id, CompanyId, Code, Name, Type, IsActive, IsDeleted)
                VALUES (@partnerId, @C, 'TSUP-' + @sfx, N'Test Tedarikçi', 'VENDOR', 1, 0);
            INSERT INTO Item (Id, CompanyId, Code, Name, CategoryId, BaseUomId, IsActive, IsLotTracked, IsSerialTracked, CreatedAt, IsDeleted, TaxRate)
                VALUES (@itemId, @C, 'TITM-' + @sfx, N'Test Ürün', @catId, @uomId, 1, 0, 0, GETUTCDATE(), 0, 20.00);
            INSERT INTO Warehouse (Id, CompanyId, Code, Name, IsActive, IsDeleted)
                VALUES (@whId, @C, 'TWH-' + @sfx, N'Test Depo', 1, 0);
            INSERT INTO Bin (Id, WarehouseId, Code, Zone, SortNo, IsPickingArea, IsReceivingArea, IsActive, IsDeleted)
                VALUES (@binId, @whId, 'TGRS-' + @sfx, 'GRS', 1, 0, 1, 1, 0);
            INSERT INTO ReceivingHeader (Id, CompanyId, WarehouseId, PartnerId, PurchaseOrderId, ReceivingMode, DocNo, Status, Notes, CreatedBy)
                VALUES (@headerId, @C, @whId, @partnerId, NULL, 'FREE', 'TRCV-' + @sfx, 'DRAFT', N'Test', @userId);
            INSERT INTO ReceivingLine (HeaderId, ItemId, UomId, QtyOriginal, QtyBase, LotNo, PurchaseOrderLineId)
                VALUES (@headerId, @itemId, @uomId, @qty, @qty, NULL, NULL);",
            new { C = SystemCompany, catId, partnerId, itemId, whId, binId, headerId, uomId, userId, qty, sfx = suffix });

        return new Scenario(headerId, itemId, userId);
    }

    private readonly record struct Scenario(Guid HeaderId, Guid ItemId, string UserId);
}
