# Requirements Traceability Matrix — Valheim Light Source Mod

> Mod name: **Inferno** · Author: **GrundleLord** · Target: Valheim **1.0.17** (latest stable). Status legend: `OPEN` = needs user decision, `DRAFT` = proposed, awaiting approval,
> `APPROVED`, `IMPL` = implemented, `VERIFIED` = tested (unit + in-game).
>
> Rule: nothing moves from `OPEN`/`DRAFT` to `APPROVED` without the project owner's sign-off, recorded in §7.

## 1. Goal

A Valheim mod that lets the server operator configure the behaviour of every light source (campfires, bonfires,
hearths, all torches, sconces, braziers, lanterns, etc.). It runs **on the server only**, so vanilla clients —
including Xbox/Game Pass crossplay clients, which cannot install mods — need nothing installed. An optional PC client
mod (phase 2) adds an in-game settings tab and client-only features.

## 2. Functional requirements

| ID | Requirement | Server-only feasible? | Phase | Status | Design ref | Test ref |
|----|-------------|-----------------------|-------|--------|------------|----------|
| R-01 | In-game menu tab to configure settings (location not important yet) | No — UI exists only on clients; delivered by the optional client mod: ConfigurationManager (F1) shows the server's settings, edits are sent as commands | 2 | IMPL | §4.16 | `SyncTests`, M-01–M-07 |
| R-02 | Settings **per item** (prefab) for every piece that burns fuel (wood, resin, coal, …): light sources **and** smelters, ovens, hot tubs, etc. | Yes — server discovers prefabs with a fuel component at startup (incl. modded pieces) | 1 | IMPL | §4.8 | `ItemCatalogTests`, G-02 |
| R-02a | Defaults: **light sources always on** (every fireplace-type item incl. hot tubs); smelters/ovens **vanilla** | Yes | 1 | IMPL | §4.8 | `ItemSettingsTests`, A-03, A-05 |
| R-02b | Bulk targets `all`, `lights`, `stations` | Yes | 1 | IMPL | §4.9 | `ItemCatalogTests`, `CommandExecutorTests` |
| R-03 | `AlwaysOn` — fuel kept full at all times, **including areas no player is near** (not dark on arrival). Overrides the schedule | Yes — server writes `fuel` (+ `lastTime` when unattended) | 1 | IMPL | §4.3 | `FuelControllerTests`, A-01–A-04 |
| R-04 | `BurnRate` level −10…+10, 0 = vanilla, each step 10 % (−10 = no fuel used, +10 = double) | Yes — server scales observed fuel loss (approximate, §4.11) | 1 | IMPL | §4.3 | `BurnRateTests`, `FuelControllerTests`, B-01–B-04 |
| R-05 | Schedule: `OnTime` / `OffTime`, each as hour (0–24) + minute (0–60) like NoSmokeStayLit; one window per day; **On = Off means always on** (default 00:00/00:00 = no schedule); applies to every item with an on/off switch (all light sources) | Yes — server writes vanilla `state` | 1 | IMPL (core) | §4.2, §4.3 | `ClockSettingTests`, `DailyScheduleTests`, `GameClockTests`, `FuelControllerTests` |
| R-05a | Scheduled "off" uses the vanilla on/off state: **fuel is kept** | Yes | 1 | IMPL | §4.3 | `FuelControllerTests`, S-03 |
| R-06 | Per-item smoke toggle (`Smoke`, default on) | Client mod only (smoke is local); vanilla clients see smoke | 2 | IMPL | §4.16 | `ItemSettingsTests`, M-08, M-09 |
| R-07 | Server-only; no client install; seamless with crossplay | Core goal — strategy C (hybrid) | 1 | APPROVED | §4.4 | G-01–G-03 |
| R-08 | "More TBD later" | — | — | — | — | — |
| R-09 | Code 100 % tested and robust | Yes for logic; game glue verified in-game | 1 | APPROVED — gate active | §5 | coverage gate |
| R-10 | Config file on server with live reload | Yes | 1 | IMPL | §4.10 | F-01–F-04 |
| R-11 | Commands via chat (`!fires …`, needs 2+ players online), signs (`!fires …`, always) and F5 console (`listkeys fires …`, always, PC with `-console`) | Yes | 1 | IMPL | §4.5, §4.9 | `CommandParserTests`, `CommandExecutorTests`, C-01–C-11 |
| R-11a | Setting to hide commands from other players' chat, **hidden by default** | Yes — server drops the relayed message | 1 | IMPL | §4.5 | `GeneralSettingsTests`, C-02, C-03 |
| R-12 | `AdminOnly` setting, default **off**. Off: anyone may change any setting (incl. turning `AdminOnly` on). On: only admins may change settings (incl. turning it off) | Yes | 1 | IMPL (core) | §4.5 | `PermissionPolicyTests` |
| R-12a | Nobody can change who the server admins are through Inferno; admin list comes from the server (`adminlist.txt`, set via the host's panel) | Yes — Inferno only reads it, using the platform id from the network connection (can't be spoofed) | 1 | IMPL | §4.5 | C-08–C-10 |
| R-13 | Log everything: every command (source, player, platform id), every change (old → new), config-file edits, denials | Yes | 1 | IMPL | — | `SettingsDiffTests`, F-01, F-03 |
| R-15 | `IgnoreRain` general setting, **off by default** (lights go out in rain like vanilla); on = relight lights the rain switched off | Partial — server can only relight afterwards, can't tell rain from a player (§4.11) | 1 | IMPL | §4.11 | `FuelControllerTests`, W-01, W-02 |
| R-17 | Opt-in **server ownership** mode (`ServerOwnership`, default off): server keeps ownership of eligible fires so no client burns fuel or applies rain; server handles add-fuel/toggle RPCs and lends ownership for all other actions | Yes (experimental) | 1 | IMPL | §4.13 | `OwnershipTests`, O-01–O-11 |
| R-18 | `!fires status` (read-only, always allowed) shows versions, tracked objects, in-game time and general settings — lets remote/console testers confirm Inferno runs | Yes | 1 | IMPL | — | `CommandExecutorTests`, C-12 |
| R-19 | Hardening: per-player rate limit across all command sources (burst 10, 1/s; one warning, then silent), 200-character command cap, truncated log lines, batched settings broadcasts (≤ 2/s), hello cooldown, client-side batching of menu edits (0.75 s) | Yes | 1 | IMPL | — | `RateLimiterTests`, `CommandParserTests`, C-13, C-14 |
| R-20 | **Testing priority: server-only with vanilla clients first**; console players test via signs with a plain-language guide | — | — | APPROVED | docs/tester-guide.md | in-game checklist P1 |
| R-21 | Commands refer to items by their **in-game name** (case/space/punctuation-insensitive; shared names change all matching items; internal names still work; "did you mean" suggestions) | Yes | 1 | IMPL | — | `ItemCatalogTests`, `CommandParserTests`, C-15 |
| R-22 | Readable replies: one combined on-screen message; sign commands leave a short answer on the sign; replies logged on the server | Yes | 1 | IMPL | — | `SignReplyTests`, C-05 |
| R-16 | Uninstall leaves no fire dark: scheduled-off lights are switched back on before the final save on shutdown; fuel is left as is | Yes (normal shutdown only) | 1 | IMPL | §4.11 | `FuelControllerTests`, S-06 |
| R-14 | Publishing quality: README that accurately describes features, sample config file, list of Valheim versions tested | — | 1 | IMPL | README.md, docs/sample-config.cfg | — |

## 3. Non-functional requirements

| ID | Requirement | Status |
|----|-------------|--------|
| N-01 | No version check / handshake that kicks vanilla clients (no Jötunn network-compat enforcement, no ServerSync "required" mode) | DRAFT |
| N-02 | Never corrupts the world save: writes only vanilla keys (`fuel`, `state`, `lastTime`, sign `text`) with values vanilla accepts, plus one extra int key `Inferno_ScheduledOff` that vanilla ignores | IMPL |
| N-03 | Uninstalling returns fires to vanilla behaviour (see R-16) | IMPL |
| N-04 | Low server CPU cost: object sweep every 30 s in 20 000-object chunks per frame; settings applied to tracked objects every 5 s | IMPL (measure in-game) |
| N-05 | Config hot reload without server restart | APPROVED (= R-10) |
| N-06 | Logging: see R-13. Levels: changes/commands at Info, per-fire actions at Debug, problems at Warning | DRAFT |
| N-07 | Runs on the owner's hosted servers (Indifferent Broccoli, DatHost; believed Windows, `-crossplay` toggled in the web panel) and on Linux servers | DRAFT |
| N-08 | Clean contributor experience: one-command build/test, documented in `AGENTS.md`, works on Linux/WSL and Windows | IMPL |
| N-09 | Supports latest stable Valheim only; each new patch is re-tested; no backwards compatibility promise | APPROVED |

## 4. Research findings

### 4.1 Environment (this machine, 2026-10-06)
- Valheim at `/mnt/c/Program Files (x86)/Steam/steamapps/common/Valheim`, BepInEx pack 5.4.2351.
- Previously tried mods (in BepInEx backups): `FuelEternal`, `TorchesEternal`, `ConfigurationManager`.
- Installed user-locally: .NET SDK 10.0.401 and `ilspycmd` 11.1 in `~/.dotnet` (approved, Q-8).

### 4.2 Vanilla behaviour — verified by decompiling `assembly_valheim.dll`
**Fireplace** (shared by every fuel-burning light source):
- ZDO keys: `fuel` (float), `lastTime` (long ticks), `state` (int: `1` = on, `2` = off; default `1`).
- `UpdateFireplace` runs every 2 s. **Only the ZDO owner** burns fuel: `fuel -= elapsedSeconds / m_secPerFuel`,
  and only while `IsBurning()` and `state == 1`. Elapsed time is measured from `lastTime`, so a fire in an unloaded
  zone burns all the missed time at once when a player next loads it.
- `IsBurning()` is evaluated **on every client** from ZDO data: `state == 1` **and** `fuel > 0` (or `m_infiniteFuel`)
  **and** not blocked (roof / under terrain / own smoke) **and** not under water.
- Owner-side RPCs `RPC_SetFuelAmount`, `RPC_AddFuelAmount`, `RPC_ToggleOn` exist, but all of them play the
  "fuel added"/toggle effect — so direct ZDO writes are preferable for silent background changes.
- In wind/rain, pieces with `m_canTurnOff` that are uncovered toggle **themselves** off on the owner client.
  Blocked/wet/under-water checks are client-local physics: **the server cannot override them**.
- `m_infiniteFuel`, `m_secPerFuel`, `m_maxFuel` are prefab fields on each client: the server cannot change them for
  vanilla clients.

**Ownership:** the owner of a fire is a client (normally the first player in the zone). A vanilla dedicated server
stores and relays ZDOs but does not simulate them. ([source](https://www.nexusmods.com/valheim/mods/3617)) Hence a
Harmony patch on `Fireplace` in a server-only mod does nothing for client-owned fires — the reason other mods say
"the zone owner must have it". ([source](https://thunderstore.io/c/valheim/p/nbusseneau/Fuel_Daylight_Saving/v/0.2.0/))

**Smoke:** `SmokeSpawner` creates smoke with a plain local `Instantiate` — it is not networked. Smoke (and the "blocked
by own smoke" check) cannot be controlled from the server. → R-06 needs the client mod.

**Time of day:** `EnvMan.m_dayLengthSec` (prefab value to be read in-game), day index = `time / dayLength`; morning
starts at day fraction 0.15. Mapping fraction → 24 h clock to be confirmed (see Q-6).

**Chat:** on the client, text starting with `/` is run as a **local** terminal command and never reaches the server.
Any other text is sent as a `ChatMessage` routed RPC, which passes through the server.

### 4.2b Other fuel users (decompiled, 1.0.17)
- **Smelter** (smelter, blast furnace, charcoal kiln, eitr refinery, …): ZDO `fuel`; owner burns
  `1 / (m_secPerProduct / m_fuelPerProduct)` fuel per second **only while processing ore**. Pieces with
  `m_maxFuel = 0` (e.g. the charcoal kiln, which takes wood as *ore*) use no fuel at all → nothing to configure.
  No on/off switch → **schedule not possible** for smelters.
- **CookingStation** with `m_useFuel` (e.g. stone oven): ZDO `fuel`; burns `dt / m_secPerFuel` while lit.
  No on/off switch → **schedule not possible**.
- All three store fuel under the same ZDO key `fuel`, so one server-side mechanism covers always-on and burn rate.
- The server can read every prefab's component values (`m_maxFuel`, `m_secPerFuel`, `m_canTurnOff`, …) from
  `ZNetScene` prefabs, so per-item limits never need to be hard-coded.

### 4.2c In-game clock (decompiled, implemented)
- Displayed time = rescaled day fraction × 24 h. Valheim stretches daylight: raw 0.15 → 06:00 (sunrise),
  raw 0.85 → 18:00 (sunset). Implemented in `Inferno.Core.Time.GameClock`, tested against these anchor points.
- Day length comes from `EnvMan.m_dayLengthSec` on the server (to verify in-game: G-04).

### 4.3 Proposed mechanism (strategy A core) — DRAFT
| Feature | Mechanism | Caveats |
|---------|-----------|---------|
| Always on | Server scans fireplace ZDOs every N s; if `fuel` < threshold, writes `fuel = m_maxFuel` | Fire still goes out if wet/blocked/under water (client physics). Possible lost write if it collides with the owner's own 2 s write → harmless, corrected next scan. |
| Burn multiplier `m` | Server remembers each fire's last seen `fuel`; on decrease `d`, writes `fuel = current − (m − 1)·d` (clamped to `[0, max]`). `m = 0` ≡ always on; `m = 2` burns twice as fast. Increases (player added fuel) just reset the baseline. | Correction lags by one scan interval. |
| Schedule | Server writes `state = 2` at off-time and `state = 1` at on-time. Vanilla stops burning fuel while `state = 2`, so **fuel is preserved**. | Pieces without `m_canTurnOff` can't be relit manually by players while off (see Q-14). |
| Unloaded zones | Server has every ZDO, so it can also refresh fires nobody is near (no burst burn on arrival). | Must keep scans cheap (N-04). |

### 4.4 Strategies considered (decision: **C**)
| Strategy | Summary |
|----------|---------|
| A. ZDO writer | Server-only, light, works for every client. No smoke, no rain/roof override. |
| B. Server-side simulation | Server owns zones around players and runs real `Fireplace` code. Exact, but heavy and conflicts with networking mods. ([source](https://thunderstore.io/c/valheim/p/VerdantsAscent/FiresGhettoNetworking/)) |
| **C. Hybrid (chosen)** | A for everyone + optional PC client mod later for menu, smoke and client-physics features. |

### 4.5 Chat commands & permissions — DRAFT
- Because `/` commands stay on the client, commands need a plain-text prefix, e.g. `!fires schedule 18:00-06:00`.
- The server intercepts the routed `ChatMessage`, executes it if the sender is permitted, replies only to that
  sender, and can drop the message so other players don't see it (to verify in-game).
- Permission: `AdminOnly = true` → sender must be in the server's `adminlist.txt`; `false` (default) → anyone.
- To verify: Xbox/crossplay chat may be restricted by platform privacy settings.

### 4.8 Prefab discovery — DRAFT
Extracting the prefab list from the game files offline didn't work (the asset scan returned nothing). Instead the server
enumerates `ZNetScene` prefabs at startup, picks every prefab with a `Fireplace`, fuel-using `Smelter` or fuel-using
`CookingStation`, and classifies it as *light source* (Fireplace) or *other*. That also covers future patches and
pieces added by other mods. The committed sample config will be generated from a real server run.

### 4.9 Chat commands — PROPOSAL (Q-23)
```
!fires help                                   list commands
!fires list [lights|stations]                 list items and their settings
!fires show <target>                          settings of one item / group
!fires alwayson <target> on|off
!fires burnrate <target> <-10..10>
!fires schedule <target> <HH:MM> <HH:MM>      on time, off time (same time = always on)
!fires schedule <target> off                  remove schedule (= 00:00 00:00)
!fires reset <target>                         back to defaults
!fires adminonly on|off
!fires hidecommands on|off
!fires restore                                switch back on everything Inferno switched off
```
`<target>` = a prefab name (e.g. `piece_groundtorch_wood`), or a group: `all`, `lights`, `stations`.
Replies go only to the sender. Every command and change is logged (who, what, old → new).

### 4.10 Config file layout — PROPOSAL (Q-24)
BepInEx config `BepInEx/config/GrundleLord.Inferno.cfg`, live-reloaded:
```
[General]
AdminOnly = false
HideCommands = true

[piece_groundtorch_wood]        ## one section per discovered item; comment says its kind and name
AlwaysOn = true
BurnRate = 0                    ## -10..10
OnTimeHour = 0                  ## 0..24
OnTimeMinute = 0                ## 0..60
OffTimeHour = 0
OffTimeMinute = 0
```
Items without an on/off switch (smelters, ovens) have no schedule keys. Chat commands write to this same file, so
it always shows the current settings.

### 4.11 Findings from implementation (decompiled, 1.0.17)
- **Chat routing:** `Talker.Say` / `Chat.SendText` send each chat line separately to every other player
  (`Chat.CheckPermissionsAndSendChatMessageRPCsAsync`; Steam has a relations provider). The sender's own copy never
  leaves their PC, so the server only sees chat when 2+ players are online, once per recipient. Inferno runs the
  command once (`DuplicateFilter`) and hides it by dropping the relayed copies (`ZRoutedRpc.RouteRPC`).
- **F5 console:** `Terminal.TryRunCommand` forwards a built-in command marked `remoteCommand` (e.g. `listkeys`) as
  full text to `ZNet.RPC_RemoteCommand` when it isn't valid locally. Inferno handles `listkeys fires …` there and
  replies with `ZNet.RemotePrint`. Unknown commands (e.g. `fires …`) never leave the client.
- **Signs:** `Sign.SetText` claims ownership and writes `text` + `author` to the ZDO → the server sees the text and
  the owner peer. Default `m_characterLimit` = 50.
- **Server writes:** `ZDO.Set` on the server has no owner check and bumps the data revision. A client owner accepts
  it only if it hasn't written since (`ZDOMan.RPC_ZDOData`); fireplaces write every 2 s, so a write is sometimes
  lost. Always-on and schedule correct themselves on the next 5 s pass; burn rate is approximate.
- **Shutdown:** `Game.OnApplicationQuit` → `ZNet.Shutdown(save)` → `Save`. Inferno restores lights in a prefix.
- **Admin check:** `ZNet.IsAdmin(hostName)` with the socket host name (platform id) — same check vanilla uses.

### 4.12 Closing gaps server-side (2026-10-07)
| Gap | Fix | Where |
|-----|-----|-------|
| Owner burns all missed time at once on arrival, ignoring schedule and burn rate | Server burns fuel for **unattended** fireplaces itself every ≥ 30 s (`elapsed / m_secPerFuel × multiplier`, only while switched on) and moves `lastTime` forward | `FuelController` (`UnattendedStepSeconds`), `FuelScanner.ReadState` |
| Server writes lost to the owner's 2 s writes | Refills and switch changes on owned items bump the ZDO data revision by 8 so the owner accepts them (`MustWin`). Burn-rate corrections don't, so a player's freshly added fuel is never overwritten | `FuelDecision.MustWin`, `FuelScanner.Apply` |
| Frequent always-on refill writes | Refill only once ≥ 0.5 below full (≤ half capacity for tiny tanks). Fuel display and "can't add more" round up, so it still looks full and players can't waste fuel | `FuelController.RefillMargin` |
| A game update renames a hooked method → whole mod fails | Each Harmony hook is installed separately; a failing hook disables only its feature, with a log message | `InfernoPlugin.ApplyPatches` |
| Unknown game version | Warning in the log when the game version differs from the tested one | `InfernoPlugin.TestedGameVersion` |
| Which lights react to rain? | Logged at startup (fires with `m_canTurnOff` and low/high flame objects, mirroring `Fireplace.CheckEnv`) | `PrefabDiscovery.IsRainSensitive` |

**Not fixable server-only (verified):** smoke (`SmokeSpawner.Spawn` instantiates locally, only near the local
player), roof / under-water blocking (client physics), chat from a lone player (client never sends it).

**Possible but invasive — server ownership (proposal, Q-27):** the server keeps ownership of managed fireplaces, so
no client runs owner logic (no rain switch-off, no client burning → exact burn rate, no write races). Requires:
patching `ZDOMan.ReleaseNearbyZDOS` (it reassigns owners every frame), emulating `RPC_AddFuel`,
`RPC_AddFuelAmount`, `RPC_SetFuelAmount`, `RPC_ToggleOn` on the server (otherwise `ZRoutedRpc.HandleRoutedRPC`
drops them because the server has no instance → players lose wood), initialising new fires' fuel, and accepting
the loss of owner-only effects: fuel-added / toggle sounds, Deep North snow melting (`UpdateSnowMelt`) and fire
spreading (`UpdateIgnite`). Would be opt-in and could exclude fires with snow-melt/ignite.

### 4.13 Server ownership mode — design (implemented, experimental)
- **Claim:** every 5 s pass, eligible fires are set to owner = server session id (`OwnershipPolicy`), unless lent.
- **Keep:** `ZDOMan.ReleaseNearbyZDOS` reassigns owners every frame; a prefix/finalizer marks that call and a
  `ZDO.SetOwner` prefix skips owner changes of protected server-owned fires during it.
- **Burn:** a server-owned fire without a local instance counts as "unattended", so the existing exact server-side
  burn (§4.12) applies all the time. Always-on fires never lose fuel at all.
- **Player actions:** `ZRoutedRpc.HandleRoutedRPC` prefix catches RPCs addressed to the server for these fires.
  `RPC_AddFuel`, `RPC_AddFuelAmount`, `RPC_SetFuelAmount`, `RPC_ToggleOn` are re-implemented on the ZDO
  (`FireplaceRpc`, ported from `Fireplace`). Any other RPC (`RPC_Remove`, `RPC_Damage`, `RPC_Repair`, …) lends
  ownership to the sender for 30 s (`LoanBook`) and re-routes the RPC to them after 1 s, so the owner update
  arrives first.
- **Eligible:** fireplaces that burn over time, without `m_snowMelter`, without fire spreading
  (`m_igniteInterval`/`m_igniteCapsuleRadius`), and without Smelter / CookingStation / Container / Fermenter /
  Beehive / Ship parts. Logged at startup.
- **Release:** setting off → released on the next pass; Inferno stop and server shutdown → all released.
- **Known side effects:** no fuel-added / toggle effects (they're created by the owner's handler); no rain
  switch-off (so `IgnoreRain` is moot for these fires); owner-run `WearNTear` upkeep (weather wear, support
  recalculation, snow build-up on the piece) pauses while server-owned.
- **Hosted (non-dedicated) games:** fires near the host have an instance on the host, so they run vanilla owner
  logic there and Inferno treats them as attended.

### 4.14 Community libraries — evaluation (2026-10-07)
| Library | What it gives | Helps the server-only mod? | Helps the phase-2 client mod? |
|---------|---------------|-----------------------------|-------------------------------|
| [Jötunn](https://github.com/Valheim-Modding/Jotunn) | Item/piece/creature/zone managers, console commands, custom RPCs, config sync with an in-game admin menu, network compatibility checks | **No.** Its features act through the client (commands, config sync, GUI), so vanilla/console clients get nothing. It would add a server dependency; its version check is safe only with `NotEnforced` (the default) / `ServerMustHaveMod` ([docs](https://valheim-modding.github.io/Jotunn/tutorials/networkcompatibility.html)) | **Yes.** Config sync + admin-locked in-game menu ≈ R-01 out of the box; custom RPCs for client→server changes |
| [ServerSync](https://github.com/blaxxun-boop/ServerSync) | Config sync with admin lock, embedded into the mod (no separate install) | No (needs the client) | Yes, lighter alternative to Jötunn for config sync |
| ConfigurationManager (F1 menu) | In-game settings UI for BepInEx configs | No | Yes, with Jötunn or ServerSync: the "settings tab" for PC players |
| BepInEx.AssemblyPublicizer | Compile-time access to private game members | Small: would replace a few `AccessTools` lookups | Same |

**Recommendation:** keep the server-only mod dependency-free (nothing above helps vanilla/console clients, and every
dependency is something hosts must install and keep matching). Build the phase-2 PC client mod on Jötunn (or
ServerSync) + ConfigurationManager instead of a custom menu. → Q-29.

### 4.15 Crossplay + BepInEx
Several hosting sites claim BepInEx doesn't load with `-crossplay` ("BepInEx hooks Steam networking"). That is not how
BepInEx works (it loads into the Unity process via Doorstop before any networking), the game has no crossplay/mod
block (only the voluntary `Game.isModded` flag), and **the owner has confirmed crossplay works with BepInEx**
(2026-10-07). Inferno's hooks sit on Valheim's own RPC/ZDO layer, above the Steam/PlayFab transport.

### 4.16 Client mod — design (implemented)
- **Same DLL** on server and client. On a client it is idle unless connected to an Inferno server (or hosting).
- **Channel:** custom routed RPCs `Inferno_Hello(int protocol)` / `Inferno_Command(string)` (client → server) and
  `Inferno_Settings(string)` (server → client). Vanilla clients never send them and ignore unknown RPCs, so this is
  invisible to them. No handshake, no kick.
- **Settings message:** tab-separated text (`SettingsMessage`, protocol version 1) with the receiver's edit right.
  Sent on hello and broadcast to every subscribed client after any change (command, menu, config-file edit).
- **Menu:** ConfigurationManager (optional, F1) shows the plugin's BepInEx config entries. While connected, the
  client puts its config in *mirror mode* (server values, `SaveOnConfigSet` off, file restored on disconnect).
  Edits are diffed against the last server values and sent as normal `!fires …` commands (`MenuCommands`), so
  permissions, validation and the audit log are exactly the same as for chat/sign/console. Denied edits snap back.
  `ConfigurationManagerAttributes.ReadOnly` greys the entries out for players who can't edit.
- **Smoke:** `SmokeSpawner.Spawn` prefix skips the puff but keeps `m_lastSpawnTime` bookkeeping, so vanilla's
  "smoke is blocked → fire goes out" still works.
- **Rain:** `Fireplace.CheckWet` postfix clears `m_wet` when `IgnoreRain` is on (only for fires this player's game
  runs; the server relights the rest).

### 4.6 Optional client mod (phase 2) — not started
Settings tab in the game's settings menu, pushes changes to the server over a custom RPC that vanilla clients simply
never call; adds smoke toggle and rain/roof overrides for its own user.

### 4.7 Prior art reviewed
- [ValheimInfiniteFire](https://old.thunderstore.io/c/valheim/p/MidnightMods/ValheimInfiniteFire/) — server-synced config, must be on clients.
- [Fuel Daylight Saving](https://thunderstore.io/c/valheim/p/nbusseneau/Fuel_Daylight_Saving/v/0.2.0/) — daylight off-switch; zone owner must have it.
- [BreatheEasy](https://thunderstore.io/c/valheim/p/RandomSteve/BreatheEasy/) — smoke removal; kicks clients without it when on the server.
- [FiresGhettoNetworking](https://thunderstore.io/c/valheim/p/VerdantsAscent/FiresGhettoNetworking/) — server-side simulation (strategy B).
- [Valheim Community Patch](https://www.nexusmods.com/valheim/mods/3617) — documents server vs. client ownership.

## 5. Test strategy — APPROVED
- **Core library** (no Unity/Valheim references): schedule math, burn-rate math, classification, config parsing,
  command parsing, permissions. xUnit + coverlet; **100 % line and branch coverage enforced** — build fails below it.
- **Adapter layer** (Harmony / ZDO / RPC glue): as thin as possible, behind interfaces so its logic is unit-tested
  with fakes. Code that can only run inside the game is listed in an in-game test checklist (`docs/`) that the
  project owner runs and records results for.
- Analyzers on, warnings as errors, `.editorconfig` formatting checked in CI-equivalent `dotnet format --verify-no-changes`.

## 6. Open questions

| ID | Question | Answer |
|----|----------|--------|
| Q-1 | How are settings changed without a client menu? | Config file + live reload, chat commands, optional client menu later; `AdminOnly` setting default off. |
| Q-2 | Strategy A, B or C? | **C — Hybrid.** |
| Q-3 | Where does the server run? | Hosting providers **Indifferent Broccoli** and **DatHost** (several servers). |
| Q-4 | "100 % tested" definition OK? | Yes. |
| Q-5 | Settings granularity and scope? | **Per item.** Everything that burns fuel (wood, resin, …), incl. smelters, kilns, ovens, hot tubs. Non-light sources vanilla by default. |
| Q-6 | Schedule semantics? | **One window per day**, `HH:MM`, **off by default**; lets users mimic day/night. |
| Q-7 | Smoke? | Keep vanilla for now; per-item smoke toggle later (client mod). |
| Q-8 | Install .NET SDK + ilspycmd? | Yes — done. |
| Q-9 | Target Valheim version? | Latest stable (1.0.17 today). Test each new patch; no backwards-compat guarantee. |
| Q-10 | Name / author / license / publishing? | **Inferno** by **GrundleLord**; proprietary for now (maybe MIT later); maybe publish later. |
| Q-11 | `AdminOnly` permissions and logging? | `AdminOnly` can be changed by anyone; nobody can change the actual admins; **log everything**. |
| Q-11b | When `AdminOnly` is on, can non-admins turn it off? | No: anyone can turn it on, only admins can turn it off. |
| Q-12 | Host OS and crossplay? | Believed Windows; `-crossplay` on (web-panel toggle). |
| Q-13 | Chat command prefix; hide commands? | `!fires`; setting to hide commands from others, **hidden by default**. |
| Q-14 | How does scheduled "off" work? | Vanilla on/off `state`: **fuel is kept**. |
| Q-15 | Default settings for light sources? | **Always on** (all torches, sconces, fires, hearths, …, incl. late-game ones). |
| Q-16 | Per-item settings? | `AlwaysOn`; `BurnRate` −10…+10 (0 vanilla, 10 % per step); `OnTime`/`OffTime` as hours 0–24 + minutes 0–60 (NoSmokeStayLit style) for all light sources. |
| Q-17 | Bulk targets? | Yes. |
| Q-18 | On = Off? 24:00? | On = Off → always on (00:00/00:00 = default). 24:00 = 00:00, xx:60 rolls into the next hour (Claude's interpretation of "0–24 / 0–60"). |
| Q-19 | Always on + schedule? | **Always on overrides the schedule.** To schedule a light, turn `AlwaysOn` off (use `BurnRate -10` for a scheduled light that never needs fuel). |
| Q-20 | Top up where no player is nearby? | Yes, everything topped up all the time. |
| Q-21 | Hot tub classification? | Treat as a light source (always on): every fireplace-type item is a light source. |
| Q-22 | Uninstall behaviour? | Keep current fuel (full); burns like vanilla afterwards. Implemented by switching scheduled-off lights back on at shutdown; `restore` command dropped as unnecessary. |
| Q-23 | Command syntax? | Approved (§4.9). Added `ignorerain`, dropped `restore`. |
| Q-24 | Config layout? | Approved (§4.10); `IgnoreRain` added to `[General]`. |
| Q-25 | Rain? | General `IgnoreRain` rule, **off by default** (go out in rain). |
| Q-26 | Chat only reaches the server with 2+ players online. Other input methods? | All: chat, signs, F5 console (`listkeys fires …`), config file. |
| Q-27 | Build the opt-in "server ownership" mode (§4.12)? | Yes, opt-in (off by default), alongside the refill method. |
| Q-29 | Phase-2 client mod library? | "Most lightweight option that gives (almost) everything for free" → no new dependency: private RPC channel reusing the command system + ConfigurationManager as the (optional) menu (§4.16). |
| Q-28 | Console (Xbox/PlayStation) testing depends on outside testers; until then those checks stay open. | Noted (owner has no console). |

## 7. Decision log

| Date | Decision | By |
|------|----------|----|
| 2026-10-06 | RTM created | Claude |
| 2026-10-06 | Strategy C (hybrid): server-only core now, optional client mod later | Owner |
| 2026-10-06 | Config via file + hot reload, chat commands, client menu later; `AdminOnly` default off | Owner |
| 2026-10-06 | Testing: 100 % coverage gate on logic + in-game checklist for glue | Owner |
| 2026-10-06 | .NET SDK 10 + ilspycmd installed user-locally in `~/.dotnet` | Owner |
| 2026-10-06 | Owner handles all git operations; agents never run git write commands | Owner |
| 2026-10-06 | Add `AGENTS.md` for multi-agent collaboration (Claude Code reads it natively; no `CLAUDE.md` needed) | Owner |
| 2026-10-06 | Per-item settings for every fuel user; non-light sources vanilla by default | Owner |
| 2026-10-06 | One schedule window per day, HH:MM, off by default; scheduled off keeps fuel | Owner |
| 2026-10-06 | Smoke stays vanilla for now; per-item toggle later | Owner |
| 2026-10-06 | `!fires` prefix; hide commands from others by default; log everything; admin list never changeable | Owner |
| 2026-10-06 | Name Inferno, author GrundleLord, proprietary licence, target latest stable Valheim (1.0.17) | Owner |
| 2026-10-06 | README must describe features accurately, ship a sample config and a tested-versions table | Owner |
| 2026-10-06 | Light sources default to AlwaysOn; BurnRate −10…+10 (10 %/step); On/Off hour+minute; On = Off = always on; AlwaysOn overrides schedule; keep everything topped up incl. unattended areas; bulk targets; AdminOnly: anyone on, admins off | Owner |
| 2026-10-07 | Hot tubs = light sources; uninstall keeps fuel (shutdown restore); `IgnoreRain` general rule, default off | Owner |
| 2026-10-07 | Command input via chat, signs, F5 console (`listkeys fires …`) and config file | Owner |
| 2026-10-07 | Build server ownership mode, opt-in, default off | Owner |
| 2026-10-07 | Crossplay works with BepInEx (confirmed by owner) | Owner |
| 2026-10-07 | In-game (local server, vanilla client): always-on, schedule (incl. midnight), fuel kept while off, clock vs. sun, sign commands, status, in-game names, settings saved — passed | Owner |
| 2026-10-07 | Robust error logging; publish 0.1.0 beta on Thunderstore as GrundleLord-Inferno; license MIT; placeholder icon; no website link yet | Owner |
| 2026-10-07 | Replies: combined message + answer on sign; commands use in-game item names | Owner |
| 2026-10-07 | Frigid Kiln (`piece_FrostKiln`, ice → Liquid Frost) is a production station: vanilla by default, like the charcoal kiln. Rule: a smelter-type piece is a light source only if it has no ore slots **and** produces nothing (hot tub) | Owner |
| 2026-10-07 | First local server run: discovery, display names and shutdown hook verified in-game | Owner |
| 2026-10-07 | Hardening; prioritise server-only testing (most players are on console or won't install mods) | Owner |
| 2026-10-07 | Phase 2 go-ahead: lightest option → no new dependency; ConfigurationManager menu; smoke toggle | Owner |
| 2026-10-07 | Keep working without in-game tests; get as close to "perfect server-only" as possible | Owner |
| 2026-10-06 | Scaffold: Core `netstandard2.0` (no game refs), plugin `net48`, xUnit v3 on Microsoft.Testing.Platform, coverlet gate 100 % line+branch, central package versions, `.editorconfig`, warnings as errors | Owner (approved "all sounds good") |
