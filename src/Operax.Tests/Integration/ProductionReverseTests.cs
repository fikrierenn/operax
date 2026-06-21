using Dapper;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Operax.Tests.Integration;

/// <summary>
/// Üretim emri iptali (sp_ProductionReverse) ledger doğruluğu: iptal hem mamul girişini (RECEIPT)
/// hem de pick'ten gelen hammadde sarfını (PICKING ISSUE) IsCancelled=1 ile kapatmalı — aksi halde
/// iptal sonrası hammadde sarf edilmiş kalır (hayalet sarfiyat). sql-sp-reviewer IMP-1 fix.
/// </summary>
[Collection("Database")]
public sealed class ProductionReverseTests(DatabaseFixture fixture)
{
    private static readonly Guid SystemCompany = new("00000000-0000-0000-0000-000000000000");

    private SqlConnection Open()
    {
        var conn = new SqlConnection(fixture.ConnectionString);
        conn.Open();
        return conn;
    }

    [Fact]
    public void ProductionReverse_Cancels_Both_Receipt_And_Picking_Consumption()
    {
        using var conn = Open();
        var s = SetupCompletedProductionWithPick(conn, rawQty: 30m, finQty: 10m);

        // Ön koşul: hammadde pick ile sarf edildi (PICKING ISSUE -30), mamul girişi yazıldı (+10)
        Assert.Equal(-30m, ItemBalance(conn, s.RawItemId));
        Assert.Equal(10m, ItemBalance(conn, s.FinItemId));

        conn.Execute("dbo.sp_ProductionReverse",
            new { OrderId = s.OrderId, CompanyId = SystemCompany, UserId = s.UserId },
            commandType: System.Data.CommandType.StoredProcedure);

        // İş kuralı: iptal sonrası hem mamul (RECEIPT) hem hammadde (PICKING ISSUE) net 0 — hayalet sarfiyat yok
        Assert.True(ItemBalance(conn, s.RawItemId) is null or 0m,
            "İptal sonrası hammadde bakiyesi 0 olmalı (PICKING sarfı geri alınmadı = hayalet sarfiyat)");
        Assert.True(ItemBalance(conn, s.FinItemId) is null or 0m,
            "İptal sonrası mamul bakiyesi 0 olmalı (RECEIPT geri alınmadı)");
    }

    private static decimal? ItemBalance(SqlConnection conn, Guid itemId) => conn.ExecuteScalar<decimal?>(
        "SELECT SUM(QtyBalance) FROM dbo.tvf_InventoryBalance(@C) WHERE ItemId = @ItemId",
        new { C = SystemCompany, ItemId = itemId });

    /// <summary>
    /// SYSTEM şirketinde COMPLETED üretim emri kurar: hammadde sp_PickLinePost ile sarf edilir
    /// (PICKING ISSUE), mamul girişi (PRODUCTION RECEIPT) elle yazılır (reverse guard'ı için gerekli).
    /// </summary>
    private static Scenario SetupCompletedProductionWithPick(SqlConnection conn, decimal rawQty, decimal finQty)
    {
        var userId = conn.ExecuteScalar<string>("SELECT TOP 1 Id FROM AspNetUsers")
            ?? throw new InvalidOperationException("Test için kullanıcı yok (seed_core).");
        var uomId = conn.ExecuteScalar<Guid>(
            @"SELECT TOP 1 dv.Id FROM DictionaryValue dv
              JOIN DictionaryType dt ON dt.Id = dv.TypeId AND dt.Code = 'UOM'
              WHERE dt.CompanyId = @C AND dv.Code = 'EACH'", new { C = SystemCompany });

        var catId = Guid.NewGuid(); var rawItemId = Guid.NewGuid(); var finItemId = Guid.NewGuid();
        var whId = Guid.NewGuid(); var binId = Guid.NewGuid();
        var orderId = Guid.NewGuid(); var taskId = Guid.NewGuid(); var lineId = Guid.NewGuid();
        var sfx = orderId.ToString("N")[..8];

        conn.Execute(@"
            INSERT INTO Category (Id, CompanyId, Code, Name, IsActive, IsDeleted)
                VALUES (@catId, @C, 'RCAT-' + @sfx, N'Üretim Test Kategori', 1, 0);
            INSERT INTO Item (Id, CompanyId, Code, Name, CategoryId, BaseUomId, IsActive, IsLotTracked, IsSerialTracked, CreatedAt, IsDeleted, TaxRate)
                VALUES (@rawItemId, @C, 'RRAW-' + @sfx, N'Hammadde', @catId, @uomId, 1, 0, 0, GETUTCDATE(), 0, 20.00);
            INSERT INTO Item (Id, CompanyId, Code, Name, CategoryId, BaseUomId, IsActive, IsLotTracked, IsSerialTracked, CreatedAt, IsDeleted, TaxRate)
                VALUES (@finItemId, @C, 'RFIN-' + @sfx, N'Mamul', @catId, @uomId, 1, 0, 0, GETUTCDATE(), 0, 20.00);
            INSERT INTO Warehouse (Id, CompanyId, Code, Name, IsActive, IsDeleted)
                VALUES (@whId, @C, 'RWH-' + @sfx, N'Üretim Depo', 1, 0);
            INSERT INTO Bin (Id, WarehouseId, Code, Zone, SortNo, IsPickingArea, IsReceivingArea, IsActive, IsDeleted)
                VALUES (@binId, @whId, 'RPCK-' + @sfx, 'PCK', 1, 1, 0, 1, 0);
            INSERT INTO ProductionOrder (Id, CompanyId, DocNo, ItemId, QtyTarget, QtyProduced, Status, TargetWarehouseId, TargetBinId)
                VALUES (@orderId, @C, 'PRD-' + @sfx, @finItemId, @finQty, @finQty, 'COMPLETED', @whId, @binId);
            INSERT INTO PickTask (Id, CompanyId, DocNo, SourceDocId, SourceDocType, Status)
                VALUES (@taskId, @C, 'PRD-PCK-' + @sfx, @orderId, 'PRODUCTION', 'ASSIGNED');
            INSERT INTO PickTaskLine (Id, PickTaskId, ItemId, UomId, TargetWarehouseId, TargetBinId,
                                      QtyRequested, QtyRequestedBase, QtyPicked, QtyPickedBase, IsCancelled)
                VALUES (@lineId, @taskId, @rawItemId, @uomId, @whId, @binId, @rawQty, @rawQty, 0, 0, 0);",
            new { C = SystemCompany, catId, rawItemId, finItemId, uomId, whId, binId, orderId, taskId, lineId, rawQty, finQty, sfx });

        // Hammadde sarfı: gerçek SP ile (ShipmentId NULL → PICKING ISSUE yazar)
        conn.Execute("dbo.sp_PickLinePost",
            new { LineId = lineId, TaskId = taskId, Qty = rawQty, CompanyId = SystemCompany, UserId = userId },
            commandType: System.Data.CommandType.StoredProcedure);

        // Mamul girişi (RECEIPT): sp_ProductionFinish muadili — reverse guard'ı RECEIPT varlığı bekler
        conn.Execute(@"
            INSERT INTO StockMovement
                (CompanyId, WarehouseId, BinId, ItemId, MovementType,
                 QtyBase, UomId, QtyOriginal, SourceDocType, SourceDocId, SourceDocNo, CreatedBy, BranchId)
            VALUES
                (@C, @whId, @binId, @finItemId, 'PRODUCTION',
                 @finQty, @uomId, @finQty, 'PRODUCTION', @orderId, 'PRD-' + @sfx, @userId,
                 ISNULL((SELECT BranchId FROM Warehouse WHERE Id = @whId), dbo.fn_DefaultBranchId(@C)));",
            new { C = SystemCompany, whId, binId, finItemId, uomId, orderId, finQty, userId, sfx });

        return new Scenario(orderId, rawItemId, finItemId, userId);
    }

    private readonly record struct Scenario(Guid OrderId, Guid RawItemId, Guid FinItemId, string UserId);
}
