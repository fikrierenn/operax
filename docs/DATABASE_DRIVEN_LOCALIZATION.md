# Operax — Veritabanı Tabanlı Dinamik Çoklu Dil (Localization) Mimarisi
**Versiyon:** 1.0 (SQL-First Genişletme)  
**Kapsam:** Derleme/Derleme Olmadan Dinamik Dil ve Terminoloji Yönetimi  

Bu döküman, Operax Platformu'nun tüm kullanıcı arayüzü (UI) metinlerini, butonlarını, etiketlerini ve hata mesajlarını veritabanı düzeyinde dinamik olarak yöneten **Veritabanı Tabanlı Çoklu Dil (Database-Driven Localization) Mimarisi**'ni tanımlar.

---

## 1. MİMARİ VE TASARIM FİKRİ

Geleneksel ASP.NET Core uygulamalarında çoklu dil desteği `.resx` (Resource) dosyaları ile yapılır. Ancak bu yaklaşım:
1.  Her değişiklikte **C# kodunun yeniden derlenmesini** ve deploy edilmesini gerektirir.
2.  Müşterinin (single-tenant) kendi terminolojisini (Örn: "Cari Hesap" yerine "Müşteri / Tedarikçi" deme isteğini) kod düzeyinde dallanma yapmadan çözemez.

**Operax Veritabanı Tabanlı Dil Mimarisi**, iş kuralları ve veri yönetiminin SQL katmanında toplanması (SQL-First) vizyonunun arayüz katmanındaki karşılığıdır.

---

## 2. VERİTABANI ŞEMASI (`AppTranslation`)

Uygulamadaki tüm ekran etiketleri `AppTranslation` tablosunda saklanır:

```sql
CREATE TABLE AppTranslation (
    Id               UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CompanyId        UNIQUEIDENTIFIER NOT NULL, -- Sistem Şirketi '00000000-...' veya Müşteri Şirket Id
    Code             NVARCHAR(250) NOT NULL,    -- 'Common.Save', 'Items.Title', 'PO.Approve'
    ValueTr          NVARCHAR(MAX) NOT NULL,    -- 'Kaydet', 'Ürün Yönetimi', 'Siparişi Onayla'
    ValueEn          NVARCHAR(MAX) NOT NULL,    -- 'Save', 'Item Management', 'Approve Order'
    CreatedAt        DATETIME2 DEFAULT GETUTCDATE(),
    UpdatedAt        DATETIME2 NULL,
    CONSTRAINT FK_Translation_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id)
);

-- Hızlı arama ve mükerrer kayıt önleme index'i
CREATE UNIQUE INDEX IX_AppTranslation_Code ON AppTranslation(CompanyId, Code);
```

---

## 3. PERFORMANS VE BELLEK YÖNETİMİ (IN-MEMORY CACHE)

Her sayfa render edildiğinde onlarca etiket için veritabanına sorgu atmak performansı olumsuz etkiler. Bu engeli aşmak için **In-Memory Thread-Safe Cache** yapısı kurulur:

1.  **Uygulama Başlangıcında Yükleme (Warm-Up):** Uygulama ayağa kalkarken veya ilk istekte veritabanındaki tüm aktif çeviriler okunur ve C# tarafında `ConcurrentDictionary` nesnesine doldurulur.
2.  **Bellekten Hızlı Erişim (0 ms):** Arayüzdeki `@L.T(...)` çağrıları doğrudan bellekten (RAM) beslenir.
3.  **Önbellek Temizleme (Cache Eviction / Live Reload):** Yönetici paneli üzerinden bir çeviri güncellendiğinde, bellekteki dictionary otomatik olarak sıfırlanır (veya SignalR / DB trigger ile tetiklenerek) ve sonraki isteklerde güncel veriyi tablodan tekrar yükler.

---

## 4. OTO-KEŞİF VE OTOMATİK SEED (AUTO-DISCOVERY / AUTO-SEED)

Geliştiricinin her yeni eklediği arayüz etiketi için veritabanına manuel kayıt girmesini önlemek amacıyla **Akıllı Keşif (Auto-Discovery)** mekanizması uygulanır:

```csharp
public static class L
{
    private static ConcurrentDictionary<string, (string Tr, string En)> _cache = new();
    private static bool _isLoaded = false;

    public static string T(string code, string defaultTr, string defaultEn)
    {
        // 1. Önbellek boşsa veritabanından yükle
        if (!_isLoaded) EnsureCacheLoaded();

        // 2. Bellekte varsa aktif kültüre göre dön
        if (_cache.TryGetValue(code, out var trans))
        {
            return IsEn ? trans.En : trans.Tr;
        }

        // 3. Bellekte YOKSA (Yeni bir etiket keşfedildi!):
        // Arka planda veritabanına bu varsayılan değerlerle insert et (Auto-Seed)
        Task.Run(() => AutoSeedTranslationAsync(code, defaultTr, defaultEn));

        // Belleğe geçici olarak yaz
        _cache[code] = (defaultTr, defaultEn);

        return IsEn ? defaultEn : defaultTr;
    }
}
```

### Bu Yapının Avantajları:
-   **Sıfır Manuel Giriş:** Geliştirici sadece kod yazar. Sayfa ilk kez yüklendiğinde, kullanılan tüm kelimeler veritabanına `AppTranslation` tablosuna otomatik olarak varsayılan Türkçe ve İngilizce karşılıklarıyla kaydolur.
*   **Tam Müşteri Kontrolü:** Sistem kurulduktan sonra, müşteri yönetici panelindeki **"Dil / Terminoloji Editörü"** ekranına girdiğinde oto-keşif sayesinde tüm kelimeleri listede hazır bulur ve istediği gibi güncelleyebilir.

---

## 5. RAZOR VIEW KULLANIM STANDARDI

Razor sayfalarında kullanımı son derece basit ve yalındır:

```html
<!-- Başlık -->
<h1 class="text-2xl font-bold">@L.T("Items.Title", "Ürün Yönetimi", "Item Management")</h1>

<!-- Form Input Label -->
<label class="text-xs text-slate-400">@L.T("Common.Warehouse", "Depo", "Warehouse")</label>

<!-- Arama Placeholder -->
<input type="text" placeholder="@L.T("Common.Search", "Ara...", "Search...")" class="form-ctrl" />

<!-- Buton -->
<button type="submit" class="btn btn-primary">
    @L.T("Common.Save", "Kaydet", "Save")
</button>
```

---

## 6. SQL KATMANINDA ÇOKLU DİL DESTEĞİ

SQL Server Stored Procedure'ları veya View'ları içinde hata fırlatırken de bu tablodan dil parametresine göre dinamik hata mesajı çekilebilir:

```sql
CREATE PROCEDURE dbo.sp_GetTranslatedMessage
    @CompanyId UNIQUEIDENTIFIER,
    @Code NVARCHAR(250),
    @Language NVARCHAR(10), -- 'tr' veya 'en'
    @Message NVARCHAR(MAX) OUTPUT
AS
BEGIN
    SELECT @Message = CASE WHEN @Language = 'en' THEN ValueEn ELSE ValueTr END
    FROM AppTranslation
    WHERE CompanyId = @CompanyId AND Code = @Code;

    IF @Message IS NULL
        SET @Message = @Code; -- Bulunamazsa kodu dön
END
```

Bu sayede sadece C# arayüzü değil, veritabanından fırlatılan iş mantığı hataları da kullanıcının seçtiği dile göre (Türkçe veya İngilizce) otomatik olarak veritabanı katmanında çözülmüş olur.

---

## 7. SONUÇ

Bu veritabanı tabanlı dil mimarisi, Operax'ın **"Sıfır C# kod değişikliği ile maksimum müşteri uyarlaması"** hedefini taçlandırmaktadır. Müşteri, hiçbir yazılımcıya ihtiyaç duymadan uygulamanın tüm dil yapısını ve dikey terimlerini kendi kurumsal kültürüne göre baştan aşağı özelleştirebilir.
