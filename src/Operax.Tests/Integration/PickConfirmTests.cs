using Dapper;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Operax.Tests.Integration;

/// <summary>
/// Terminal toplama onayı (sp_PickConfirm) doğruluğu: barkod ürün koduyla eşleşince satır tamamlanır
/// ve görev COMPLETED olur — ledger hareketi YAZILMAZ (stok yalnız sevkiyat POSTED'da çıkar). Yanlış
/// barkod THROW 51581 ile reddedilir. Plan 33 DEBT — Pick smoke.
/// </summary>
[Collection("Database")]
public sealed class PickConfirmTests(DatabaseFixture fixture)
{
    private static readonly Guid SystemCompany = new("00000000-0000-0000-0000-000000000000");

    private SqlConnection Open()
    {
        var conn = new SqlConnection(fixture.ConnectionString);
        conn.Open();
        return conn;
    }

    [Fact]
    public void PickConfirm_MatchingBarcode_Completes_Without_StockMovement()
    {
        using var conn = Open();
        var s = SetupPickTask(conn, qty: 12m);

        conn.Execute("dbo.sp_PickConfirm",
            new { TaskId = s.TaskId, LineId = s.LineId, Barcode = s.ItemCode, CompanyId = SystemCompany, UserId = s.UserId },
            commandType: System.Data.CommandType.StoredProcedure);

        // İş kuralı: satır tamamlanır (QtyPickedBase = istenen)
        var picked = conn.ExecuteScalar<decimal>(
            "SELECT QtyPickedBase FROM PickTaskLine WHERE Id = @LineId", new { LineId = s.LineId });
        Assert.Equal(12m, picked);

        // İş kuralı: tek satır tamamlanınca görev COMPLETED
        var status = conn.ExecuteScalar<string>(
            "SELECT Status FROM PickTask WHERE Id = @TaskId", new { TaskId = s.TaskId });
        Assert.Equal("COMPLETED", status);

        // İş kuralı: terminal toplama ledger'a dokunmaz — stok yalnız sevkiyat POSTED'da çıkar
        var moves = conn.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM StockMovement WHERE ItemId = @ItemId", new { s.ItemId });
        Assert.Equal(0, moves);
    }

    [Fact]
    public void PickConfirm_WrongBarcode_Is_Rejected()
    {
        using var conn = Open();
        var s = SetupPickTask(conn, qty: 5m);

        // İş kuralı: barkod ürün koduyla eşleşmezse THROW 51581 (yanlış ürün tarandı)
        var ex = Assert.Throws<SqlException>(() =>
            conn.Execute("dbo.sp_PickConfirm",
                new { TaskId = s.TaskId, LineId = s.LineId, Barcode = "YANLIS-BARKOD", CompanyId = SystemCompany, UserId = s.UserId },
                commandType: System.Data.CommandType.StoredProcedure));

        Assert.Equal(51581, ex.Number);
    }

    /// <summary>SYSTEM şirketinde taze GUID'lerle bir sevkiyat toplama görevi + satırı kurar.</summary>
    private static Scenario SetupPickTask(SqlConnection conn, decimal qty)
    {
        var userId = conn.ExecuteScalar<string>("SELECT TOP 1 Id FROM AspNetUsers")
            ?? throw new InvalidOperationException("Test için kullanıcı yok (seed_core).");
        var uomId = conn.ExecuteScalar<Guid>(
            @"SELECT TOP 1 dv.Id FROM DictionaryValue dv
              JOIN DictionaryType dt ON dt.Id = dv.TypeId AND dt.Code = 'UOM'
              WHERE dt.CompanyId = @C AND dv.Code = 'EACH'", new { C = SystemCompany });

        var catId = Guid.NewGuid(); var itemId = Guid.NewGuid();
        var whId = Guid.NewGuid(); var binId = Guid.NewGuid(); var shipmentId = Guid.NewGuid();
        var taskId = Guid.NewGuid(); var lineId = Guid.NewGuid();
        var sfx = taskId.ToString("N")[..8];
        var itemCode = "PCITM-" + sfx;

        conn.Execute(@"
            INSERT INTO Category (Id, CompanyId, Code, Name, IsActive, IsDeleted)
                VALUES (@catId, @C, 'PCCAT-' + @sfx, N'Onay Test Kategori', 1, 0);
            INSERT INTO Item (Id, CompanyId, Code, Name, CategoryId, BaseUomId, IsActive, IsLotTracked, IsSerialTracked, CreatedAt, IsDeleted, TaxRate)
                VALUES (@itemId, @C, @itemCode, N'Onay Test Ürün', @catId, @uomId, 1, 0, 0, GETUTCDATE(), 0, 20.00);
            INSERT INTO Warehouse (Id, CompanyId, Code, Name, IsActive, IsDeleted)
                VALUES (@whId, @C, 'PCWH-' + @sfx, N'Onay Test Depo', 1, 0);
            INSERT INTO Bin (Id, WarehouseId, Code, Zone, SortNo, IsPickingArea, IsReceivingArea, IsActive, IsDeleted)
                VALUES (@binId, @whId, 'PCPCK-' + @sfx, 'PCK', 1, 1, 0, 1, 0);
            INSERT INTO ShippingHeader (Id, CompanyId, WarehouseId, DocNo, Status, IsDeleted)
                VALUES (@shipmentId, @C, @whId, 'SHP-' + @sfx, 'DRAFT', 0);
            INSERT INTO PickTask (Id, CompanyId, DocNo, ShipmentId, SourceDocType, Status)
                VALUES (@taskId, @C, 'PCK-' + @sfx, @shipmentId, 'SHIPMENT', 'DRAFT');
            INSERT INTO PickTaskLine (Id, PickTaskId, ItemId, UomId, TargetWarehouseId, TargetBinId,
                                      QtyRequested, QtyRequestedBase, QtyPicked, QtyPickedBase, IsCancelled)
                VALUES (@lineId, @taskId, @itemId, @uomId, @whId, @binId, @qty, @qty, 0, 0, 0);",
            new { C = SystemCompany, catId, itemId, itemCode, whId, binId, shipmentId, taskId, lineId, uomId, qty, sfx });

        return new Scenario(taskId, lineId, itemId, itemCode, userId);
    }

    private readonly record struct Scenario(Guid TaskId, Guid LineId, Guid ItemId, string ItemCode, string UserId);
}
