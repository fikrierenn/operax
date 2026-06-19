# Plan 20 — Genel Bildirim Hub (Email/SMS/WhatsApp/KEP)

**Tarih:** 2026-06-01 · **Durum:** `Taslak (onay bekliyor)` · **Modül:** M16 (Integration Bridge) · **Kaynak:** kullanıcı talebi + plan 19 SentChannel

> **3 skill doğrulandı:** reference-researcher (ERPNext Communication+EmailQueue / Odoo mail.message+mail.notification),
> mali-evrak-mevzuat (TTK md.18/3 KEP / 6563 İYS / KVKK saklama). Stack KOPYALANMADI — Dapper/SP/Hangfire sabit.

---

## 1. Problem

Sistemde merkezi bildirim/iletişim altyapısı yok. Her olay (mutabakat, fatura, ödeme hatırlatma,
sevkiyat) kendi başına gönderim yapamıyor. Plan 19 `SentChannel` tek-kolon → çoklu kanal modellenemez.
Gönderim izi, durum takibi (teslim/okundu), retry, şablon, mevzuat-kanal eşleşmesi yok.

## 2. Scope (genel hub — kullanıcı kararı)

### Dahili — 3 tablo (ERPNext/Odoo deseni)
- **`NotificationMessage`** (kanonik iz — ERPNext Communication / Odoo mail.message):
  - Id, CompanyId, **SourceDocType + SourceDocId** (polymorphic — mevcut AM/StockMovement deseni)
  - PartnerId NULL, Subject, BodyTemplate, RenderedBody, Direction (OUTBOUND/INBOUND)
  - **LegalChannelRequirement** (NONE / QUALIFIED) — ihtar/itiraz QUALIFIED (TTK md.18/3)
  - **IsCommercialMessage** BIT — ticari ileti İYS kontrolü (6563 md.6/3)
  - **RetentionClass** (LEGAL_DOCUMENT 10yıl / COMMERCIAL_CONSENT 1yıl / OPERATIONAL KVKK-kısa)
  - CreatedAt/By, IsDeleted
- **`NotificationDispatch`** (per-kanal gönderim + retry — Email Queue / mail.notification):
  - Id, CompanyId, MessageId (FK)
  - **Channel** (EMAIL/SMS/WHATSAPP/KEP — CHECK)
  - Recipient, Status (QUEUED/SENDING/SENT/DELIVERED/READ/FAILED/CANCELLED)
  - **FailureType** (TRANSIENT/INVALID_ADDRESS/PROVIDER_REJECT — retry kararı) + FailureReason
  - ChannelPayload (JSON — provider msg id, KEP delil kaydı)
  - SentAt, **DeliveredAt** (KEP tebliğ tarihi = itiraz süresi başlangıcı), ReadAt
  - RetryCount, NextRetryAt, SendAfter, ProviderId (FK)
- **`NotificationTemplate`** (şablon — mutabakat/fatura/ödeme hatırlatma):
  - Id, CompanyId, Code, Channel, Subject, BodyTemplate (placeholder), IsActive
- **`NotificationProvider`** (provider soyutlama — EBelgeProvider deseni, ApiSecretEnc DPAPI şifreli)

### SP/Job
- `sp_NotificationEnqueue` — mesaj + dispatch satır(lar)ı oluştur; mevzuat guard (aşağıda)
- `sp_NotificationApplyReceipt` — webhook teslim/okundu → dispatch Status + DeliveredAt/ReadAt (idempotent)
- Hangfire `NotificationDispatcher` — QUEUED + NextRetryAt<=now; FailureType bazlı backoff
- C# `INotificationChannel` adapter (SmtpChannel/SmsChannel/WhatsAppChannel/KepChannel) — Status'a değil SP üzerinden yazar (SQL-first)

### Mevzuat guard'ları (mali-evrak)
- **TTK md.18/3:** LegalChannelRequirement=QUALIFIED → yalnızca KEP/NOTER kanalı; EMAIL/SMS/WhatsApp THROW
- **6563 İYS:** IsCommercialMessage=1 → gönderim öncesi İYS onay kontrolü (onay yoksa skip/THROW); fatura/bakiye/mutabakat muaf (saf bilgilendirme şartıyla)
- **WhatsApp:** yalnızca LegalChannelRequirement=NONE; ihtar/itiraz'da disabled (delivery receipt resmi delil değil)
- **KVKK imha:** RetentionClass süresi sonunda anonimleştirme/purge job

### Kapsam Dışı (ilk sürüm)
- Gerçek provider API entegrasyonu (SMTP/SMS gateway/WhatsApp Business API) — adapter iskelet, gerçek M16 fazı
- İn-app bildirim merkezi (Odoo inbox) — ayrı
- Pazarlama kampanya yönetimi (İYS toplu) — ayrı

## 3. Alternatifler
- **A: Kanal başına ayrı tablo** (EmailLog/SmsLog/...) — Reddedildi: 4x SP, 4x retry, birleşik "bu belgeye ne gönderildi" sorgusu imkânsız.
- **B: Tek mesaj + per-dispatch satır (seçilen)** — Odoo mail.notification deseni; çoklu kanal doğal, ortak alan %80.
- **C: Domain tablolarına gömülü SentChannel** (mevcut) — Reddedildi: çoklu kanal/durum/retry yok.

## 4. Riskler
| Risk | Önlem |
|---|---|
| Webhook çift gelir (teslim 2 kez) | sp_NotificationApplyReceipt idempotent (ProviderMsgId + Status guard) |
| Mevzuat kanal ihlali (ihtar email'le) | sp_NotificationEnqueue QUALIFIED guard THROW |
| İYS onaysız ticari ileti | IsCommercialMessage=1 → İYS kontrol |
| Retry sonsuz döngü | FailureType=INVALID_ADDRESS terminal; TRANSIENT max retry + backoff |
| KVKK veri saklama aşımı | RetentionClass + imha job |

## 5. Done Criteria
- [ ] 4 tablo (Message/Dispatch/Template/Provider) şema
- [ ] sp_NotificationEnqueue + mevzuat guard (QUALIFIED/İYS)
- [ ] sp_NotificationApplyReceipt idempotent
- [ ] Hangfire NotificationDispatcher + FailureType backoff
- [ ] INotificationChannel adapter iskelet (4 kanal, gerçek API stub)
- [ ] Plan 19 mutabakat → NotificationDispatch bağı + Log.SentAt sync (DeliveredAt'ten)
- [ ] KVKK imha job (RetentionClass)
- [ ] sql-sp-reviewer + security-reviewer

## 6. Rollback
4 tablo DROP, SP CREATE OR ALTER önceki, Hangfire job kaldır, adapter DI sök.

## 7. Adımlar
- [ ] **Faz 1:** 4 tablo şema (schema_M16_Notification.sql)
- [ ] **Faz 2:** sp_NotificationEnqueue (mevzuat guard) + sp_NotificationApplyReceipt
- [ ] **Faz 3:** C# INotificationChannel adapter iskelet + DI
- [ ] **Faz 4:** Hangfire NotificationDispatcher job + FailureType backoff
- [ ] **Faz 5:** Plan 19 mutabakat entegrasyonu (dispatch + DeliveredAt→Log.SentAt + itiraz süresi)
- [ ] **Faz 6:** KVKK imha job + UI iz görünümü
- [ ] **Faz 7:** sql-sp-reviewer + security-reviewer + smoke

## 8. 5 Lens
- 🔴 **Contrarian:** Provider API yok → adapter stub'da kalırsa hub "boş kabuk". İlk sürüm en az SMTP gerçek olmalı.
- 🔵 **First Principles:** Soru "nasıl gönderirim" değil "ne gönderildi + mevzuata uydu mu + izi var mı".
- 🟢 **Expansionist:** Temel = tüm sistem olay→bildirim (sevkiyat, ödeme, stok min). Bu onun omurgası.
- ⚪ **Outsider:** "Mesaj + Dispatch ayrı 2 tablo neden?" → çoklu kanal + retry mekaniği kaydı kirletmesin.
- 🟡 **Executor:** Önce tablo + enqueue + SMTP adapter; SMS/WhatsApp/KEP sonra.

## 9. İlişkili
- `docs/MODULE_SPECS/M16_Integration_EInvoice_Carrier.md` — WebhookEvent inbound altyapısı (tüketici)
- `docs/sql/schema_M04_EBelge.sql` — Envelope+Queue+Provider deseni (referans şablon)
- `plans/19-partner-reconciliation-statement.md` — SentChannel (hub tüketicisi); itiraz süresi DeliveredAt'ten
- TTK md.18/3 (KEP ihtar) · 6563 md.6/3 (İYS bilgilendirme istisna) · KVKK md.7 (imha) · TTK md.82 (10 yıl)
- `.claude/skills/mali-evrak-mevzuat/SKILL.md` — KEP/İYS/saklama notları (eklenecek)

## 10. Mevzuat DOĞRULANMADI (production öncesi YMM/hukuk + birincil metin)
- İYS onay/gönderim kaydı saklama: 1 yıl mı 3 yıl mı (yönetmelik birincil)
- KEP teslim tarihi = kutuya ulaşma mı okuma mı (BTK KEP yönetmeliği)
- TTK md.94 itiraz süresi kanuni mi sözleşmesel mi
- WhatsApp delivery receipt TR mahkeme emsal delil değeri
- 6563 B2B ticari ileti istisnası tam lafız
