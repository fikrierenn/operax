# Operax — Master Execution Plan (Modül-Sıralı Yapılacaklar)

**Tarih:** 2026-05-30 · **Mantık:** Bir modülü TAMAMEN bitir → kapanış kriterini doğrula → sonrakine geç.
**Kaynak birikim:** `docs/REFERENCE_STUDY.md` (B1-B18) · `docs/MIKRO_V16_ANALYSIS.md` (§12 E1-E13, §13) ·
`docs/Operax_Mikro_GAP_Analizi.xlsx` · `plans/12-16` · `docs/TODO.md` (CRIT/HIGH/IMP) · K1-K10 kararlar.

> **Okuma:** Her modülün `Bağımlılık` satırı önce bitmiş olmalı. `DoD` (Definition of Done) sağlanmadan modül
> kapanmaz. `[plan: NN]` referansı varsa o plan dosyası implementasyon detayını taşır. Öncelik yukarıdan aşağı.

---

## ⚡ ÖNCELİK ÖZETİ (faz sırası)

| Faz | Modül | Neden bu sırada | Durum |
|---|---|---|---|
| **F0** | Güvenlik & Defter Zemini | Her şey bunun üstünde durur; existential riskler | 🔴 ÖNCE |
| **F1** | Cari/Defter Omurgası | Para akışı doğruluğu (R0 drift) | 🔴 |
| **F2** | Stok Belge Tipleri | İrsaliye/fatura/iade/fire — operasyon doğruluğu | 🟠 |
| **F3** | Maliyet (FIFO) | TR enflasyon COGS/vergi | 🟠 |
| **F4** | Finans Derinleştirme | Virman/çek/vade farkı | 🟡 |
| **F5** | WMS Olgunlaştırma | Available/Allocated, lokasyon eligibility | 🟡 |
| **F6** | Kalem Tipleri (Hizmet/Masraf) | Stoksuz kalem | 🟡 |
| **F7** | Ertelenmiş (GL/Demirbaş/Personel) | Mevzuat skill ön koşul | ⚪ SONRA |

---

# FAZ 0 — GÜVENLİK & DEFTER ZEMİNİ 🔴

> Bu faz bitmeden yeni feature YAZILMAZ. Üzerine bina dikilen temel.

## M-F0.1 — Kod Review Borçları (CRIT/HIGH) [TODO: CRIT-1..4]
- [ ] **CRIT-1** SP THROW catch handler — PO/Receiving/Shipping/PO-Cancel Details (`SqlException when 50000-59999`)
- [ ] **CRIT-2** XSS — `_PageHeader.Sub` `@Html.Raw` + ham PartnerName (SubHtml ayrımı + encode)
- [ ] **CRIT-3** Magic string `"APPROVED"` → `DocStatus` sabiti + SQL IN parametre
- [ ] **CRIT-4** `ILogger<T>` DI eksik tüm yeni PageModel'ler
- [ ] **HIGH-1** SP THROW kod aralığı 60001-72001 → 50000-59999 standardına çek (Lib/Errors.cs sözleşme)
- [ ] **HIGH-2** PO/SO Cancel direct UPDATE → `sp_*Cancel` SP + `sp_ValidateStatusTransition`
- [ ] **IMP-1/2/3** SQL string interpolation (Cheques) · sync ExecuteScalar → async · hardcoded 14-gün vade
- **DoD:** code-reviewer + security-reviewer + silent-failure-hunter temiz (≥80 bulgu yok); build 0/0.
- **Bağımlılık:** yok (ilk iş).

## M-F0.2 — Multi-Company İzolasyon [plan: 12] [B1]
- [ ] Company-kapsamlı vs firma-bağımsız tablo envanteri
- [ ] Desen 1: okuma TVF'leri `@CompanyId`-sargılı (`tvf_X(@CompanyId)`); ham `FROM Tablo` ele
- [ ] Desen 3: statik analiz guard/test — CompanyId'siz company-tablo sorgusu → fail (CI/pre-commit)
- [ ] Mevcut ihlal sweep + düzelt
- **DoD:** Tarama testi yeşil; CompanyId'siz sorgu kalmadı; `.claude/rules` + sprint-kapanış şartı.
- **Bağımlılık:** M-F0.1. **Not:** Güvenliği M-F0.3'e bağlı (claim serbest değişirse dekoratif).

## M-F0.3 — Switch-Company Güvenlik + Firma-Yetki Model 3 [plan: 13] [K10/B15]
- [ ] `UserCompany(UserId, CompanyId, Role)` köprü tablosu
- [ ] switch-company: antiforgery geri ekle + UserCompany erişim kontrolü (yetkisiz → 403)
- [ ] switch-company: company + **rol claim** aktif firmaya göre yeniden set (rol-aware — ZORUNLU)
- [ ] CurrentUser.Roles firma-bağlamlı çözüm
- [ ] Mevcut kullanıcı claim'leri → UserCompany migration
- **DoD:** Yetkisiz firmaya geçiş reddediliyor; B firmasında A'nın rolüyle dolaşılamıyor; AR-003 kapalı.
- **Bağımlılık:** M-F0.1.

## M-F0.4 — Ledger Bütünlüğü Paketi [plan: 14] [AR-004/005 · K4/K6/K8]
> ⚠️ ÖN KOŞUL: `sys.indexes` ile StockMovement/AccountMovement PK'sı clustered+NEWID + `IX_StockMovement_*` basılı TEYİT.
- [ ] **(a)** ADR: ledger clustered key (BIGINT/INT identity clustered + GUID nonclustered)
- [ ] **(b)** AccountMovement `IsDeleted` KALDIR → REVERSAL ters kayıt
- [ ] **(c)** StockMovement cancel → ters hareket + `IsCancelled=1` (`sp_*Reverse`; şu an hiç set edilmiyor)
- [ ] **(d) K4 Dönem kontrolü:** `AccountingPeriod` (firma bazlı OPEN/CLOSED/LOCKED) + `sp_GuardPeriodOpen` + DB trigger + `sp_GuardStockFrozen` kancası (no-op)
- [ ] **(e) K8 İstisna/iz:** `PeriodOverrideLog` (silinmez) + guard statü davranışı (CLOSED yetki+gerekçe+atomik log, LOCKED istisna yok) + self-approval engeli
- [ ] **(f) R4:** clustered PK migration (yeni tablolar + mevcut için faz-2 script)
- [ ] **(g) B19 — StockMovement.MovementDate** (fiili hareket/kabul tarihi) ekle; `sp_GuardPeriodOpen` ve tüm dönem/bakiye sorguları MovementDate kullansın (CreatedAt değil). 3 tarih ayrı: belge(DocDate) ≠ işlenme(CreatedAt) ≠ fiili(MovementDate). AccountMovement zaten taşıyor.
- **DoD:** Ledger silinemez (reversal); kapalı döneme yazım reddediliyor (MovementDate'e göre); override loglanıyor; clustered PK uygulandı; StockMovement MovementDate + guard bağlı.
- **Bağımlılık:** M-F0.1. **Performans kuralı (K6):** SUM-bakiye index'leri gevşetilemez (MovementDate üzerine).

---

# FAZ 1 — CARİ / DEFTER OMURGASI 🔴

## M-F1.1 — Hafif Cari Besleme [plan: 16] [R0/B3/K3]
- [ ] İşaret/yön matrisi (her SourceDocType için Borç/Alacak)
- [ ] `sp_GenerateSalesInvoiceFromShipping` → AccountMovement Borç + `sp_GuardPeriodOpen`
- [ ] Alış faturası onayı → AccountMovement Alacak
- [ ] Tahsilat/ödeme → ters yön
- [ ] Çift-post koruması (`UX_AccountMovement_Source`) + backfill çakışma kontrolü
- **KAPSAM DIŞI:** kebir, COGS, SRBNB, çift-taraflı GL, masraf merkezi, hesap planı (= K1 ertelenmiş).
- **DoD:** Her belge cari deftere atomik yazıyor; `v_AccountBalance` belge zinciriyle tutarlı; drift yok.
- **Bağımlılık:** M-F0.4 (dönem guard + immutability omurgası).

## M-F1.2 — Açık-Kalem Kapama [B16]
- [ ] `AccountReconciliation(BorcMovementId, AlacakMovementId, Tutar, Bileşen)` tablosu (Mikro CARI_HAREKET_BORC_ALACAK_ESLEME deseni)
- [ ] Kapama SP'si (hangi tahsilat hangi faturayı kapattı) + kısmi kapama
- [ ] Yaşlandırma `tvf_PaymentPlanAging` kapama tablosunu okusun + "açık fatura" raporu
- **DoD:** Açık/kapalı kalem ayırt edilebiliyor; doğru yaşlandırma; K9 mutabakat ile uyumlu.
- **Bağımlılık:** M-F1.1.

---

# FAZ 2 — STOK BELGE TİPLERİ 🟠 [B17/§12]

> Çözüm deseni (§0.5): YENİ LEDGER TABLOSU AÇMA → SourceDocType kataloğu + ADJUST sebep kodu + belge zinciri.

## M-F2.1 — İrsaliye ↔ Fatura Ayrımı + Dönüşüm [E1]
- [ ] İrsaliye (mal hareketi) ile Fatura (mali belge) belge ayrımı netleştir
- [ ] İrsaliyeden faturaya dönüşüm zinciri (Receiving→EI, Shipping→SI) + bağ
- [ ] SourceDocType ayrı (DELIVERY_NOTE vs INVOICE)
- **DoD:** Mal hareketi irsaliyede, mali belge faturada; dönüşüm zinciri + immutability bağı.
- **Bağımlılık:** M-F0.4.

## M-F2.2 — Alış/Satış İade [E2] — SATIR BAZLI (KARAR 2026-05-30)
> Mevzuat: 28.03.2025 GİB iade-fatura referansı zorunlu (mali-evrak-mevzuat skill). Detay: MIKRO §12.8.
- [ ] `ReturnInvoiceHeader/Line` belge tipi
- [ ] **`ReturnInvoiceLine.SourceInvoiceLineId`** — satır bazlı kaynak fatura-satırı eşleme (UI: stok seç → orijinal fatura satırı seç)
- [ ] **`SourceLinkType`** LINKED/UNLINKED — kaçış valfi (faturasız/eski mal/açılış iadesi) + sebep kodu; UNLINKED'de header mevzuat referansı yine zorunlu
- [ ] Validasyon: iade miktarı orijinal satır bakiyesini (sevk − önceki iadeler) aşamaz
- [ ] SourceDocType=RETURN_IN/RETURN_OUT + ters StockMovement + AccountMovement (immutability, silme yok)
- [ ] FIFO katman geri-açma: `StockCostConsumption` (K7) ters — doğru maliyet katmanı geri yükle (LINKED'de)
- [ ] Header BillingReference satırların distinct kaynak faturalarından türet (UBL-TR e-Belge)
- **DoD:** İade ayrı belge; satır bazlı kaynağa bağlı (veya UNLINKED+sebep); kısmi iade + aşırı-iade guard; ters ledger; KDV/tevkifat orijinalden; mevzuat referansı dolu.
- **Bağımlılık:** M-F2.1, M-F3.1 (FIFO geri-açma için). **Plan gerekli (Tier 3).**

## M-F2.3 — Fire/Zayi + Sayım Fazla/Eksik + Açılış [E4/E5/E7]
- [ ] ADJUST'a `AdjustReason` (COUNT_PLUS/COUNT_MINUS/WASTE/SCRAP/OPENING/REVALUATION)
- [ ] Fire/zayi belgesi + maliyet+vergi etkisi (E4)
- [ ] Sayım fazla/eksik ayrımı + rapor (E5)
- [ ] Stok açılış/devir fişi — SourceDocType=OPENING_STOCK (E7)
- **DoD:** Her stok düzeltmesi sebep kodlu; fire maliyete yansıyor; açılış fişi go-live'da çalışıyor.
- **Bağımlılık:** M-F0.4.

---

# FAZ 3 — MALİYET (FIFO) 🟠

## M-F3.1 — FIFO Kalıcı Eşleme [B5/K7]
- [ ] `StockCostConsumption(CikisMovementId, GirisMovementId, Miktar, Maliyet)` tablosu (Mikro STOK_HAREKET_MALIYET_DETAYLARI deseni)
- [ ] Çıkış onay SP'sinde FIFO katman tüketim eşleme (snapshot DEĞİL)
- [ ] İade'de katman geri-açma
- [ ] Moving Average ile yan yana (kalem bazında yöntem seçimi)
- **DoD:** FIFO COGS doğru; katman tüketimi denetlenebilir; snapshot reddi (K6) korundu.
- **Bağımlılık:** M-F2.2 (iade), M-F0.4.

---

# FAZ 4 — FİNANS DERİNLEŞTİRME 🟡

## M-F4.1 — Virman Evrakı [E11/plan: 11]
- [ ] Kasa↔kasa, banka↔banka, cari↔cari virman belgesi
- [ ] Hesap↔hesap virman (FinancialTransaction TRANSFER_IN/OUT)
- **DoD:** Virman evrakı iki tarafı atomik yazıyor; dönem guard'dan geçiyor.
- **Bağımlılık:** M-F1.1.

## M-F4.2 — Çek/Senet Genişletme [§6/§12.5]
- [ ] Çek statü: TEMİNAT (sck_sonpoz=3) ekle
- [ ] Çek statü: KISMİ ÖDEME (sck_sonpoz=9) + kalan tutar
- [ ] Çek KONUM izleme (`sck_nerede_cari` — şu an kimde/hangi banka)
- [ ] (ops) İcra/protesto statüleri
- **DoD:** Teminat + kısmi ödeme akışları çalışıyor; çek konumu izlenebiliyor; document-immutability §2.4 güncel.
- **Bağımlılık:** M-F1.1.

## M-F4.3 — Vade Farkı + Dekont [E12]
- [ ] Vade farkı faturası (geç ödeme) — AccountMovementType=LATE_FEE
- [ ] Serbest borç/alacak dekontu
- **DoD:** Vade farkı + dekont cari deftere işliyor.
- **Bağımlılık:** M-F1.1.

---

# FAZ 5 — WMS OLGUNLAŞTIRMA 🟡

## M-F5.1 — Available vs Allocated Stok [B6]
- [ ] `tvf_InventoryBalance` rezervasyon/allocated kolonu
- [ ] Picking doğruluğu (mevcut = onhand − allocated)
- **DoD:** Sipariş rezervasyonu stoğu kilitliyor; available doğru.
- **Bağımlılık:** M-F2.3.

## M-F5.2 — Lokasyon Eligibility [B8]
- [ ] Lokasyon `IsReceivable`/`IsPickable` bayrakları
- [ ] Hedef hücre guard (mal kabul/sevk eligibility)
- **DoD:** Uygun olmayan hücreye hareket reddediliyor.
- **Bağımlılık:** M-F5.1.

---

# FAZ 6 — KALEM TİPLERİ (HİZMET/MASRAF) 🟡 [B18/§13]

## M-F6.1 — Hizmet/Masraf Kalem Tipi
- [ ] `Item.ItemKind` (GOODS/SERVICE/EXPENSE) — base şemaya taşı (StarterFields ALTER değil)
- [ ] Onay SP guard: `IF ItemKind <> 'GOODS' → StockMovement atla` (SERVICE/EXPENSE stok yazmaz)
- [ ] `ExpenseType`'a AccountCode + Direction + DefaultTaxRate + IsKkeg
- [ ] Fatura satırında karışık kalem (mal+hizmet) → satır bazında koşullu stok etkisi
- **DoD:** Hizmet satışı/alışı stok defteri kirletmiyor; masraf kartı GL kodlu; SERVICE kalem bug'ı kapalı.
- **Bağımlılık:** M-F1.1.

## M-F6.2 — Dönemsel Gider (ertelenebilir) [Defer]
- [ ] `DeferredExpense(StartDate, EndDate, TotalAmount, SourceAccount, TargetAccount, Method)` (Mikro DONEMLERE_YAYILAN deseni)
- [ ] Aylık tahakkuk Hangfire job
- **DoD:** Peşin kira/sigorta aylara yayılıyor.
- **Bağımlılık:** M-F6.1. **Öncelik:** düşük, ertele.

---

# FAZ 7 — ERTELENMİŞ (mevzuat/ileri modüller) ⚪

## M-F7.1 — Periyodik GL Muhasebeleştirme [K1/K2]
> **ÖN KOŞUL: muhasebe-mevzuat skill'i** (VUK / e-Defter tebliğleri / hesap planı standardı / berat / GİB) — bu yazılmadan modül AÇILMAZ.
- [ ] (mevzuat skill sonrası) Hesap Planı (tip + çalışma şekli + hiyerarşi) — Mikro MUHASEBE_HESAP_PLANI deseni
- [ ] PostingRule(MuhasebeGrup + HareketTipi → HesapKodu) normalize eşleme — Mikro STOK_MUHASEBE_GRUPLARI deseni
- [ ] Masraf merkezi/proje boyutu — Mikro SORUMLULUK_MERKEZLERI
- [ ] Muhasebeleştirme SP: subledger→grup+yön→hesap→fiş, `fis_ticari_uid` geri-bağ
- [ ] Mahsup türleri kataloğu (SMM/kur farkı/enflasyon/dönem kapanış — Mikro fis_fmahsup_tipi 23)
- **DoD:** Aylık muhasebeleştirme yevmiye fişi üretiyor; subledger↔GL tutarlı.
- **e-Defter ÜRETİMİ kapsam DIŞI (K5):** Operax sadece LOCKED döneme saygı gösterir.
- **Bağımlılık:** M-F1.1, M-F3.1, mevzuat skill.

## M-F7.2 — Sayım Freeze (stok satırı bazlı) [K5/M08/S7]
- [ ] `sp_GuardStockFrozen` gerçek gövde (kanca M-F0.4'te açıldı)
- [ ] Sayım oturumu = dondurulmuş satır kümesi; iptal→giriş→yeniden say döngüsü
- **DoD:** Dondurulmuş kaleme hareket reddediliyor; biten oturumlar korunuyor.
- **Bağımlılık:** M-F0.4, M-F2.3. Detay: `docs/MODULE_SPECS/M08_CycleCount_Freeze.md`.

## M-F7.3 — Cari Mutabakat Freeze (partner+tarih) [K9/M11]
- [ ] `sp_GuardPartnerReconciled` + mutabakat tablosu
- **DoD:** Mutabık partnerin geçmiş hareketleri kilitli; geçmişe giriş override+log.
- **Bağımlılık:** M-F1.2, M-F0.4 (K8 override).

## M-F7.4 — Kapsam Dışı (değerlendirme bekleyen)
- [ ] Demirbaş/Amortisman · Personel/Bordro · İthalat/İhracat+GTİP · Konsinye (E6) · Fason (E8) · Perakende/POS (E3) · Kur farkı (E10) · B2B Portal · Servis/RMA (M12) · ASN (B10)
- **Durum:** HAYIR-ŞİMDİ / sektöre bağlı. İhtiyaç çıkınca ayrı plan.

---

## 📌 ÇALIŞMA DİSİPLİNİ
- Her modül kapanışında: build 0/0 + ilgili agent review (sql-sp-reviewer SP'ler için) + journal + commit.
- Tier 3 modüller (yeni şema/pattern) için `plans/NN-*.md` yaz, onay al, sonra kod.
- Modül bitmeden sonrakine GEÇME (kullanıcı kuralı).
- Mikro enum DOĞRULANMADI kalemleri (sth_cins 14/15, cha_evrak_tip 51-137, hizmet/masraf kolon) implementasyon öncesi resmi dokümandan teyit.

## İlişkili
- `docs/Operax_Mikro_GAP_Analizi.xlsx` — sheet 08 backlog detay
- `docs/REFERENCE_STUDY.md` · `docs/MIKRO_V16_ANALYSIS.md` · `docs/MASTER_ROADMAP.md` (modül kapsam)
- `plans/12-16` · `docs/TODO.md` (CRIT/HIGH/IMP) · `docs/BUGS.md` (AR-001..009)
