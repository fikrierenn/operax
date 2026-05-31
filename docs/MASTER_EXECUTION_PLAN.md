# Operax — Master Execution Plan (Modül-Sıralı Yapılacaklar)

**Tarih:** 2026-05-30 · **Mantık:** Bir modülü TAMAMEN bitir → kapanış kriterini doğrula → sonrakine geç.
**Kaynak birikim:** `docs/reference/REFERENCE_STUDY.md` (B1-B18) · `docs/reference/MIKRO_V16_ANALYSIS.md` (§12 E1-E13, §13) ·
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

## M-F0.1 — Kod Review Borçları (CRIT/HIGH) ✅ KAPANDI 2026-05-31
- [x] **CRIT-1** SP THROW catch — PO/SO/Receiving/Shipping Details `when (sqlEx.Number >= 50000)` mevcut
- [x] **CRIT-2** XSS — `_PageHeader.Sub` HtmlEncode + Raw yok (doğrulandı)
- [x] **CRIT-3** Magic string → 38 SQL literal `DocStatus` parametresine çevrildi (14 dosya) + cshtml literalleri
- [x] **CRIT-4** ILogger — 10 dosyada enjekte ama boştaydı → try/catch+LogError'a bağlandı (10→0 uyarı)
- [x] **HIGH-1** SP THROW — catch `>= 50000` açık uçlu → 60001+ hataları ulaşıyor (stale doğrulandı)
- [x] **HIGH-2** SO Approve/Cancel — `sp_ValidateStatusTransition` UPDATE öncesi çağrılıyor (bypass yok)
- [x] **IMP-1/2/3** Cheques interpolation yok · ExecuteScalarAsync (async) · PaymentTermDays kullanılıyor
- **DoD:** ✅ build 0/0; CRIT/HIGH/IMP hepsi canlı kod doğrulandı (`docs/TODO.md` F0.1 + 119-159 superseded).
- **EK (plan 17 — master plan dışı production-hardening):** RateLimit + SecurityHeaders + Serilog + cookie/HSTS + **DB-driven RBAC** (RoleModuleAccess + Admin/Roles UI) + sex→sqlEx rename + STYLE-1 inline cleanup. M-F0.3'ün rol-aware kısmıyla kısmen örtüşür (Model 3 firma-bağlamlı rol HÂLÂ eksik).
- **Bağımlılık:** yok (ilk iş).

## M-F0.2 — Multi-Company İzolasyon [plan: 12] [B1] ✅ KAPANDI 2026-05-31
- [x] Company-kapsamlı vs firma-bağımsız tablo envanteri (52 tablo)
- [x] `operax-cli scan-isolation` statik guard — CompanyId'siz sorgu → exit 1
- [x] İhlal sweep + fix (AutoTraceability, Transfer/Replenishment, Production/Terminal)
- **Not:** Dead servisler (ProductionReceipt/Activity/DynamicBom) → isolation-guard:ignore, karar ertelendi.

## M-F0.3 — Switch-Company Güvenlik + Firma-Yetki Model 3 [plan: 13] [K10/B15] ✅ KAPANDI 2026-05-31
- [x] UserCompany(UserId, CompanyId, Role) köprü tablosu
- [x] switch-company: antiforgery + UserCompany erişim kontrolü (yetkisiz → 403)
- [x] switch-company: company + rol claim aktif firmaya göre yeniden set (ClaimsPrincipalFactory)
- [x] CurrentUser.Roles firma-bağlamlı çözüm

## M-F0.4 — Ledger Bütünlüğü Paketi [plan: 14] [AR-004/005 · K4/K6/K8] ✅ KAPANDI 2026-06-01
- [x] ADR: BIGINT IDENTITY clustered + GUID nonclustered (docs/ADR/01-ledger-clustered-key.md)
- [x] AccountMovement IsDeleted kaldırıldı → REVERSAL ters kayıt + Debit/Credit rename
- [x] StockMovement cancel → sp_*Reverse (5 SP) + IsCancelled=1
- [x] AccountingPeriod + sp_GuardPeriodOpen + tr_GuardPeriod_StockMovement/AccountMovement
- [x] PeriodOverrideLog + self-approval engeli + K8 istisna mekanizması
- [x] Post-SP guard enjeksiyonu (5 onay SP)
- **NOT:** (f) clustered PK migration (mevcut tablolar) Faz 2'ye ertelendi. (g) MovementDate kısmen — CreatedAt hâlâ yaygın.

---

# FAZ 1 — CARİ / DEFTER OMURGASI 🔴

## M-F1.1 — Hafif Cari Besleme [plan: 16] [R0/B3/K3] ✅ KAPANDI 2026-06-01
- [x] İşaret/yön matrisi + tüm SP sistematik analizi (39 SP açıklama)
- [x] sp_GenerateSalesInvoiceFromShipping → AM Debit + sp_GuardPeriodOpen
- [x] sp_ExpenseInvoicePost (yeni) → AM Credit satır bazlı (CostCenterId, ExpenseTypeId, KDV)
- [x] Tahsilat/ödeme/çek/senet → AM Credit/Debit (5 SP)
- [x] Fatura+ödeme reversal SP'leri (sp_SalesInvoiceReverse, sp_ExpenseInvoiceReverse, sp_PaymentReverse)
- [x] sql-sp-reviewer bulguları fix (guard eksik, THROW aralığı, NOTE_IN)
- [x] Backfill çakışma yok + idempotency smoke ✅
- **Kapsam genişledi:** AM şema (DueDate/TaxAmount/NetAmount/CostCenterId/ExpenseTypeId), çek/senet tahsil, tüm SP Türkçe header.

## M-F1.2 — Açık-Kalem Kapama [B16]
- [ ] `AccountReconciliation(BorcMovementId, AlacakMovementId, Tutar, Bileşen)` tablosu (Mikro CARI_HAREKET_BORC_ALACAK_ESLEME deseni)
- [ ] Kapama SP'si (hangi tahsilat hangi faturayı kapattı) + kısmi kapama
- [ ] Yaşlandırma `tvf_PaymentPlanAging` kapama tablosunu okusun + "açık fatura" raporu
- **DoD:** Açık/kapalı kalem ayırt edilebiliyor; doğru yaşlandırma; K9 mutabakat ile uyumlu.
- **Bağımlılık:** M-F1.1.

---

# FAZ 2 — STOK BELGE TİPLERİ 🟠 [B17/§12]

> Çözüm deseni (§0.5): YENİ LEDGER TABLOSU AÇMA → SourceDocType kataloğu + ADJUST sebep kodu + belge zinciri.

## M-F2.1 — İrsaliye ↔ Fatura Ayrımı + Dönüşüm + Birleştirme [E1]
- [ ] İrsaliye (mal hareketi) ile Fatura (mali belge) belge ayrımı netleştir
- [ ] İrsaliyeden faturaya dönüşüm zinciri (Receiving→EI, Shipping→SI) + satır bazlı bağ
- [ ] SourceDocType ayrı (DELIVERY_NOTE vs INVOICE)
- [ ] **E1.B İRSALİYE BİRLEŞTİRME (§12.9):** N irsaliye → 1 fatura. `InvoiceLine.SourceShipmentLineId` (satır bazlı bağ). Sadece aynı cari+para+faturalanmamış birleşir; 7-gün VUK guard; birleşen irsaliye `IsInvoiced=1`+bağ (immutability); kısmi (irsaliye satırları farklı faturalara). e-Belge: DespatchDocumentReference çoklu.
- **DoD:** Mal hareketi irsaliyede, mali belge faturada; N→1 birleştirme + kısmi; 7-gün guard; immutability bağı.
- **Bağımlılık:** M-F0.4. **Plan gerekli (Tier 3).**

## M-F2.2 — Alış/Satış İade [E2] — SATIR BAZLI (KARAR 2026-05-30)
> Mevzuat: 28.03.2025 GİB iade-fatura referansı zorunlu (mali-evrak-mevzuat skill). Detay: MIKRO §12.8.
- [ ] `ReturnInvoiceHeader/Line` belge tipi
- [ ] **`ReturnInvoiceLine.SourceInvoiceLineId`** — satır bazlı kaynak fatura-satırı eşleme (UI: stok seç → orijinal fatura satırı seç)
- [ ] **`SourceLinkType`** LINKED/UNLINKED — kaçış valfi (faturasız/eski mal/açılış iadesi) + sebep kodu; UNLINKED'de header mevzuat referansı yine zorunlu
- [ ] Validasyon: iade miktarı orijinal satır bakiyesini (sevk − önceki iadeler) aşamaz
- [ ] SourceDocType=RETURN_IN/RETURN_OUT + ters StockMovement + AccountMovement (immutability, silme yok)
- [ ] İade stoğa geri girerken kaynak satırın **orijinal maliyetiyle** girer (iade kaynak seçimi LIFO). NOT: K7 FIFO satış COGS değerlemesi AYRI konu, karıştırma.
- [ ] Header BillingReference satırların distinct kaynak faturalarından türet (UBL-TR e-Belge)
- [ ] **ÇOK-KAYNAK TAHSİS (§12.8.1) — LIFO:** 80 iade ↔ N kaynak fatura → TEK iade faturası, çok satır (her satır ayrı SourceInvoiceLineId). `AllocationMode` LIFO_AUTO/MANUAL: sistem **LIFO** önerir (en YENİ faturadan doldur, taşanı öncekine — elde kalan = son giren), kullanıcı ezebilir. Çoklu BillingReference. Kümülatif validasyon (aynı kaynak satıra toplam iade ≤ bakiye).
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
- `docs/reference/REFERENCE_STUDY.md` · `docs/reference/MIKRO_V16_ANALYSIS.md` · `docs/MASTER_ROADMAP.md` (modül kapsam)
- `plans/12-16` · `docs/TODO.md` (CRIT/HIGH/IMP) · `docs/BUGS.md` (AR-001..009)
