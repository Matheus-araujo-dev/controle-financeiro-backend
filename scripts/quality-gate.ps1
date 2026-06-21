# Quality Gate Script for Backend
# Runs build, test with coverage thresholds, and verifies results

param(
    # Threshold do relatório MERGED de cobertura (line, stat=total). Atingido: 80,6% (alvo de 80% OK).
    [int]$Threshold = 80
)

$ErrorActionPreference = "Stop"

# Garante que os comandos 'dotnet' (restore/build/list) rodem na raiz do backend,
# independentemente do diretório de onde o script foi chamado.
Set-Location (Resolve-Path (Join-Path $PSScriptRoot '..'))

Write-Host "=== Backend Quality Gate ===" -ForegroundColor Cyan
Write-Host "Threshold: $Threshold%" -ForegroundColor Cyan

Write-Host "`n[0/3] Cleaning up processes..." -ForegroundColor Yellow
Get-Process -Name "ControleFinanceiro.Api" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1

Write-Host "`n[1/4] Restoring packages..." -ForegroundColor Yellow
dotnet restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "RESTORE FAILED" -ForegroundColor Red
    exit 1
}
Write-Host "Restore succeeded" -ForegroundColor Green

Write-Host "`n[2/4] Building solution..." -ForegroundColor Yellow
dotnet build --configuration Release --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "BUILD FAILED" -ForegroundColor Red
    exit 1
}
Write-Host "Build succeeded" -ForegroundColor Green

Write-Host "`n[3/4] Running tests with MERGED coverage gate (line >= $Threshold%)..." -ForegroundColor Yellow
# Gate aplicado sobre o relatório merged das 4 suítes (coverlet.collector + ReportGenerator).
& (Join-Path $PSScriptRoot 'coverage-gate.ps1') -Threshold $Threshold

if ($LASTEXITCODE -ne 0) {
    Write-Host "`nQUALITY GATE FAILED: Tests failed or merged coverage below $Threshold%" -ForegroundColor Red
    exit 1
}

Write-Host "`n[4/4] Checking for vulnerabilities..." -ForegroundColor Yellow
dotnet list package --vulnerable
if ($LASTEXITCODE -ne 0) {
    Write-Host "VULNERABILITIES FOUND" -ForegroundColor Red
    exit 1
}
Write-Host "No vulnerabilities found" -ForegroundColor Green

Write-Host "`n[5/5] Verifying results..." -ForegroundColor Yellow
Write-Host "QUALITY GATE PASSED: All tests passed with >= $Threshold% coverage" -ForegroundColor Green
exit 0