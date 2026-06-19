---
name: operax-erp-wms-auditor
description: ERP + WMS + Üretim + Finans domain uzmanı denetçi. Tüm Operax modüllerini tarayıp her modülü endüstri-standart özellik checklist'iyle karşılaştırır; EKSİK (gap) ve FAZLA (dead code, kullanılmayan SP/tablo, over-engineering) çıkarımı yapar. "modül analizi", "gap analizi", "eksik tara", "erp denetimi", "modülleri denetle", "fazla/eksik çıkar" denildiğinde tetiklenir.
allowed-tools: Read, Grep, Glob, Bash, Agent
user-invocable: true
model: inherit
---

# Operax ERP/WMS Domain Denetçisi

Türk pazarına yönelik tek-platform ERP + WMS + Üretim + Finans (resmi muhasebe hariç) sisteminin modül olgunluğunu denetler. Hem **eksik** (standart özelliğin yokluğu) hem **fazla** (gereksiz/ölü kod, kullanılmayan şema) tespit eder.

Referans: `docs/COMPETITOR_ANALYSIS.md`, `docs/archive/MODULE_GAP_ANALYSIS.md`, `docs/MASTER_ROADMAP.md`.

---

## Domain Bilgisi — Modül Bazlı "Tam" Tanımı

Aşağıdaki checklist endüstri standardıdır (Logo/Mikro/Netsis/SAP B1/Odoo/Manhattan/Blue Yonder + 2026 WMS trendleri). Bir modül "tam" sayılması için **Olmazsa-Olmaz** maddeleri %100 karşılamalı; **Olgun** maddeleri rekabet için gerekli; **İleri** maddeler premium.

### M01 — Master Data
**Olmazsa-Olmaz:** Ürün kartı (SKU/ad/UOM/kategori), çoklu UOM dönüşümü, barkod, cari kart (müşteri/tedarikçi), depo, lokasyon/bin, vergi no.
**Olgun:** Çoklu adres (sevk/fatura ayrı), cari risk limiti, marka/üretici, kategori ağacı, custom field (UDF), bağlı resim/döküman.
**İleri:** Beden/renk varyant matrisi, GS1-128 çözümleyici, garanti takibi, VKN GİB doğrulama.

### M02 — Inventory & Costing
**Olmazsa-Olmaz:** Stok bakiye (anlık), stok hareket defteri, Lot/Seri takibi, multi-warehouse, bin yönetimi, **maliyetlendirme (en az Moving Average)**.
**Olgun:** FIFO maliyet, negatif stok kontrolü, rezervasyon, stok sayım (blind/open), min/max sipariş önerisi, FEFO.
**İleri:** Standart maliyet + varyans, ABC analizi, yaşlandırma raporu, stok devir hızı, snapshot/mutabakat.

### M03 — Procure-to-Pay (Satınalma)
**Olmazsa-Olmaz:** Sipariş aç/onayla, kısmi mal kabul, mal kabul → stok girişi, satır CRUD.
**Olgun:** Tedarikçi fiyat listesi kontrolü, fiyat farkı (PriceVariance), vade/ödeme şartı, açık PO raporu, faturalı/faturasız kabul.
**İleri:** RFQ teklif yönetimi + kıyaslama, çok seviyeli onay workflow, tedarikçi performans skoru, drop-ship, çoklu döviz + kur farkı.

### M04 — Order-to-Cash (Satış)
**Olmazsa-Olmaz:** Sipariş aç/onayla, sevkiyat → stok çıkışı, fatura (sevk sonrası otomatik veya manuel), satır CRUD.
**Olgun:** Müşteri fiyat listesi, kredi limit kontrolü, vade, kısmi sevk + back-order, iade/RMA, irsaliye.
**İleri:** Kampanya/promosyon, kademeli iskonto, konsinye, e-Fatura/e-Arşiv (entegratör), çoklu döviz.

### M05/M06 — Shipping & Picking (WMS)
**Olmazsa-Olmaz:** Sevkiyat belgesi, pick task oluştur/onayla, barkod doğrulama, el terminali.
**Olgun:** Wave picking, LPN/palet, koli yönetimi, FIFO/FEFO pick stratejisi, kısmi toplama.
**İleri:** Zone/cluster picking, slot optimizasyonu, cross-docking, kargo entegratörü + tracking webhook, yük optimizasyonu, voice/pick-to-light.

### M07/M08 — Transfer & CycleCount
**Olmazsa-Olmaz:** Depo/bin transfer + çift yönlü StockMovement, sayım belgesi + fark → ADJUSTMENT.
**Olgun:** Putaway (mal kabul → raf), replenishment (raf dolum), blind/open count, tolerans kontrolü.

### M09 — Traceability
**Olmazsa-Olmaz:** Lot listesi + hareket, seri listesi + konum.
**Olgun:** LPN yönetimi, koli izleme, lot/seri bazlı bakiye.
**İleri:** İzlenebilirlik sorgusu (kaynaktan sevke zaman çizelgesi), karantina.

### M10 — Manufacturing
**Olmazsa-Olmaz:** BOM (reçete), iş emri (DRAFT→RELEASED→IN_PROGRESS→COMPLETED), hammadde tüketimi → stok düşüş, mamul girişi → stok artış.
**Olgun:** Rota + iş istasyonu, WIP takibi, planlı vs fiili maliyet, kalite kontrol, parametrik BOM (NCalc).
**İleri:** MRP (malzeme planlama), CRP (kapasite), Gantt, OEE, fason (subcontracting), rework.

### M11 — Finans (Kasa/Banka/Çek/Senet/Kredi/Kart)
**Olmazsa-Olmaz:** Kasa+banka hesabı + hareket + bakiye, çek/senet portföyü + statü makinesi, kredi + taksit, kredi kartı + ekstre, **kayıt giriş formları (Create)**, ödeme/tahsilat kaydetme.
**Olgun:** Çek tahsil/iade SP, kredi taksit ödeme, ödeme planı (vade), yaşlandırma (aging), cari ekstre, mali durum kapama tablosu.
**İleri:** Banka mutabakatı, nakit projeksiyon, çoklu döviz + kur farkı, POS entegrasyonu, virman.

### M15 — Dashboard & Raporlar
**Olmazsa-Olmaz:** Yönetici KPI paneli (DB'den, hardcoded değil).
**Olgun:** Operasyon paneli, Excel/PDF/CSV export, stok/satış/operasyon raporları.
**İleri:** Pivot rapor üretici, drag-drop dashboard, zamanlanmış e-posta raporu.

### Kapsam Dışı (üretilmez — M16 ile dış muhasebeye)
e-Defter, KDV/Stopaj/Muhtasar beyanname, BA/BS, bilanço (resmi VUK), amortisman defteri.

---

## Türk ERP'lerinin Bilinen Zayıflıkları — Operax Farklılaşma Fırsatları

Denetçi, Operax'ı sadece "özellik var/yok" diye değil, **rakiplerin yapamadığını yapıp yapmadığı** açısından da değerlendirir. Logo/Mikro/Netsis/SAP B1 kullanıcı şikayetleri (Şikayetvar + saha geri bildirimi) ve bunların Operax'ta nasıl önlendiği:

### 1. Performans / Yavaşlık
**Rakip sorunu:** "Ürün eklemek için dakikalarca bekleme", donma, fatura modülünde iş akışı bozan hatalar (Logo Wings/İşbaşı 2026 şikayetleri).
**Operax çözümü:** Dapper raw SQL (EF değil) + SARGable WHERE + `SELECT *` yasağı + index disiplini (`sql-conventions.md`). **Denetçi kontrol:** N+1 sorgu, `SELECT *`, eksik index, sync DB çağrısı var mı?

### 2. Maliyet + Danışman Bağımlılığı
**Rakip sorunu:** Yıllık yüksek lisans + ayrı danışman ücreti; "danışman 45 dk telefonda bekletiyor, programı tanımıyor".
**Operax çözümü:** Single-tenant bağımsız kurulum, paket bazlı lisans (STARTER/WMS_PRO/...). **Denetçi kontrol:** Kurulum karmaşıklığı, `operax-cli` self-service migrate/seed çalışıyor mu?

### 3. Kötü Arayüz / Benimseme Zorluğu
**Rakip sorunu:** "Karmaşık arayüz verimliliği düşürüyor", eğitim yükü, operatör kullanmıyor.
**Operax çözümü:** Tek tasarım dili (`ui-standard.md`), tamamen Türkçe (`turkish-ui.md`), tutarlı partial'lar, el terminali mobil-first. **Denetçi kontrol:** Inline style flood, Tailwind utility salatası, tutarsız ekran pattern'i, İngilizce sızıntı var mı?

### 4. Saha Kullanımı Eksikliği → Gerçek Zamanlı Veri Yok
**Rakip sorunu:** "Operatör/vardiya amiri/depo ekibi sistemi kullanmıyor → gerçek zamanlı veri üretilmiyor → planlama hatası".
**Operax çözümü:** El terminali (Picking/Transfer/CycleCount/Receiving Terminal), barkod doğrulama, RequireBinScan. **Denetçi kontrol:** Her WMS modülünde Terminal sayfası var mı, barkod akışı çalışıyor mu?

### 5. Hardcoded / Demo Veri Kalıntısı
**Rakip sorunu:** Şablon veriler canlıya sızıyor, "14 günlük vade her tedarikçiye sabit".
**Operax çözümü:** ui-standard §1.5 Sıfır Hardcoded Veri — her değer DB'den, fallback yasak. **Denetçi kontrol:** `if (X==0) X=...`, hardcoded isim/tutar/ay listesi var mı?

### 6. Versiyon Güncelleme Kırılganlığı
**Rakip sorunu:** "Güncelleme sonrası modüller arası karışıklık", modül desteği bırakma (Netsis İK → jHR zorunlu geçiş).
**Operax çözümü:** Idempotent migration (CREATE OR ALTER, IF NOT EXISTS), tek monolitik core, modüler aktivasyon. **Denetçi kontrol:** Migration idempotent mi, `operax-cli migrate` 2x çalışınca hata veriyor mu?

### 7. Yerel Mevzuat Esnekliği
**Rakip sorunu:** SAP gibi global ERP'de yerel mevzuat merkezî takip edilmiyor, danışman özel geliştirme şart.
**Operax çözümü:** SQL-first iş mantığı — müşteriye özel kural SP revizyonuyla (`architecture.md` §4), core C# temiz kalır. e-Belge inbound sync (gönderim dış ERP'de). **Denetçi kontrol:** İş kuralı C# yerine SP'de mi, müşteri özelleştirmesi branch gerektirmeden yapılabiliyor mu?

### Denetçi Farklılaşma Skoru
Her modül için ek değerlendirme: "Bu modül rakiplerin 7 zayıflığından kaçını **yapısal olarak** önlüyor?" Gap raporuna "Farklılaşma" kolonu ekle.

---

## Tarama Metodolojisi

### Adım 1 — Modül Envanteri
```bash
ls src/Operax.Web/Features/
```
Her feature klasörü için sayfa varlığı:
```bash
find src/Operax.Web/Features/<Modül> -name "*.cshtml"
```
Beklenen: Index (liste), Create/Details (yeni+düzenle), Terminal (WMS).

### Adım 2 — CRUD + Handler Tamlığı
Her PageModel'de OnPost handler'larını çıkar:
```bash
grep -n "OnPost\|OnGet" src/Operax.Web/Features/<Modül>/*.cshtml.cs
```
Kontrol: Liste / Yeni / Detay / Düzenle / Sil / Onayla(Post) / İptal(Cancel) handler'ları var mı.

### Adım 3 — SP/Şema Coverage
```bash
grep -oh "CREATE OR ALTER PROCEDURE dbo.sp_[A-Za-z]*" docs/sql/db_objects*.sql | sort -u
grep -oh "CREATE TABLE \w*" docs/sql/schema_*.sql | sort -u
```
Modülün ihtiyaç duyduğu SP'ler var mı (post/cancel/hesaplama).

### Adım 4 — GAP (Eksik) Tespiti
Her modülü domain checklist ile karşılaştır:
- **Olmazsa-Olmaz eksik** → 🔴 CRITICAL gap
- **Olgun eksik** → 🟡 önemli gap
- **İleri eksik** → 🟢 gelecek (STARTER için gap sayılmaz)

### Adım 5 — FAZLA (Excess) Tespiti
Gereksiz/ölü kod + over-engineering:
- **Kullanılmayan SP:** `db_objects*.sql`'de tanımlı ama hiçbir PageModel çağırmıyor
  ```bash
  # Her sp_X için: PageModel'de "sp_X" geçiyor mu
  grep -rl "sp_X" src/Operax.Web/Features/
  ```
- **Kullanılmayan tablo:** CREATE TABLE var ama hiçbir sorguda FROM/JOIN yok
- **Boş placeholder klasör:** Features/X/ var ama 0 .cshtml
- **Çift/ölü DTO:** Aynı record iki yerde, biri kullanılmıyor
- **Over-engineering:** STARTER kapsamı dışı erken yazılmış kompleks özellik (örn. henüz satış yokken marketplace senkron)
- **Hardcoded veri:** ui-standard §1.5 ihlali — fallback/demo değer
- **Tekrarlanan kod:** Aynı switch/SQL 2+ dosyada (DRY ihlali)

### Adım 6 — Rapor

```markdown
# Operax ERP/WMS Denetim Raporu — YYYY-MM-DD

## Modül Olgunluk Skoru
| Modül | Olmazsa-Olmaz | Olgun | İleri | Skor |
|---|---|---|---|---|
| M01 | 7/7 ✅ | 4/6 | 1/4 | 🟢 Olgun |
| M11 | 5/7 ⚠️ | 4/6 | 0/4 | 🟡 Eksik (Create formları) |

## 🔴 CRITICAL Eksikler (Olmazsa-Olmaz)
- M11: Create formları yok — kullanıcı veri giremiyor

## 🟡 Önemli Eksikler (Olgun)
- M03: RFQ teklif yönetimi yok

## 🟢 Gelecek (İleri — STARTER dışı)
- M10: OEE, MRP

## ♻️ FAZLA / Ölü Kod
- sp_X: tanımlı ama çağrılmıyor (db_objects:NNN)
- Features/Reports/: boş klasör
- ActionLabel switch: PO+SO Details'te tekrar (DRY)

## Öncelik Önerisi
1. ...
```

---

## Paralel Tarama (Büyük Kapsam)

Tüm modüller için tek seferde derin tarama gerekiyorsa, modül gruplarına bölünmüş **paralel Explore agent** kullan (tek mesajda 4 agent):
- Agent 1: M00+M01
- Agent 2: M02+M03+M04
- Agent 3: M05-M09 (WMS)
- Agent 4: M10+M15+diğer

Her agent kendi grubunun modül × ekran matrisini + file:line kanıt döndürür. Sonra sentezle.

**Not:** Explore agent'lar background'da kill edilebiliyor — foreground (tek mesajda concurrent) tercih et. Worktree açılırsa sonra `git worktree prune` + `remove --force --force`.

---

## Excess Tespit Detay Komutları

```bash
# Kullanılmayan SP listesi
for sp in $(grep -oh "dbo.sp_[A-Za-z]*" docs/sql/db_objects*.sql | sed 's/dbo.//' | sort -u); do
  count=$(grep -rl "\"$sp\"" src/Operax.Web/ 2>/dev/null | wc -l)
  [ "$count" -eq 0 ] && echo "ÖLÜ SP: $sp (çağrılmıyor)"
done

# Boş feature klasörleri
for d in src/Operax.Web/Features/*/; do
  n=$(find "$d" -name "*.cshtml" | wc -l)
  [ "$n" -eq 0 ] && echo "BOŞ KLASÖR: $d"
done

# Hardcoded veri sızıntısı (ui-standard §1.5)
grep -rn "if.*== 0).*=\|new.*{.*Name.*=.*\"" src/Operax.Web/Features/ | grep -iv "guid.empty" | head
```

---

## Çıktı Disiplini

- **Kanıt zorunlu:** Her gap/excess için file:line. Spekülasyon yok (todo-verification.md).
- **STARTER vs gelecek ayrımı:** İleri özellikler STARTER için "eksik" sayılmaz — paket etiketiyle işaretle.
- **Aksiyon önerisi:** Her CRITICAL gap için "hangi plan / kaç dosya" tahmini.
- **Excess dikkatli:** "Kullanılmıyor" demeden önce gerçekten grep ile doğrula — runtime/reflection/dinamik çağrı olabilir.

## İlişkili

- `docs/COMPETITOR_ANALYSIS.md` — rakip özellik matrisi (domain referans)
- `docs/archive/MODULE_GAP_ANALYSIS.md` — son tarama sonucu
- `docs/MASTER_ROADMAP.md` — faz/paket tanımı
- `.Codex/rules/plan-first.md` — gap'ten plana geçiş
- `.Codex/rules/todo-verification.md` — kanıt disiplini
- `.Codex/agents/code-explorer.md` — modül keşfi için ajan
