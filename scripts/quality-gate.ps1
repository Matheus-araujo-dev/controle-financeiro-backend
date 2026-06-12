# Quality Gate Script for Backend
# Runs build, test with coverage thresholds, and verifies results

param(
    [int]$Threshold = 80
)

$ErrorActionPreference = "Stop"

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

Write-Host "`n[3/4] Running tests with coverage (threshold $Threshold%)..." -ForegroundColor Yellow
dotnet test --configuration Release --no-build --collect:"XPlat Code Coverage" /p:CollectCoverage=true /p:CoverletOutputFormat=opencover /p:Threshold=$Threshold /p:ThresholdType=line

if ($LASTEXITCODE -ne 0) {
    Write-Host "`nQUALITY GATE FAILED: Tests failed or coverage below $Threshold%" -ForegroundColor Red
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