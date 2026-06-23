using Microsoft.AspNetCore.Authorization;
using Operax.Web.Lib;

namespace Operax.Web.Features.Dashboard;

// Mosaic (Cruip) tasarım DENEMESİ — gerçek Dashboard'a dokunmaz.
// IndexModel'i subclass ederek tüm anasayfa verisini (KPI'lar, listeler, OnGetAsync)
// SIFIR tekrar ile miras alır; yalnızca görünüm (.cshtml) farklı.
[Authorize]
public class MosaicModel(Db db, ICurrentCompany company) : IndexModel(db, company);
