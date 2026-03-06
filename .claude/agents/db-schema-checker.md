# Agent: DB Schema Checker
> Bu agent veritabanı şema dosyalarını ve çalışan DB'yi karşılaştırır.
> Görevi: Schema SQL dosyaları ile gerçek DB arasındaki farkları bul.

## Tetiklenme

- Yeni bir modüle geçmeden önce
- "schema kontrol et", "db checker", "tablo eksik mi" denildiğinde
- Seed data sorunları olduğunda

## Görevler

1. `docs/sql/` klasöründeki tüm schema dosyalarını listele
2. Her schema dosyasını oku — hangi tabloları oluşturuyor?
3. SQL Server'a bağlanarak mevcut tabloları listele (Operax.Cli aracılığıyla)
4. Eksik tabloları raporla
5. Seed data kontrolü: `seed_core.sql` çalıştırılmış mı?

## Schema Dosyaları ve Modüller

| Dosya | Modül | Tablolar |
|---|---|---|
| schema_M00.sql | Platform Core | Company, DictionaryType, DictionaryValue, Parameter, Module... |
| schema_M01.sql | Master Data | Item, Partner, Warehouse, Bin, ItemUOM, ItemBarcode... |
| schema_M02.sql | Inventory | StockMovement, InventoryBalance... |
| schema_M03.sql | Receiving | ReceivingHeader, ReceivingLine, PutawayTask... |
| schema_M04.sql | Sales Orders | SalesOrderHeader, SalesOrderLine... |
| schema_M05.sql | Shipping | ShippingHeader, ShippingLine... |
| schema_M06.sql | Picking | PickTask, PickTaskLine... |
| schema_M07.sql | Transfer | StockTransfer, StockTransferLine... |
| schema_M08.sql | Cycle Count | CycleCount, CycleCountLine... |
| schema_M09.sql | Traceability | LPN, Lot, Serial... |
| schema_M10.sql | Manufacturing | ProductionOrder, ItemBOM, WorkCenter... |

## Rapor Formatı

```
## DB Schema Kontrol — [tarih]

### Eksik Tablolar
| Tablo | Schema Dosyası | Öneri |
|---|---|---|
| ... | ... | SQL dosyasını çalıştır |

### Mevcut Tablolar (✅)
...

### Seed Data
- seed_core.sql: ✅ Çalıştırıldı / ❌ Eksik
- DictionaryType kayıt sayısı: X
- DictionaryValue kayıt sayısı: X
- Company kayıt sayısı: X
```
