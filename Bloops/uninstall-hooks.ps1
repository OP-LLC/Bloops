# Bloops by Online Perseverance - removes Bloops' hooks from Claude Code settings.
# Only entries whose command talks to localhost:47321 are removed; every other hook is left alone.
$path = Join-Path $env:USERPROFILE ".claude\settings.json"
if (-not (Test-Path $path)) { Write-Host "No settings.json found, nothing to remove."; exit }
$json = Get-Content $path -Raw | ConvertFrom-Json
if (-not $json.hooks) { Write-Host "No hooks in settings.json, nothing to remove."; exit }
Copy-Item $path "$path.bak-bloops"

$removed = 0
foreach ($ev in @($json.hooks.PSObject.Properties.Name)) {
    $before = @($json.hooks.$ev)
    $kept = @($before | Where-Object { $_ -and -not ($_.hooks.command -match "47321") })
    $removed += $before.Count - $kept.Count
    if ($kept.Count) { $json.hooks | Add-Member $ev $kept -Force } else { $json.hooks.PSObject.Properties.Remove($ev) }
}
[IO.File]::WriteAllText($path, ($json | ConvertTo-Json -Depth 20), (New-Object Text.UTF8Encoding $false))  # no BOM
Write-Host "Removed $removed Bloops hook entries from $path (backup: settings.json.bak-bloops)"
