using Dapper;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Operax.Tests.Integration;

/// <summary>
/// Fiyat sapma kontrolü (sp_CheckPriceVariance) doğruluğu: gerçek fiyat liste fiyatına eşitse
/// variance yazılmaz; her sapma (TOLERANS YOK — Plan 27) bir PriceVariance kaydı üretir.
/// Plan 37 Faz 3e — gerçek SP + tvf_PriceListEffective.
/// </summary>
[Collection("Database")]
public sealed class PriceVarianceTests(DatabaseFixture fixture)
{
    private static readonly Guid SystemCompany = new("00000000-0000-0000-0000-000000000000");

    private SqlConnection Open()
    {
        var conn = new SqlConnection(fixture.ConnectionString);
        conn.Open();
        return conn;
    }

    [Fact]
    public void CheckPriceVariance_EqualPrice_Creates_No_Record()
    {
        using var conn = Open();
        var s = SetupItemWithPurchaseList(conn, listPrice: 10.0000m);

        // İş kuralı: gerçek fiyat = liste fiyatı → sapma yok → VarianceId NULL döner
        var varianceId = RunCheck(conn, s, actualPrice: 10.0000m);

        Assert.Null(varianceId);
    }

    [Fact]
    public void CheckPriceVariance_DifferentPrice_Creates_Record_With_Correct_Delta()
    {
        using var conn = Open();
        var s = SetupItemWithPurchaseList(conn, listPrice: 10.0000m);

        // İş kuralı: gerçek fiyat liste fiyatından sapıyor (11 > 10) → PriceVariance DRAFT kaydı
        var varianceId = RunCheck(conn, s, actualPrice: 11.0000m);

        Assert.NotNull(varianceId);

        var row = conn.QuerySingle(
            @"SELECT ExpectedPrice, ActualPrice, Variance, VariancePercent, Status
              FROM PriceVariance WHERE Id = @Id", new { Id = varianceId });

        Assert.Equal(10.0000m, (decimal)row.ExpectedPrice);
        Assert.Equal(11.0000m, (decimal)row.ActualPrice);
        Assert.Equal(1.0000m, (decimal)row.Variance);          // 11 − 10
        Assert.Equal(10.0000m, (decimal)row.VariancePercent);  // 1 / 10 × 100
        Assert.Equal("DRAFT", (string)row.Status);
    }

    [Fact]
    public void CheckPriceVariance_NoPriceList_Creates_No_Record()
    {
        using var conn = Open();
        var s = SetupItemWithPurchaseList(conn, listPrice: 10.0000m, withList: false);

        // İş kuralı: liste fiyatı yoksa sapma hesaplanamaz → VarianceId NULL
        var varianceId = RunCheck(conn, s, actualPrice: 99.0000m);

        Assert.Null(varianceId);
    }

    /// <summary>sp_CheckPriceVariance'i OUTPUT parametresiyle çağırır, üretilen VarianceId'i döner.</summary>
    private static Guid? RunCheck(SqlConnection conn, Scenario s, decimal actualPrice)
    {
        var p = new DynamicParameters();
        p.Add("CompanyId", SystemCompany);
        p.Add("PoHeaderId", Guid.NewGuid());
        p.Add("PoLineId", Guid.NewGuid());
        p.Add("ItemId", s.ItemId);
        p.Add("PartnerId", s.PartnerId);
        p.Add("ActualPrice", actualPrice);
        p.Add("UserId", (Guid?)null);
        p.Add("VarianceId", dbType: System.Data.DbType.Guid, direction: System.Data.ParameterDirection.Output);

        conn.Execute("dbo.sp_CheckPriceVariance", p, commandType: System.Data.CommandType.StoredProcedure);
        return p.Get<Guid?>("VarianceId");
    }

    /// <summary>
    /// SYSTEM şirketinde taze GUID'lerle kalem + tedarikçi kurar; withList=true ise kaleme
    /// genel (PartnerId NULL) bir PURCHASE fiyat listesi satırı ekler.
    /// </summary>
    private static Scenario SetupItemWithPurchaseList(SqlConnection conn, decimal listPrice, bool withList = true)
    {
        var uomId = conn.ExecuteScalar<Guid>(
            @"SELECT TOP 1 dv.Id FROM DictionaryValue dv
              JOIN DictionaryType dt ON dt.Id = dv.TypeId AND dt.Code = 'UOM'
              WHERE dt.CompanyId = @C AND dv.Code = 'EACH'", new { C = SystemCompany });

        var catId = Guid.NewGuid(); var partnerId = Guid.NewGuid(); var itemId = Guid.NewGuid();
        var suffix = itemId.ToString("N")[..8];

        conn.Execute(@"
            INSERT INTO Category (Id, CompanyId, Code, Name, IsActive, IsDeleted)
                VALUES (@catId, @C, 'PCAT-' + @sfx, N'Fiyat Test Kategori', 1, 0);
            INSERT INTO Partner (Id, CompanyId, Code, Name, Type, IsActive, IsDeleted)
                VALUES (@partnerId, @C, 'PSUP-' + @sfx, N'Fiyat Test Tedarikçi', 'VENDOR', 1, 0);
            INSERT INTO Item (Id, CompanyId, Code, Name, CategoryId, BaseUomId, IsActive, IsLotTracked, IsSerialTracked, CreatedAt, IsDeleted, TaxRate)
                VALUES (@itemId, @C, 'PITM-' + @sfx, N'Fiyat Test Ürün', @catId, @uomId, 1, 0, 0, GETUTCDATE(), 0, 20.00);",
            new { C = SystemCompany, catId, partnerId, itemId, uomId, sfx = suffix });

        if (withList)
        {
            var plId = Guid.NewGuid();
            conn.Execute(@"
                INSERT INTO PriceList (Id, CompanyId, Code, Name, Direction, IsActive, Priority)
                    VALUES (@plId, @C, 'PPL-' + @sfx, N'Fiyat Test Alış Listesi', 'PURCHASE', 1, 0);
                INSERT INTO PriceListLine (Id, PriceListId, ItemId, UnitPrice, MinQty, LineType)
                    VALUES (NEWID(), @plId, @itemId, @listPrice, 0, 'FIXED');",
                new { C = SystemCompany, plId, itemId, listPrice, sfx = suffix });
        }

        return new Scenario(itemId, partnerId);
    }

    private readonly record struct Scenario(Guid ItemId, Guid PartnerId);
}
