# Mimari ve Tasarım Kuralları

Bu dosya, Operax platformunun temel mimari yapısını, veritabanı bağlantı yönetimi prensiplerini, evrak yaşam döngülerini ve klasör organizasyonunu tanımlar.

---

## 1. Single-Tenant (Müşteri Başına Bağımsız Dağıtım) Yaklaşımı

*   **Kullanıcı Kararı (26 Mayıs 2026):** Operax platformu, her müşteriye bağımsız veritabanı ve bağımsız sunucu kurulumu yapılacak şekilde (single-tenant per instance) kurgulanmıştır.
*   **Mimari Etkisi:**
    *   Veri tabanı ve uygulama seviyesinde dinamik multi-tenant veri izolasyon kontrolleri (kod içi yoğun CompanyId filtreleri) merkezi öncelik olmaktan çıkarılmıştır.
    *   Yine de şema bütünlüğünün korunması adına tablolardaki `CompanyId` kolonları silinmez, ancak kod geliştirmelerinde multi-tenant sızıntı risklerini engellemeye yönelik karmaşık sorgu bypass lojikleri yerine doğrudan tekil kurulum performansı gözetilir.

---

## 2. Dapper Veri Erişimi ve Performans

*   **Dapper Tek ORM İlkesi:** Projede Entity Framework Core kesinlikle kullanılmaz. Tüm veri erişimi, doğrudan T-SQL sorguları ve Dapper üzerinden yapılır.
*   **Bağlantı Yönetimi:**
    *   Veritabanı bağlantısı `Db` sınıfı üzerinden `using var conn = db.Open();` şeklinde açılır.
    *   Bağlantının `using` bloğu içerisinde tutulması ve işi bittiğinde otomatik kapanması zorunludur.
*   **Performans Önceliği:**
    *   Sorgularda select listesine sadece ihtiyaç duyulan sütunlar yazılmalıdır. `SELECT *` kullanımı kesinlikle yasaktır.
    *   Büyük detay verilerinde, performansı artırmak adına Dapper'ın `QueryMultipleAsync` metodu tercih edilmelidir.

---

## 3. Evrak Yaşam Döngüsü (DocStatus) ve Sabitler

Her belge/evrak (Receiving, Shipping, Transfer, CycleCount, SalesOrder vb.) deterministik bir yaşam döngüsünü takip eder:

```
[ DRAFT ] ──(Onayla / Post)──> [ POSTED ] ──(İptal Et / Cancel)──> [ CANCELLED ]
```

*   **Magic String Yasağı:** Kod içerisinde durum kontrolü yapılırken `"DRAFT"`, `"POSTED"` gibi sihirli string'ler doğrudan yazılamaz.
*   **DTO ve Sabitler:** `src/Operax.Web/Lib/Dtos.cs` içerisindeki `DocStatus` sınıfı veya ilgili Enum'lar kullanılmalıdır:
    *   `DocStatus.Draft`
    *   `DocStatus.Posted`
    *   `DocStatus.Cancelled`

---

## 4. SQL-First İş Mantığı Mimarisi (SQL-First Architecture)

*   **İş Mantığı ve Hesaplamaların SQL Katmanında Olması:**
    *   Tüm iş mantığı, onay kuralları, durum doğrulamaları (Örn: `dbo.sp_ValidateStatusTransition`), stok tahsisat algoritmaları, maliyet/fiyat hesaplamaları ve kompleks matematiksel formüller **C# katmanı yerine veritabanı katmanında (Stored Procedure, Function ve View'lar)** yazılmalıdır.
    *   C# kodu, sadece HTTP isteklerini yönlendiren, yetkilendirme yapan, Dapper ile veritabanındaki Stored Procedure'leri çağıran ve dönen hata/mesajları kullanıcıya gösteren **yalın bir orkestratör (lean controller)** olarak kalmalıdır.
*   **Müşteriye Özel Değişiklikler (Tenant Customization):**
    *   Müşterilerin özel iş akışları, ilave alan hesaplama mantıkları veya kargo/etiket doğrulama kuralları veritabanı katmanındaki ilgili Stored Procedure veya Function'ların revize edilmesiyle gerçekleştirilmelidir.
    *   Bu sayede core C# kod tabanı %100 temiz ve standart kalır; müşteriye özel versiyon (branch) yönetimi maliyetinden kaçınılır.
*   **Atomik Onay (Post) İşlemleri:**
    *   Bir evrak onaylandığında (DRAFT -> POSTED) evrak durum güncellemesi ve ilişkili stok hareketlerinin (`StockMovement`) yazılması **tek bir veritabanı transaction'ı** içerisinde gerçekleştirilmelidir.
    *   Onay metotlarında veritabanı düzeyinde transaction (`BEGIN TRANSACTION` / `COMMIT` / `ROLLBACK`) yönetilmeli ve hata durumunda SQL `THROW` ile Türkçe açıklayıcı hata fırlatılmalıdır.
*   **Stored Procedure Tercihi:** Çok adımlı, karmaşık ve veri yoğunluğu yüksek onay iş mantıklarında Stored Procedure kullanımı (C# içerisinden `commandType: CommandType.StoredProcedure` ile çağrılarak) zorunludur.

---

```

---

## 5. Ön Muhasebe ↔ Resmi Muhasebe Katmanı (TÜM İŞLEYİŞ İÇİN GEÇERLİ)

*   **Kullanıcı kararı (26 Haziran 2026):** Operax bir **ön muhasebe** (operasyonel alt-defter) sistemidir. **Resmi muhasebe katmanı (GL yevmiye fişi, TDHP hesap-kodlu mizan, 9xx nazım hesaplar, beyanname) HENÜZ YOK** — ileride ayrı modül olarak gelebilir.
*   **Değişmez ilke — mapping-ready tasarım:** Resmi katman olmasa da **her operasyonel işlem, gelecekteki resmi muhasebeye sorunsuz eşlenebilecek (posting-rule ile yevmiyeye dönüşebilecek) şekilde tasarlanır.** "Ön muhasebe yapıyoruz ama resmi muhasebeye uyumlu" — bu yapı tüm modüllerde (çek/senet, kredi, teminat, cari, kasa/banka, stok, fatura) geçerlidir.
*   **Pratik sonuçları:**
    *   **Yön + an doğru:** Her `AccountMovement`/`FinancialTransaction`/`StockMovement` doğru borç/alacak yönünde ve doğru ANDA (dönemsellik — `MovementDate`, alış-anı cari kapama vb.) yazılır → resmi katman bunu olduğu gibi posting'ler. Yanlış yön/an = ileride yevmiye yanlış.
    *   **Belge izi zorunlu:** `SourceDocType`+`SourceDocId` her harekette (belgelendirme kavramı) → posting-rule kaynağı bulur.
    *   **Off-balance ayrı:** Gerçek ledger'ı etkilemeyen ama resmi-muhasebede nazım gerektiren kalemler (kredi teminatı → 920/921, çek ciro → 91x, koşullu yükümlülük) **ayrı tabloda** (örn. `LoanCollateral`) **off-balance** tutulur; `FinancialTransaction`/`AccountMovement`'a YAZILMAZ — ama tür/grup/değer alanları (NazimGroup, ValuationType) saklanır ki resmi katman 9xx'e postlayabilsin.
    *   **TDHP kodu gömülmez:** Operasyonel tablolar TDHP hesap kodu taşımaz (101/320/920…); resmi eşleme posting-rule katmanında yapılır. Ön muhasebe semantiği (Direction, MovementType, InstrumentType) → resmi hesap kodu dönüşümü ileride.
*   **Tasarım kuralı:** Yeni finans/stok/evrak işleyişi yazarken sor: *"Bu işlem ileride yevmiyeye nasıl dönüşür? Doğru yön/an/belge-izi var mı? Off-balance mı?"* → `muhasebe-mevzuat` skill §2.5/§3 (nazım + subledger→GL posting deseni). Resmi-muhasebe uyumu, ön muhasebede "kayıt çalışıyor"dan önce gelir.

---

## 6. Modüler Aktivasyon Kuralları (MRP vs. Ticari vs. Proje)

*   **Modül Bağımlılığı Kontrolü:** Yeni bir modül geliştirildiğinde veya aktif edildiğinde, `docs/TODO.md` bağımlılık hiyerarşisine sadık kalınmalıdır.
*   **MRP / BOM Formül Güvenliği:** BOM veya reçete hesaplamalarında kullanıcı girdileri doğrudan değerlendirilmemeli, her zaman NCalc kütüphanesi kullanılmalıdır (`ncalc` paketi suppress uyarısıyla projeye dahildir).
*   **Basit vs. Gelişmiş Üretim Ayrımı:** Modül ayarlarında basit reçete (tek tıkla giriş) ile gelişmiş iş emri ve rota ( Hangfire background worker'lı WIP takibi) ayrı akışlar olarak tasarlanmalıdır.

---

## 7. WMS Sevkiyat ve Lojistik Kuralları

*   **Dalga Toplama (Wave Picking):** Picking modülünde toplama görevleri oluşturulurken birden fazla siparişin aynı rotada birleştirilebilmesi (`WaveNo`) desteklenmeli, terminal ekranı buna göre tasarlanmalıdır.
*   **Ambalaj ve Sevk Etiketleri (LPN / Carton):** Paketleme doğrulamalarında sevk kolisi (`Carton`) veya palet (`LPN`) etiketlerinin Zebra ZPL (`Operax.PrintServer`) formatında basılması ve sevk barkod doğrulamalarının el terminalinden yapılması sağlanmalıdır.
*   **Taşıyıcı ve Kargo Webhook Altyapısı:** Sevkiyatlarda kargo takip bilgileri `CarrierInfo` alanında tutulmalı, kargo webhook ve API entegrasyonları için `M16 (Integration Bridge)` yapısı kullanılmalıdır. Sevkiyat `POSTED` olduğu anda kargo firması API'sine asenkron entegrasyon isteği gönderilmelidir.
*   **WMS İşlem Otomasyon Seviyeleri (Manuel vs. Yarı Otomatik vs. Tam Otomatik):**
    *   **Manuel (Serbest) Mod:** FIFO/FEFO kontrolü yapılmaz. Depo personeli terminalde okuttuğu hedef/kaynak hücreyle işlemi manuel tamamlar.
    *   **Yarı Otomatik (Öneri) Mod:** Sistem `AllocationStrategy` parametresine göre hedef/kaynak hücre önerir ancak personelin terminalde bu öneriyi ezmesine (override) izin verilir.
    *   **Tam Otomatik (Kilitli) Mod:** Rota ve hücre sistem tarafından kilitlenir. Personel terminalde sistemin belirttiği hedef/kaynak barkodu dışında bir hücre taranamaz.

---

## 8. Dinamik Kullanıcı Tanımlı Alanlar (UDF) Kuralları

*   **Şema ve Veri Depolama:** Dinamik alanlar veritabanında yeni kolonlar olarak değil, ilgili tablolardaki `AdditionalFields` JSON kolonunda tutulur.
*   **Arayüz (UI) Render Kuralları:**
    *   Dinamik form alanları her zaman `UserFieldDefinition` tablosundaki tanımlara göre `_CustomFields.cshtml` Component'ı üzerinden dinamik render edilmelidir.
    *   **Varsayılan Değerler:** `DefaultValue = 'TODAY'` parametresi gelirse tarih alanına bugünün tarihi (`DateTime.Today`) dinamik basılmalıdır.
    *   **Güvenli Lookup (Veri Kaynağı):** `DataSourceType = 'TABLE'` olduğunda SQL injection açığı oluşturulmaması için Dapper sorgusu öncesi `DataSourceKey` (Örn: `Partner`, `Users`) beyaz listeye (whitelist) göre kontrol edilerek çalıştırılmalıdır.
*   **Veri Akış Zinciri (UDF Inheritance):** Siparişten Mal Kabule veya Sevkiyata dönüşüm yapılırken, satır bazlı dinamik JSON alanları (`AdditionalFields`) veritabanı onay SP'leri (`sp_ReceivingPost`, `sp_ShippingPost` vb.) veya C# servisleri tarafından doğrudan hedef tablonun `AdditionalFields` alanına kopyalanmalıdır.



