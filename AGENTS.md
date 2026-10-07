# AGENTS.md

Guidance for AI coding agents (and humans) working in this repository. Keep it short, current and true —
update it in the same change that makes it wrong.

## What this project is

A Valheim mod (BepInEx 5 + Harmony) that lets a server operator configure every light source: always-on,
fuel burn-rate multiplier, daily on/off schedule, and more. **It must work when installed on the server only**,
so vanilla and Xbox/crossplay clients need nothing installed. An optional PC client mod comes later.

**`RTM.md` is the source of truth** for requirements, research findings, open questions and decisions.
Read it before starting any task.

## Ground rules

1. **No unilateral decisions.** Anything that changes behaviour, scope, config surface, dependencies or project
   structure needs the project owner's approval. Ask, then record the answer in `RTM.md` §6/§7.
2. **Git is handled by the project owner.** Do not run `git add/commit/push/pull/rebase/reset/checkout/stash`
   or any other command that changes git state. Read-only commands (`git status`, `git diff`, `git log`) are fine.
3. **Server-only first — it is the product.** Most players are on console or won't install mods. The optional
   client mod may add comfort but must never be needed for anything, and in-game testing prioritises vanilla
   clients (`docs/in-game-test-checklist.md`, P1). Before writing code, ask "does this run on the server for a
   vanilla client?":
   - A dedicated server does **not** simulate fires — the owning client does. Harmony patches on `Fireplace`
     methods do nothing server-side for client-owned fires. Act on ZDO data instead (see `RTM.md` §4.2–4.3).
   - Never add anything that makes the server reject or kick vanilla clients (version handshakes, required-mod
     checks, Jötunn network-compat enforcement, ServerSync "required" mode).
   - Never rely on new prefabs, assets or RPCs that a vanilla client would have to know about. (Inferno's own
     `Inferno_*` RPCs are only for the optional client mod; vanilla clients never see them.)
4. **Verify game behaviour from the decompiled source, not from memory or forum posts.** Cite the class/method in
   `RTM.md` when a finding drives a design decision.
5. **Do not commit game or third-party binaries** (`assembly_valheim.dll`, Unity, BepInEx DLLs). They are
   copyrighted; reference them from the local game install or from public NuGet packages.
6. **Keep the world save safe.** Only write vanilla ZDO keys with values vanilla code accepts. Behaviour must
   revert cleanly when the mod is removed.

## Testing — required for every change

- All logic lives in code that can be unit-tested without Unity or the game, behind small interfaces.
- **100 % line and branch coverage is enforced**; the build fails below it. No `[ExcludeFromCodeCoverage]` without
  the owner's approval and a comment explaining why.
- Code that can only run inside the game is kept minimal and listed in the in-game test checklist under `docs/`,
  which the owner runs. Add or update a checklist entry whenever such code changes.
- Write the test first or alongside the change; a bug fix starts with a failing test.

## Project layout

| Path | What | Builds without the game? |
|------|------|--------------------------|
| `src/Inferno.Core/` | All rules (clock, schedule, fuel math, config, commands, permissions). `netstandard2.0`. **Must not reference Unity, BepInEx or the game.** | Yes |
| `src/Inferno/` | BepInEx plugin (`net48`): thin glue between the game and Inferno.Core | No — needs `ValheimDir` |
| `tests/Inferno.Core.Tests/` | xUnit v3 tests for Inferno.Core, 100 % coverage gate | Yes |
| `docs/in-game-test-checklist.md` | Manual checks for code that only runs in-game, plus a results log | — |
| `docs/tester-guide.md` | Plain-language guide for testers without mods (incl. console players) |
| `docs/sample-config.cfg` | Commented sample of the generated config; keep in sync with `ConfigSettingsStore` | — |
| `RTM.md` | Requirements, research, open questions, decision log | — |

## Commands

```sh
# Tests + coverage gate (fails below 100 % line or branch coverage of Inferno.Core)
dotnet test --project tests/Inferno.Core.Tests

# Build everything (plugin needs Directory.Build.user.props with ValheimDir; copy the .example file)
dotnet build

# Formatting / style check (must pass)
dotnet format Inferno.slnx --verify-no-changes

# Release packages: artifacts/GrundleLord-Inferno-<version>.zip (Thunderstore upload)
# and artifacts/Inferno-<version>.zip (unzip into a server's Valheim folder)
dotnet build -c Release
```

Coverage report (Cobertura XML): `artifacts/TestResults/`. Setting `InfernoDeployDir` in
`Directory.Build.user.props` copies the plugin there after each build.

## Toolchain

- .NET SDK 10 (pinned in `global.json`; tests use Microsoft.Testing.Platform). On the owner's WSL machine the SDK
  is user-local: `export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$HOME/.dotnet/tools:$PATH`
- Package versions are central in `Directory.Packages.props`; BepInEx packages come from the BepInEx feed
  (`nuget.config`). Game assemblies come from the local install and are never copied or committed.
- Target game version: latest stable Valheim (currently **1.0.17**). No backwards compatibility with older versions.
- Decompiling the game for research: `ilspycmd -t <TypeName> -r <Valheim>/valheim_Data/Managed <Valheim>/valheim_Data/Managed/assembly_valheim.dll`.
  Write output to a temp/scratch directory, never into the repo.

## Code conventions

- C#, matching `.editorconfig`. Analyzers on (`latest-recommended`), warnings are errors, nullable enabled.
- Public members of Inferno.Core need XML doc comments.
- Small, pure functions for rules (schedule, burn math, permissions); thin adapters for the game.
- Defensive at the game boundary: null/invalid ZDOs, unknown prefabs and bad config values must be handled and
  logged, never thrown into the game loop.
- Log through the BepInEx logger: actions at Debug, recoverable problems at Warning.
- Comments explain *why* (especially game-engine quirks), not *what*.

## Releasing

- Thunderstore versions are permanent: every upload needs a new `<Version>` in `Directory.Build.props`
  (semantic versioning) and a matching `CHANGELOG.md` section. Update `TestedGameVersion` in `InfernoPlugin.cs`
  and the README compatibility table only for game versions actually tested.
- `dotnet build -c Release` builds both zips; the owner uploads the Thunderstore zip.

## When you finish a task

- Tests pass, coverage gate is green, build has zero warnings, `dotnet format --verify-no-changes` passes.
- `README.md` stays accurate: a feature is marked *Available* only when implemented **and** verified in-game;
  the compatibility table lists only game versions actually tested.
- User-visible changes are added to `CHANGELOG.md` under *Unreleased*.
- `RTM.md` updated: requirement status, design/test refs, any new findings or open questions.
- Summarise what changed and anything the owner should test in-game. Leave committing to the owner.
