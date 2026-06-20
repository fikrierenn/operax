using Dapper;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Operax.Tests.Integration;

/// <summary>
/// DB integration harness'ı uçtan uca doğrular (migrate + seed çalıştı mı) ve Plan 35 baseline
/// referans seed'inin done-criteria'sını regresyon olarak kilitler (her şirkette UOM + Adet=C62).
/// </summary>
[Collection("Database")]
public sealed class BaselineSeedTests(DatabaseFixture fixture)
{
    private SqlConnection Open()
    {
        var conn = new SqlConnection(fixture.ConnectionString);
        conn.Open();
        return conn;
    }

    [Fact]
    public void Migrate_And_Seed_Ran_Tables_Exist()
    {
        using var conn = Open();
        // Çekirdek tablolar oluştu mu (schema_all + addon'lar) — harness kanıtı
        var tableCount = conn.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'");
        Assert.True(tableCount > 50, $"Beklenen >50 tablo, bulunan {tableCount}");
    }

    [Fact]
    public void SeedReference_Every_Company_Has_Uom_Baseline()
    {
        using var conn = Open();
        // İş kuralı (Plan 35 done-criteria): her şirkette UOM ≥ 12 değer olmalı
        var minUom = conn.ExecuteScalar<int>(@"
            SELECT MIN(c.Cnt) FROM (
                SELECT COUNT(dv.Id) AS Cnt
                FROM Company comp
                JOIN DictionaryType dt ON dt.CompanyId = comp.Id AND dt.Code = 'UOM' AND dt.IsDeleted = 0
                JOIN DictionaryValue dv ON dv.TypeId = dt.Id AND dv.IsDeleted = 0
                GROUP BY comp.Id
            ) c");
        Assert.True(minUom >= 12, $"Her şirkette UOM ≥ 12 beklenir, en az olan {minUom}");
    }

    [Fact]
    public void SeedReference_Adet_Has_UnEce_C62()
    {
        using var conn = Open();
        // e-Belge zorunlu: Adet birimi UN/ECE C62 + tam-sayı olmalı (Plan 35)
        var c62Count = conn.ExecuteScalar<int>(@"
            SELECT COUNT(*)
            FROM DictionaryValue dv
            JOIN DictionaryType dt ON dt.Id = dv.TypeId AND dt.Code = 'UOM'
            WHERE dv.Code = 'EACH' AND dv.UnEceCode = 'C62' AND dv.IsWholeNumber = 1 AND dv.IsDeleted = 0");
        Assert.True(c62Count >= 1, "En az bir şirkette Adet=C62 (IsWholeNumber=1) beklenir");
    }

    [Fact]
    public void InventoryBalance_Tvf_Executes_Without_Error()
    {
        using var conn = Open();
        // db_objects deploy oldu mu — TVF çağrılabiliyor mu (sonuç boş olabilir, hata olmamalı)
        var companyId = conn.ExecuteScalar<Guid?>("SELECT TOP 1 Id FROM Company");
        Assert.NotNull(companyId);
        var rows = conn.Query("SELECT * FROM dbo.tvf_InventoryBalance(@CompanyId)", new { CompanyId = companyId });
        Assert.NotNull(rows); // çağrı patlamadıysa harness + TVF sağlam
    }
}
