param(
  [Parameter(Mandatory = $true)][string]$Base,
  [string]$Pin = "1234"
)
# Прогон E2E против стенда XDiary (TE-007..009). Требует .NET 8 SDK + доступ к стенду (Tailscale).
$env:XDIARY_E2E_BASE_URL = $Base
$env:XDIARY_E2E_PIN = $Pin
$proj = Join-Path $PSScriptRoot "E2E.Tests\E2E.IntegrationTests.csproj"
Write-Host "== E2E -> $Base (pin=$Pin) =="
dotnet test $proj --verbosity normal
