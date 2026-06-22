# Plan 46 — M0 Modül: Ayarlar/Tanımlar/Sabitler (Tam Tamamlama)

**Tarih:** 2026-06-22
**Durum:** Onay bekliyor (sonraki oturumda implement)
**Tier:** 3 (modül-tam-tamamlama — Plan 45 roadmap M0)
**Paket:** V1 foundational
**Bağlam:** Plan 45 modül-bazlı productization roadmap'inin İLK modülü. DoD D1-D8'e ulaşana dek kapanmaz.

---

## 1. Problem

Ayarlar/Tanımlar/Sabitler katmanı (her şeyin temeli — sözlük, UDF, parametre, seri, statü geçişi, modül, kullanıcı/rol) **kurulu ama komple değil**. Admin ekranları + servisler + seed var; ancak CRUD eksikleri + kırık ekran + Plan 42 vaadinin kırılması mevcut. Audit 2026-06-22 (koddan doğrulandı).

## 2. Audit — Gap Listesi (kod kanıtlı, DOĞRULANDI)

### G1 — Sözlük (Dictionary) — CRITICAL (Plan 42 bağımlılığı)
- **`Details.cshtml.cs` TAMAMEN KIRIK:** `WHERE DictionaryTypeId = @Id` (satır 34) + `INSERT ... DictionaryTypeId` (46) + `SortNo` (DTO/view) kullanıyor. **Gerçek kolonlar `TypeId` + `OrderNo`** (canlı VT doğrulandı: `DictionaryTypeId` yok → "Invalid column name" runtime). Ekran Index'ten LİNKSİZ (ölü) + add-only.
- **`Values.cshtml(.cs)` CANLI ekran (Index→./Values):** doğru kolon (`TypeId`/`OrderNo`) AMA **salt-okuma** (yalnız OnGetAsync) + **`WHERE CompanyId = @CompanyId` filtresi global değerleri GİZLER** → 290 değerin çoğu global (CompanyId=0), şirket görünümünde 0 satır görünür.
- **Plan 42 vaadi KIRIK:** enum etiketleri artık sözlükten okunuyor ("admin'den etiket değiştir → her yere yansır") ama admin değer DÜZENLEYEMİYOR.
- **Cache:** `DictionaryLabels` (IMemoryCache 5dk, key `dict-labels-v1`) — değer değişince invalidate edilmiyor → düzenleme 5dk gecikmeli yansır.
- **Tip yönetimi yok:** DictionaryType create/edit/delete handler yok.

### G2 — NumberSeries — edit-only
- `OnGetAsync` + `OnPostSaveAsync` var; **yeni seri ekle / sil yok**. Yeni belge tipi serisi tanımlanamaz (kod-seed dışı). `sp_NextNumber` atomik ✅ (Plan-doğrulandı).

### G3 — Modules — salt-okuma
- `OnGetAsync` var; **CompanyModule aktivasyon toggle yok**. `Module`/`CompanyModule`/`RoleModuleAccess` tabloları var ama UI'dan modül aç/kapa edilemiyor. (V1'de gerekli mi — karar.)

### G4 — Settings — placeholder
- `Index.cshtml.cs` 261 byte, **handler yok**. Fonksiyonel şirket ayarı (logo/VKN/varsayılan depo/para birimi/InvoiceMode vb.) yok — sadece nav landing. Bu ayarların bir kısmı `Parameter` tablosunda; UI bağlanmalı mı?

### G5 — StatusTransitions — salt-okuma (muhtemelen kasıtlı)
- `OnGetAsync` var; düzenleme yok. Statü akışı bozulmasın diye salt-okuma MAKUL. **Karar: kasıtlı kabul + dokümante** (kullanıcı düzenlemesi V1 kapsam dışı).

### G6 — Tam CRUD olanlar (doğrulandı ✅)
- **UdfFields:** Create/Save/Delete + Get ✅
- **Parameters:** Create/Save/Delete + Get ✅
- **Roles/Users:** (önceki oturum S1'de yapıldı — doğrulanacak)
- **AuditLog:** salt-okuma (doğru — denetim izi değişmez)

## 3. Scope

**Dahil:** G1 (Sözlük tam CRUD + global görünürlük + cache invalidation + ölü Details sil), G2 (NumberSeries create/delete), G3 (Modül aktivasyon — karar sonrası), G4 (Settings — karar sonrası), G5 (dokümante). UDF/Parametre/Rol/Kullanıcı/AuditLog DoD-doğrula (D5 güvenlik + D4 UI).

**Hariç:** Yeni ayar tipi icat etme (footprint-ladder) · dış entegrasyon ayarları (M16/Logo connector) · profitability config.

## 4. Fazlar

### Faz 1 — Sözlük CRUD (ADR-02 hibrit) ✅ TAMAMLANDI 2026-06-22
ADR-02 (kod-çıpalı vs dinamik) uyumlu CRUD — "tam CRUD" değil, **gating'li**:
- **migration_46:** `DictionaryType.AllowValueCrud` BIT (kod-çıpalı=0 / dinamik=1) + eksik `DictionaryValue.UpdatedAt` audit kolonu.
- `Values.cshtml.cs`: query global+şirket göster (`(CompanyId=@CompanyId OR CompanyId=@Global) AND IsDeleted=0`). Handler'lar: `OnPostEditAsync` (NameTr/NameEn/OrderNo — **Code asla, identity**; TÜM tiplerde — Plan 42 vaadi), `OnPostToggleActiveAsync` (TÜM tipler), `OnPostAddAsync`/`OnPostDeleteAsync` (**yalnız AllowValueCrud=1**, server-side `IsCrudAllowedAsync` ile yeniden kontrol — UI gizlese de). Yazma WHERE'leri CompanyId-simetrik + `UpdatedBy=@UserId` (security §8, audit).
- `IMemoryCache` inject → her değişiklikte `Remove("dict-labels-v1")` (Plan 42 cache invalidation → anında yansır).
- `Values.cshtml`: kod-çıpalı tipte uyarı banner + add-form/sil gizli; dinamik tipte tam UI. Alpine inline edit-toggle.
- Ölü `Details.cshtml(.cs)` SİLİNDİ (kırık + linksiz, D8 hijyen).
- **Kapanış ✅:** build 0/0 · code-reviewer (audit/UpdatedBy fix) · security-reviewer (IDOR/gating KAPALI, CompanyId asimetri fix) · E2E smoke (dinamik add/edit/del + kod-çıpalı gating + forged-delete red + global edit + UpdatedBy runtime).

### Faz 2 — NumberSeries create/delete ✅ TAMAMLANDI 2026-06-22 (commit e9ca652)
- `OnPostCreateAsync` (katalog-whitelist 9 tip + soft-delete revive) + `OnPostDeleteAsync` (soft-delete, yalnızca NextNo=1). UI: AvailableDocTypes dropdown'lu yeni seri formu + NextNo=1 satırlarda Sil. DocTypeLabel magic-string → sabit.
- **Karar:** Free-text DocType yerine whitelist (caller'sız orphan seri engeli). Delete=soft (mevcut IsActive toggle ile redundant değil); NextNo>1 silinemez (kod çakışması). Revive: soft-delete'li tip Create'te diriltilir (unique ihlali önlenir).
- **Kapanış ✅:** build 0/0 · code-reviewer (DocTypeLabel sabit fix) · security-reviewer (IDOR/CSRF/whitelist temiz) · E2E smoke (render + Sil gating + Çek soft-delete → dropdown → Create revive tek-satır duplicate'siz + console hatasız).

### Faz 3 — Modül aktivasyon (Seviye A) ✅ TAMAMLANDI 2026-06-22 (commit 9cd513f + bugfix 645826f)
**Gerçekleşen:** Plan aynen uygulandı. Servis adı `CompanyModuleAccess` (Authz.ModuleAccessHandler ile çakışmasın). E2E smoke yeşil (Operax Demo LTD: M03 kapat→Satınalma gizlendi → enable→döndü, cache anında, login etkilenmez, console temiz).
**Yan-bug yakalandı+düzeltildi (645826f):** `Admin/Modules` "Etkinleştir" butonu HİÇ çalışmıyordu (pre-existing) — `value="@(!m.IsActive)"` Razor bool-attribute minimization ile `value="value"` üretiyor → bool bind false → enable no-op. String render'a çevrildi. Faz 3 toggle'ı anlamlı yapınca yakalandı.


**Karar (kullanıcı):** Seviye A (sadece sidebar görünürlük) + bağımlılık zorlaması YOK (admin sorumlu) + çekirdek whitelist. Route guard (Seviye B) ertelendi (lisans zorlaması gerçek ihtiyaç olunca).
**Bulgu:** `Module`(18)+`CompanyModule` tabloları + `Admin/Modules` toggle ZATEN var ama **no-op** — `_Layout` okumuyor. Faz 3 = mevcut toggle'ı sidebar'a bağlamak (yeni şema yok).
**Etkin kural (opt-out):** Modül, `CompanyModule`'de açıkça `IsActive=0` satırı YOKSA AÇIK. (Mevcut install'larda CompanyModule boş → tümü açık, regresyon yok.)
**Eşleme (yalnızca 4 temiz grup gate'lenir):** Satınalma→M03 · Satış→M04 · Stok→M02 · Ana Veri→M01. **Gate'siz (çekirdek/eşlemesiz):** Dashboard, Sistem (Ayarlar/Belge Serileri/Özel Alanlar). **Finans gate'siz** — _Layout yorumu M11 diyor ama M11 katalogda "B2B Portal" (semantik uyumsuzluk) → V1'de açık bırak, bilinen gap.
**Dosyalar:**
1. `Lib/ModuleAccess.cs` (yeni) — `IModuleAccess`: `EnsureLoadedAsync` (şirket-başına IMemoryCache, 5dk TTL, OFF-kod seti) + `IsActive(code)` (sync, `!off.Contains`).
2. `Program.cs` — `AddScoped<IModuleAccess, ModuleAccess>()`.
3. `_Layout.cshtml` — `@inject IModuleAccess` + üstte `@{ await Modules.EnsureLoadedAsync(); }` (Razor async) + 4 `<details>` grubunu `@if (Modules.IsActive("M0x"))` ile sar.
4. `Admin/Modules/Index.cshtml.cs` — POST'ta `cache.Remove(ModuleAccess.CacheKey(company.Id))` + display sorgusu efektif (no-row=ON: `cm.CompanyId IS NULL OR cm.IsActive=1`).
**Kapanış:** build 0/0 · code-reviewer · security-reviewer (yeni servis) · E2E smoke (modül kapat → sidebar grubu kaybolur → aç → döner; login etkilenmez).

### Faz 3.5 — Sidebar yeniden organizasyon ✅ TAMAMLANDI 2026-06-23 (commit 444a268)
**Gerçekleşen:** Tam reorg (kullanıcı onayı) — TANIMLAR ayrı bölüm, DEPO/WMS + ÜRETİM yeni bölümler (mevcut sayfalar bağlandı, modül-gate'li), FİNANS alt-gruplandı (gate'siz çekirdek). Yanlış M11 yorumu silindi.
**Yan-bug'lar yakalandı (yeni linkleyince yüzeye çıktı):** (1) CycleCount/Index nested-aggregate SQL 500 → düzeltildi + IsDeleted=0 eklendi. (2) /Production/Terminal 500 (`ProductionOrder.CurrentRouteStepId` kolonu yok) = bilinen DEAD/WIP → menüye EKLENMEDİ.
**Finans M11 kararı:** Seçenek 3 uygulandı (yeni kod yok, gate'siz çekirdek, yanlış yorum silindi). M11=B2B Portal canonical korundu. Tam çözüm (B2B taşıma/yeni Finans kodu) hâlâ açık backlog.

### Faz 3.5 (orijinal backlog notu — referans için korundu)
Kullanıcı sidebar gruplaması "alakasız" buldu. ERP danışmanı önerisi (SAP B1/Logo/Mikro/Netsis/Odoo referansı):
- **Ana Veri'yi OPERASYON altından çıkar → ayrı üst-bölüm** (4/4 olgun ERP master data'yı operasyondan ayırır; ürün/cari/depo operasyon değil kaynak).
- **Finans'ı alt-grupla:** Kasa&Banka (Hesaplar/Ödeme-Tahsilat) · Çek&Senet · Kredi (Krediler/Kredi-Kartları) · Vade&Plan (Ödeme Planı) · Raporlar (Yaşlandırma/Mali Durum). Şu an 8 düz öğe.
- **Yeni DEPO/WMS bölümü:** Stok görünümü (Bakiye/Hareket/Sarf) + Transfer(M07)/Toplama(M06)/Sayım(M08) — şu an menüde YOK ama modül var.
- **Yeni ÜRETİM bölümü** (M10 açıksa): İş Emirleri/Reçete/Sarf-Mamul.
- Önerilen grup→M eşlemesi: Anasayfa→M15(gate ekle) · Ana Veri→M01 · Satınalma→M03 · Satış→M04(+M05 alt-gate) · Depo→M02 + M06/M07/M08 · Üretim→M10 · Sistem→M00(çekirdek).
**BLOKER KARAR — Finans canonical M kodu:** DB `M11=B2B Portal`, ama `_Layout`/`COMPETITOR_ANALYSIS.md` yorumu `M11=Finans` → çelişki. Finans gate'i bu karar verilmeden yazılamaz. Seçenekler: (1) B2B'yi başka koda taşı M11=Finans yap (DB migration), (2) Finans'a yeni kod (M18), (3) Finans çekirdek kalsın gate'siz + yanlış M11 yorumunu sil. Tier 3 — ayrı plan + kullanıcı kararı.

### Faz 4 — Settings fonksiyonel ✅ TAMAMLANDI 2026-06-23 (commit feadd9a)
**Doğrulama bulgusu:** Faz 4 büyük ölçüde ZATEN vardı — Settings hub çalışıyor (Dictionary/Parametreler/Roller/Kullanıcılar/AuditLog). Operasyonel ayarlar (CostingMethod/InvoiceMode/KDV/ödeme vadesi) `/Admin/Parameters` ile düzenlenebilir (Parameter tablosu, global+şirket override). Plan'daki "logo/varsayılan depo/para" Company tablosunda YOK (kolon yok) + para birimi sabit ₺ (turkish-ui) → kapsam dışı.
**Yapılan (tek gerçek boşluk):** Şirket profili editörü — `Features/Admin/CompanyProfile` (Company.Name + TaxNumber/VKN). e-Belge/evrak başlığı için. Administrator-only, oturum-scoped, VKN 10/11 hane guard. Settings hub'a link kartı.
**Kapanış ✅:** build 0/0 · security-reviewer temiz · code-reviewer (yeni dosyalar uyumlu) · E2E smoke (yükle/edit/persist/guard/link).
**Debt (pre-existing, kapsam dışı):** Settings/Index.cshtml hub'ı Tailwind utility-salatası + ham renk (ui-standard §1/§2 ihlali, _PageHeader yok). Eklediğim link kartı mevcut pattern'i taklit etti (tutarlılık). Tüm hub'ın semantic-class refactor'ı ayrı Tier 2 iş — TODO'da.

### Faz 5 — DoD doğrulama + kapanış
- UDF/Parametre/Rol/Kullanıcı D5 güvenlik (CompanyId/authz/IDOR) + D4 UI taraması. StatusTransitions kasıtlı-salt-okuma dokümante. Modül kapanış kapısı: build+reviewer'lar+E2E. Plan arşive.

## 5. Riskler
| Risk | Etki | Mitigasyon |
|---|---|---|
| Global değer düzenleme tüm tenant'ları etkiler | orta | single-tenant kurulum (architecture.md) → kabul; düzenleme tenant'ın kendi sözlüğü |
| Cache invalidation kaçağı | düşük | her CRUD handler'da Remove; TTL 5dk fallback |
| Ölü Details silme referans kırar | düşük | linksiz + kırık (zaten çalışmıyor); grep ile referans 0 doğrula |
| NumberSeries Code/Padding validasyon | orta | format guard + benzersizlik (DocType+CompanyId) |

## 6. Done Criteria (M0 DoD)
- [ ] Sözlük: add/edit/delete/toggle + global görünür + cache invalidation; ölü Details silindi
- [ ] NumberSeries: create/delete çalışır
- [ ] Modül aktivasyon (karar olumlu ise) veya "hepsi-açık" dokümante
- [ ] Settings fonksiyonel (karar olumlu ise) veya nav-hub dokümante
- [ ] StatusTransitions kasıtlı salt-okuma notu
- [ ] UDF/Parametre/Rol/Kullanıcı/AuditLog D5+D4 doğrulandı
- [ ] build 0/0 · code-reviewer · security-reviewer · E2E smoke
- [ ] Plan arşive + journal

## 7. Sonraki oturum başlangıç
1. **Faz 1 (Sözlük CRUD)** ile başla — en kritik (Plan 42 unlock), karar gerektirmez.
2. `Values.cshtml.cs` query global+şirket + 4 handler + cache invalidation.
3. `Values.cshtml` add/edit/delete UI.
4. Ölü `Details.cshtml(.cs)` sil (önce `grep -r "Dictionary/Details"` referans 0 teyit).
5. Faz 3/4 öncesi G3/G4 kararları sor (modül-aç-kapa? hangi settings?).

## 8. İlişkili
- `plans/45-module-completion-roadmap.md` (meta-roadmap, M0 ilk sırada)
- `plans/archive/42-dictionary-label-refactor.md` (DictionaryLabels servisi + cache — Faz 1 invalidation buraya bağlı)
- `.claude/rules/phase-review-gate.md` · `.claude/rules/security-principles.md` (Admin authz/IDOR)
