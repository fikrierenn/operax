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

**5 lens:** 🔴 False-positive (join üzerinden filtre) → whitelist + yorum-pragma ile bastırma. 🔵 Gerçek ihtiyaç: "hiçbir sorgu CompanyId'siz olmasın". 🟢 Aynı tarama IDOR/yetki testine genişler. ⚪ "Neden runtime değil?" → maliyet; statik %80'i yakalar. 🟡 grep + xunit test = yarım gün.

## 4. Done
- [ ] Company-kapsamlı tablo listesi tanımlı
- [ ] Tarama testi: CompanyId'siz company-tablo sorgusu → fail
- [ ] Mevcut ihlaller raporlandı + düzeltildi (varsa)
- [ ] `.claude/rules` kuralı + sprint-kapanış şartı

## 5. Adımlar
1. [ ] Company-kapsamlı vs firma-bağımsız tablo envanteri (Meta sözlüğü — Plan 15 ile sinerji)
2. [ ] Tarama testi/script
3. [ ] İhlal sweep + düzelt
4. [ ] Kural + hook

## 6. Onay
- [ ] Gösterildi · [ ] Onay: <tarih>

> İlişkili: AR-001, security-principles.md §8, Plan 15 (tablo envanteri kaynağı)
