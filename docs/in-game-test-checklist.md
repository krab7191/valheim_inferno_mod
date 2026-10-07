# In-game test checklist

Code that can only run inside Valheim cannot be unit-tested. Every such behaviour is listed here and must be
checked in-game before a release. Record each run in the results table at the bottom.

## Test priorities

Inferno is a **server-only** mod first: most players will never install anything. Test in this order.

| Priority | What | Who can do it | Sections |
|----------|------|---------------|----------|
| **P1** | Server-only, vanilla clients (PC, Xbox, PlayStation) | Server owner + any player; friends use [tester-guide.md](tester-guide.md) | 1–6, 9 |
| P2 | Server ownership mode (experimental, opt-in) | Server owner + vanilla players | 7 |
| P3 | Optional PC client mod | Players willing to install mods | 8 |

### Local test server (Windows)

1. Steam → Library → Tools → **Valheim Dedicated Server** (or Win+R `steam://install/896660`).
2. Copy the contents of [BepInExPack_Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/)'s
   `BepInExPack_Valheim` folder (`BepInEx`, `winhttp.dll`, `doorstop_config.ini`) into the server folder.
3. Set `InfernoDeployDir` in `Directory.Build.user.props` to `<server>/BepInEx/plugins/Inferno`; `dotnet build`
   then deploys automatically (stop the server first — Windows locks loaded DLLs).
4. Start script (keep the test world separate with `-savedir`; the password may not appear in the server name):
   `valheim_server -nographics -batchmode -name "Inferno Test" -port 2456 -world "InfernoTest" -password "firetest1" -public 0 -savedir "%~dp0inferno_test_saves"`
   (add `-crossplay` for console testers; they join with the code shown in the server log).
5. Join from a **vanilla** game: Join Game → Join IP → `127.0.0.1:2456`.
6. Admin: add your platform id (e.g. `Steam_7656…`, shown in the server log when you join) to
   `inferno_test_saves/adminlist.txt`, then restart.

### P1 quick run (server owner, ~30 min, no client mods)

1. Install Inferno on the server; start it; check the log (G-01, G-02, G-06, G-07).
2. Join with your normal vanilla client (G-03). Write `!fires status` on a sign (C-12).
3. Place a torch, campfire, hearth and hot tub (A-01, A-03); check a smelter (A-05).
4. `!fires schedule lights 18:00 06:00` + `!fires alwayson lights off` + `!fires burnrate lights -10`; watch one
   dusk and dawn (S-01, S-02, S-03); then `!fires reset all`.
5. Leave the area for an in-game day and come back (A-04).
6. Edit `BurnRate` of one item in the cfg file through the host's file manager (F-01).
7. Spam a sign/chat command a dozen times quickly (C-13).
8. Hand [tester-guide.md](tester-guide.md) to console players for G-04 / C-11.

**Setup:** dedicated server with BepInEx + Inferno (unzip `artifacts/Inferno-<version>.zip` into the server
folder). Clients are **vanilla** (no BepInEx) unless stated. Server log: `BepInEx/LogOutput.log`.
Tip: a small test world makes it easier: place one of each torch/fire/hearth/hot tub/smelter.
**Console checks** (marked *crossplay*) need an Xbox/PlayStation tester; leave them open until one is available.

## 1. Loading and joining

| ID | Check | How | Expected | RTM |
|----|-------|-----|----------|-----|
| G-01 | Plugin loads | Start server, read log | `Inferno <version> loaded (Valheim 1.0.17)` and `Running on server.` with no errors | R-07 |
| G-02 | Items discovered | Read log | `Found light source: …` lines for torches, sconces, fires, hearths, braziers, lanterns, hot tub; `Found fuel station: …` for smelter, blast furnace, oven, …; no charcoal kiln; summary line with counts | R-02 |
| G-03 | Vanilla client joins | Join with an unmodded PC client | Joins normally, no mismatch kick | R-07, N-01 |
| G-04 | Xbox client joins *(crossplay)* | Join from Xbox on a `-crossplay` server | Joins normally | R-07 |
| G-06 | Rain-sensitive list | Read log | `Lights that switch themselves off in rain …` line (or "No lights …") | R-15 |
| G-07 | All hooks installed | Read log | No `Could not install hook` errors | N-01 |
| G-05 | Config file created | Open `BepInEx/config/GrundleLord.Inferno.cfg` | `[General]` plus one section per discovered item, defaults as in `docs/sample-config.cfg` | R-10 |

## 2. Always on (defaults)

| ID | Check | How | Expected | RTM |
|----|-------|-----|----------|-----|
| A-01 | New torch is full and lit | Place a standing wood torch | Hover shows full fuel within ~5 s; stays lit | R-03 |
| A-02 | Fuel never runs out | Watch a campfire over 2+ in-game days | Fuel stays full | R-03 |
| A-03 | Hot tub stays lit | Build a hot tub with no wood | Lit and full within ~5 s | R-02a |
| A-04 | Not dark on arrival | Lit hearth at a base, travel far away for 1+ in-game day, come back | Hearth is lit and full the moment the area loads | R-03 |
| A-06 | Unattended burn follows schedule | Torch with always on off, burn rate 0, schedule 18:00–06:00; note fuel; leave the area for a full in-game day; return | Fuel dropped by about 12 in-game hours of burning, not 24 | §4.12 |
| A-07 | Fuel adds are never lost | Torch with burn rate −5; add wood repeatedly | Every added wood shows up in the fuel count | §4.12 |
| A-05 | Stations are vanilla | Smelter with ore and coal | Burns coal as in vanilla | R-02a |

## 3. Burn rate

| ID | Check | How | Expected | RTM |
|----|-------|-----|----------|-----|
| B-01 | Slower | `!fires alwayson piece_groundtorch_wood off`, `!fires burnrate piece_groundtorch_wood -5`; note fuel, wait | Fuel drops about half as fast as vanilla | R-04 |
| B-02 | No fuel used | Burn rate −10 | Fuel stays the same (± one small step) | R-04 |
| B-03 | Faster | Burn rate +10 | Fuel drops about twice as fast | R-04 |
| B-04 | Smelter | `!fires burnrate smelter -10`, smelt ore | Ore processes; coal stays roughly constant | R-04 |

## 4. Schedule

| ID | Check | How | Expected | RTM |
|----|-------|-----|----------|-----|
| S-01 | Server clock matches the sun | `!fires schedule lights 06:00 18:00` with always on off | Lights go off at dusk, on at dawn (within a few seconds) | R-05 |
| S-02 | Night window | `!fires schedule lights 18:00 06:00` | On at dusk, off at dawn | R-05 |
| S-03 | Fuel kept while off | Note fuel before switch-off, check after switch-on | Same fuel | R-05a |
| S-04 | Always on overrides | `!fires alwayson lights on` during an off window | Lights come back on within ~5 s | R-03 |
| S-05 | Schedule off | `!fires schedule lights off` | Lights on and stay on | R-05 |
| S-06 | Shutdown restore | Stop the server normally during an off window; check log; remove Inferno; start; visit | Log says lights were switched back on; lights are lit after uninstall | Q-22 |

## 5. Rain

| ID | Check | How | Expected | RTM |
|----|-------|-----|----------|-----|
| W-01 | Default: vanilla | Uncovered switchable light (candle/lantern) in rain | Goes out, stays out | R-15 |
| W-02 | Ignore rain | `!fires ignorerain on`, same light in rain | Relit within ~5 s (may flicker in storms) | R-15 |

## 6. Commands and permissions

| ID | Check | How | Expected | RTM |
|----|-------|-----|----------|-----|
| C-01 | Chat, 2 players | Second player online; type `!fires list lights` | Reply top-left on your screen; log shows the command | R-11 |
| C-02 | Chat hidden | Same, look at the other player's chat | They don't see the command (HideCommands = true) | R-11a |
| C-03 | Chat shown | `!fires hidecommands off`, type a command | Other player sees it; command runs once (not once per player) | R-11a |
| C-04 | Chat alone | Alone on server, type `!fires help` | Nothing happens (expected limit; documented) | R-11 |
| C-05 | Sign | Alone, write `!fires alwayson lights off` on a sign | Reply on screen; sign text cleared; log shows command and author | R-11 |
| C-06 | F5 console | Client with `-console`: `listkeys fires list` | Reply in F5 console | R-11 |
| C-07 | F5 on vanilla command | `listkeys` alone | Vanilla behaviour ("You are not admin" for non-admins) | R-11 |
| C-08 | AdminOnly on by non-admin | Non-admin: `!fires adminonly on` | Allowed | R-12 |
| C-09 | AdminOnly blocks | Non-admin: `!fires adminonly off`, `!fires burnrate all 3` | "Only admins can change …"; log shows denied | R-12 |
| C-10 | Admin can turn off | Admin (in adminlist.txt): `!fires adminonly off` | Allowed | R-12 |
| C-11 | Xbox *(crossplay)* | Xbox player writes a command on a sign | Works like C-05 | R-11 |
| C-12 | Status | Write `!fires status` on a sign | 3 lines: versions; item types and tracked objects; in-game time and general settings | R-18 |
| C-13 | Rate limit | Send ~15 commands within a few seconds | First 10 run; then one "Slow down" message; log shows one warning, not 15 | R-19 |
| C-14 | Long text | Chat a `!fires` line longer than 200 characters | "Command too long"; log line is cut off | R-19 |

## 7. Server ownership mode (experimental)

Run sections 2–5 again with `!fires serverownership on`, then these:

| ID | Check | How | Expected | RTM |
|----|-------|-----|----------|-----|
| O-01 | Eligible list | Read log at start | `Server ownership mode can manage N fire type(s). Excluded: …` | R-17 |
| O-02 | Fuel never drops | Always-on campfire; watch hover for an in-game day | Exactly full the whole time | R-17 |
| O-03 | Add fuel works | Always on off; add wood several times | Each wood counts; no wood lost (no sound, expected) | R-17 |
| O-04 | Switch works | Switchable light (e.g. lantern): use it to switch off and on | Switches each time | R-17 |
| O-05 | Deconstruct works | Hammer-remove a lit fire | Removed within ~1 s, resources dropped (retry once if not) | R-17 |
| O-06 | Damage works | Let an enemy hit a fire / hit it yourself | Takes damage, can be destroyed | R-17 |
| O-07 | Rain | Uncovered switchable light in rain | Stays on | R-17 |
| O-08 | Exact burn rate | Always on off, burn rate −5, note fuel over time while standing next to it | Exactly half the vanilla rate | R-17 |
| O-09 | Turn off | `!fires serverownership off` | Log shows fires given back; vanilla behaviour returns (with refills) | R-17 |
| O-10 | Two players | Two players near the same fire add fuel and remove it | No errors, nothing lost | R-17 |
| O-11 | Xbox *(crossplay)* | Xbox player adds fuel to / removes a server-owned fire | Same as O-03 / O-05 | R-17 |

## 8. Optional client mod (PC)

Install Inferno + ConfigurationManager on a PC client; server runs Inferno.

| ID | Check | How | Expected | RTM |
|----|-------|-----|----------|-----|
| M-01 | Sync | Join; read client log | `Synced Inferno settings from the server (N items, editable)` | R-01 |
| M-02 | Menu shows server values | F1 → Inferno | Values match the server's config file, not the client's | R-01 |
| M-03 | Menu edits apply | Change a torch's BurnRate in F1 | Server log: `menu command from …`; server cfg updated; message on screen | R-01 |
| M-04 | Other menus update | Two modded clients; one edits | The other's F1 shows the new value | R-01 |
| M-05 | Read-only | Non-admin, `AdminOnly` on | Inferno settings shown greyed out | R-12 |
| M-06 | Local file untouched | Disconnect; open client's cfg | Client's own values unchanged | R-01 |
| M-07 | Vanilla server | Join a server without Inferno | Nothing happens; no errors | R-07 |
| M-08 | Smoke off | `!fires smoke lights off` | No smoke from lights for this player; vanilla client still sees smoke; fires don't go out | R-06 |
| M-09 | Smoke blocked still works | Smoke off; build a fire under a low roof | Still goes out like vanilla (smoke can't escape) | R-06 |
| M-10 | No rain flicker | `IgnoreRain` on, switchable light in rain, modded client owns it | Stays lit, no flicker | R-15 |
| M-11 | Version mismatch | (Later releases) old client, new server | Message: settings menu disabled | — |

## 9. Config file and logging

| ID | Check | How | Expected | RTM |
|----|-------|-----|----------|-----|
| F-01 | Live reload | Edit `BurnRate` of an item in the cfg while running | Log shows `Config file edited: [item] Settings: … -> …` within seconds; behaviour follows | R-10, R-13 |
| F-02 | Commands persist | Change via command, restart server | Setting kept | R-10 |
| F-03 | Audit log | Run several commands | Each logged with player name, platform id, old → new | R-13 |
| F-04 | Bad values | Put `BurnRate = 99` in the file | Clamped to 10 by BepInEx; no errors | R-10 |

## Results

| Date | Valheim | Inferno | Tester | Server host | Checks passed | Notes |
|------|---------|---------|--------|-------------|---------------|-------|
| | | | | | | |
