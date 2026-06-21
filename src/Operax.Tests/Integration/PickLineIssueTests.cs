using Dapper;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Operax.Tests.Integration;

/// <summary>
/// Toplama-sevkiyat çift-düşüm fix doğruluğu: SEVKİYAT toplaması (PickTask.ShipmentId dolu)
/// sp_PickLinePost'ta ledger hareketi YAZMAZ — stok yalnız sp_ShippingPost'ta çıkar. Üretim/sarfiyat
/// toplaması (ShipmentId NULL) ISSUE yazmayı sürdürür (tek sarf noktası). Plan 33 DEBT — Pick-vs-shipping.
/// </summary>
[Collection("Database")]
public sealed class PickLineIssueTests(DatabaseFixture fixture)
{
    private static readonly Guid SystemCompany = new("00000000-0000-0000-0000-000000000000");

    private SqlConnection Open()
    {
        var conn = new SqlConnection(fixture.ConnectionString);
        conn.Open();
        return conn;
    }

    [Fact]
    public void ShipmentPick_Writes_No_StockMovement()
    {
        using var conn = Open();
        var s = SetupPickScenario(conn, qty: 40m, isShipmentPick: true);

        conn.Execute("dbo.sp_PickLinePost",
            new { LineId = s.LineId, TaskId = s.TaskId, Qty = 40m, CompanyId = SystemCompany, UserId = s.UserId },
            commandType: System.Data.CommandType.StoredProcedure);

        // İş kuralı: sevkiyat toplaması ledger'a dokunmaz (çift-düşüm engeli) — stok sevkiyat POSTED'da çıkar
        var moves = conn.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM StockMovement WHERE ItemId = @ItemId AND SourceDocType = 'PICKING'",
            new { s.ItemId });
        Assert.Equal(0, moves);

        // Toplama satırı yine de toplanmış olarak işaretlenir (durum/tahsisat adımı çalışır)
        var picked = conn.ExecuteScalar<decimal>(
            "SELECT QtyPicked FROM PickTaskLine WHERE Id = @LineId", new { LineId = s.LineId });
        Assert.Equal(40m, picked);
    }

    [Fact]
    public void ProductionPick_Writes_Issue_Movement()
    {
        using var conn = Open();
        var s = SetupPickScenario(conn, qty: 25m, isShipmentPick: false);

        conn.Execute("dbo.sp_PickLinePost",
            new { LineId = s.LineId, TaskId = s.TaskId, Qty = 25m, CompanyId = SystemCompany, UserId = s.UserId },
            commandType: System.Data.CommandType.StoredProcedure);

        // İş kuralı: sevkiyat-dışı toplama (üretim hammadde sarfı) ISSUE yazmayı sürdürür (tek sarf noktası)
        var balance = conn.ExecuteScalar<decimal?>(
            "SELECT SUM(QtyBalance) FROM dbo.tvf_InventoryBalance(@CompanyId) WHERE ItemId = @ItemId",
            new { CompanyId = SystemCompany, s.ItemId });
        Assert.Equal(-25m, balance);
    }

    /// <summary>
    /// SYSTEM şirketinde taze GUID'lerle toplama senaryosu kurar. isShipmentPick=true → PickTask.ShipmentId
    /// dolu (DRAFT ShippingHeader); false → SourceDocId dolu (üretim emri taklidi, PRD-PCK DocNo).
    /// </summary>
    private static Scenario SetupPickScenario(SqlConnection conn, decimal qty, bool isShipmentPick)
    {
        var userId = conn.ExecuteScalar<string>("SELECT TOP 1 Id FROM AspNetUsers")
            ?? throw new InvalidOperationException("Test için kullanıcı yok (seed_core).");
        var uomId = conn.ExecuteScalar<Guid>(
            @"SELECT TOP 1 dv.Id FROM DictionaryValue dv
              JOIN DictionaryType dt ON dt.Id = dv.TypeId AND dt.Code = 'UOM'
              WHERE dt.CompanyId = @C AND dv.Code = 'EACH'", new { C = SystemCompany });

        var catId = Guid.NewGuid(); var itemId = Guid.NewGuid();
        var whId = Guid.NewGuid(); var binId = Guid.NewGuid();
        var taskId = Guid.NewGuid(); var lineId = Guid.NewGuid();
        var sfx = taskId.ToString("N")[..8];

        conn.Execute(@"
            INSERT INTO Category (Id, CompanyId, Code, Name, IsActive, IsDeleted)
                VALUES (@catId, @C, 'KCAT-' + @sfx, N'Toplama Test Kategori', 1, 0);
            INSERT INTO Item (Id, CompanyId, Code, Name, CategoryId, BaseUomId, IsActive, IsLotTracked, IsSerialTracked, CreatedAt, IsDeleted, TaxRate)
                VALUES (@itemId, @C, 'KITM-' + @sfx, N'Toplama Test Ürün', @catId, @uomId, 1, 0, 0, GETUTCDATE(), 0, 20.00);
            INSERT INTO Warehouse (Id, CompanyId, Code, Name, IsActive, IsDeleted)
                VALUES (@whId, @C, 'KWH-' + @sfx, N'Toplama Test Depo', 1, 0);
            INSERT INTO Bin (Id, WarehouseId, Code, Zone, SortNo, IsPickingArea, IsReceivingArea, IsActive, IsDeleted)
                VALUES (@binId, @whId, 'KPCK-' + @sfx, 'PCK', 1, 1, 0, 1, 0);",
            new { C = SystemCompany, catId, itemId, whId, binId, uomId, sfx });

        Guid? shipmentId = null; Guid? sourceDocId = null; string docNo;
        if (isShipmentPick)
        {
            shipmentId = Guid.NewGuid();
            docNo = "PCK-" + sfx;
            conn.Execute(@"
                INSERT INTO ShippingHeader (Id, CompanyId, WarehouseId, DocNo, Status, IsDeleted)
                    VALUES (@shipmentId, @C, @whId, 'SHP-' + @sfx, 'DRAFT', 0);",
                new { C = SystemCompany, shipmentId, whId, sfx });
        }
        else
        {
            sourceDocId = Guid.NewGuid();        // üretim emri taklidi (FK yok)
            docNo = "PRD-PCK-" + sfx;
        }

        conn.Execute(@"
            INSERT INTO PickTask (Id, CompanyId, DocNo, ShipmentId, SourceDocId, SourceDocType, Status)
                VALUES (@taskId, @C, @docNo, @shipmentId, @sourceDocId, @srcType, 'ASSIGNED');
            INSERT INTO PickTaskLine (Id, PickTaskId, ItemId, UomId, TargetWarehouseId, TargetBinId,
                                      QtyRequested, QtyRequestedBase, QtyPicked, QtyPickedBase, IsCancelled)
                VALUES (@lineId, @taskId, @itemId, @uomId, @whId, @binId, @qty, @qty, 0, 0, 0);",
            new
            {
                C = SystemCompany, taskId, docNo, shipmentId, sourceDocId,
                srcType = isShipmentPick ? "SHIPMENT" : "PRODUCTION",
                lineId, itemId, uomId, whId, binId, qty
            });

        return new Scenario(taskId, lineId, itemId, userId);
    }

    private readonly record struct Scenario(Guid TaskId, Guid LineId, Guid ItemId, string UserId);
}
