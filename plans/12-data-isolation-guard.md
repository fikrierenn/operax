# Plan 12 — Multi-Company Veri İzolasyon Güvencesi

**Tarih:** 2026-05-29 · **Durum:** `Onaylandı (2026-05-29)` · **Modül:** M00 · **Kaynak:** AR-001 (🔴 KRİTİK)

## 1. Problem
Dapper'da EF benzeri global query filter yok. Company-kapsamlı her sorgu elle `WHERE CompanyId = @CompanyId` yazıyor. Tek unutulan sorgu = firmalar arası veri sızıntısı (BKM 5 firma / tek DB, ilişkili-taraf holding). Denetim + güvenlik riski. Şu an hiçbir otomatik kontrol yok.

## 2. Scope
### Dahili
- Company-kapsamlı tabloların beyaz/kara listesi (firma-bağımsız sistem tabloları hariç: Dictionary*, Meta*, NumberSeries firma-bağımlı vb. ayrımı).
- `Features/**/*.cshtml.cs` içindeki Dapper SQL'lerini tarayan kural/test: company-kapsamlı tablodan SELECT/UPDATE/DELETE varsa `CompanyId` predikatı zorunlu.
- `.claude/rules/security-principles.md` §8'e bağlı somut **otomatik kontrol** (grep tabanlı test veya antipattern hook).
### Dışı
- Runtime interceptor / Dapper wrapper ile zorlama (büyük refactor — ileride değerlendir).

## 3. Alternatifler
- A: Sadece kural metni (mevcut) — Reddedildi: insan unutur, otomatik değil.
- B: Dapper QueryWrapper ile runtime enforcement — Reddedildi (şimdilik): tüm çağrı yerlerini sarmak büyük; ileride.
- C (seçilen): Statik tarama testi/hook — `WHERE CompanyId` içermeyen company-tablo sorgularını yakalar, CI/pre-commit'e bağlanır. Düşük maliyet, yüksek kapsam.

> **Referans deseni (REFERENCE_STUDY.md §3, 2026-05-30 / B1):** Smartstore/nopCommerce ikisi de EF global
> filter kullanmıyor; izolasyon her sorguda elle. Operax `CompanyId` her satırda zorunlu olduğu için zaten
> daha sıkı. Bu plan iki deseni birleştirir:
> - **Desen 1 (birincil): SQL TVF/View `@CompanyId`-sargılı** — mevcut `tvf_AccountBalance`/`tvf_InventoryBalance`
>   deseni genişletilir; okuma tarafı ham `FROM Tablo` yerine `FROM tvf_X(@CompanyId)`. İzolasyon DB'de yaşar (SQL-first uyumlu).
> - **Desen 3 (emniyet ağı): statik analiz guard** — yukarıdaki tarama testi/hook. Tek başına yetersiz, Desen 1'in üstüne.
> - Reddedilen: Desen 4 (marker interface + generic repository) — Transaction Script'e ters.

**5 lens:** 🔴 False-positive (join üzerinden filtre) → whitelist + yorum-pragma ile bastırma. 🔵 Gerçek ihtiyaç: "hiçbir sorgu CompanyId'siz olmasın". 🟢 Aynı tarama IDOR/yetki testine genişler. ⚪ "Neden runtime değil?" → maliyet; statik %80'i yakalar. 🟡 grep + xunit test = yarım gün.

## 4. Done
- [x] Company-kapsamlı tablo listesi tanımlı — 52 doğrudan + 27 dolaylı + 11 global (IsolationScanner.cs, 2026-05-31)
- [x] Tarama testi: CompanyId'siz company-tablo sorgusu → fail — `operax-cli scan-isolation` (exit 1)
- [x] Mevcut ihlaller raporlandı + düzeltildi — 55 aday → 2 gerçek fix + 35 suppress (defense-in-depth) + 20 kalan (Features/Production kırık StockMovement INSERT'leri, ayrı işe alındı)
- [x] `.claude/rules` kuralı — security-principles.md §8'e guard pointer eklendi
- [ ] Sprint-kapanış şartı: production StockMovement düzeltilince guard 0'a inmeli + blocking hook'a bağlanmalı (pre-commit/session-start)

## 5. Adımlar
1. [ ] Company-kapsamlı vs firma-bağımsız tablo envanteri (Meta sözlüğü — Plan 15 ile sinerji)
2. [ ] Tarama testi/script
3. [ ] İhlal sweep + düzelt
4. [ ] Kural + hook

## 6. Onay
- [x] Gösterildi · [x] Onay: 2026-05-29 · Uygulandı: 2026-05-31 (commit 1051e87 guard, 9324c5d+f731136 fix/suppress)

> ⚠️ **BAĞIMLILIK (K10):** İzolasyon güvenliği **plan 13 §3'e bağlı** (Model 3, rol-aware + erişim kontrollü
> switch-company). Bu plan "claim neyse onu süz" der; plan 13 §3 "claim'i ancak hak ettiğin firmaya çevirebilirsin"
> der. switch-company claim'i serbest değiştirilebilirse bu izolasyon **dekoratif kalır** — ikisi birlikte gerekir.

> İlişkili: AR-001, security-principles.md §8, Plan 13 §3 (yetki — güvenlik ön koşulu), Plan 15 (tablo envanteri kaynağı)
