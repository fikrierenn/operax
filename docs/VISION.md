# Operax Vizyonu — Real-Time Enterprise Control System

> **Karar (2026-05-29):** Operax bir ERP/WMS yazılımı olarak değil, **kategori yaratan operasyon beyni** olarak konumlandırılır. Tüm ürün anlatımı, site içeriği ve roadmap bu vizyona hizalanır.

---

## 1. Temel Fark

| | Klasik ERP | Operax |
|---|---|---|
| Soru | "Ne oldu?" | "Şu an ne oluyor ve 5 dakika sonra ne olacak?" |
| Doğa | Kayıt + rapor (geçmiş) | Gerçek zamanlı kontrol (şimdi + gelecek) |
| Rol | Veriyi depolar | Veriyle şirketi yönetir |

ERP işletmeyi **kaydeder**. Operax işletmeyi **çalıştırır**.

## 2. Katman Modeli (3 katman)

### 🟤 Alt Katman — ERP (Truth System)
Resmi kayıt, finans, muhasebe, e-belge. **Değişmez.** Belge bütünlüğü burada yaşar.

### 🔵 Orta Katman — Operax (Canlı Sistem)
Operasyon akışı, stok hareketi, üretim akışı, lojistik koordinasyon. Gerçek zamanlı.

### 🔴 Üst Katman — Decision Layer (Şirket Beyni)
Analiz, öneri, otomasyon, aksiyon. (→ bkz. Plan 07 AI Asistanı = bu katmanın ilk implementasyonu.)

## 3. Operax Ne Satar?

**Satmadığı:** ERP yazılımı / WMS / stok sistemi (commodity).

**Sattığı:** *"Operasyon gecikmesini ortadan kaldırma sistemi."*

## 4. Değer Önermeleri (parayı bunlar üretir)

1. **Zaman kazancı** — karar süresi ↓, operasyon gecikmesi ↓
2. **Hata azalması** — manuel işlem ↓, stok hatası ↓
3. **Görünürlük** — kör alan yok, gerçek zamanlı kontrol

## 5. Stratejik Gerçek

- **ERP diye satarsan:** commodity olursun, fiyat rekabetine girersin, SAP gölgesinde kalırsın.
- **Operax olarak satarsan:** kategori yaratırsın, premium fiyat alırsın, SAP'nin üstüne çıkarsın.

Oyun: ERP implementer değil → **Enterprise Operation Brain Builder.**

## 6. Ürün Tanımı (net)

> **Operax = Real-Time Enterprise Control System**
> Bu bir yazılım değil, kategori kurma projesidir.

## 7. Bu Vizyonun Ürüne Etkisi (uygulama notları)

- **Site/pazarlama dili:** "ERP & WMS" değil → "operasyon beyni / gerçek zamanlı kontrol" dili.
- **Decision Layer önceliği:** Plan 07 (AI/RAG/alarm/agent) sadece "özellik" değil, vizyonun çekirdeği. Faz 2 (proaktif alarm — "5 dakika sonra ne olacak") ve Faz 3 (aksiyon) stratejik olarak en değerli.
- **3 katman ayrımı mimaride korunur:** ERP truth (immutable) / Operax canlı / Decision ayrı katman.
- **Roadmap kararları** bu doktrine göre filtrelenir: bir özellik "kayıt mı tutuyor yoksa şirketi mi çalıştırıyor?"

## 8. İlişkili

- `plans/07-ai-assistant-rag-alarms.md` — Decision Layer ilk implementasyon
- `site/` — kurumsal anlatım (bu vizyona hizalanacak)
- `docs/COMPETITOR_ANALYSIS.md` — SAP/Logo/Mikro karşı konumlandırma
- `docs/MASTER_ROADMAP.md` — modül öncelik (Decision Layer yukarı çekilir)
