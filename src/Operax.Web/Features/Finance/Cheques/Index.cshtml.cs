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

    // Sayfalama (PF-1) — Items/Index template'i
    [BindProperty(SupportsGet = true)] public int Page { get; set; } = 1;
    public int PageSize { get; } = 50;
    public int FilteredCount { get; set; }
    public int TotalPages => (int)System.Math.Ceiling((double)FilteredCount / PageSize);

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
        var page = Page < 1 ? 1 : Page;
        var parms = new DynamicParameters();
        parms.Add("CompanyId", company.Id);
        parms.Add("Page", page);
        parms.Add("PageSize", PageSize);

        // Type'a göre sabit FROM/WHERE (tablo adı kullanıcıdan gelmiyor → injection yok)
        string cols, fromWhere;
        if (Type == "note")
        {
            cols = @"n.Id, n.Direction, n.NoteNo AS DocNo, NULL AS BankName, NULL AS BranchName,
                     n.DrawerName, n.Amount, n.Currency, n.IssueDate AS DocDate, n.DueDate, n.Status,
                     n.PartnerId, p.Name AS PartnerName, DATEDIFF(DAY, GETUTCDATE(), n.DueDate) AS DaysToDue";
            fromWhere = "FROM PromissoryNote n LEFT JOIN Partner p ON p.Id = n.PartnerId WHERE n.CompanyId = @CompanyId AND n.IsDeleted = 0";
        }
        else
        {
            cols = @"Id, Direction, ChequeNo AS DocNo, BankName, BranchName, DrawerName, Amount, Currency,
                     ChequeDate AS DocDate, DueDate, Status, PartnerId, PartnerName, DaysToDue";
            fromWhere = "FROM v_ChequePortfolio WHERE CompanyId = @CompanyId";
        }

        var filter = "";
        if (Direction != "all") { filter += " AND Direction = @Direction"; parms.Add("Direction", Direction); }
        if (Status != "all")    { filter += " AND Status = @Status";       parms.Add("Status", Status); }

        // Sayfa satırları + aynı filtrenin toplam sayısı tek round-trip (PF-1)
        var sql = $@"
            SELECT {cols} {fromWhere}{filter}
            ORDER BY DueDate ASC
            OFFSET (@Page - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;

            SELECT COUNT(*) {fromWhere}{filter};";

        using var grid = await conn.QueryMultipleAsync(sql, parms);
        Items = (await grid.ReadAsync<ChequeRowDto>()).ToList();
        FilteredCount = await grid.ReadSingleAsync<int>();
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
