# PostgreSQL Geçiş Pilotu — Gerçek Çeviri Örneği

SQL Server (T-SQL) → PostgreSQL (PL/pgSQL). 2 gerçek SP + C# çağrı + gotcha listesi.
Amaç: gerçek eforu somut göstermek.

---

## 1. BASİT — sp_DepositCheque (guard + UPDATE + THROW)

### T-SQL (mevcut)
```sql
CREATE OR ALTER PROCEDURE dbo.sp_DepositCheque
    @ChequeId UNIQUEIDENTIFIER, @AccountId UNIQUEIDENTIFIER,
    @CompanyId UNIQUEIDENTIFIER, @DepositDate DATETIME2 = NULL, @UserId UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    DECLARE @Status NVARCHAR(20);
    SELECT @Status = Status FROM Cheque WHERE Id = @ChequeId AND CompanyId = @CompanyId AND IsDeleted = 0;
    IF @Status IS NULL THROW 60001, N'Çek bulunamadı.', 1;
    IF @Status <> 'PORTFOLIO' THROW 60002, N'Sadece portföydeki çekler bankaya verilebilir.', 1;
    UPDATE Cheque SET Status='IN_BANK', DepositedToAccountId=@AccountId,
        DepositedAt=ISNULL(@DepositDate,GETUTCDATE()), UpdatedAt=GETUTCDATE(), UpdatedBy=@UserId
    WHERE Id=@ChequeId;
END
```

### PostgreSQL (port)
```sql
CREATE OR REPLACE FUNCTION sp_deposit_cheque(
    p_cheque_id uuid, p_account_id uuid, p_company_id uuid,
    p_deposit_date timestamptz DEFAULT NULL, p_user_id uuid DEFAULT NULL
) RETURNS void LANGUAGE plpgsql AS $$
DECLARE v_status text;
BEGIN
    SELECT status INTO v_status FROM cheque
    WHERE id = p_cheque_id AND company_id = p_company_id AND is_deleted = false;
    IF v_status IS NULL THEN
        RAISE EXCEPTION 'Çek bulunamadı.' USING ERRCODE = 'OP601';
    END IF;
    IF v_status <> 'PORTFOLIO' THEN
        RAISE EXCEPTION 'Sadece portföydeki çekler bankaya verilebilir.' USING ERRCODE = 'OP602';
    END IF;
    UPDATE cheque SET status='IN_BANK', deposited_to_account_id=p_account_id,
        deposited_at=COALESCE(p_deposit_date, now()), updated_at=now(), updated_by=p_user_id
    WHERE id=p_cheque_id;
END $$;
```

**Çeviri haritası (her SP'de tekrarlanır):**
| T-SQL | PostgreSQL |
|---|---|
| `UNIQUEIDENTIFIER` | `uuid` |
| `NVARCHAR(20)` | `text` |
| `DATETIME2` | `timestamptz` |
| `@param` | `p_param` |
| `DECLARE @x; SELECT @x=` | `DECLARE v_x; SELECT ... INTO v_x` |
| `THROW 60001, N'msg', 1` | `RAISE EXCEPTION 'msg' USING ERRCODE='OP601'` |
| `ISNULL(a,b)` | `COALESCE(a,b)` |
| `GETUTCDATE()` | `now()` |
| `IsDeleted = 0` | `is_deleted = false` (bit→bool) |
| `SET XACT_ABORT ON` | (gereksiz — fonksiyon atomik) |

---

## 2. KARMAŞIK — sp_MaterialIssuePost (ledger: guard + TVF + OUTER APPLY + INSERT-SELECT)

### Kritik dönüşümler (T-SQL → PG)
| T-SQL | PostgreSQL | Not |
|---|---|---|
| `BEGIN TRY/TRANSACTION ... CATCH` | (yok) — fonksiyon **kendiliğinden atomik** | PG daha basit; hata = otomatik rollback |
| `EXEC sp_GuardPeriodOpen @a,@b` | `PERFORM sp_guard_period_open(p_a,p_b)` | |
| `WITH (UPDLOCK, ROWLOCK)` | `... FOR UPDATE` | satır kilidi |
| `OUTER APPLY (SELECT ...) bal` | `LEFT JOIN LATERAL (SELECT ...) bal ON true` | korelasyonlu alt-sorgu |
| `SELECT TOP 1 @x = ... ORDER BY y` | `SELECT ... INTO v_x ... ORDER BY y LIMIT 1` | |
| `tvf_InventoryBalance(@CompanyId)` | `tvf_inventory_balance(p_company_id)` | TVF → `RETURNS TABLE` fonksiyon |
| `INSERT INTO X SELECT ...` | aynı yapı | iç içe `TOP 1` → `LATERAL`/`LIMIT 1` |

### PL/pgSQL gövdesi (özet — guard + ledger insert)
```sql
CREATE OR REPLACE FUNCTION sp_material_issue_post(
    p_header_id uuid, p_company_id uuid, p_user_id text
) RETURNS void LANGUAGE plpgsql AS $$
DECLARE
    v_now timestamptz := now();
    v_warehouse_id uuid; v_status text; v_doc_no text;
    v_short_item text; v_picking_bin uuid; v_branch_id uuid;
BEGIN
    PERFORM sp_guard_period_open(p_company_id, v_now, p_user_id);   -- EXEC → PERFORM

    SELECT warehouse_id, status, doc_no INTO v_warehouse_id, v_status, v_doc_no
    FROM material_issue_header
    WHERE id = p_header_id AND company_id = p_company_id
    FOR UPDATE;                                                     -- UPDLOCK → FOR UPDATE

    IF v_warehouse_id IS NULL THEN RAISE EXCEPTION 'Sarf fişi bulunamadı.' USING ERRCODE='OP1550'; END IF;
    IF v_status = 'POSTED' THEN RAISE EXCEPTION 'Sarf fişi zaten onaylanmış.' USING ERRCODE='OP1551'; END IF;
    -- ... (CANCELLED, kalem yok guard'ları aynı kalıp)

    -- Negatif stok guard — OUTER APPLY → LATERAL
    SELECT i.name INTO v_short_item
    FROM material_issue_line l
    JOIN item i ON i.id = l.item_id
    LEFT JOIN LATERAL (
        SELECT COALESCE(SUM(inv.qty_balance),0) AS on_hand
        FROM tvf_inventory_balance(p_company_id) inv
        WHERE inv.warehouse_id = v_warehouse_id AND inv.item_id = l.item_id
    ) bal ON true
    WHERE l.header_id = p_header_id AND bal.on_hand < l.qty_base
    LIMIT 1;
    IF v_short_item IS NOT NULL THEN
        RAISE EXCEPTION 'Yetersiz stok — depo bakiyesi sarf miktarını karşılamıyor.' USING ERRCODE='OP1554';
    END IF;

    -- ... picking bin fallback + branch (TOP 1 → LIMIT 1, ISNULL → COALESCE)

    -- Stok çıkışı: INSERT ... SELECT (yapı birebir; iç TOP 1 → LATERAL/LIMIT)
    INSERT INTO stock_movement
        (company_id, warehouse_id, bin_id, item_id, movement_type,
         qty_base, uom_id, qty_original, unit_cost,
         source_doc_type, source_doc_id, source_doc_no, created_by, branch_id)
    SELECT p_company_id, v_warehouse_id,
        COALESCE(l.bin_id, (
            SELECT inv.bin_id FROM tvf_inventory_balance(p_company_id) inv
            WHERE inv.warehouse_id=v_warehouse_id AND inv.item_id=l.item_id AND inv.qty_balance>0
            ORDER BY inv.qty_balance DESC LIMIT 1
        ), v_picking_bin),
        l.item_id, 'ISSUE', -l.qty_base, l.uom_id, l.qty, COALESCE(ic.avg_cost,0),
        'CONSUMPTION', p_header_id, v_doc_no, p_user_id, v_branch_id
    FROM material_issue_line l
    LEFT JOIN item_cost ic ON ic.company_id=p_company_id AND ic.item_id=l.item_id
    WHERE l.header_id = p_header_id;

    UPDATE material_issue_header SET status='POSTED', updated_at=v_now WHERE id=p_header_id;
END $$;
```

**Mantık birebir aynı** — sadece sözdizimi. İş kuralı (negatif stok guard, bin fallback, maliyet, ledger) hiç değişmedi.

---

## 3. C# ÇAĞRI TARAFI — hata yönetimi (her catch bloğu)

### Mevcut (SQL Server)
```csharp
catch (SqlException sqlEx) when (sqlEx.Number >= 50000 && sqlEx.Number < 60000)
{ TempData["Error"] = sqlEx.Message; }   // iş kuralı → kullanıcıya
```

### PostgreSQL (Npgsql)
```csharp
catch (PostgresException pgEx) when (pgEx.SqlState?.StartsWith("OP") == true)
{ TempData["Error"] = pgEx.MessageText; }   // iş kuralı → kullanıcıya
```

- `sqlEx.Number` (50000-59999) → `pgEx.SqlState` ('OP' önekli özel kod)
- SP çağrısı: `conn.ExecuteAsync("SELECT sp_material_issue_post(@p_header_id,...)", ...)` (CommandType.Text — fonksiyon SELECT'le çağrılır) veya `CALL` (procedure)
- `Microsoft.Data.SqlClient` → `Npgsql` paketi

---

## 4. GERÇEK EFOR — neyin ne kadar tuttuğu

| İş | Hacim | Zorluk | Not |
|---|---|---|---|
| **53 SP** T-SQL→PL/pgSQL | 53 | Orta | Yukarıdaki kalıp tekrar; mantık aynı, sözdizimi mekanik |
| **Identifier casing** ⚠️ | TÜM sorgular | Düşük ama yaygın | PascalCase→snake_case VEYA her yeri `"PascalCase"` quote. Her sorgu dokunulur — **en büyük volume** |
| **Ham Dapper sorgular** | yüzlerce | Düşük | TOP→LIMIT, ISNULL→COALESCE, GETDATE→now, [x]→"x", DATEDIFF |
| **Hata yönetimi** | tüm catch | Düşük | Number→SqlState konvansiyon |
| **TVF/View** | ~10 | Orta | RETURNS TABLE / view dialect |
| **Identity store** | DapperUserStore/Role | Düşük | Sorgu portu |
| **Hangfire** | 1 paket | Düşük | Hangfire.PostgreSql |
| **operax-cli migrate** | şema | Orta | Tip + sözdizimi |
| **E2E retest** | tüm zincir | Yüksek-önem | Bu oturum kurduğum smoke pattern'iyle (PO→cost, loan, payment, merge) |

**Tahmin:** ~2-4 hafta odaklı çalışma. En büyük volume = identifier casing (her sorgu), en yüksek risk = ledger SP'leri (retest şart). Mantık yeniden yazılmıyor — **çeviri.**

---

## 5. KARAR
- **Bu pilot kanıtlıyor:** mantık DB'de kalıyor, sözdizimi çevriliyor. EF'in aksine **mimari korunur**.
- **Açılan kapı:** Postgres → Oracle Cloud Free ARM (24GB, bedava 7/24) veya Neon/Supabase managed free.
- **Sonraki adım:** tam plan (`plans/NN-postgres-migration.md`) — faz: (1) şema+tip (2) TVF/View (3) 53 SP grup grup (4) Dapper sorgu sweep (5) C# hata+paket (6) E2E retest.
