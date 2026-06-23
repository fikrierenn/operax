using Microsoft.Data.SqlClient;
using System.Data;

namespace Operax.Web.Lib;

public class Db(IConfiguration config)
{
    private readonly string _connStr = config.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Connection string 'Default' not found.");

    // Açık bir bağlantı döner (adı + CLAUDE.md kontratı gereği). Dapper sorguları kapalı
    // bağlantıyı otomatik açar ama conn.BeginTransaction() açmaz → eager açıyoruz ki
    // transaction kullanan handler'lar (Expenses/Transfer/Production) "bağlantı kapalı" almasın.
    public IDbConnection Open()
    {
        var conn = new SqlConnection(_connStr);
        conn.Open();
        return conn;
    }
}
