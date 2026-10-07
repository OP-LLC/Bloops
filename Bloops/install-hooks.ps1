# Bloops by Online Perseverance - adds Claude Code hooks that ping Bloops on localhost.
# What it does: backs up settings.json, then for each event below adds ONE hook entry that pipes the event JSON
# to Bloops on localhost:47321 with curl (silent, gives up in 0.3s if Bloops is closed). Other hooks are never touched.
# Run again any time; it replaces old Bloops hooks instead of adding duplicates. Undo with uninstall-hooks.ps1.
$path = Join-Path $env:USERPROFILE ".claude\settings.json"
$json = if (Test-Path $path) { Get-Content $path -Raw | ConvertFrom-Json } else { [pscustomobject]@{} }
Copy-Item $path "$path.bak-bloops" -ErrorAction SilentlyContinue

$cmd = 'curl -s --connect-timeout 0.3 -m 1 -H "Content-Type: application/json" --data-binary @- http://localhost:47321/ >/dev/null 2>&1 || true'
if (-not $json.hooks) { $json | Add-Member hooks ([pscustomobject]@{}) }

foreach ($ev in "SessionStart","UserPromptSubmit","PreToolUse","PostToolUse","PostToolUseFailure","SubagentStart","SubagentStop","Stop","Notification","SessionEnd","PreCompact","PermissionRequest","PermissionDenied") {
    $list = @($json.hooks.$ev | Where-Object { $_ -and -not ($_.hooks.command -match "47321") })
    $list += [pscustomobject]@{ hooks = @([pscustomobject]@{ type = "command"; command = $cmd; timeout = 3 }) }
    $json.hooks | Add-Member $ev $list -Force
    Write-Host "  + $ev"
}
[IO.File]::WriteAllText($path, ($json | ConvertTo-Json -Depth 20), (New-Object Text.UTF8Encoding $false))  # no BOM
Write-Host "Bloops hooks installed in $path (backup: settings.json.bak-bloops)"
