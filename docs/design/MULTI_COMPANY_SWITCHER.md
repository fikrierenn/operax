# Operax — Çoklu Şirket (Multi-Company) Yönetimi ve Geçiş Mimarisi
**Versiyon:** 1.0 (Grup Şirket Desteği)  
**Kapsam:** Tek Kurulum Altında Birden Fazla Şirket ve Hızlı Geçiş Alt Yapısı  

Bu döküman, tek bir Operax kurulumu (veya single-tenant veritabanı) altında aynı müşteriye ait birden fazla farklı şirketin/şubenin (Örn: İthalat-İhracat firması, Lojistik firması, İmalat firması) nasıl izole bir şekilde yönetileceğini ve kullanıcının bu şirketler arasında anında nasıl geçiş yapacağını tanımlar.

---

## 1. TEMEL MİMARİ YAKLAŞIM

Operax, grup şirketleri yapısını desteklemek amacıyla **Claims-Based Company Binding** (Claim Tabanlı Şirket Bağlama) modelini kullanır:

1.  **Şirket Tanımları (`Company` Tablosu):** Her alt tüzel kişilik veya şube, `Company` tablosunda ayrı bir satır olarak tanımlanır.
2.  **Veri İzolasyonu (`CompanyId`):** Sistemdeki her ürün, cari hesap, depo, raf, sipariş ve stok hareketi doğrudan bir `CompanyId` kolonuna sahiptir ve bu kolona bağlıdır.
3.  **Kullanıcı Yetkilendirmesi (`UserCompany` Tablosu):** Bir kullanıcının hangi şirketlerde çalışmaya yetkili olduğu yetkilendirme tablosunda tutulur.
4.  **Aktif Bağlam (`ICurrentCompany`):** Kullanıcının tarayıcısındaki oturum cookie'sinde, o an aktif olarak çalıştığı şirketin Id'si (`company` claim'i) saklanır.

---

## 2. KULLANICI YETKİLERİ VE BAĞLANTI ŞEMASI

Hangi kullanıcının hangi şirketlere erişebileceğini yöneten `UserCompany` tablosu:

```sql
CREATE TABLE UserCompany (
    Id          UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId      NVARCHAR(450) NOT NULL, -- AspNetUsers.Id referansı
    CompanyId   UNIQUEIDENTIFIER NOT NULL, -- Company.Id referansı
    CreatedAt   DATETIME2 DEFAULT GETUTCDATE(),
    CONSTRAINT FK_UserCompany_User FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id) ON DELETE CASCADE,
    CONSTRAINT FK_UserCompany_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id) ON DELETE CASCADE
);

-- Hızlı arama için index
CREATE UNIQUE INDEX IX_UserCompany_User_Company ON UserCompany(UserId, CompanyId);
```

---

## 3. SEÇİLEN ŞİRKETİN DEĞİŞTİRİLMESİ (COOKIE RE-SIGNING)

Kullanıcı arayüzden başka bir şirkete geçmek istediğinde (Örn: *Operax Demo LTD* -> *Merkez Lojistik*), C# katmanındaki bir Page Handler veya API tetiklenir. Bu tetikleme, kullanıcının oturumunu kapatmadan **cookie içindeki şirket claim'ini günceller**:

```csharp
public class CompanySwitcherModel : PageModel
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly Db _db;

    public CompanySwitcherModel(
        UserManager<IdentityUser> userManager,
        SignInManager<IdentityUser> signInManager,
        Db db)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _db = db;
    }

    public async Task<IActionResult> OnPostSwitchCompanyAsync(Guid targetCompanyId)
    {
        var userId = _userManager.GetUserId(User);
        
        // 1. Güvenlik Kontrolü: Kullanıcı bu şirkete gerçekten yetkili mi?
        using var conn = _db.Open();
        var isAuthorized = await conn.ExecuteScalarAsync<bool>(
            "SELECT COUNT(1) FROM UserCompany WHERE UserId = @UserId AND CompanyId = @CompanyId",
            new { UserId = userId, CompanyId = targetCompanyId });

        if (!isAuthorized)
        {
            return BadRequest("Bu şirkete erişim yetkiniz bulunmamaktadır.");
        }

        // 2. Kullanıcı nesnesini yükle
        var user = await _userManager.FindByIdAsync(userId);
        
        // 3. Mevcut 'company' claim'ini kaldır ve yeni şirket claim'ini ekle
        var claims = await _userManager.GetClaimsAsync(user);
        var oldCompanyClaim = claims.FirstOrDefault(c => c.Type == "company");
        if (oldCompanyClaim != null)
        {
            await _userManager.RemoveClaimAsync(user, oldCompanyClaim);
        }
        await _userManager.AddClaimAsync(user, new System.Security.Claims.Claim("company", targetCompanyId.ToString()));

        // 4. Cookie'yi yenile (Kullanıcı logout olmadan oturum güncellenir)
        await _signInManager.RefreshSignInAsync(user);

        // 5. Sayfayı yenile — tüm veriler yeni şirket filtresiyle yüklenecektir
        return RedirectToPage("/Dashboard/Index");
    }
}
```

---

## 4. KULLANICI ARAYÜZÜ (UI) ENTEGRASYONU

Çift dil seçicimizin (TR/EN) hemen yanına, kullanıcının yetkili olduğu şirketleri listeleyen son derece şık bir **Şirket Seçici (Company Switcher) Dropdown**'ı yerleştirilir.

### A. Üst Barda Gösterim (`_Layout.cshtml` Güncellemesi):
```html
@if (CurrentUser.IsAuthenticated)
{
    <!-- Kullanıcının Yetkili Olduğu Şirketlerin Dropdown Olarak Render Edilmesi -->
    <div class="relative inline-block text-left mr-2">
        <form method="post" asp-page="/Admin/Settings/SwitchCompany" id="switchCompanyForm">
            <select name="targetCompanyId" onchange="this.form.submit()" class="bg-indigo-50 border border-indigo-100 rounded-full px-3 py-1 text-[11px] font-bold text-indigo-600 outline-none cursor-pointer hover:bg-indigo-100 transition-all">
                <!-- Arka planda kullanıcının yetkili şirket listesi doldurulur -->
                <option value="@CurrentCompany.Id" selected>@CurrentCompany.Name</option>
                @foreach (var company in Model.AuthorizedCompanies)
                {
                    @if(company.Id != CurrentCompany.Id)
                    {
                        <option value="@company.Id">@company.Name</option>
                    }
                }
            </select>
        </form>
    </div>
}
```

---

## 5. KAZANIMLAR VE SONUÇ

-   **%100 Modüler İzolasyon:** Her şirketin stok hareketleri, siparişleri ve faturaları kendi `CompanyId` filtresi altında izole kalır. Raporlama katmanında grup konsolide raporları çekmek istendiğinde ise tek bir SQL JOIN ile tüm grup performansı tek ekranda analiz edilebilir.
-   **Kullanıcı Dostu Hızlı Geçiş:** Kullanıcılar sistemden çıkıp tekrar girmek zorunda kalmadan, tek bir tıklamayla saniyeler içinde şirketler arasında güvenli bir şekilde geçiş yapabilirler.
-   **Yalın Kod Yönetimi:** Şirket bazlı dallanmalar için C# kodunda karmaşık `if-else` blokları yazılmaz; Dapper sorgularındaki standart `@CompanyId` parametresi tüm işi arka planda otomatik halleder.
