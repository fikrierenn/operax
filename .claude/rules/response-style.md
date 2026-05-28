# Yanıt Stili Kuralları

## Özlülük — Birinci Kural

**Uzun açıklama yasak.** Her yanıt olabildiğince kısa olmalı:

- Değişiklik tek satırda özetlenebiliyorsa paragraf yazma
- "Şimdi X yapıyorum, ardından Y, sonra Z" sıralaması yapma — sadece yap
- Tamamlanan iş için tekrar anlatım yapma — diff zaten gösteriyor
- Seçenekler sunmak gerekiyorsa en fazla 2-3 madde, her biri 1 cümle

## Yasak Kalıplar

- "Mükemmel, harika bir yaklaşım..." — iltifat yok
- "Özet olarak şunları yaptım: ..." — oturum sonu handoff dışında özet yok
- "Şimdi X adımına geçiyorum..." — adım duyurusu yok, direkt geç
- "Bunu yapabilirim, ama önce şunu belirteyim..." — belirtme, yap
- Bullet-point ile madde madde "ne yaptım" listesi (→ sadece sonuç ve "ne sıra" bilgisi)

## İzin Verilen

- Tek cümle durum güncellemesi: "Build yeşil, Faz 4'e geçiyorum."
- Kritik bulgu: "X'te silent failure var, düzeltiyorum."
- Onay gerektiren karar: "X'i silmek üzereyim, devam edeyim mi?"
- Oturum sonu handoff özeti (`/handoff` skill — bu kurala tabi değil)
- Tier 3 plan özeti (plan-first.md gereği)

## Caveman Mode

Kullanıcı `/caveman` veya `CAVEMAN MODE ACTIVE` enjekte ederse:
- Article ve filler kelimeler düş ("the", "a", "şu an", "aslında", "basitçe")
- Kısa cümle/fragment OK
- Technical term aynen kalır
- Kod blokları aynen kalır
- Hata mesajları tırnak içinde aynen alıntılanır

## Ölçüt

Kullanıcı cevabı okumadan tool call sonucuna bakıp ne olduğunu anlayabiliyorsa → metin fazladır.
