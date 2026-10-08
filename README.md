# Inferno

**Server-side control over every fire, torch and fuel-burning station in Valheim. Players install nothing,
so it works with Xbox / Game Pass crossplay.**

Source code, documentation and issue tracker: https://github.com/krab7191/valheim_inferno_mod

> **Status: beta.** Every feature is unit-tested. Features marked *Available* have also been verified in-game on
> dedicated servers (local and hosted) with vanilla PC clients; the others are awaiting in-game testing
> (see [Compatibility](#compatibility)).

## Features

| Feature | Status | Notes |
|---------|--------|-------|
| Server-only install (clients need nothing, crossplay-safe) | Available (PC clients tested; console clients not yet) | No version check, no new items: vanilla and Xbox clients join as normal |
| Per-item settings for every fuel-burning piece | Available | Found automatically at startup, including pieces added by other mods |
| Always on | Available | **Default for all light sources** (torches, sconces, fires, hearths, braziers, lanterns, hot tubs, …). Keeps fuel full everywhere, also where nobody is nearby, so bases aren't dark when you arrive. While on, burn rate and schedule are kept but don't apply |
| Fuel burn rate | Implemented, awaiting in-game test | −10 to +10, 0 = vanilla, each step 10 %: −10 uses no fuel, +10 burns twice as fast |
| Daily on/off schedule | Available | On and off time on the in-game clock (06:00 = sunrise, 18:00 = sunset), one window per day. Equal times (default 00:00/00:00) = no schedule. Fuel is kept while a light is scheduled off. Only used when *always on* is off |
| Ignore rain | Implemented, awaiting in-game test | Off by default (lights go out in the rain as in vanilla). The server log lists which lights react to rain. See [Limits](#known-limits) |
| Correct burning while nobody is nearby | Implemented, awaiting in-game test | Vanilla burns all missed time at once when a player arrives, ignoring schedules. Inferno burns it on the server as time passes, following schedule and burn rate |
| Server ownership mode (experimental, off by default) | Implemented, awaiting in-game test | The server keeps ownership of fires, so no player's game burns their fuel or puts them out in rain: fuel only changes when the server says so. Exact burn rate and schedule. See [Server ownership mode](#server-ownership-mode-experimental) |
| Commands on signs, in chat and in the F5 console | Available | See [Commands](#commands). Chat needs a second player online |
| In-game item names and word groups | Available | `hot tub`, `standing wood torch`, `torches`, `braziers`; typos get suggestions |
| Presets and undo | Available | `preset night` / `eternal` / `vanilla` in one command; `undo` steps back through your last 10 changes |
| Readable replies | Available | One message on screen, kept in Compendium → Message log; sign commands leave a short answer on the sign (formatting like `<color=green>` is kept) |
| Per-base control (`nearby`) | Implemented, awaiting in-game test | Change only the fires in your ward's area (or within 20 m); ward access required, like building |
| Config file with live reload | Available | Edit through your host's web file manager; no restart |
| `AdminOnly` permission switch | Available | Off by default: anyone may change settings. Anyone can turn it on; only admins can turn it off. Inferno never changes the server's admin list |
| Full audit log and error reporting | Available | Every command, every change (who, old → new) and every config-file edit goes to the BepInEx log |
| Safe uninstall | Implemented, awaiting in-game test | On a normal server shutdown Inferno switches scheduled-off lights back on before the world is saved; fuel stays as it is |
| Optional PC client mod: in-game settings menu | Implemented, awaiting in-game test | Same DLL on a PC client + [ConfigurationManager](https://thunderstore.io/c/valheim/p/Azumatt/Official_BepInEx_ConfigurationManager/) (F1). Shows and edits the **server's** settings live; read-only for non-admins while `AdminOnly` is on. See [Client mod](#optional-client-mod-pc) |
| Per-item smoke toggle | Implemented, awaiting in-game test | Removes smoke for players who have the client mod; everyone else sees vanilla smoke |
| No rain flicker (client mod) | Implemented, awaiting in-game test | With `IgnoreRain` on, players with the client mod never have their lights put out by rain |

Items that are not light sources (smelters, blast furnaces, ovens, …) keep vanilla behaviour until you change them.
Items without fuel (e.g. the charcoal kiln) and fires with infinite fuel are not listed.

## Installation (server)

**With a mod manager** (r2modman, Thunderstore Mod Manager, or your host's Thunderstore mod installer): install
**[GrundleLord-Inferno](https://thunderstore.io/c/valheim/p/GrundleLord/Inferno/)**. BepInExPack Valheim is installed
with it.

**By hand:**
1. Install [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) on the server
   (most hosts have a one-click option).
2. Copy `Inferno.dll` and `Inferno.Core.dll` (in the download's `plugins` folder) to the server's
   `BepInEx/plugins/Inferno/` folder.
3. Restart the server. The log (`BepInEx/LogOutput.log`) shows `Inferno … loaded` and lists every item it found.
4. Settings are in `BepInEx/config/GrundleLord.Inferno.cfg` (created on first start). A fully commented
   `sample-config.cfg` comes with the download (also [on GitHub](https://github.com/krab7191/valheim_inferno_mod/blob/main/docs/sample-config.cfg)).

Players don't install anything. A one-page [guide for players](https://github.com/krab7191/valheim_inferno_mod/blob/main/docs/tester-guide.md)
(also console players) explains how to use Inferno with signs.

## Commands

Three ways to type the same commands:

| Where | How | Works when | Reply |
|-------|-----|------------|-------|
| Chat | `!fires <command>` | **At least one other player is online.** Valheim sends chat directly to other players, so a lone player's chat never reaches the server | On screen, top left |
| Sign | Write `!fires <command>` on any sign | Always, including alone and on Xbox. Signs hold 50 characters | On screen, and a short answer replaces the command on the sign |
| F5 console | `listkeys fires <command>` | Always. Needs the `-console` launch option (Steam: Properties → Launch options). Not available on Xbox | In the console |

The F5 form borrows the vanilla `listkeys` command because Valheim only forwards built-in server commands to the
server. Without Inferno it just lists world keys, so it is harmless.

| Command | Example |
|---------|---------|
| `help [items\|presets\|admin]` | `!fires help`, `!fires help presets` |
| `status` | `!fires status` (is Inferno running? versions, counts, in-game time) |
| `list [all\|lights\|stations]` | `!fires list lights` |
| `show <item>` | `!fires show hot tub` |
| `preset eternal\|night\|vanilla [item]` | `!fires preset night` (all lights: lit at night, no fuel) |
| `undo` | `!fires undo` (reverts your own last change) |
| `alwayson <item> on\|off` | `!fires alwayson lights off` |
| `smoke <item> on\|off` | `!fires smoke lights off` (client mod players only) |
| `burnrate <item> <-10..10>` | `!fires burnrate all -5` |
| `schedule <item> <on HH:MM> <off HH:MM>` | `!fires schedule lights 18:00 06:00` |
| `schedule <item> night\|day\|off` | `!fires schedule torches night` |
| `reset <item>` | `!fires reset all` |
| `adminonly on\|off` | `!fires adminonly on` |
| `hidecommands on\|off` | `!fires hidecommands off` |
| `ignorerain on\|off` | `!fires ignorerain on` |
| `serverownership on\|off` | `!fires serverownership on` |

`<item>` is the name **as shown in game**, e.g. `hot tub`, `standing wood torch`, `campfire` (case, spaces and
punctuation don't matter; a name shared by several items changes all of them); a **word** that selects every item
with it in its name, e.g. `torches`, `braziers`, `fires`, `lanterns`; or a group: `all`, `lights`, `stations`.
Internal names like `piece_bathtub` work too. Typos get "did you mean" suggestions. When a word selects several
items, the reply lists them.

**Presets** set everything in one go: `eternal` = always on (the default), `night` = lit 18:00–06:00 and never
uses fuel, `vanilla` = plain game behaviour. They apply to all lights unless you name something
(`!fires preset night torches`); `vanilla` without a name applies to everything.

Replies appear top left for a few seconds and stay in the game's message log (**Compendium → Logs**). A command
written on a sign is replaced by a short answer, so the result stays readable until you write the next command.

**Example: torches that light at dusk and never need fuel.** One command: `!fires preset night torches`.
That's the same as doing it by hand (always on overrides the schedule, so it's switched off):

```
!fires alwayson torches off
!fires burnrate torches -10
!fires schedule torches night
```

Changed your mind? `!fires undo`.

**Only your base: `nearby`.** Add `nearby` after the item to change only the fires around you (chat, F5) or
around the sign, e.g. `!fires preset night nearby` or `!fires burnrate torches nearby -5`.
- Inside a **ward**, "nearby" means that ward's area, and only players with access to the ward (its builder and the
  players they added) can change its fires: the same rule as building there.
- Without a ward it means everything within **20 m**, and anyone may change it, as with building.
- Those fires keep their **own settings** (saved with the fire, also after a restart); later changes to their item
  type don't affect them. `!fires show nearby` lists them; `!fires reset nearby` makes them follow their item type
  again.

**Always on vs. burn rate and schedule:** while always on is on, burn rate and schedule have no effect. Setting a
burn rate or a schedule window therefore switches always on off for those items (the reply tells you).
Turning always on back on keeps your burn rate and schedule stored, so nothing is lost.

## Configuration

Everything the commands change is stored in `BepInEx/config/GrundleLord.Inferno.cfg`, and edits to that file
apply live. A fully commented `sample-config.cfg` comes with the download (also
[on GitHub](https://github.com/krab7191/valheim_inferno_mod/blob/main/docs/sample-config.cfg)).

| Section | Setting | Default | Meaning |
|---------|---------|---------|---------|
| `[General]` | `AdminOnly` | `false` | Only admins may change settings |
| | `HideCommands` | `true` | Hide `!fires` chat lines from other players |
| | `IgnoreRain` | `false` | Relight lights put out by rain or wind |
| | `ServerOwnership` | `false` | Experimental: server keeps ownership of fires |
| `[<item>]` | `AlwaysOn` | lights `true`, stations `false` | Keep fuel full. While on, burn rate and schedule don't apply |
| | `BurnRate` | `0` | −10 … +10, 10 % per step |
| | `Smoke` | `true` | Smoke on/off (client mod players only) |
| | `OnTimeHour`, `OnTimeMinute` | `0`, `0` | Hour 0–24, minute 0–60 (lights only) |
| | `OffTimeHour`, `OffTimeMinute` | `0`, `0` | Same; on = off means no schedule |

## Optional client mod (PC)

Players don't need anything. PC players **may** install the same `Inferno` files (plus
[ConfigurationManager](https://thunderstore.io/c/valheim/p/Azumatt/Official_BepInEx_ConfigurationManager/), which
many modded setups already have) to get:

- **A settings menu:** press **F1**, open *Inferno*. While connected to a server running Inferno, the menu shows the
  server's settings and changes apply to the whole server immediately (they are sent as normal commands, so the
  same permissions and logging apply). With `AdminOnly` on, non-admins see the settings read-only.
- **Smoke toggle:** items set to `Smoke = off` make no smoke for this player.
- **No rain flicker:** with `IgnoreRain` on, this player's game never puts lights out in the rain.

On a server without Inferno, or in single-player without a world loaded, the client mod does nothing. Version
mismatches between client and server are detected; the menu is then disabled with a message.

## Server ownership mode (experimental)

Normally the player nearest a fire "owns" it, and their game burns its fuel and switches it off in rain; Inferno
corrects from the server. With `ServerOwnership` on, the server keeps ownership of every eligible fire instead:

- No player's game burns the fuel, so it changes **only** when Inferno says so: always-on fires never drop, burn
  rate and schedule are exact, and there are no update collisions.
- Rain and wind can't switch these fires off.
- Adding fuel and switching on/off are handled by the server, so nothing is lost.
- Deconstructing, repairing or damaging a fire briefly hands it to that player (30 s) so the game handles it
  normally. The action takes about a second longer than usual; if it doesn't happen, try again.
- Side effects: no sound or flash when adding fuel or switching these fires.
- Excluded (keep normal behaviour): fires that melt Deep North snow or spread fire, and pieces with other
  player-run parts (smelters, containers, …). The server log lists them.
- Turning it off gives every fire back immediately; Inferno also gives them back on shutdown.

## Known limits

These come from how Valheim works and apply to any server-only mod:

- **Rain, wind, roofs and water** are checked on players' PCs. Some lights (e.g. candles, lanterns) switch
  themselves off in the rain; with `IgnoreRain` on, Inferno switches them back on within a few seconds, but they
  may flicker during storms. The server can't tell rain from a player's hand, so `IgnoreRain` also relights
  lights players switched off. Lights blocked by a roof or under water stay out.
- **Burn rate is close, not exact, while a player is nearby.** That player's game burns the fuel; Inferno corrects
  the amount every 5 seconds. While nobody is nearby, the server burns fuel itself, exactly, following the burn
  rate and the schedule.
- **Chat commands need a second player online** (see [Commands](#commands)); signs and the F5 console always work.
  Console players whose platform privacy settings block text chat can always use signs.
- **Smoke** can only be removed for players who have the client mod.
- **Uninstall:** stop the server normally (not a forced kill), then remove the files. Fuel levels stay as they are
  and burn normally afterwards.

## Troubleshooting

1. Write `!fires status` on a sign (or `listkeys fires status` in the F5 console). If nothing answers, Inferno isn't
   running on the server. The last line shows **errors since start**.
2. Open the server's `BepInEx/LogOutput.log` (most hosts show it in their file manager). Search for `Inferno`:
   - the **startup diagnostics** block lists versions, crossplay on/off, other mods, and any other mod that hooks
     the same game methods as Inferno (the usual cause of conflicts);
   - every command, change and reply is logged;
   - errors are logged in full the first time and summarised if they repeat. Inferno skips the failing object
     and keeps running.
3. Report problems on [GitHub Issues](https://github.com/krab7191/valheim_inferno_mod/issues) and include the startup diagnostics block and the first error.

## Compatibility

| Valheim version | Inferno version | Tested in-game |
|-----------------|-----------------|----------------|
| 1.0.17 | 0.1.0 – 0.2.x | Yes: local and hosted dedicated servers, vanilla PC clients (incl. two players). Crossplay with console clients: not yet |
| 1.0.17 | 0.3.0 | `nearby` (per-base control) awaiting in-game test |

Inferno targets the **latest stable Valheim release** only. New game patches are tested as they ship; older game
versions are not supported.

<!-- thunderstore:exclude-start -->
## Building from source

Requirements: [.NET SDK 10](https://dotnet.microsoft.com/download). The game is only needed to build the plugin,
not to run the tests.

```sh
# Run all core tests (enforces 100 % line and branch coverage)
dotnet test --project tests/Inferno.Core.Tests

# Build the plugin (needs Valheim installed; see Directory.Build.user.props.example)
cp Directory.Build.user.props.example Directory.Build.user.props   # then edit ValheimDir
dotnet build

# Release package: artifacts/Inferno-<version>.zip
dotnet build -c Release
```

Testing: [in-game checklist](https://github.com/krab7191/valheim_inferno_mod/blob/main/docs/in-game-test-checklist.md) (server-only first) and a plain-language
[tester guide](https://github.com/krab7191/valheim_inferno_mod/blob/main/docs/tester-guide.md) for players without mods, including console players.

Contributor and AI-agent guidelines are in [AGENTS.md](AGENTS.md); requirements and design decisions are in
[RTM.md](RTM.md).

<!-- thunderstore:exclude-end -->
## License

MIT © 2026 GrundleLord.
