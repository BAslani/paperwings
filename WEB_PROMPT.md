# Prompt: PaperWings Web (Office Dogfight)

> Paste everything below the line into a new Claude Code session opened in a **new, empty folder**
> (e.g. `D:\Dev\Projects\paperwings-web`). The Unity project at `D:\Dev\Projects\paperwings` is the
> reference: read from it, write to it only where step 0 says so. Screenshots are in
> `D:\Dev\Projects\paperwings\Screenshots\web-prompt\`.

---

## Goal

Build a browser version of **PaperWings**, my Unity game, cut down to **one room (the Office)** and
**one mode (Dogfight)**, keeping the **Hangar upgrade flow** (earn paper clips, unlock planes and
per-plane upgrades) but with cheaper, faster progression.

The Unity project at `D:\Dev\Projects\paperwings` is the source of truth for look, feel and numbers.
Read the C# before guessing. When this prompt and the Unity code disagree, this prompt wins
(it's the web scope); otherwise match Unity.

## Hard constraints

1. **One JavaScript file.** It will be served from an edge server where I can upload a single
   `.js` file. The build output is `dist/paperwings.js`: when loaded (via a one-line
   `<script src>` or served as the whole page by a tiny edge handler), it creates its own canvas,
   styles and UI. **Everything is inlined**: geometry, textures, sounds, CSS. No other network
   requests, no CDN, no external fonts. Also emit `dist/index.html` (just the script tag) for
   local testing.
2. **Size budget: under 2.5 MB minified, under 1 MB gzip.** Print both sizes at the end of every
   build and fail the build if over budget.
3. **Stack:** TypeScript + Three.js (npm, bundled) + Vite (library/IIFE mode, single chunk,
   `inlineDynamicImports`). No React. Plain DOM + CSS for menus and HUD.
4. **Controls:**
   - **Desktop: keyboard only.** Do **not** port the Unity mouse-aim scheme. Use the Unity
     "Classic" scheme (screenshot `12-controls-strip.jpg`): `W/S` pitch (W dives), `A/D` bank and
     turn, `Space` hold to wind up and release to throw, `Shift` boost, `F` fire staples (hold),
     `G` fire pencil missile, `R` reset to the desk, `Q/E` change plane on the desk,
     `Esc` pause menu. Arrow keys mirror WASD.
   - **Mobile/touch:** a virtual left stick (pitch + bank), right-side buttons for Fire (hold),
     Pencil, Boost, and a big "Hold to throw" button on the desk. Touch UI only shows on touch
     devices. Lock to landscape (show a "rotate your phone" card in portrait).
   - Menus work with keyboard (arrows/Enter/Esc), mouse clicks and touch.
5. **Save** progress in `localStorage` (wrapped in try/catch; game must still run if it fails).

## Scope

**In:**
- The Office room only (geometry, lighting mood, air currents).
- Dogfight mode: shoot down **5** Accounting rivals; run timer; medals; results screen.
- Throw from the desk, crash/respawn back at the desk (a crash costs time, never ends the run).
- 4 planes (folds) with their own flight stats: Arrowhead (start), Crane, Manta, Dragon.
- Weapons: staple gun (all planes), pencil missiles (upgrade).
- Paper integrity (damage), boost, stalls, air currents (fan push, radiator/laptop thermals, vent downdraft).
- Hangar: per-plane upgrades bought with paper clips, unlock gating by wins.
- Main menu, Hangar, pause menu, results, a small Options panel (volume, difficulty, show touch controls).
- Procedural audio (WebAudio synth: paper whoosh by airspeed, staple "tick", hit, crumple, UI clicks). No audio files.

**Out (do not build):** other rooms, campaigns/nights, Flight School, Ring Run, Free Flight,
Squadron, bosses, wingmen, the cat, couriers/escort, wetness/steam effects, mouse aim, gamepad,
the dev HUD and dev panel (except a hidden `?debug` URL flag for testing).

## Step 0: export the Office from Unity (do this first)

The Office isn't an imported model. It was assembled by many editor builder scripts over time
(`Assets/_Project/Editor/Stage1Builder.cs`, `Stage5Builder.cs`, `Stage15Builder.cs`,
`Stage22Builder.cs`, `Stage38Builder.cs` "Office look pass", ...) and lives in
`Assets/_Project/Scenes/DeskPrototype.unity` under `OfficeRoom`, `Room_Office`, `OfficeAir`,
`OfficeLook`, `Desk`, `Props`. Porting all those builders by hand is error-prone, so instead:

1. Write **one** editor script in the Unity project: `Assets/_Project/Editor/WebExport.cs`, menu
   `PaperWings > Web > Export Office`. It walks the office objects and writes
   `D:\Dev\Projects\paperwings-web\assets\office.json` containing:
   - every visible `MeshRenderer`: world position/rotation/scale, a mesh id, base color,
     emission color, smoothness, and whether it casts shadows;
   - a **deduplicated mesh table** (vertices, normals, uvs, indices, quantized to int16 and
     base64'd), converting Unity's left-handed coordinates to glTF/Three right-handed
     (`x = -x`, flip triangle winding; see the note at the top of `Editor/GlbImport.cs`, which
     does the reverse);
   - colliders as simple boxes/capsules (for plane collisions in the web build);
   - lights (type, color, intensity, range, position) and the ambient/fog settings;
   - every air source in the office (`Scripts/Air/*Source.cs`: fan, ceiling fan, radiators,
     thermals like the laptop, vent) with its transform and its settings asset values;
   - the throw point (`SpawnPoint` / the office Room's spawn point) and rival spawn points from
     the office Combat group;
   - the 4 plane meshes (from the `Fold_*.asset` → `mesh`) and their paper textures, downscaled
     to 256 px PNG, base64.
   Keep the export well under 1.5 MB; drop tiny props if needed (log what was dropped).
2. Tell me to run the menu item (you may not have Unity CLI access), then continue once
   `office.json` exists. The web build imports it as a module so it ends up inside the one JS file.
3. Do **not** change any other file in the Unity project.

## Look and feel (match the screenshots)

Screenshots, all from the Unity build (`D:\Dev\Projects\paperwings\Screenshots\web-prompt\`):

| File | What to take from it |
|---|---|
| `01-main-menu.jpg` | Main menu is a **walnut desk top-down**: title card "PAPER WINGS / THE NIGHT SHIFT WAR", a clipboard memo listing modes (web: only "Dogfight", plus "Hangar" and "Options"), sticky notes for Hangar (yellow, shows clip count), room (blue, fixed to "Office"), Options (pink). Typewriter + handwriting fonts. |
| `02-hangar-arrowhead.jpg` | Hangar layout: plane tabs across the top, polaroid of a slowly spinning 3D plane on the left, stat bars (Speed/Glide/Paper), "F · FLY IT" button, parts list on the right with cost or OWNED, clip count top-right. |
| `03-hangar-manta-locked.jpg` | Locked plane: red "LOCKED" stamp over the polaroid, the plane itself is the first row with its price, upgrades greyed with "needs Manta Stunt". |
| `04-hangar-dragon.jpg` | Fully owned plane, "FLYING ✈" state on the active plane. |
| `05-crane-polaroid.jpg` | Crane fold silhouette reference. |
| `06-launch-on-desk.jpg` | The throw: chase camera behind the plane on the desk, laptop, pillows, bookshelf, "Hold SPACE to wind up, release to throw. Steer to aim." prompt, plane card (◀ ARROWHEAD ▶ with stat bars) to switch plane before throwing, bottom bar "DOGFIGHT  Rivals shot down 0 / 5  Time 0.0s". |
| `07-flight-over-desk.jpg` | In flight: small circular crosshair with ammo count under it, red off-screen rival markers (chevron + "RIVAL 42" distance) at screen edges, speed-line streaks, "Rivals shot down" top-right. Warm desk lamp, night windows, glowing monitor wall. |
| `08-banking-turn.jpg` | Banking: camera rolls with the plane; desk fan and radiator visible (air sources). |
| `09-firing-staples.jpg` | Firing staples (count 40/45), the coworker corner with desk/chair/filing cabinet. |
| `10-rival-marker-above.jpg` | Rival above you: marker at the top edge. |
| `11-crash.jpg` | Crash: big red "Crunch!" text, then auto-reset to the desk after ~1.5 s. |
| `12-controls-strip.jpg` | Bottom controls hint strip (web: show keyboard or touch hints depending on device). |

Mood: night office, warm lamp pools against cool blue windows, soft rounded furniture
(mid-century palette: walnut, teal, burnt orange, mustard, cream, olive), light fog, soft shadows.
Use one directional "moonlight" + the exported point lights (cap shadow-casting lights at 2 for
performance). Mobile: lower pixel ratio (max 1.5), no shadows below a perf threshold.

## Gameplay rules and numbers (from the Unity assets; keep them in one `config.ts`)

Read the referenced files for exact behavior. Values below are the Unity "Normal" tuning.

**Flight** (`Scripts/Flight/PlaneController.cs`, `FlightSettings_*.asset`): a gliding arcade model,
not a physics sim. Speed trades with height (diving gains, climbing costs), glide ratio sets sink,
bank turns the plane, auto-level when no input, stall below stall speed (nose drops, recovers at
recover speed), boost = speed burst + short lift on cooldown, throw speed scales with wind-up time.
Units are Unity meters.

| Fold | cruise | max | glide ratio | pitch rate | max bank | turn rate | boost | boost lift | paper |
|---|---|---|---|---|---|---|---|---|---|
| Arrowhead (`Dart`) | 14 | 45 | 9 | 70 | 55 | 75 | 12 | 7 | 100 |
| Crane (`Glider`) | 11 | 34 | 14 | 55 | 50 | 60 | 9 | 8 | 75 |
| Manta (`Stunt`) | 16 | 50 | 7 | 100 | 70 | 115 | 13 | 6 | 140 |
| Dragon (`Canard`) | 13.5 | 42 | 12 | 82 | 55 | 72 | 14 | 9 | 95 |

Shared (Dart values, read the others from their assets): stall 7, stall recover 10, boost cooldown 3 s,
boost lift duration 1 s, throw speed 12–20 over 0.6 s wind-up.

**Air** (`Scripts/Air/*`): each source adds a velocity field in a volume (fan = cone push,
radiator/laptop = rising column, vent = downdraft, ceiling fan = wide downward swirl). The plane
blends toward the local air velocity (`airInfluence`, `airResponse`). Draw subtle animated
streaks in air volumes so players can see them.

**Staples** (`Weapon_PlayerStaples.asset`, `StapleGun.cs`): 0.22 s interval, 30 per throw
(+15 with Staple Magazine), speed 75, life 1.2 s, damage 18, spread 1°, aim assist 8° within 45 m.
Refill on every throw.

**Pencil missiles** (`Pencils.asset`, `PencilLauncher.cs`, `PencilMissile.cs`): lock on by keeping
a rival within 15° for 0.7 s (keeps lock within 28°, range 70); show a lock ring. Speed 50,
turn rate 140°/s, life 5 s, proximity fuse 1.8, damage 60. 2 per throw (4 with Pencil Rack).

**Paper integrity** (`PaperDamage.cs`, `Paper_*.asset`): bumps into furniture and enemy staples
reduce it; at low integrity the plane glides and steers worse and looks crumpled; at 0 it crashes.
Heavy Paper = +25% integrity.

**Rivals** (`RivalPilot.cs`, `Pilot_Rival.asset`, `FlightSettings_Rival.asset`, `Paper_Rival.asset`):
red-trailed paper planes using the same flight model (cruise 13, max 40, turn 62). Patrol around
radius 22 at height ~22; engage within 45 m, disengage past 70; fire bursts of 3 when within 30 m
and 10° of nose, 1.6 s pause; avoid obstacles at 14 m; climb in warm air to recover; respawn 4 s
after being shot down. Integrity 60 (4 staple hits or 1 pencil). Keep **3 rivals** in the air.
Difficulty (`Scripts/Gameplay/Difficulty.cs`): Easy / Normal / Hard multipliers as in that file;
default **Easy** on the web.

**Dogfight run** (`ModeDirector.cs`): timer starts on the first throw; win at 5 kills; crashes
reset you to the desk with the timer running. Results card: time, medal, clips earned, new best.

## Progression (easier than Unity)

Same structure as `Assets/_Project/Scripts/Gameplay/ProgressionSettings.cs` (read it): folds are
global unlocks; each fold buys its **own** copy of every upgrade; an upgrade needs its fold (and
Pencil Rack needs that fold's Pencil Missiles). Unity gates items behind campaign nights; the web
has no nights, so **gate by Dogfight wins** instead (the item shows "win N dogfights to unlock").

| Item | Unity cost | Web cost | Web gate (total wins) |
|---|---|---|---|
| Staple Magazine (+15 staples) | 145 | 40 | 0 |
| Pencil Missiles (2 homing pencils) | 235 | 60 | 1 |
| Crane Glider fold | 405 | 100 | 1 |
| Heavy Paper (+25% integrity) | 290 | 80 | 2 |
| Manta Stunt fold | 630 | 150 | 2 |
| Pusher Propeller (boost also lifts, longer) | 380 | 100 | 3 |
| Dragon Canard fold | 900 | 200 | 3 |
| Pencil Rack (4 pencils, needs Pencil Missiles) | 495 | 120 | 4 |

Earning (Unity → web): per kill 3 → **6**; per won run 10 → **30**; medal bonus 10/20/40 → **20/40/80**
(paid once per tier, per plane); **first win with each plane: +50**. Medal times (seconds,
bronze/silver/gold): Unity 140/100/70 → web **180/130/90**. Target: a player who wins every run
owns everything in about 45–60 minutes. Put all of these in `config.ts` and add a `?debug` panel
with "+500 clips", "unlock all", "reset save".

Upgrades show on the plane model (pencils under the wings, propeller on the tail, staple
magazine as a small block), like the hangar polaroids in the screenshots.

## Architecture

```
src/
  main.ts          boot, single canvas, game loop (fixed 60 Hz physics, interpolated render)
  config.ts        every tunable above
  assets/office.json (from step 0) + loader that builds Three meshes, colliders, lights, air
  world/           office scene, air field, collision (simple boxes/capsules, swept sphere for plane)
  flight/          plane controller, folds, paper damage, chase camera
  combat/          staples, pencils, lock-on, rivals AI, spawner
  game/            dogfight run state, progression + save, difficulty
  ui/              menu, hangar (with 3D polaroid preview in a second small renderer or render target), HUD, results, pause, options, touch controls
  audio/           WebAudio synth sounds
```
CSS goes in a TS string injected at boot. Fonts: system stacks (`"Courier New", Consolas, monospace`
for typewriter; `"Ink Free", "Segoe Print", "Bradley Hand", "Comic Sans MS", cursive` for
handwriting), same as `Scripts/UI/ModeMenu.Desk.cs`.

## Milestones (stop and show me after each)

1. **Export + scene:** `WebExport.cs`, office loads in the browser, free camera, size report.
2. **Flight:** throw from the desk, keyboard flight, chase camera, collisions, crash/reset, air currents, touch controls.
3. **Dogfight:** rivals, staples, pencils + lock-on, paper damage, HUD (crosshair, markers, kill counter, timer), win/results.
4. **Progression:** clips, Hangar with all 4 planes and upgrades, gating, save, menus, options.
5. **Polish:** audio, effects (speed lines, staple sparks, crumple), mobile perf pass, final single-file build under budget.

## Done means

- `npm run build` produces `dist/paperwings.js` (+ test `index.html`), one file, under budget, no network requests at runtime (check the browser network tab).
- Plays at 60 fps on a mid laptop and is playable on a recent phone in landscape.
- A fresh save can win a dogfight with the Arrowhead on Easy within a few tries, and everything is unlockable in about an hour.
- Side-by-side with the screenshots, the office, planes, menu and hangar read as the same game.
