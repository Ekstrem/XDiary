param(
  [string]$BaseUrl = $env:XDIARY_E2E_BASE_URL,
  [string]$Token = $env:XDIARY_E2E_TOKEN,
  [int]$Minutes = 10,
  [int]$PerMinute = 1,
  [string]$Out = "load-report.csv"
)

# TE-010: baseline 1 tpm. Без внешних зависимостей (PowerShell 5+/7).
if ([string]::IsNullOrWhiteSpace($BaseUrl)) { Write-Error "Задай XDIARY_E2E_BASE_URL"; exit 1 }
if ([string]::IsNullOrWhiteSpace($Token))   { Write-Error "Задай XDIARY_E2E_TOKEN"; exit 1 }

$base = $BaseUrl.TrimEnd('/')
$headers = @{ Authorization = "Bearer $Token" }
$rows = New-Object System.Collections.Generic.List[object]
$ok = 0; $fail = 0

$total = $Minutes * $PerMinute
for ($i = 0; $i -lt $total; $i++) {
  $sw = [System.Diagnostics.Stopwatch]::StartNew()
  try {
    $r = Invoke-WebRequest "$base/api/v1/journal/records" -Headers $headers -UseBasicParsing -TimeoutSec 30
    $code = [int]$r.StatusCode
    $status = if ($code -eq 200) { "ok" } else { "http_$code" }
  } catch {
    $status = "error"
    $code = 0
  }
  $sw.Stop()
  if ($status -eq "ok") { $ok++ } else { $fail++ }
  $rows.Add([pscustomobject]@{ at = (Get-Date).ToString("o"); status = $status; code = $code; ms = [math]::Round($sw.Elapsed.TotalMilliseconds, 1) })
  Start-Sleep -Milliseconds ([int](60000 / $PerMinute))
}

$rows | Export-Csv -Path $Out -NoTypeInformation -Encoding UTF8

$lat = $rows | Where-Object { $_.status -eq "ok" } | ForEach-Object { $_.ms }
$p50 = if ($lat) { ($lat | Sort-Object)[[int]($lat.Count * 0.5)] } else { $null }
$p95 = if ($lat) { ($lat | Sort-Object)[[math]::Min($lat.Count - 1, [int]($lat.Count * 0.95))] } else { $null }

Write-Host ("Итераций: {0}, ok: {1}, fail: {2}" -f $total, $ok, $fail)
Write-Host ("Латентность p50: {0} ms, p95: {1} ms" -f $p50, $p95)
Write-Host ("Отчёт: {0}" -f $Out)
