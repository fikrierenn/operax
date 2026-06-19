---
description: "Oturumda çözülen anlamlı bir sorunu kalıcı kurala/derse dönüştürür (ECC /learn — instinct pattern). Aynı hata 2. kez yaşanmasın."
argument-hint: "[öğrenilen ders — boş bırakılırsa oturumdan çıkarılır]"
allowed-tools: ["Read", "Grep", "Glob", "Edit", "Write"]
---

# /learn — Çözülen Sorundan Kalıcı Ders

Oturumda debug edilip çözülen anlamlı sorunları kalıcılaştırır. `session-memory.md` "aynı hatayı 2. kez yapıyorum → rule yaz" kuralının yürütücüsü.

## Adımlar

1. **Dersi çıkar:** Argüman verilmişse onu kullan; verilmemişse bu oturumda çözülen sorunları tara (hata → kök neden → çözüm üçlüsü net olanlar). Önemsizleri (typo, tek seferlik) ALMA.

2. **Sınıflandır → hedef dosya:**
   | Ders tipi | Hedef |
   |---|---|
   | Kod yazım kuralı (C#/Razor/SQL) | `.claude/rules/csharp-conventions.md` / `razor-conventions.md` / `sql-conventions.md` |
   | Süreç/davranış kuralı | İlgili `.claude/rules/*.md` (yoksa yeni dosya) |
   | Anti-pattern (silme/refactor kazası) | `.claude/rules/before-major-change.md` § Anti-pattern |
   | Modüle özgü domain bilgisi | `docs/MODULE_SPECS/M*.md` ilgili bölüm |
   | Tek seferlik bağlam | journal'a not — rule'a YAZMA |

3. **Atomic yaz:** Bir ders = bir madde. Format: **ne yapma → neden → doğrusu** (+ tarih). Mevcut benzer madde varsa güncelle, duplike etme.

4. **Bildir:** Hangi dosyaya ne eklendiğini tek satırda raporla.

## Kurallar
- Kullanıcı onayı olmadan kural SİLME (ekleme serbest).
- CLAUDE.md'ye yazma — o fihrist; dersler rules/'a.
- Spekülatif "ileride lazım olur" dersi yazma — sadece gerçekleşen hata.
