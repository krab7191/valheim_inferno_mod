# Changelog

All notable changes are documented here. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
versioning: [Semantic Versioning](https://semver.org/).

## [Unreleased]

## [0.3.0] - 2026-10-08

**Released without in-game testing** (unit-tested only). Changes since 0.2.x run on every fire, not only for
players who use `nearby`. If something misbehaves, go back to 0.2.2 and please report it on GitHub.

### Added
- **Per-base control with `nearby`**: add `nearby` to an item command to change only the fires around you or the
  sign, e.g. `!fires preset night nearby` or `!fires burnrate torches nearby -5`. Inside a ward, "nearby" means that
  ward's area and only players with access to the ward can change its fires (same rule as building); without a
  ward it means everything within 20 m. Those fires keep their own settings (saved with the fire, also after a
  restart); `!fires reset nearby` makes them follow their item type again; `!fires show nearby` lists them.
- Wrong word order gets a suggestion: `!fires all preset eternal` → "Did you mean: !fires preset eternal all".
- `!fires undo` steps back through your last 10 changes (was 1).
- Performance line in the server log every 10 minutes (objects tracked, time per pass and sweep, writes, errors).
- `!fires status` also counts fires with their own settings and wards.
- Link to the GitHub repository on the mod page; README links to the tester guide and sample config work on
  Thunderstore.

## [0.2.2] - 2026-10-07

### Changed
- Help is easier to read on screen and in the message log: one command per line with a short description, and
  topic pages `!fires help items`, `!fires help presets`, `!fires help admin` (each fits on screen).

## [0.2.1] - 2026-10-07

### Changed
- Setting a burn rate or a schedule window on an always-on item now switches always on off, so the setting takes
  effect (the reply says so). Turning always on back on keeps the stored burn rate and schedule for later.
  `schedule … off` leaves always on alone.

### Fixed
- Commands written with formatting, e.g. `<color=green>!fires status` on a sign, were ignored. Formatting tags are
  now ignored when reading commands, and the answer on the sign keeps the player's formatting.

## [0.2.0] - 2026-10-07

Tested on a local dedicated server (Valheim 1.0.17): presets, word groups, instant sign answers. `undo` is
unit-tested but not yet tested in-game.

### Added
- Presets: `!fires preset eternal|night|vanilla [item]` (e.g. `!fires preset night` = lights lit 18:00–06:00 without
  using fuel).
- Word groups: `torches`, `braziers`, `fires`, `lanterns`, … select every item with that word in its name; replies
  list what changed. Schedule words `night` and `day`.
- `!fires undo` reverts your own last change.

### Changed
- New fires are filled and sign commands answered within about a second (previously up to 30 s / 5 s).
- `!fires torch` style words now select matching items instead of only suggesting names.

## [0.1.0] - 2026-10-07

First public beta. Tested in-game on a local dedicated server (Valheim 1.0.17) with vanilla PC clients.

### Added
- Error reporting: each problem is logged once in full (repeats summarised), one failing object never stops
  Inferno, startup diagnostics (versions, crossplay, other mods, shared hooks), error count in `!fires status`.
- Thunderstore package (GrundleLord-Inferno); MIT license.
- Project scaffold: core library, BepInEx plugin, test project with a 100 % coverage gate.
- In-game clock conversion that matches Valheim's own day/night rescaling (06:00 = sunrise, 18:00 = sunset).
- Server-side plugin: discovers every fuel-burning item, keeps light sources (incl. hot tubs) always on by default,
  burn rate −10…+10, daily on/off schedule, `IgnoreRain`, shutdown restore of scheduled-off lights.
- Commands via chat (`!fires …`), signs (`!fires …`) and the F5 console (`listkeys fires …`); groups `all`,
  `lights`, `stations`; `AdminOnly` / `HideCommands` settings.
- Server burns fuel itself for fires nobody is near (follows schedule and burn rate; no burst on arrival).
- Reliable server writes for refills and schedule switches; fewer network writes for always-on items.
- Each game hook installs separately (a game update can't take the whole mod down); version-mismatch warning;
  startup list of lights that react to rain.
- Experimental server ownership mode (`ServerOwnership`, off by default): the server owns fires, so fuel only
  changes when Inferno says so; add-fuel and switch actions are handled on the server; other actions briefly hand
  the fire to the player.
- Optional PC client mod (same DLL): ConfigurationManager (F1) menu that shows and edits the server's settings
  live through a private channel vanilla clients never see; per-item smoke toggle; no rain flicker with
  `IgnoreRain`.
- `!fires status` for remote testing; per-player rate limit, command length cap, batched broadcasts and menu
  edits; plain-language tester guide for players without mods.
- Commands accept in-game item names (`!fires alwayson hot tub on`) with suggestions for typos; replies are one
  combined message, sign commands leave a short answer on the sign, and replies are logged on the server.
- BepInEx config file with live reload; audit log of every command, change and config-file edit.
- Release packaging (`dotnet build -c Release` → `artifacts/Inferno-<version>.zip`), sample config, in-game test
  checklist.
