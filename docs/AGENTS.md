# OPERAX — Paralel Agent Çalışma Stratejisi
> Bu dosya, Claude ile birlikte çalışırken hangi agent'ların ne zaman
> ve nasıl paralel olarak kullanılacağını tanımlar.

---

## Neden Paralel Agent?

Tek bir agent sırayla çalışırken zaman kaybeder.
Paralel agent'lar aynı anda farklı işleri yaparak süreci hızlandırır.

---

## Agent Türleri ve Sorumlulukları

### 1. Build Validator Agent
```
Dosya  : .claude/agents/build-validator.md
Görev  : dotnet build çalıştır, hata/uyarı raporla
Ne zaman: Her sprint sonu, her büyük kod değişikliği sonrası
```

### 2. Code Reviewer Agent
```
Dosya  : .claude/agents/code-reviewer.md
Görev  : RULES.md'ye uyumluluk kontrolü (yorum, UI dili, uzunluk)
Ne zaman: Yeni dosya yazılınca veya sprint tamamlanınca
```

### 3. Test Runner Agent
```
Dosya  : .claude/agents/test-runner.md
Görev  : Test projelerini çalıştır, sonuçları raporla
Ne zaman: Test projesi kurulduktan sonra her sprint sonunda
```

### 4. DB Schema Checker Agent
```
Dosya  : .claude/agents/db-schema-checker.md
Görev  : Schema SQL vs gerçek DB karşılaştırması
Ne zaman: Yeni modüle geçmeden önce, schema sorunu olduğunda
```

---

## Paralel Çalışma Senaryoları

### Senaryo 1: Sprint Sonu Doğrulama (En Sık Kullanılan)

Bir sprint bittiğinde aynı anda 3 agent çalışır:

```
┌─────────────────────┐  ┌─────────────────────┐  ┌─────────────────────┐
│   Build Validator   │  │   Code Reviewer     │  │    Test Runner      │
│                     │  │                     │  │                     │
│ dotnet build        │  │ RULES.md uyum       │  │ dotnet test         │
│ 0 hata? 0 uyarı?    │  │ Türkçe yorum var mı │  │ Tüm testler geçti mi│
│                     │  │ UI Türkçe mi?       │  │                     │
└─────────────────────┘  └─────────────────────┘  └─────────────────────┘
         │                        │                        │
         └────────────────────────┴────────────────────────┘
                              RAPOR
```

**Tetikleyici:** "Sprint X tamamlandı, doğrula"

---

### Senaryo 2: Yeni Modüle Geçiş Hazırlığı

```
┌─────────────────────┐  ┌─────────────────────┐
│  DB Schema Checker  │  │   Build Validator   │
│                     │  │                     │
│ Hedef modülün       │  │ Mevcut build        │
│ tabloları DB'de var │  │ temiz mi?           │
│ mı kontrol et       │  │                     │
└─────────────────────┘  └─────────────────────┘
```

**Tetikleyici:** "Sprint X'e başlıyoruz"

---

### Senaryo 3: Büyük Refactor Sonrası

```
┌─────────────────────┐  ┌─────────────────────┐
│   Build Validator   │  │   Code Reviewer     │
│                     │  │                     │
│ Derleme bozulmadı   │  │ Refactor edilen     │
│ mı kontrol et       │  │ dosyalar kurallara  │
│                     │  │ uyuyor mu?          │
└─────────────────────┘  └─────────────────────┘
```

---

## Agent Çalıştırma Komutları

Aşağıdaki ifadeler ilgili agent'ı tetikler:

| İfade | Tetiklenen Agent |
|---|---|
| "build kontrol et" / "derlemeyi doğrula" | Build Validator |
| "kodu incele" / "kuralları kontrol et" | Code Reviewer |
| "testleri çalıştır" / "test al" | Test Runner |
| "schema kontrol et" / "DB'yi doğrula" | DB Schema Checker |
| "sprint doğrula" / "sprint X bitti" | Build + Review + Test (paralel) |
| "modüle geçiyoruz" | DB Schema + Build (paralel) |

---

## Sprint Doğrulama Protokolü

Her sprint sonunda aşağıdaki adımlar **sırayla** uygulanır:

```
AŞAMA 1 — Paralel Kontrol (aynı anda)
  ├── Build Validator: 0 hata, 0 uyarı
  ├── Code Reviewer: RULES.md uyumu
  └── Test Runner: tüm testler geçti

AŞAMA 2 — Manuel Kabul Testleri
  └── Kullanıcı tarafından uygulama test edilir
      (kabul kriteri listesi docs/SPRINTS.md'de)

AŞAMA 3 — Belge Güncelleme
  ├── PLAN.md: ilgili sprint `DONE` olarak işaretle
  ├── docs/TODO.md: tamamlanan taskları `[x]` yap
  └── docs/BUGS.md: çözülen bugları kaydet

AŞAMA 4 — Bir Sonraki Sprint Hazırlığı
  ├── DB Schema Checker: hedef modül tabloları var mı?
  └── PLAN.md ve docs/SPRINT_X.md okunur, görevler belirlenir
```

---

## Paralel Kod Yazma Stratejisi

Bağımsız dosyalar aynı anda yazılabilir:

### Örnek: M03 Receiving Sprint'i

```
Paralel yazılabilenler (birbirinden bağımsız):
  ├── Receiving/Index.cshtml       ← Liste ekranı
  ├── Receiving/Index.cshtml.cs    ← Liste page model
  └── Receiving/Terminal.cshtml    ← Terminal ekranı

Sırayla yazılacaklar (bağımlılık var):
  1. Receiving/Details.cshtml.cs   ← Önce page model (DTO'lar burada)
  2. Receiving/Details.cshtml      ← Sonra view (DTO'yu kullanır)
```

### Örnek: Birden Fazla Modül Schema'sı

```
Paralel yazılabilenler:
  ├── schema_M03.sql çalıştır      ← Receiving tabloları
  └── seed_receiving_test.sql      ← Test verisi
```

---

## Agent Koordinasyon Notu

- Her agent kendi görevi dışına çıkmaz
- Build Validator kod yazmaz — sadece çalıştırır ve raporlar
- Code Reviewer kod düzeltmez — sadece ihlal raporlar
- Test Runner test yazmaz — sadece çalıştırır ve raporlar
- Düzeltme kararı her zaman ana Claude konuşmasında verilir
