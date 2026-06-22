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

### Faz 2 — NumberSeries create/delete
- `OnPostCreateAsync` (yeni DocType serisi: Prefix/Padding/Separator/NextNo) + `OnPostDeleteAsync` (soft veya IsActive=0). UI: yeni seri formu + sil.
- **Kapanış:** build + smoke (seri ekle → sp_NextNumber yeni seriden üretir).

### Faz 3 — Modül aktivasyon (G3 — KARAR gerek)
- Modüller ekranına CompanyModule aktif/pasif toggle. **Onay sorusu:** V1'de modül aç/kapa gerekli mi, yoksa hepsi-açık mı?

### Faz 4 — Settings fonksiyonel (G4 — KARAR gerek)
- Şirket ayarları (logo/VKN/varsayılan depo/para/InvoiceMode) → Parameter tablosuna bağlı form. **Onay sorusu:** hangi ayarlar V1'de düzenlenebilir olmalı?

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
