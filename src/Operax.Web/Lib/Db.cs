using Microsoft.Data.SqlClient;
using System.Data;

namespace Operax.Web.Lib;

public class Db(IConfiguration config)
{
    private readonly string _connStr = config.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Connection string 'Default' not found.");

    public IDbConnection Open() => new SqlConnection(_connStr);
}
