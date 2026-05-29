# Plan 13 — /api/switch-company Güvenlik Sıkılaştırma

**Tarih:** 2026-05-29 · **Durum:** `Taslak` · **Modül:** M00 · **Kaynak:** AR-003 (🟠 YÜKSEK)

## 1. Problem
`Program.cs:97-116` `/api/switch-company`:
- `.DisableAntiforgery()` → CSRF açık (kötü niyetli sayfa kullanıcının firmasını değiştirebilir).
- Hedef firmaya kullanıcının **erişim yetkisi kontrol edilmiyor** — herhangi bir `companyId` doğrudan `company` claim'i olarak yazılıyor. Kullanıcı yetkisi olmayan firmaya geçebilir → **yetki atlama + veri sızıntısı** (AR-001 ile birleşince ağır).

## 2. Scope
### Dahili
- Antiforgery'yi geri ekle (sidebar formu zaten `@Html.AntiForgeryToken()` üretiyor — `DisableAntiforgery()` kaldır, token doğrula).
- Firma-erişim yetki kontrolü: kullanıcı yalnızca **kendisine atanmış firmalara** geçebilir. Kaynak: kullanıcının izinli firma listesi (claim seti / yeni `UserCompany` tablosu — AÇIK SORU bkz. §3).
### Dışı
- Tam firma-bazlı RBAC (kullanıcı firma başına farklı rol) — AÇIK TASARIM SORUSU #3, ayrı iş.

## 3. Açık Tasarım Sorusu (Fikri kararı bekliyor)
Kullanıcının izinli firmaları nerede tutulacak?
- (a) Çoklu `company` claim (her izinli firma bir claim) — basit, mevcut yapıya yakın.
- (b) `UserCompany(UserId, CompanyId, Role)` tablosu — firma-bazlı rol için zemin (AÇIK SORU #3 ile birleşir).
Karar verilmeden enforcement yazılmaz (varsayım yapma).

## 4. Alternatifler
- A: Sadece antiforgery ekle, yetki kontrolü yok — Reddedildi: asıl açık (yetki atlama) kalır.
- B: Çoklu claim ile izin — hızlı, ama firma-bazlı rol vermez.
- C: UserCompany tablosu — tam çözüm, daha çok iş. Karar §3.

**5 lens:** 🔴 Tek-firmalı kullanıcıda switcher zaten görünmüyor (userCompanies.Count>1) ama endpoint hâlâ açık → doğrudan POST riski. 🔵 Gerçek ihtiyaç: "yalnız yetkili firmaya geç". 🟢 UserCompany → firma-bazlı RBAC açar. ⚪ "switcher tüm firmaları listeliyor" (Layout: `SELECT ... FROM Company`) → o da yetkiye göre filtrelenmeli. 🟡 antiforgery=5dk; yetki=karara bağlı.

## 5. Done
- [ ] DisableAntiforgery kaldırıldı, token doğrulanıyor
- [ ] Yetkisiz companyId → 403/redirect (claim yazılmaz)
- [ ] Sidebar firma listesi yetkiye göre filtreli
- [ ] Audit log: firma değiştirme (kim, hangi firma)

## 6. Onay
- [ ] Gösterildi · [ ] §3 kararı alındı · [ ] Onay: <tarih>

> İlişkili: AR-003, AR-001, security-principles.md, Açık Soru #3 (firma-bazlı yetki)
