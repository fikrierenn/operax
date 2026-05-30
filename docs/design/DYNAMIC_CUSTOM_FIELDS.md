# Operax — Dinamik Kullanıcı Tanımlı Alanlar (UDF) Mimarisi
**Versiyon:** 2.2 (Gelişmiş Vizyon)  
**Kapsam:** Core Modüllere Dokunmadan Müşteriye Özel Alan Ekleme Mimarisi  

Bu döküman, Operax Platformu'nun çekirdek (core) kod yapısına ve standart veritabanı şemasına müdahale etmeden, her müşterinin kendine özgü veri takip ihtiyaçlarını (tekstil, kitap, gıda, endüstriyel parça vb. tüm sektörlere uyumlu) esnek bir şekilde karşılamasını sağlayan **Meta-Veri Tabanlı JSON / EAV Hibrit UDF (User Defined Fields)** mimarisini detaylandırır.

---

## 1. GİRİŞ VE VİZYON

E-Ticaret ve WMS/MRP projelerinde karşılaşılan en büyük zorluklardan biri, dikey sektörlerin (Örn: Tekstil için *Beden/Renk*, Kitap için *Yazar/ISBN/Yayınevi*, Gıda için *Alerjen/Sertifika*) veri takip alanlarının standart çekirdek şemada bulunmamasıdır. 

Her yeni alan için veritabanına kolon eklemek ve C# sınıflarını (Entity modelleri, DTO'lar, Arayüz ekranları) güncellemek hem bakım maliyetini artırır hem de çoklu dağıtımlarda (multi-instance/tenant) versiyon yönetimini imkansız hale getirir.

**Operax UDF Vizyonu**, Core kodlara dokunmadan:
1.  Herhangi bir master karta (Ürün, Cari, Depo vb.) veya hareket satırına (Sipariş Satırı, İrsaliye/Mal Kabul Satırı vb.) dinamik alanlar eklenmesini,
2.  Eklenen alanlara veri tipleri (Metin, Sayı, Tarih, Seçim Listesi) atanabilmesini,
3.  Varsayılan değerler tanımlanabilmesini veya dinamik sözlük/tablo ilişkileri kurulmasını (Lookup),
4.  Tüm arayüzlerde bu alanların otomatik olarak render edilip formlarda doldurulabilmesini,
5.  Siparişten faturaya dönüşüm gibi zincirleme işlemlerde bu verilerin otomatik olarak sonraki aşamalara miras (Inheritance) kalmasını sağlar.

---

## 2. HİBRİT VERİ MODELİ (JSON + EAV)

Operax, geleneksel hantal **EAV (Entity-Attribute-Value)** yapısının getirdiği karmaşık JOIN sorguları ve düşük performans sorunlarını aşmak için **JSON tabanlı dinamik veri saklama** yöntemini kullanır.

### A. Veritabanı Şeması
Dinamik alan eklenmesi muhtemel tüm master ve hareket tablolarına `AdditionalFields` kolonu eklenmiştir:
```sql
ALTER TABLE Item ADD AdditionalFields NVARCHAR(MAX) NULL; -- JSON formatında veriler
ALTER TABLE Partner ADD AdditionalFields NVARCHAR(MAX) NULL;
ALTER TABLE SalesOrderLine ADD AdditionalFields NVARCHAR(MAX) NULL;
ALTER TABLE ReceivingLine ADD AdditionalFields NVARCHAR(MAX) NULL;
```

Bu kolonda veriler düz bir JSON nesnesi olarak saklanır:
```json
{
  "CustomBeden": "L",
  "CustomRenk": "Mavi",
  "CustomYazar": "Ömer Seyfettin",
  "CustomHediyeNotu": "Doğum günün kutlu olsun!"
}
```

### B. SQL Server ve Dapper ile Sorgulama
SQL Server 2022'nin yerleşik JSON fonksiyonları sayesinde bu alanlar üzerinde doğrudan filtreleme, sıralama ve raporlama yapılabilir:
```sql
-- Örnek: 'CustomBeden' değeri 'L' olan ürünleri filtreleme
SELECT Id, Code, Name, 
       JSON_VALUE(AdditionalFields, '$.CustomBeden') AS Beden
FROM Item
WHERE JSON_VALUE(AdditionalFields, '$.CustomBeden') = 'L';
```

Aynı şekilde veritabanı seviyesinde performans kazanmak için sık sorgulanan JSON özellikleri üzerinde **Hesaplanmış Kolon (Computed Column)** veya **Filtered Index** oluşturulabilir.

---

## 3. METADATA TANIM TABLOSU (`UserFieldDefinition`)

Hangi tablolarda hangi dinamik alanların yer alacağını, tiplerini ve kısıtlarını tanımlayan metadata tablosu `UserFieldDefinition` şöyledir:

```sql
CREATE TABLE UserFieldDefinition (
    Id               UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CompanyId        UNIQUEIDENTIFIER NOT NULL,
    EntityName       NVARCHAR(100) NOT NULL,    -- 'Item', 'Partner', 'SalesOrderLine', 'ReceivingLine'
    FieldName        NVARCHAR(100) NOT NULL,    -- 'CustomRenk' (JSON içindeki anahtar)
    LabelText        NVARCHAR(200) NOT NULL,    -- 'Ürün Rengi' (UI ekranındaki etiket)
    FieldType        NVARCHAR(50) NOT NULL,     -- 'TEXT', 'NUMBER', 'DATE', 'SELECT', 'BOOLEAN'
    DefaultValue     NVARCHAR(MAX) NULL,         -- Varsayılan değer (Örn: 'true', 'TODAY')
    DataSourceType   NVARCHAR(50) NULL,          -- 'STATIC', 'DICTIONARY', 'TABLE'
    DataSourceKey    NVARCHAR(250) NULL,        -- Dropdown için kaynak anahtarı
    OrderNo          INT DEFAULT 0,              -- UI gösterim sırası
    IsRequired       BIT DEFAULT 0,              -- Zorunlu alan kontrolü
    CreatedAt        DATETIME2 DEFAULT GETUTCDATE(),
    IsDeleted        BIT DEFAULT 0,
    CONSTRAINT FK_UDF_Company FOREIGN KEY (CompanyId) REFERENCES Company(Id)
);
```

### Alan Tipleri ve Özellikleri:
1.  **`TEXT`:** Düz metin girişi. `textarea` veya standart `input[type="text"]` olarak render edilir.
2.  **`NUMBER`:** Sayısal veri girişi. `input[type="number"]` olarak render edilir.
3.  **`DATE`:** Tarih verisi. UI üzerinde tarih seçici (Datepicker) olarak render edilir.
4.  **`BOOLEAN`:** Evet/Hayır seçeneği. Switch veya Checkbox olarak render edilir. Varsayılan değer `true` veya `false` olabilir.
5.  **`SELECT`:** Seçim listesi. `DataSourceType` özelliğine göre dropdown oluşturur.

---

## 4. DİNAMİK VERİ KAYNAKLARI (LOOKUP / DATA SOURCE)

Seçim listesi (`SELECT`) tipindeki alanların alacağı değerler statik bir listeden, sistem sözlüklerinden veya doğrudan diğer tablolardan dinamik olarak çekilebilir:

### A. Statik Kaynak (`DataSourceType = 'STATIC'`)
`DataSourceKey` alanında virgülle ayrılmış değerler veya basit bir JSON dizisi tutulur.
*   **DataSourceKey:** `Siyah, Beyaz, Kırmızı, Mavi`
*   **UI Çıktısı:** Bu renklerden oluşan basit bir dropdown listesi.

### B. Sözlük Kaynağı (`DataSourceType = 'DICTIONARY'`)
Sistemdeki `DictionaryType` ve `DictionaryValue` yapısını kullanır.
*   **DataSourceKey:** `BRAND` (Marka sözlüğü) veya `CARRIER_TYPE` (Taşıyıcı tipi)
*   **UI Çıktısı:** `DictionaryValue` tablosundan aktif olan tüm değerleri okuyup dropdown'a doldurur.

### C. Tablo İlişkisi (`DataSourceType = 'TABLE'`)
İlave alanın sistemdeki başka bir Master veri tablosu ile ilişkili olmasını sağlar.
*   **DataSourceKey:** `Partner` (Cari listesi), `Warehouse` (Depo listesi), `Item` (Ürün listesi) veya `Bin` (Raf/Göz listesi)
*   **Güvenlik (SQL Injection Koruması):** C# katmanında, `DataSourceKey` doğrudan ham SQL sorgusuna eklenmez. Bunun yerine tanımlanmış güvenli bir **Tablo Beyaz Listesi (Whitelist Table Mapper)** kullanılır:
    ```csharp
    public static class UdfWhitelist
    {
        private static readonly Dictionary<string, string> AllowedTables = new()
        {
            { "Partner", "SELECT Id AS Value, Name AS Text FROM Partner WHERE IsActive = 1 AND IsDeleted = 0" },
            { "Warehouse", "SELECT Id AS Value, Name AS Text FROM Warehouse WHERE IsActive = 1 AND IsDeleted = 0" },
            { "Item", "SELECT Id AS Value, Name AS Text FROM Item WHERE IsActive = 1 AND IsDeleted = 0" }
        };

        public static string GetQuery(string key)
        {
            if (AllowedTables.TryGetValue(key, out var query)) return query;
            throw new UnauthorizedAccessException("Geçersiz tablo UDF ilişkisi!");
        }
    }
    ```

---

## 5. DİNAMİK UI MOTORU VE C# ENTEGRASYONU

Dinamik alanların arayüzlerde gösterilmesi ve kaydedilmesi iki aşamalı çalışır.

### A. Razor Component ile Otomatik Render (`_CustomFields.cshtml`)
Form tasarımlarında dinamik alanların yer alması istenen yere ortak bir Razor Component çağrısı eklenir:
```html
@await Html.PartialAsync("_CustomFields", new UdfViewModel { 
    EntityName = "Item", 
    EntityId = Model.ItemId, // Düzenleme modunda ise mevcut JSON'ı çözmek için
    CurrentValuesJson = Model.AdditionalFields 
})
```

Component arka planda şu mantıkla çalışır:
1.  Giriş yapan kullanıcının şirketi (`CompanyId`) ve `EntityName = 'Item'` filtresine göre `UserFieldDefinition` tablosundan aktif tanımları okur.
2.  Eğer `EntityId` varsa (düzenleme modu) `AdditionalFields` kolonundaki JSON nesnesini `Dictionary<string, string>` olarak deserialize eder.
3.  Tanımlardaki her bir satır için ilgili HTML etiketlerini oluşturur:
    -   `IsRequired = 1` ise HTML5 `required` niteliği eklenir.
    -   `DefaultValue` tanımlıysa ve yeni kayıt ekranıysa değer otomatik basılır (Örn: `DefaultValue = 'TODAY'` -> Bugünü basar).
    -   `DataSourceType = 'TABLE'` ise whitelisted metot üzerinden ilgili dropdown verileri doldurulur.

### B. Form Kayıt Aşaması (Model Binding & Dapper)
Arayüz formu POST edildiğinde dinamik alanlar form verileri arasından ayırt edilerek yakalanır:
1.  Form elemanlarının adlandırılması standartlaştırılır: `UDF_CustomRenk`, `UDF_CustomBaskiNotu` vb.
2.  C# Controller/Page Handler seviyesinde istekteki form parametreleri taranarak `UDF_` önekiyle başlayan değerler bir `Dictionary<string, string>` içine toplanır.
3.  Elde edilen dictionary nesnesi `System.Text.Json` yardımıyla JSON string'e dönüştürülür ve Dapper parametresi olarak gönderilir:
    ```csharp
    var udfValues = Request.Form.Keys
        .Where(k => k.StartsWith("UDF_"))
        .ToDictionary(k => k.Replace("UDF_", ""), k => Request.Form[k].ToString());

    string additionalFieldsJson = JsonSerializer.Serialize(udfValues);

    // Dapper ile Güncelleme
    string query = "UPDATE Item SET Name = @Name, AdditionalFields = @AdditionalFields WHERE Id = @Id";
    await _db.ExecuteAsync(query, new { Id, Name, AdditionalFields = additionalFieldsJson });
    ```

---

## 6. EVRAKLAR ARASI OTOMATİK TAŞIMA (UDF INHERITANCE)

Operax vizyonunun en güçlü halkalarından biri, belgeler arasındaki zincirleme veri akışıdır (Örn: Satış Siparişi -> Çıkış Sevkiyatı -> Paketleme -> e-Fatura).

### Senaryo:
Müşteri e-ticaret siparişini verirken ürün satırına özel bir hediye notu (`CustomGiftNote`) yazmıştır:
1.  **Satış Siparişi Satırı (`SalesOrderLine`):** `AdditionalFields = '{"CustomGiftNote":"Doğum günü kartı eklensin."}'`
2.  **Onay Süreci:** Sipariş onaylanıp `ShippingLine` (Sevkiyat Toplama Satırı) oluşturulduğunda, onay Stored Procedure'u (`sp_SalesOrderPost` veya benzeri bir C# servisi) kaynak satırdaki `AdditionalFields` verisini **core koda dokunmadan** doğrudan hedef satıra kopyalar:
    ```sql
    -- Siparişten Sevkiyata Dönüşüm Sırasında UDF Taşıma Örneği
    INSERT INTO ShippingLine (Id, HeaderId, SalesOrderLineId, ItemId, QtyOriginal, AdditionalFields)
    SELECT NEWID(), @ShippingHeaderId, sol.Id, sol.ItemId, sol.QtyOrdered, sol.AdditionalFields
    FROM SalesOrderLine sol
    WHERE sol.HeaderId = @SalesOrderHeaderId;
    ```
3.  **Paketleme Ekranı:** Depo personeli paketleme yaparken sevkiyat satırındaki `AdditionalFields` içindeki notu el terminalinde veya paketleme ekranında otomatik olarak görür.
4.  **Fatura Satırı (`InvoiceLine`):** Paketleme onaylandığında fatura satırına da aynı UDF alanı miras kalır ve fatura şablonunda bu UDF alanı basılabilir.

Bu mimari sayesinde core sistemdeki hiçbir sınıf, metod veya tablo kolonu değişmeden, tamamen dinamik SQL / JSON kopyalaması ile uçtan uca özel veri takibi sağlanmış olur.

---

## 7. SONUÇ VE KAZANIMLAR

-   **%100 Core Bağımsızlık:** Herhangi bir müşteriye özel alan eklemek için tek bir C# satırı yazmaya veya veritabanı şemasını (kolon ekleme) bozmaya gerek kalmaz.
-   **Tüm Sektörlere Uyum:** Operax tek bir kod tabanıyla tekstilciden kitapçıya, yedek parçacıdan gıdacıya kadar her sektöre dakikalar içinde uyarlanabilir hale gelir.
-   **Gelişmiş Validasyon ve Tiplendirme:** Statik listeler, sistem sözlükleri ve whitelist onaylı ilişkili veritabanı tablolarıyla zengin veri kısıtları sağlanır.
-   **Kusursuz Entegrasyon:** Siparişten faturaya otomatik miras kalma yapısı operasyonel doğruluğu en üst düzeye çıkarır.
