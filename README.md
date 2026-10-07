# Bloops, a desktop buddy for Claude Code

A pixel desktop buddy for Claude Code: one blob per chat, reacts live to what Claude is doing.

![Bloops reacting to Claude Code](docs/demo.gif)

> Bloops is a fun visual only. It never approves permissions, never runs commands, and never sends anything online. It just listens on your own PC and animates.

## Features

- **One Bloop per chat.** Each Claude Code chat gets its own blob, sitting on your taskbar.
- **Activities.** Reads with glasses (Read/Grep), types on a tiny keyboard (Edit/Write), spins a gear (Bash), fumes (PowerShell), peeks through a telescope (web), thinks with a lightbulb.
- **Moods and events.** Waves when Claude needs you, victory hop when it's done, pancakes when a command fails, sweats on long tasks, pouts when a permission is denied.
- **Special moves.** Tosses a package on `git push`, launches a rocket on deploys, catches pixels in a bucket while downloading, wears headphones when you work on audio files, gets squished on `/compact`.
- **Agent squad.** When 3+ subagents run at once, mini Bloops orbit around him.
- **Hats.** 26 of them: party hat, wizard hat, crown, headphones, halo, goggles beanie, rubber duck, rain cloud, moon, sprout, orca, shark fin, choir note, dino hood, Raspberry Pi, hard hat, grad cap, chef hat, traffic cone, mushroom, frog, captain hat, donut, OP sticker, plus holiday hats (pumpkin on Oct 31, santa all December).
- **Done flag.** Waves a little flag in its color when Claude finishes, until you click it, send a message, or 30 seconds pass. Shows a "!" bubble when Claude needs you.
- **Sounds.** A soft ding when Claude is done. Turn on All sounds for spawn, poof, needs-you and error sounds. Volume and mute in Settings.
- **Colors.** Hue wheel and preset swatches. Colors and hats are saved per chat.
- **Idle life.** Chills when quiet, falls asleep after 30 min, waves goodbye and poofs after another 30. Random yawns, juggling, looking around, high-fives between neighbors, and a few rare surprises.
- **Drag him anywhere.** Click and drag to carry him, let go to drop. Click without dragging for the menu.

## Requirements

- Windows 10 or 11
- Claude Code

## Install

1. Download `Bloops.exe` from [Releases](../../releases) and put it anywhere (nothing else to install).
2. Run it. Windows may say "Windows protected your PC" because the exe isn't code-signed (that costs money every year, and this is a free hobby app). Click **More info > Run anyway**. Don't trust it? Read the source here first.
3. Click **Yes** when Bloops asks to connect to Claude Code. A Bloop appears the next time a chat does something.

Connecting backs up `%USERPROFILE%\.claude\settings.json` to `settings.json.bak-bloops`, then adds one hook entry per event. It never edits or removes your other hooks. The same scripts are in this repo as `install-hooks.ps1` / `uninstall-hooks.ps1` if you'd rather run them yourself.

Click a Bloop > **Settings** for speed, sounds, Start with Windows, and Connect/Disconnect.

## Uninstall

1. Click a Bloop > Settings > **Disconnect from Claude**, then **Quit Bloops**.
2. Turn off Start with Windows first if you turned it on.
3. Delete `Bloops.exe`, and `%APPDATA%\Bloops` (saved colors, hats, speed) if you want.

## How it works

```
Claude Code ──hook──> curl POST http://localhost:47321/ ──> Bloops.exe ──> animation
```

- Claude Code [hooks](https://docs.claude.com/en/docs/claude-code/hooks) run a tiny `curl` command on events like `PreToolUse`, `Stop` and `Notification`, sending the event JSON to Bloops on your own machine.
- If Bloops isn't running, the command gives up in 0.3 seconds and stays silent, so Claude Code is never slowed down or shown errors.
- Bloops only listens on localhost. It ignores requests from web browsers, oversized requests and bad JSON.
- Bloops looks at the event name, tool name and command text only to pick an animation (for example `git push` = package toss). Nothing is stored except your color, hat and speed picks. Start with Windows adds one value under your user's Run registry key.
- No telemetry, no accounts, no network calls besides localhost.

Hooked events: `SessionStart`, `UserPromptSubmit`, `PreToolUse`, `PostToolUse`, `PostToolUseFailure`, `PermissionRequest`, `PermissionDenied`, `SubagentStart`, `SubagentStop`, `PreCompact`, `Notification`, `Stop`, `SessionEnd`.

## FAQ

**Can Bloops approve permissions or run things for me?**
No. The hook only sends data to Bloops and prints nothing back, so Claude Code makes every decision exactly as it would without Bloops. When Claude needs you, the Bloop just waves.

**Why did a Bloop disappear?**
It waves goodbye and poofs when its chat ends, or after an hour with no activity. It comes back the next time that chat does something, with the same color and hat.

**Does it slow Claude Code down?**
No. The hook call takes a few milliseconds, and fails instantly when Bloops is closed.

**macOS or Linux?**
Not yet. Bloops is built with WPF, which is Windows only.

**Port 47321 is taken?**
Bloops will tell you on launch. Close whatever is using it, or change the port in `App.xaml.cs` and `install-hooks.ps1`.

**Found a bug, or Bloops froze?**
Right-click the Bloops tray icon > **Open log folder**, and attach `error.log` to a [GitHub issue](../../issues). Bloops logs crashes and freezes (with the last few events) there. Nothing is ever sent anywhere automatically.

## Building from source

```
cd Bloops
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o ../dist
```

## Credits

Made by **Online Perseverance**. Sprites made with [PixelLab](https://pixellab.ai) and edited by hand.

Not affiliated with Anthropic. Claude is a trademark of Anthropic.

Source-available, free to use. Not open source: no reselling or re-uploading. See [LICENSE](LICENSE).
