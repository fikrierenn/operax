---
name: screen-ux-standard
description: Operax ekranlarını kullanıcı-dostu + pratik yapma standardı. Yeni ekran yazarken veya mevcut ekranı elden geçirirken danış. Form akışı, otomatik doldurma, klavye, boş durum, hata geri bildirimi, satır girişi kalıpları. "ekran gözden geçir", "ux", "kullanıcı dostu yap", "ekranı iyileştir" denildiğinde çağrılır.
allowed-tools: Read, Grep, Glob, Edit
user-invocable: true
model: inherit
---

# Operax Ekran UX Standardı

**Amaç:** Her ekran veri girişini hızlandırsın, tıklama/yazım sayısını azaltsın, hatayı erken yakalasın. "Çalışıyor" yetmez — **pratik** olmalı. Bu skill ekran yazımı/elden geçirme sırasında danışılır; `ui-standard.md` görsel katmanı, bu skill **etkileşim/akış** katmanını tanımlar.

İlişkili: `.claude/rules/ui-standard.md` (CSS/partial), `.claude/rules/razor-conventions.md`, `.claude/rules/turkish-ui.md`, `.claude/rules/inline-style-guard.md`.

---

## 1. Otomatik Doldurma (En Yüksek Etki)

Kullanıcının zaten DB'de olan bir veriyi elle girmesi = hata + yavaşlık. Bir alan başka bir seçimden türetilebiliyorsa **otomatik gelir**.

| Seçim | Otomatik gelen | Kaynak |
|---|---|---|
| **Ürün seçildi** | Birim (UOM) | `Item.BaseUomId` |
| **Ürün seçildi** (satış) | Satış fiyatı + KDV oranı | `Item.SalesPrice`, `Item.TaxRate` |
| **Ürün seçildi** (alış) | Önerilen alış fiyatı | `ItemCost.AvgCost` (son maliyet) veya tedarikçi PriceList |
| **Tedarikçi/Müşteri seçildi** | Ödeme vadesi, para birimi, vergi no | `Partner.PaymentTermDays/Currency/TaxNumber` |
| **Tedarikçi seçildi** (mal kabul) | Açık siparişler filtrelenir | `tvf_OpenPurchaseOrders` PartnerId |
| **Tarih alanı** (yeni belge) | Bugün | `DateTime.Today` (asla MinValue → SqlDateTime taşması) |
| **Belge no** | Seri'den otomatik | `NumberSeries.NextAsync` (kullanıcı yazmaz) |

**Uygulama (client-side):** Sayfada satır verisini JS sözlüğüne göm (`data-*` attribute veya `<script>` JSON), seçim `change` event'inde hedef alanları doldur. Sunucuya ekstra round-trip gerekmez. Doldurulan değer **override edilebilir** (öneri, kilit değil) — kullanıcı fiyatı değiştirebilir.

```javascript
// Ürün seçimi → birim + fiyat otomatik
var map = @Html.Raw(itemJson);  // { itemId: { uomId, uomName, price, taxRate } }
itemSelect.addEventListener('change', function () {
    var d = map[this.value]; if (!d) return;
    if (uomSelect && !uomSelect.value) uomSelect.value = d.uomId;
    if (priceInput && !priceInput.value) priceInput.value = d.price;
});
```

---

## 2. Form Akışı

- **Tek kolon yerine mantıksal gruplama:** ilişkili alanlar yan yana (`form-row`/`form-row-3`), grup başlıkları.
- **Zorunlu alanlar işaretli:** `<span class="req">*</span>` + `required`.
- **Tab sırası doğal:** üst→alt, sol→sağ. `tabindex` ile bozma.
- **İlk anlamlı alana autofocus:** sayfa açılınca ilk boş zorunlu alan odaklanır.
- **Kaydet'ten sonra nereye:** yeni kayıt → detay sayfası (id ile redirect); satır ekleme → aynı sayfa (modal kapanır, satır görünür).
- **Çift submit engeli:** submit'te buton disabled + "Kaydediliyor…".

---

## 3. Satır Girişi (Evrak Detayı)

Mal kabul / sipariş / fatura satırları en sık girilen ekran. Kritik kalıplar:

- **Ürün araması:** uzun listede `<select>` yetmez → arama kutusu (yazınca filtrele) veya combobox.
- **Seçili ürün otomatik doldurur** (§1): birim + fiyat + KDV.
- **Satır ekledikten sonra** input'lar temizlenir, focus tekrar ürün alanına (ardışık giriş hızlı).
- **Anlık ara toplam:** satır ekl/sil → toplam JS ile güncellenir, kaydet beklenmez.
- **Yanlış satır kolay silinir:** her satırda "Çıkar" + onay (yalnızca DRAFT'ta).
- **Klavye:** Enter = satır ekle, Esc = modal kapat.

---

## 4. Açık/İlişkili Kayıt Seçimi

Bir belge başka bir belgeye bağlanırken (mal kabul→sipariş, fatura→irsaliye):

- **Bağlamla filtrele:** tedarikçi seçiliyse sadece o tedarikçinin açık siparişleri.
- **Aranabilir:** belge no ile hızlı bulma kutusu.
- **Zengin etiket:** sadece "PO-001" değil → `PO-001 — Tedarikçi (12 May 2026)`.
- **Boş durum açık:** "Seçili tedarikçinin açık siparişi yok" ipucu.

---

## 5. Geri Bildirim ve Hata

- **Başarı:** `TempData["Success"]` → yeşil banner (`_Layout` global). Sessiz başarı yok.
- **İş kuralı hatası (SP THROW 50000-59999):** `TempData["Error"]` → kullanıcıya Türkçe mesaj.
- **Sistem hatası:** generic mesaj + log; `ex.Message` gösterme.
- **Doğrulama:** alan altında `form-error`, submit öncesi client-side asgari kontrol.
- **Yükleniyor:** uzun işlemde spinner/disabled; donmuş ekran yok.
- **Yıkıcı işlem onayı:** Sil/İptal → `confirm()` veya modal.

---

## 6. Boş Durum (Empty State)

Veri yoksa boş tablo değil → `_EmptyState` partial: ikon + "Henüz X yok" + birincil aksiyon butonu ("İlk X'i ekle"). Kullanıcı ne yapacağını bilsin.

---

## 7. Durum-Bağlı Görünüm

`document-immutability.md` ile uyumlu:
- **DRAFT:** alanlar editable, "Kaydet/Onayla/Sil" butonları.
- **POSTED:** alanlar readonly, "İptal Et/Yazdır/Denetim İzi".
- **CANCELLED:** sadece "Yazdır".
- Buton seti duruma göre; tıklanamaz aksiyonu gösterme (gizle veya disabled+tooltip).

---

## 8. Ekran Elden Geçirme Checklist'i

Bir ekranı gözden geçirirken sırayla:
1. Türetilebilen alan elle mi giriliyor? → otomatik doldur (§1).
2. Tarih/belge no/birim default geliyor mu?
3. Uzun dropdown aranabilir mi?
4. İlişkili kayıt seçimi bağlamla filtreli + aranabilir mi (§4)?
5. Satır ekleyince focus geri dönüyor + toplam anlık güncel mi (§3)?
6. Başarı/hata geri bildirimi var mı, sessiz fail yok mu (§5)?
7. Boş durum yönlendiriyor mu (§6)?
8. DRAFT/POSTED görünüm doğru mu (§7)?
9. `ui-standard.md` görsel + `turkish-ui.md` dil uyumlu mu?
10. Inline style (renk/font) yok mu (`inline-style-guard.md`)?

---

## 9. Önceliklendirme (tüm ekran programı)

Tek seferde tüm ekranlar elden geçmez. Sıra:
1. **En sık kullanılan veri-giriş ekranları:** Sipariş, Mal Kabul, Fatura satır girişi (otomatik doldurma en yüksek etki).
2. **Cari/Ürün kartları:** sık açılan, çok alanlı.
3. **Liste/rapor ekranları:** filtre + arama + sıralama.
4. **Admin/ayar ekranları:** en son.

Her ekran ayrı commit; faz sonu `phase-review-gate.md` zinciri.
