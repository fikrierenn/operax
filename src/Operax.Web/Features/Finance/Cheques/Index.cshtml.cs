using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Operax.Web.Lib;

namespace Operax.Web.Features.Finance.Cheques;

/// <summary>
/// Çek + Senet portföyü.
/// v_ChequePortfolio'dan vade ve statü bilgisi ile birlikte okur.
/// Sekmeler: Tümü / Alınan / Verilen / Portföyde / Bankada / Tahsil / Karşılıksız.
/// </summary>
[Authorize]
public class IndexModel(Db db, ICurrentCompany company) : PageModel
{
    [BindProperty(SupportsGet = true)] public string Direction { get; set; } = "all";   // all/RECEIVED/ISSUED
    [BindProperty(SupportsGet = true)] public string Status    { get; set; } = "all";   // all/PORTFOLIO/IN_BANK/COLLECTED/RETURNED
    [BindProperty(SupportsGet = true)] public string Type      { get; set; } = "cheque"; // cheque/note

    public List<ChequeRowDto> Items   { get; set; } = [];
    public StatusCounts       Counts  { get; set; } = new(0, 0, 0, 0, 0);
    public decimal            TotalInPortfolio { get; set; }
    public decimal            TotalInBank      { get; set; }

    public async Task OnGetAsync()
    {
        using var conn = db.Open();
        var p = new { CompanyId = company.Id };

        await LoadCountsAsync(conn, p);
        await LoadItemsAsync(conn);
    }

    private async Task LoadCountsAsync(System.Data.IDbConnection conn, object p)
    {
        // İş kuralı: tablo adı kullanıcıdan gelmiyor — sabit iki SQL bloğuyla injection riski sıfır
        if (Type == "note")
        {
            Counts = await conn.QuerySingleAsync<StatusCounts>(@"
                SELECT COUNT(*) AS Total,
                       SUM(CASE WHEN Status = 'PORTFOLIO' THEN 1 ELSE 0 END) AS Portfolio,
                       SUM(CASE WHEN Status = 'IN_BANK'   THEN 1 ELSE 0 END) AS InBank,
                       SUM(CASE WHEN Status = 'COLLECTED' THEN 1 ELSE 0 END) AS Collected,
                       SUM(CASE WHEN Status = 'RETURNED'  THEN 1 ELSE 0 END) AS Returned
                FROM PromissoryNote WHERE CompanyId = @CompanyId AND IsDeleted = 0", p);
            TotalInPortfolio = await conn.ExecuteScalarAsync<decimal>(
                "SELECT ISNULL(SUM(Amount),0) FROM PromissoryNote WHERE CompanyId=@CompanyId AND Status='PORTFOLIO' AND IsDeleted=0", p);
            TotalInBank = await conn.ExecuteScalarAsync<decimal>(
                "SELECT ISNULL(SUM(Amount),0) FROM PromissoryNote WHERE CompanyId=@CompanyId AND Status='IN_BANK' AND IsDeleted=0", p);
        }
        else
        {
            Counts = await conn.QuerySingleAsync<StatusCounts>(@"
                SELECT COUNT(*) AS Total,
                       SUM(CASE WHEN Status = 'PORTFOLIO' THEN 1 ELSE 0 END) AS Portfolio,
                       SUM(CASE WHEN Status = 'IN_BANK'   THEN 1 ELSE 0 END) AS InBank,
                       SUM(CASE WHEN Status = 'COLLECTED' THEN 1 ELSE 0 END) AS Collected,
                       SUM(CASE WHEN Status = 'RETURNED'  THEN 1 ELSE 0 END) AS Returned
                FROM Cheque WHERE CompanyId = @CompanyId AND IsDeleted = 0", p);
            TotalInPortfolio = await conn.ExecuteScalarAsync<decimal>(
                "SELECT ISNULL(SUM(Amount),0) FROM Cheque WHERE CompanyId=@CompanyId AND Status='PORTFOLIO' AND IsDeleted=0", p);
            TotalInBank = await conn.ExecuteScalarAsync<decimal>(
                "SELECT ISNULL(SUM(Amount),0) FROM Cheque WHERE CompanyId=@CompanyId AND Status='IN_BANK' AND IsDeleted=0", p);
        }
    }

    private async Task LoadItemsAsync(System.Data.IDbConnection conn)
    {
        var parms = new DynamicParameters();
        parms.Add("CompanyId", company.Id);

        string sql;
        if (Type == "note")
        {
            sql = @"
                SELECT
                    n.Id, n.Direction, n.NoteNo AS DocNo, NULL AS BankName, NULL AS BranchName,
                    n.DrawerName, n.Amount, n.Currency,
                    n.IssueDate AS DocDate, n.DueDate, n.Status, n.PartnerId,
                    p.Name AS PartnerName,
                    DATEDIFF(DAY, GETUTCDATE(), n.DueDate) AS DaysToDue
                FROM PromissoryNote n
                LEFT JOIN Partner p ON p.Id = n.PartnerId
                WHERE n.CompanyId = @CompanyId AND n.IsDeleted = 0";
        }
        else
        {
            sql = @"
                SELECT
                    Id, Direction, ChequeNo AS DocNo, BankName, BranchName,
                    DrawerName, Amount, Currency,
                    ChequeDate AS DocDate, DueDate, Status, PartnerId,
                    PartnerName, DaysToDue
                FROM v_ChequePortfolio
                WHERE CompanyId = @CompanyId";
        }

        if (Direction != "all")
        {
            sql += " AND Direction = @Direction";
            parms.Add("Direction", Direction);
        }
        if (Status != "all")
        {
            sql += " AND Status = @Status";
            parms.Add("Status", Status);
        }
        sql += " ORDER BY DueDate ASC";

        Items = (await conn.QueryAsync<ChequeRowDto>(sql, parms)).ToList();
    }

    public record ChequeRowDto(
        Guid     Id,
        string   Direction,
        string   DocNo,
        string?  BankName,
        string?  BranchName,
        string   DrawerName,
        decimal  Amount,
        string   Currency,
        DateTime DocDate,
        DateTime DueDate,
        string   Status,
        Guid?    PartnerId,
        string?  PartnerName,
        int      DaysToDue);

    public record StatusCounts(int Total, int Portfolio, int InBank, int Collected, int Returned);
}
