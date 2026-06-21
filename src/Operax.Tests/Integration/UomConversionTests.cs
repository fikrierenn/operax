using Dapper;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Operax.Tests.Integration;

/// <summary>
/// UOM dönüşüm fonksiyonu (fn_GetConversionRate) doğruluğu: baz birim 1.0 döner,
/// tanımlı alternatif birim ItemUOM.ConversionRate'i döner, tanımsız birim 1.0'a düşer.
/// Plan 37 Faz 3e — saf fonksiyon, ledger'a dokunmaz.
/// </summary>
[Collection("Database")]
public sealed class UomConversionTests(DatabaseFixture fixture)
{
    // SYSTEM şirketi baseline UOM sözlüğünü (EACH/CASE/KG) taşır
    private static readonly Guid SystemCompany = new("00000000-0000-0000-0000-000000000000");

    private SqlConnection Open()
    {
        var conn = new SqlConnection(fixture.ConnectionString);
        conn.Open();
        return conn;
    }

    [Fact]
    public void ConversionRate_BaseUom_Is_One()
    {
        using var conn = Open();
        var s = SetupItemWithAltUom(conn, altRate: 12m);

        // İş kuralı: kalemin baz birimi için dönüşüm oranı her zaman 1.0
        var rate = conn.ExecuteScalar<decimal>(
            "SELECT dbo.fn_GetConversionRate(@ItemId, @UomId)",
            new { s.ItemId, UomId = s.BaseUomId });

        Assert.Equal(1.0m, rate);
    }

    [Fact]
    public void ConversionRate_DefinedAltUom_Returns_ItemUomRate()
    {
        using var conn = Open();
        var s = SetupItemWithAltUom(conn, altRate: 12m);

        // İş kuralı: tanımlı alternatif birim (ItemUOM) kayıtlı ConversionRate'i döner
        var rate = conn.ExecuteScalar<decimal>(
            "SELECT dbo.fn_GetConversionRate(@ItemId, @UomId)",
            new { s.ItemId, UomId = s.AltUomId });

        Assert.Equal(12m, rate);
    }

    [Fact]
    public void ConversionRate_UndefinedUom_Falls_Back_To_One()
    {
        using var conn = Open();
        var s = SetupItemWithAltUom(conn, altRate: 12m);

        // İş kuralı: kaleme tanımlanmamış (ItemUOM kaydı olmayan, baz da olmayan) birim 1.0'a düşer
        var rate = conn.ExecuteScalar<decimal>(
            "SELECT dbo.fn_GetConversionRate(@ItemId, @UomId)",
            new { s.ItemId, UomId = s.UnmappedUomId });

        Assert.Equal(1.0m, rate);
    }

    /// <summary>
    /// SYSTEM şirketinde taze GUID'lerle bir kalem kurar: baz birim EACH, alternatif birim CASE
    /// (verilen oranla ItemUOM'e işlenir), KG ise hiç tanımlanmaz (fallback senaryosu için).
    /// </summary>
    private static Scenario SetupItemWithAltUom(SqlConnection conn, decimal altRate)
    {
        var baseUom     = UomId(conn, "EACH");
        var altUom      = UomId(conn, "CASE");
        var unmappedUom = UomId(conn, "KG");

        var catId = Guid.NewGuid(); var itemId = Guid.NewGuid();
        var suffix = itemId.ToString("N")[..8];

        conn.Execute(@"
            INSERT INTO Category (Id, CompanyId, Code, Name, IsActive, IsDeleted)
                VALUES (@catId, @C, 'UCAT-' + @sfx, N'Birim Test Kategori', 1, 0);
            INSERT INTO Item (Id, CompanyId, Code, Name, CategoryId, BaseUomId, IsActive, IsLotTracked, IsSerialTracked, CreatedAt, IsDeleted, TaxRate)
                VALUES (@itemId, @C, 'UITM-' + @sfx, N'Birim Test Ürün', @catId, @baseUom, 1, 0, 0, GETUTCDATE(), 0, 20.00);
            INSERT INTO ItemUOM (Id, ItemId, UomId, ConversionRate)
                VALUES (NEWID(), @itemId, @altUom, @altRate);",
            new { C = SystemCompany, catId, itemId, baseUom, altUom, altRate, sfx = suffix });

        return new Scenario(itemId, baseUom, altUom, unmappedUom);
    }

    /// <summary>SYSTEM şirketinin UOM sözlüğünden verilen kodun DictionaryValue Id'sini çeker.</summary>
    private static Guid UomId(SqlConnection conn, string code) => conn.ExecuteScalar<Guid>(
        @"SELECT TOP 1 dv.Id FROM DictionaryValue dv
          JOIN DictionaryType dt ON dt.Id = dv.TypeId AND dt.Code = 'UOM'
          WHERE dt.CompanyId = @C AND dv.Code = @code", new { C = SystemCompany, code });

    private readonly record struct Scenario(Guid ItemId, Guid BaseUomId, Guid AltUomId, Guid UnmappedUomId);
}
