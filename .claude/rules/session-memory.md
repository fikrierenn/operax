# Oturum Hafızası Kuralları

Bu dosya, Claude/Antigravity'nin bağlam çöküşü (context window depletion) yaşamasını önlemek ve projeye dair hafızasını her zaman taze tutmak için uyulması gereken bağlam kurallarını tanımlar.

---

## 1. Üç Katmanlı Bilgi Ayrımı

Bilgi kaybını ve mükerrer güncellemeleri önlemek amacıyla, her bilgi **tam olarak tek bir yerde** saklanır:

| Katman | Nerede Saklanır | Ne Yazılır | Örnek |
|---|---|---|---|
| **Kimlik (Identity)** | `CLAUDE.md` | Proje tanımı, tech stack, kurallar indeksi, hızlı referanslar. | "Dapper, net10.0, Razor Pages." |
| **Kurallar (Rules)** | `.claude/rules/*.md` | Konuya göre ayrılmış davranış, kodlama, UI ve git kuralları. | C# yorum standartları, SQL kuralları. |
| **Süreç (Process)** | `PLAN.md`, `TODO.md`, `journal/` | Sprint planları, ekran TODO listeleri, günlük oturum günlükleri. | "Sprint 1 in progress", "2026-05-26 günlüğü". |

*   **Kural:** Aynı bilgi iki farklı dosyada yaşayamaz. Eğer bir kural değişirse konuşmada bırakılmaz, doğrudan ilgili `.claude/rules/` dosyası güncellenir.

---

## 2. Bağlam Sıkıştırma (Compact) ve Temizleme (Clear) Disiplini

1.  **`/compact` Ne Zaman Tetiklenir?**
    *   Konuşma uzunluğu ve bağlam doluluğu %60'ı aştığında.
    *   Uzun dosya okuma ve arama sequence'ları bittikten sonra detayı temizlemek için.
    *   **Compact Uygulaması:** Compact tetiklendiğinde odaklanılan ana görev özetlenir, yan dallar elenir.
2.  **`/clear` Ne Zaman Tetiklenir?**
    *   Üzerinde çalışılan modül veya ana görev tamamen değiştiğinde (örn: Master Data'dan Hangfire ayarlarına geçiş).
    *   Claude'un yanlış varsayımlar veya döngüsel hatalar üretmeye başladığı "poisoned" durumlarında.
3.  **Compact Sonrası Hayatta Kalan Bilgi Matrisi:**

    | Veri / Katman | Compact Sonrası Durum | Açıklama |
    |---|---|---|
    | `CLAUDE.md` | ✅ Her zaman korunur | Kök dizinde olduğu için sistem otomatik yeniden yükler. |
    | `.claude/rules/*.md` | ✅ Her zaman korunur | Dosya referansı sistem tarafından re-inject edilir. |
    | Konuşma Geçmişi | ❌ Kaybolur | Sadece kısa bir özet kalır. |
    | Auto-Memory | ⚠️ İlk 200 satır | Kısıtlı bellek taşınır. |

*   **Kritik Kural:** Compact sonrasında kritik kuralların unutulmaması için tüm kod standartları ve mimari kararlar `.claude/rules/` dizininde, paths filtresi **kullanılmadan** (küresel) tanımlanmalıdır.
