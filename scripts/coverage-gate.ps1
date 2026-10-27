# Gate de cobertura MERGED via coverlet.collector (--collect "XPlat Code Coverage") + ReportGenerator.
#
# Mais robusto para CI que o MergeWith encadeado (que ocasionalmente reportava um suite standalone):
# cada projeto de teste emite um cobertura.xml independente; o ReportGenerator MERGE todos e produz o
# resumo; o threshold é aplicado sobre a cobertura de linha combinada.
#
# Pré-requisitos: coverlet.collector em cada projeto de teste e a ferramenta local ReportGenerator
# (`dotnet tool restore`). Config de cobertura (exclusões/escopo) em backend/coverlet.runsettings.
#
# Uso:  pwsh scripts/coverage-gate.ps1 [-Threshold 80]

param(
    [double]$Threshold = 80
)

$ErrorActionPreference = 'Stop'
$backend = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$results = Join-Path $backend 'TestResults\coverage-collector'
$report = Join-Path $backend 'TestResults\coverage-report'

foreach ($dir in @($results, $report)) {
    if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
}

function Write-Summary([string]$text) {
    if ($env:GITHUB_STEP_SUMMARY) {
        $text | Out-File -FilePath $env:GITHUB_STEP_SUMMARY -Append -Encoding utf8
    }
}

Write-Host "=== Gate de cobertura (collector + ReportGenerator, line >= $Threshold%) ===" -ForegroundColor Cyan
Write-Summary "## Coverage Gate (line >= $Threshold%)"

$testLog = Join-Path $backend 'TestResults\test-output.log'
dotnet test (Join-Path $backend 'ControleFinanceiro.sln') -c Debug --nologo `
    --collect:"XPlat Code Coverage" `
    --results-directory $results `
    --settings (Join-Path $backend 'coverlet.runsettings') `
    --logger "trx;LogFileName=results.trx" `
    2>&1 | Tee-Object -FilePath $testLog
$testExit = $LASTEXITCODE

if ($testExit -ne 0) {
    Write-Host "`nGATE REPROVADO: testes falharam." -ForegroundColor Red
    Write-Summary "`n### :x: Testes falharam (exit code $testExit)"
    # Write last 80 lines of test output to summary
    $tail = Get-Content $testLog -Tail 80 -ErrorAction SilentlyContinue
    if ($tail) {
        Write-Summary "`n<details><summary>Últimas 80 linhas do output</summary>`n`n``````"
        Write-Summary ($tail -join "`n")
        Write-Summary "```````n</details>"
    }
    exit 1
}

Write-Summary "`n### :white_check_mark: Testes passaram"

$coverageReportPattern = Join-Path (Join-Path $results '**') 'coverage.cobertura.xml'
dotnet tool run reportgenerator -- `
    "-reports:$coverageReportPattern" `
    "-targetdir:$report" `
    "-reporttypes:Cobertura;TextSummary;JsonSummary;HtmlSummary"
if ($LASTEXITCODE -ne 0) {
    Write-Host "`nGATE REPROVADO: ReportGenerator falhou." -ForegroundColor Red
    Write-Summary "`n### :x: ReportGenerator falhou"
    exit 1
}

$summary = Get-Content (Join-Path $report 'Summary.json') -Raw | ConvertFrom-Json
$line = [double]$summary.summary.linecoverage

$txtSummary = Get-Content (Join-Path $report 'Summary.txt') -ErrorAction SilentlyContinue |
    Select-Object -First 30
if ($txtSummary) {
    $txtSummary | ForEach-Object { Write-Host $_ }
    Write-Summary "`n<details><summary>Coverage Summary</summary>`n`n``````"
    Write-Summary ($txtSummary -join "`n")
    Write-Summary "```````n</details>"
}

Write-Host ("`nCobertura de linha (merged): {0}%" -f $line) -ForegroundColor Cyan
Write-Summary ("`n**Cobertura de linha (merged): {0}%** (threshold: {1}%)" -f $line, $Threshold)

if ($line -lt $Threshold) {
    Write-Host "GATE REPROVADO: $line% < $Threshold% (line, total)." -ForegroundColor Red
    Write-Summary "`n### :x: GATE REPROVADO: $line% < $Threshold%"
    exit 1
}

Write-Host "GATE APROVADO: $line% >= $Threshold% (line, total)." -ForegroundColor Green
Write-Summary "`n### :white_check_mark: GATE APROVADO: $line% >= $Threshold%"
exit 0
