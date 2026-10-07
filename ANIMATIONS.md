# Bloops animation list

How each one is made:
- **loop**: intro (base -> pose), then a loop that starts and ends on that pose. Outro = intro played backwards.
- **one-shot**: starts and ends on the base sprite, plays once.

## Activities (what Claude is doing)
1. [x] Reading (Read/Grep/Glob): tiny glasses, eyes scan left/right, glint (loop)
2. [x] Typing (Edit/Write): tiny keyboard, arms mash it, sparks (loop)
3. [x] Running commands (Bash/PowerShell): cranks a handle, vibrates, steam puff (loop)
4. [x] Browsing (WebFetch/WebSearch): telescope/magnifier, peering around (loop)
5. [x] Thinking (between steps): eyes up, lightbulb flicker (loop)
6. [x] Delegating (Agent): splits into 2 mini blobs, merges back (one-shot)

## Moods + events
7. [x] Needs you (Notification): jumping + waving both nubs (loop)
8. [x] Done (Stop): victory spin / wiggle (one-shot)
9. [x] Command failed: pancake flat, X X eyes, pops back round (one-shot)
10. [x] Long task (5+ min working): sweat + headband, try-hard (loop)
11. [x] Sleep: snot bubble grows/shrinks (loop)
12. [x] Spawn: drops in from above, squish landing (one-shot)
13. [x] Despawn: little goodbye wave, then poof (one-shot)

## Interactive
14. [x] Pet (hover): ^ ^ eyes, happy wiggle (loop)
15. [x] Carried (dragging): dangling, feet kicking (loop)
16. [x] Dropped: squish on landing (one-shot)
17. [x] Idle extra: yawn (one-shot)
18. [x] Idle extra: juggling a pixel (one-shot)
19. [x] Idle extra: looks around (one-shot)
20. [x] Blob bump / high-five when two blobs are close (one-shot, mirrored for left)

## Notes (v0.2)
- Done in code (stretching the sprite): spawn fall, drop/land squish, fail pancake, carried sway, busy hops.
- Snooze came out as "sleeps inside a floating bubble" (kept, no mouth).
- For review: yawn opens a mouth, split cuts top/bottom.
- Props get recolored with the body (headband, telescope, bulb shift hue too).
21. [x] Rage (PowerShell): angry steam face (loop). Bash gets the spinning gear instead.

## v2 pack
22. [x] Squish (PreCompact): stress-ball squash, code-stretched + squint art (one-shot)
23. [x] Git push: tosses a package up and away (one-shot, from Bash/PowerShell command text)
24. [x] Deploy: rocket launch for fly deploy / wrangler deploy (one-shot)
25. [x] Denied: arms crossed pout (PermissionDenied, or a PermissionRequest that ends in Stop with no tool run)
26. [x] Agent squad: 3+ subagents running -> 3 mini blobs orbit him (code, SubagentStart/Stop count)
27. [x] Music: headphones + head bob when tool input has .mp3/.wav/.mid/etc (until Stop)
28. [x] Downloading: bucket catching pixels (curl/wget/winget/iwr/pip/npm install/git clone) (loop)
29. [x] Hats (menu, saved per chat): party, wizard, crown, headphones, pumpkin, santa. Not recolored, ride on the head each frame.
30. [x] Rare idle (1 in 8 extras): dozes off mid-juggle and drops the pixel; or high-fives nobody, then looks around
31. [x] Holiday hats: pumpkin on Oct 31, santa all December (unless a hat or "none" is picked)
