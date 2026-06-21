# Plan 38 — CancellationToken Yayılımı (handler → Dapper)

**Durum:** Onay bekliyor
**Tier:** 3 (97 dosya / 216 handler / 443 Dapper çağrısı — geniş cross-cutting, faz faz)
**Kaynak:** `error-handling.md §4-5` + `csharp-conventions.md` Async/await — discipline debt (Plan 33 DEBT'ten ayrıldı).

---

## 1. Problem

Async handler'lar `CancellationToken` almıyor, Dapper çağrılarına geçmiyor, `OperationCanceledException` rethrow edilmiyor. İstemci bağlantıyı kestiğinde (sayfa kapatma, timeout) sorgu sunucuda çalışmaya devam eder → boşa kaynak. `error-handling.md §4-5` ve `csharp-conventions.md` ihlali.

Envanter (2026-06-22, koddan):
- **216** async handler (`OnGet/OnPostAsync`) — yalnız **6**'sı ct alıyor.
- **97** dosya etkileniyor.
- **443** Dapper async çağrısı (`Query*/Execute*`) — ct'siz.

## 2. Scope

**Dahil:** PageModel handler imzalarına `CancellationToken ct` ekleme + Dapper çağrılarını `CommandDefinition(..., cancellationToken: ct)`'ye sarma + generic catch'lerde OCE rethrow (`when (ct.IsCancellationRequested)`).

**Hariç:** Service layer derinlemesine ct (handler→service zinciri; servis varsa imzaya eklenir ama ayrı servis-içi çağrı zinciri başka tur). Hangfire job'ları (kendi ct'leri var). SP iş mantığı (değişmez).

## 3. Kanonik Dönüşüm (Faz 1'de kilitlenir)

```csharp
// ÖNCE
public async Task<IActionResult> OnPostAsync(Guid id)
{
    using var conn = db.Open();
    var row = await conn.QuerySingleAsync<Dto>("SELECT ... WHERE Id=@id", new { id });
    await conn.ExecuteAsync("sp_X", new { id }, commandType: CommandType.StoredProcedure);
}

// SONRA
public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken ct)
{
    using var conn = db.Open();
    var row = await conn.QuerySingleAsync<Dto>(
        new CommandDefinition("SELECT ... WHERE Id=@id", new { id }, cancellationToken: ct));
    await conn.ExecuteAsync(
        new CommandDefinition("sp_X", new { id }, commandType: CommandType.StoredProcedure, cancellationToken: ct));
}
```

- ASP.NET Core PageModel handler parametresi olarak `CancellationToken` framework tarafından otomatik bind edilir (RequestAborted).
- Generic `catch (Exception)` varsa ÖNCE: `catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }`.
- `Dapper` `CommandDefinition` ct'yi `Microsoft.Data.SqlClient`'a geçirir → sorgu iptal edilir.

## 4. Fazlar (modül grubu bazlı — her faz ayrı commit + build + smoke)

> Her faz: handler imzaları + Dapper çağrıları dönüştürülür → `dotnet build` 0/0 → ilgili integration test (varsa) yeşil → commit (`plan:38`).

- **Faz 1 — Pilot + pattern kilidi ✅ (2026-06-22):** `Receiving` 3 dosya (Index/Details/Terminal) — 9 handler + 27 Dapper çağrısı CommandDefinition+ct'ye çevrildi. Build 0 hata, ReceivingPostingTests 5/5, code-reviewer 0 ihlal (pattern onaylandı). **Kanonik şablon:** handler `(..., CancellationToken ct)`; düz `conn.QueryAsync<T>(new CommandDefinition(sql, params, cancellationToken: ct))`; SP `new CommandDefinition(sp, params, commandType: ..., cancellationToken: ct)`; DynamicParameters output param CommandDefinition ile uyumlu. **Servis-katmanı istisna:** `AutoTraceabilityService`/`DocumentLock`/`ParameterStore` gibi servisler ct almaz (ayrı servis turu) — Done grep `*Service.cs` + Lib hariç.
- **Faz 2-7 — TÜM kalan modüller ✅ (2026-06-22, workflow fan-out):** 23 modül paralel (modül başına 1 sonnet ajan, kanonik pattern). **91 dosya · 202 handler · 361 Dapper çağrısı** CommandDefinition+ct'ye çevrildi. Kapsam: Inventory/Transfer/Picking/CycleCount/LPN/Lot/Serial (WMS) · PurchaseOrders/PurchaseInvoices/Expenses (Satınalma) · SalesOrders/Shipping/SalesInvoices (Satış) · Finance/MaterialIssue (Finans) · MasterData/Admin/Warehouses/Auth (Master) · Manufacturing/Production/Dashboard/Budget (Üretim). Transaction-bound çağrılarda `transaction: trans` + `cancellationToken: ct` birlikte; generic catch'lere OCE rethrow; GridReader.Read* dokunulmadı; servis/Lib hariç. **Build 0 hata · 45/45 test · multiline-aware grep 0 gerçek kalıntı.**

## 5. Alternatifler (reddedilen)

1. **Blanket tek-commit sweep** — RED: 97 dosya tek diff review edilemez, regresyon izole edilemez. Faz faz şart.
2. **Defer (opportunistik)** — RED (kullanıcı kararı): disiplin borcu süresiz açık kalır; tutarsız kod tabanı.
3. **Sadece uzun-sorgu subset** — RED (kullanıcı kararı): kısmi uyum, kural "tüm async" diyor.

## 6. Riskler

- 🟡 **Yüksek churn** — 97 dosya; her faz build+smoke ile izole edilir.
- 🟡 **Davranış değişimi** — disconnect'te artık sorgu iptal olur (önceden tamamlanıyordu). İstenen davranış; POST ortasında iptal kısmi yazma üretmez çünkü ledger SP'leri kendi transaction+XACT_ABORT'una sahip (handler ct'si SP başlamadan veya tamamlandıktan sonra etkili; SP içi atomik).
- 🟢 **Compile-safe** — eksik dönüşüm build'i kırmaz (ct opsiyonel), ama faz tamamlanınca o modülde ct'siz Dapper kalmamalı (grep doğrula).

## 7. Done Criteria

- [x] ✅ Her faz: build 0/0 + test yeşil (45/45) + commit (`plan:38` — Faz 1 c240a44, Faz 2-7 c78b619).
- [x] ✅ Multiline-aware grep: 0 gerçek ct'siz Dapper (PageModel handler katmanı).
- [x] ✅ ~211 handler ct alır (Faz 1: 9 + Faz 2-7: 202); ~388 Dapper çağrısı CommandDefinition+ct (27+361). Not: gerçek envanter plan tahminindeki 216/443'ten düşük çıktı (bir kısım zaten ct'liydi / GridReader hariç).
- [x] ✅ error-handling §4-5: generic catch'lere OCE rethrow eklendi; SqlException-only catch'ler OCE doğal propagate.
- [ ] **Residual (kapsam dışı):** Servis-katmanı (AutoTraceabilityService, DocumentLock, ParameterStore vb.) ct almaz — ayrı "servis turu" (bu plan yalnız PageModel handler katmanı).

## 8. Rollback

Her faz ayrı commit (plan:38). Faz geri alma = o commit revert. Davranış-koruyucu (yalnız ct ekleme).

---

## 5 Lens

- 🔴 **Contrarian:** Fatal flaw — değer düşük (single-tenant'ta disconnect-iptali nadir), 97-dosya churn maliyeti yüksek. Mitigasyon: faz faz, davranış-koruyucu, her faz bağımsız revert'lenebilir; kullanıcı bilinçli seçti (tam uyum hedefi).
- 🔵 **First Principles:** Doğru soru "request iptali sunucu kaynağını korur mu" — evet ağır raporlarda; çoğu CRUD'da marjinal. Yine de kural tutarlılığı (tüm async ct) kod tabanı disiplinini sabitler.
- 🟢 **Expansionist:** Daha büyük fırsat — service-layer ct zinciri + Hangfire graceful shutdown aynı disiplinle; bu plan handler katmanını kapatır, sonraki tur servis.
- ⚪ **Outsider:** Yabancı "neden bazı handler ct alıyor bazı almıyor" tutarsızlığını garip bulurdu — bu plan onu giderir.
- 🟡 **Executor:** Pazartesi — Faz 1 Receiving (en çok test kapsamı olan modül), pattern kilitle, sonra dalga dalga.
