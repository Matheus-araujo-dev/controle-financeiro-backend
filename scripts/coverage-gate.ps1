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

Write-Host "=== Gate de cobertura (collector + ReportGenerator, line >= $Threshold%) ===" -ForegroundColor Cyan

dotnet test (Join-Path $backend 'ControleFinanceiro.sln') -c Debug --nologo `
    --collect:"XPlat Code Coverage" `
    --results-directory $results `
    --settings (Join-Path $backend 'coverlet.runsettings') `
    --logger "trx;LogFileName=test-results.trx" `
    --logger "console;verbosity=normal"
$testExitCode = $LASTEXITCODE

if ($testExitCode -ne 0) {
    Write-Host "`n========== TESTES FALHARAM — Detalhes dos TRX ==========" -ForegroundColor Red
    $trxFiles = Get-ChildItem -Path $results -Recurse -Filter "*.trx" -ErrorAction SilentlyContinue
    if ($trxFiles) {
        foreach ($trx in $trxFiles) {
            Write-Host "`n--- TRX: $($trx.FullName) ---" -ForegroundColor Yellow
            try {
                [xml]$xml = Get-Content $trx.FullName -Raw
                $allResults = $xml.TestRun.Results.UnitTestResult
                $failed = $allResults | Where-Object { $_.outcome -eq 'Failed' }
                $totalCount = ($allResults | Measure-Object).Count
                $failedCount = ($failed | Measure-Object).Count
                Write-Host "Total: $totalCount | Falharam: $failedCount" -ForegroundColor Cyan
                foreach ($f in $failed) {
                    Write-Host "`n  FAILED: $($f.testName)" -ForegroundColor Red
                    if ($f.Output -and $f.Output.ErrorInfo) {
                        if ($f.Output.ErrorInfo.Message) {
                            Write-Host "  Message: $($f.Output.ErrorInfo.Message)"
                        }
                        if ($f.Output.ErrorInfo.StackTrace) {
                            $stackLines = $f.Output.ErrorInfo.StackTrace -split "`n" | Select-Object -First 15
                            Write-Host "  StackTrace (first 15 lines):"
                            foreach ($line in $stackLines) {
                                Write-Host "    $line"
                            }
                        }
                    }
                    Write-Host "  ---"
                }
            } catch {
                Write-Host "  Erro ao ler TRX: $_" -ForegroundColor Yellow
            }
        }
    } else {
        Write-Host "  Nenhum arquivo TRX encontrado em $results" -ForegroundColor Yellow
    }
    Write-Host "`n========== FIM DOS DETALHES ==========" -ForegroundColor Red
    Write-Host "`nGATE REPROVADO: testes falharam." -ForegroundColor Red
    exit 1
}

$coverageReportPattern = Join-Path (Join-Path $results '**') 'coverage.cobertura.xml'
dotnet tool run reportgenerator -- `
    "-reports:$coverageReportPattern" `
    "-targetdir:$report" `
    "-reporttypes:Cobertura;TextSummary;JsonSummary;HtmlSummary"
if ($LASTEXITCODE -ne 0) {
    Write-Host "`nGATE REPROVADO: ReportGenerator falhou." -ForegroundColor Red
    exit 1
}

$summary = Get-Content (Join-Path $report 'Summary.json') -Raw | ConvertFrom-Json
$line = [double]$summary.summary.linecoverage

Get-Content (Join-Path $report 'Summary.txt') -ErrorAction SilentlyContinue |
    Select-Object -First 30 | ForEach-Object { Write-Host $_ }

Write-Host ("`nCobertura de linha (merged): {0}%" -f $line) -ForegroundColor Cyan

if ($line -lt $Threshold) {
    Write-Host "GATE REPROVADO: $line% < $Threshold% (line, total)." -ForegroundColor Red
    exit 1
}

Write-Host "GATE APROVADO: $line% >= $Threshold% (line, total)." -ForegroundColor Green
exit 0
