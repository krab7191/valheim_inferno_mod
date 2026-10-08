# Inferno — tester guide (no mods needed)

Thanks for helping! Inferno runs **only on the server**. You don't install anything: play as normal on
PC, Xbox or PlayStation. This takes about 15 minutes.

## What Inferno should do

- Torches, sconces, campfires, hearths, braziers, lanterns and hot tubs **never run out of fuel**.
- Smelters, furnaces and ovens work **exactly like normal**.
- Everything else in the game is unchanged.

## How to talk to Inferno: write on a sign

Build a sign (or use any sign) and write one of these on it. Inferno reads it within a few seconds, shows the
answer in the **top-left corner of your screen**, and writes a short answer onto the sign. Missed the
on-screen text? Open your inventory → **Compendium → Logs**. Items are named as in the game (e.g. `hot tub`).

| Write this on a sign | What should happen |
|----------------------|--------------------|
| `!fires status` | Top left: "Inferno … running on Valheim …" and a few lines of numbers |
| `!fires help` | Top left: the main commands, one per line (more: `!fires help items`, `help presets`, `help admin`) |
| `!fires show hot tub` | Top left: the hot tub's settings |
| `!fires preset night torches` | Torches go out until dusk (18:00), then light without using fuel |
| `!fires undo` | Puts back what your last command changed |
| `!fires preset night nearby` | Only the lights in your ward's area (or within 20 m) switch to night mode |

If **nothing** appears after 10 seconds, tell us: that's important.

(If another player is online, typing the same thing in **chat** works too. If you're alone, chat won't work:
that's a known Valheim limitation, so use a sign.)

## Things to check

Tick each one and note anything odd: **what you did, what you expected, what happened**, and roughly when
(time of day is fine). Screenshots or a short clip help a lot.

1. **Join the server.** You get in normally, no error or kick. *(Most important for Xbox/PlayStation.)*
2. **Sign test.** Write `!fires status` on a sign. You see the answer top left and on the sign.
3. **New torch.** Build a standing torch. Within a few seconds, look at it: fuel shows as full (e.g. 4/4) and
   it's lit.
4. **Stays lit.** Come back to that torch after a full in-game day. Still lit and full.
5. **Campfire / hearth.** Build one with no wood. It lights itself and stays full.
6. **Hot tub.** If you have one: it heats without adding wood.
7. **Leave and come back.** Go far away (another biome) for an in-game day, then come home. Your base lights are on
   **as soon as you arrive**, not dark for a moment.
8. **Smelter is normal.** A smelter or furnace still needs coal and uses it like before.
9. **Removing things.** Use the hammer to remove a burning torch or campfire. It works like normal and you get the
   materials back.
10. **Rain (if it rains).** Note whether candles or lanterns outside go out. (By default they do, like normal
    Valheim.)
11. **Anything else** that feels different from normal Valheim.

## What to send

- Your platform (PC Steam / Xbox / PlayStation / Game Pass PC).
- The list above with ✓ or ✗ and notes.
- The top-left text from `!fires status` (a photo of the screen is fine).

Thank you!
