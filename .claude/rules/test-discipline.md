# Test Disiplini — Testleri Kapatmadan Yap

Kapsam: Yeni feature / bug fix / refactor kapatma kriteri.

## Mutlak Kurallar

1. **Yeni feature / bug fix / refactor → test çalıştırılmadan kapatma.**
   - Etkilenen test'ler `dotnet test` ile **geçti** olmalı (failure veya skip yetmez)
   - "Test yok ama kod doğru görünüyor" yetmez — boşluk varsa **yeni test yaz**, sonra çalıştır
   - **Build yeşil ≠ test yeşil.** Bunu karıştırma.

2. **Yeni test eklendiğinde → en az 1 koşum yap.**
   - Test dosyasını yazmak yetmez; `dotnet test` ile çalıştır + sonucu raporla
   - Failure varsa **fix et veya scaffolding'i geri al** — yarım kalmış test commit'leme

3. **Test yokken bug fix → en az regression test yaz.**
   - Fix öncesi: failing test (bug'ı reproduce eden)
   - Fix sonrası: aynı test geçer
   - Sonraki regression yakalanır

4. **Refactor → mevcut test seti yeşil kalmalı.**
   - Refactor öncesi: `dotnet test` N/N geçti not
   - Refactor sonrası: aynı sayı geçti (veya artmış)
   - Test sayısı azalırsa kasıtlı silme dışında **regression sinyali**

## Operax Mevcut Durum (2026-05-28)

- `src/Operax.Cli/` — manuel test (CLI komutları)
- `src/Operax.Web/` — **otomatik test yok** (henüz)
- SQL SP test: manuel via `operax-cli query`
- Smoke test: tarayıcıdan manuel

**Acil eklenmesi gerekenler:**
1. `src/Operax.Tests/` xUnit projesi
2. Smoke test: `/login`, `/Dashboard`, `/api/switch-company`
3. SP integration test (test DB schema + seed + SP çağırıp sonuç doğrula)
4. PageModel unit test (Dapper IDbConnection mock)

## Anti-pattern

1. **"Build yeşil, tamam sayalım"** — test çalıştırılmadı, runtime kırık olabilir
2. **Test yazıp çalıştırmadan commit etmek** — failure git history'ye girer
3. **`[Skip]` ile testi by-pass etmek** — sebep dokümante edilmeden devre dışı kabul edilmez
4. **Failure'ı "ileride bakacağım" diye bırakmak** — stale-claim olur (`.claude/rules/todo-verification.md`)
5. **SP testini sadece dotnet build ile teyit etmek** — SP runtime'da `THROW` ile patlar, build görmez

## Workflow

> **⚠️ Build öncesi preview server'ı DURDUR.** Çalışan `dotnet run`/preview server `bin/.../Operax.Web.exe`'yi kilitler → `dotnet build` **MSB3027/MSB3021 exe-kopyalanamadı** verir. Bu KOD hatası DEĞİL, dosya kilidi. `.cshtml`/CSS değişikliği de runtime'a yansımaz (recompile yok) → her zaman: `preview_stop` → `dotnet build` → `preview_start`. (2026-06-24 dersi, çok kez yaşandı.)

### Yeni feature / bug fix kapatma kontrolü
```bash
# 1. Build (preview server durdurulduktan SONRA)
dotnet build src/Operax.Web/Operax.Web.csproj --nologo

# 2. Test (varsa)
dotnet test --filter "FullyQualifiedName~<Area>" --nologo

# 3. Migrate + seed (SP değişikliği varsa)
dotnet run --project src/Operax.Cli -- migrate
dotnet run --project src/Operax.Cli -- seed

# 4. Manuel smoke (SP değişti veya UI değişti)
# Browser'da ilgili sayfa açıp test
```

### Commit message'da test sayısı
```
fix(M11): çek tahsil SP bug

Test: dotnet test → 0/0 (manual smoke OK)
SP test: operax-cli query "SELECT ... FROM v_AccountBalance" -> doğru bakiye
```

## Tetikleyiciler

- Yeni `[Fact]` / `[Theory]` eklendiğinde
- Controller / PageModel / Service / SP değiştiğinde (regression riski)
- "Tamam / kapatıyorum / bitti" sözcüğü kullanıldığında (commit öncesi)
- `dotnet build` çalıştırıldığında (`dotnet test` ardından gelmeli)

## Override

Kullanıcı açıkça "test çalıştırmadan commit" / "test atla" derse:
- Commit message'da `[test-skipped: <gerekçe>]` notu
- `docs/TODO.md`'ye "test koşumu borcu" satırı
- Bir sonraki oturum başında session-start hook bunu uyarır

Aksi default = **test koşumu zorunlu**.

## İlişkili

- `.claude/rules/coding-discipline.md` — surgical changes
- `.claude/rules/todo-verification.md` — kanıt disiplini (build = test değil)
- `.claude/rules/csharp-conventions.md` — async test, vanilla `Assert`
