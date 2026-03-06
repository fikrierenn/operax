# Operax SQL Setup Script
# BT-FIKRI\SQLEXPRESS, sa kullanicisi

$server = "BT-FIKRI\SQLEXPRESS"
$user = "sa"
$pass = "***REMOVED***"
$connStr = "Server=$server;User Id=$user;Password=$pass;TrustServerCertificate=True;"
$sqlDir = "d:\Dev\Operax\docs\sql"

function Run-SqlFile {
    param([string]$dbName, [string]$filePath)
    $label = Split-Path $filePath -Leaf
    Write-Host "  >> Running: $label on [$dbName]..." -ForegroundColor Cyan
    try {
        $sql = Get-Content $filePath -Raw -Encoding UTF8
        $conn = New-Object System.Data.SqlClient.SqlConnection("$connStr;Database=$dbName;")
        $conn.Open()
        # GO deyimlerini parcalara bol
        $batches = $sql -split '(?mi)^\s*GO\s*$'
        foreach ($batch in $batches) {
            if ($batch.Trim().Length -gt 0) {
                $cmd = $conn.CreateCommand()
                $cmd.CommandText = $batch
                $cmd.CommandTimeout = 120
                try { $cmd.ExecuteNonQuery() | Out-Null }
                catch {
                    # Eger zaten var hatasi ise devam et
                    if ($_.Exception.Message -notmatch "already exists|There is already") {
                        Write-Host "     WARN: $($_.Exception.Message.Split("`n")[0])" -ForegroundColor Yellow
                    }
                }
            }
        }
        $conn.Close()
        Write-Host "  OK: $label" -ForegroundColor Green
    } catch {
        Write-Host "  ERROR on $label : $_" -ForegroundColor Red
    }
}

# 1. Operax DB olustur
Write-Host "`n[1/3] Creating database Operax..." -ForegroundColor Magenta
$masterConn = New-Object System.Data.SqlClient.SqlConnection("$connStr;Database=master;")
$masterConn.Open()
$cmd = $masterConn.CreateCommand()
$cmd.CommandText = "IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'Operax') CREATE DATABASE Operax"
$cmd.ExecuteNonQuery() | Out-Null
$masterConn.Close()
Write-Host "  OK: Database ready." -ForegroundColor Green

# 2. Sema dosyalari - sirayla
Write-Host "`n[2/3] Running schema files..." -ForegroundColor Magenta
$schemaFiles = @(
    "schema_M00.sql",
    "schema_M01.sql",
    "schema_M01_UOM.sql",
    "schema_M01_Bins.sql",
    "schema_M01_PriceLists.sql",
    "schema_M02.sql",
    "schema_M02_Envanter.sql",
    "schema_M02_BinBalance.sql",
    "schema_M03.sql",
    "schema_M03_PurchaseOrder.sql",
    "schema_M04.sql",
    "schema_M05.sql",
    "schema_M06.sql",
    "schema_M06_Picking.sql",
    "schema_M07_Transfer.sql",
    "schema_M07_Replenishment.sql",
    "schema_M08_CycleCount.sql",
    "schema_M09_Lot.sql",
    "schema_M09_Serial.sql",
    "schema_M09_LPN.sql",
    "schema_M09_Planning.sql",
    "schema_StatusTransitions.sql",
    "schema_M10_Production.sql",
    "schema_M10_DynamicBOM.sql",
    "schema_M10_Routing.sql",
    "schema_M10_Resources.sql",
    "schema_M10_Consumption.sql",
    "schema_M10_QualityControl.sql",
    "schema_M10_Rework.sql",
    "schema_M15_Dashboards.sql",
    "schema_M18_Expenses.sql",
    "schema_M19_Budgeting.sql"
)

foreach ($file in $schemaFiles) {
    Run-SqlFile -dbName "Operax" -filePath "$sqlDir\$file"
}

# 3. Seed data
Write-Host "`n[3/3] Running seed data..." -ForegroundColor Magenta
Run-SqlFile -dbName "Operax" -filePath "$sqlDir\seed_core.sql"
Run-SqlFile -dbName "Operax" -filePath "$sqlDir\seed_dummy_data.sql"

Write-Host "`n==========================================" -ForegroundColor Green
Write-Host "  OPERAX Database Setup Complete!  " -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green
