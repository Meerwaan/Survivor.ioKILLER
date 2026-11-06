# Run pacing targets

Shared target for everyone tuning a run. The owner's words: "qu'on soit pas direct invincible et ultra boosté dès le début, faut de la progression dans chaque partie sans qu'elle soit trop lente ni beaucoup trop rapide", "que ce soit vivant, qu'il y ait de l'action".

The game is a fair competitive game: every player starts equal, the build comes from in-run randomness and choices. No permanent power.

## Measured today (bot circling the arena, always takes the first card, editor, before this pass)

| Time | Level | Alive enemies | Player HP (max ~105) | Notes |
|---|---|---|---|---|
| 0:40 | 5 | 50 | 7 | almost died once to the first horde/elite: early spike too sharp |
| 2:00 | ~14 | 25-60 | 68-105 | |
| 3:00 | ~20 | 30 | full | from here on HP is basically always full |
| 5:00 | ~28 | 20-40 | full | enemies die as soon as they enter the screen |
| 7:00 | 37 | ~10-30 | full | ~1300 gold in run (now reduced) |
| 10:00 | 53 | 25-70 | full | |
| 13:30 | 69 | 150 | 95 | pressure finally comes back (endless-only scaling) |

Other facts: the kill combo never breaks (x18000), so FRÉNÉSIE fires every 150 kills all game long. A full build (5 weapons + 6 passives) is reached around 5-6 min, evolutions by ~4-5 min. Healing (regen, lifesteal, meat, chests, "Increvable" shockwave heal) keeps HP pinned at max.

## Targets (a decent human player, not perfect play)

| Phase | Time | Feel | Level | Build |
|---|---|---|---|---|
| Fragile start | 0:00-1:30 | 1 weapon, real danger, every level-up matters | 1-9 (first level-up ~8-10 s, then every ~10-15 s) | 1-2 weapons |
| Build forms | 1:30-5:00 | choices, first synergy tier I, first "wow" | ~10-20 | 3 weapons, 2-3 passives, 1st tier I by ~3 min |
| Power spike | 5:00-9:00 | first evolution 6-8 min, carnage, but hordes/elites/boss still bite | ~20-30 | 4-5 weapons, first tier II ~8 min |
| Peak | 9:00-14:00 | full build ~12 min, 2-3 evolutions, screen-clearing but HP dips during events | ~30-40 | full |
| Endless decline | 14:00+ | the arena overwhelms even a great build; death ~15-20 min | 40+ | |

Danger rules:
- HP should regularly drop to 40-70 % during hordes, elite waves and bosses, then recover between them. Never "always full", never "dead in 1 s".
- Waves have rhythm: pressure -> peak -> short breather -> pressure. Something notable every ~20-40 s (event, elite, horde, chest, choice).
- Combo should break between waves (breathers) so FRÉNÉSIE is an earned moment, a few times per run, not permanent.
- Campaign stages (5-10 min) use the same curve; later stages multiply enemy stats (see Meta/Campaign.cs).

## Pacing pass (after) — numbers and why

Rebuild the scene (*Survivor > Build Prototype Scene*) to apply: the builder re-adds RunDirector / Loot / pickups with
these defaults and re-applies the enemy stats. Model used to pick them: the old measurements were fitted with a kill/pickup
efficiency (~0.3 at 0:40, ~0.65 at 2-7 min, ~0.9 late), then the new stream was fed through it.

### XP curve (`PlayerXP`, serialized fields)

`required(L) = 3 + 1.5*(L-1) + 0.07*(L-1)^3` (was `3 + 3.5*(L-1) + 0.6*(L-1)^1.8`).
Per level: L1 3, L2 5, L3 7, L5 13, L10 68, L15 216, L20 512, L25 1007, L30 1754, L35 2805, L40 4214.
The cubic term is the main knob: the level grows like the cube root of the XP gathered, so the curve is robust
(2x the expected XP income only gives ~+25% levels). Gem XP growth `xpPerMinute` 0.1 -> 0.03 (the stream already grows).

| Time | Level (old -> expected) | Picks incl. chests | Build (bot, first card) |
|---|---|---|---|
| 1:00 | ~7 -> 6 | 5 | 1-2 weapons |
| 1:30 | ~10 -> 9 | 9 | 2 weapons |
| 3:00 | 20 -> 15 | 16 | 3 weapons, 2-3 passives |
| 5:00 | 28 -> 20 | 25 (boss chest at 3:20) | 3-4 weapons, first tier I |
| 8:00 | ~43 -> 26 | 36 | 4-5 weapons, 1st evolution 6-8 min |
| 10:00 | 53 -> 30 | 44 | ~full slots |
| 12:00 | ~62 -> 33 | 48 | full, 2-3 evolutions |
| 14:00 | ~71 -> 36 | 56 | |
| 18:00 | -> 42 | 67 | |

Chests still give free upgrades (boss 3, thief/challenge reward chest 1): they are ~1/3 of the picks after 8 min. If the
build fills too early, lower `Pickup.chestUpgrades` (boss) to 2 before touching the curve.

### Wave rhythm (`RunDirector`)

Each wave lasts `waveLength` = 50 s: **build** 30 s (stream 0.7x -> 1.3x) -> **peak** 12 s (1.5x + a peak event) ->
**lull** 8 s (5 s of total silence, then 0.3x). Average ~0.95x. The rhythm fades in over the first 1.5 min (wave 0 is flat-ish).
- Peak event: odd waves = HORDE ring (`14 + 7/min`, max 100; runners every 3rd after 2 min, brutes 1/9 after 5 min,
  bloaters 1/11 after 3 min). Even waves = ASSAUT, a mixed column from where the player is heading (`10 + 4/min`, max 60;
  runners, plus brutes and spitters after 2.5 min). No peak event while a boss is alive (the boss is the peak, stream x0.6).
- Old fixed breather (16 s at 0.3x after each horde) removed: the lull replaces it.
- Elites (55 s, every 40 s) never spawn during a peak (pushed to the lull). Rats / golden wave / thief only in the build phase,
  never in the last 8 s before a peak. Boss on its own timer: 200 s then every 200 s = always the start of a wave, right after a lull.
- `RunDirector.Phase`, `WaveIndex`, `PhaseTimeLeft` and the `PhaseChanged` event are public for HUD / arena events.

Timeline of the first 4 minutes: 0:20 rat packs in the stream, 0:30 first (soft) peak, 0:45 runners, 0:55 elite,
1:02 rats, 1:12 thief, 1:20 HORDE, 1:32 lull, 1:35 elite, 1:45 golden wave, 2:10 ASSAUT, 2:22 elite (pushed to the lull), 2:30 rats,
2:47 thief, 2:55 elite, 3:00 HORDE, 3:20 BOSS.

### Stream (picks/s before the wave multiplier)

`rate = 1.0 + 2.0*min + 0.8*min(min, 2)`, capped at 45 (was `2 + 1.7*min + 1*min(min,2)`).
0:00 1.0 · 1:00 3.8 · 2:00 6.6 · 5:00 12.6 · 10:00 22.6 · 15:00 32.6. Softer first minute (one weapon can't keep up at
~3/s, so the danger is real but not the old 40 s spike), more bodies from minute 3 on. 30% of the stream spawns where the
player is heading (`aheadChance`), so it can't simply be outrun. `maxAlive` 320 / `hardCap` 380 unchanged.

Stream mix (builder): zombies 1.0 (-0.05/min, floor 0.45), rats 0.25 x4 from 0:20, runners 0.5 from 0:45 (+0.05/min, max 0.9),
bloaters 0.1 from 1:30 (+0.01/min), splitters 0.2 from 1:40, brutes 0.15 from 2:10 (+0.03/min, max 0.45),
spitters 0.25 from 2:30 (+0.02/min, max 0.45). Threat comes from the mix, not from HP.

### Scaling

Health `1 + 0.15m + (0.03 + endless 0.04)m²` (endless term was 0.07): with a build ~20 levels behind the old one at 10 min,
the old curve would kill at ~11 min. Endless HP: 5 min 3.5x, 10 min 9.5x, 15 min 19x, 20 min 32x (old: 4.25x, 12.5x, 25x, 44x). Damage unchanged
(`1 + 0.13m` endless, `1 + 0.07m` campaign). Campaign stage health multipliers of chapters 2-4 lowered (max 3.4 -> 2.7).

### Enemy damage (builder)

Zombie 6 -> 7, runner 5 -> 6, brute 15 -> 18, spitter shot 9 -> 10 and every 3 -> 2.6 s, boss ring every 3.2 -> 2.8 s.

### Healing (so HP actually moves)

| Source | Before | After |
|---|---|---|
| Regen (Sang froid) | always on | pauses 2 s after any hit (`PlayerHealth.regenDelayAfterHit`) |
| Life steal (Sangsue) | 3 HP per proc, unlimited | 3 HP per proc, token bucket 6 HP, refills 2 HP/s (`Combat`) |
| Meat from normal kills | 1.2% per kill (~1 per 8 s at 10 kills/s), 25% HP | 0.8%, at most 1 per 18 s, 20% HP |
| Meat from elites / crates | 35% / 40% | 30% / 30% |
| Boss chest / reward chest heal | 50% / 50% | 30% / 15% |
| Level-up nova | r5, 15 dmg | r4.5, 10 dmg (`GameManager.levelUpNova*`) |

### Combo

`Combat.ComboWindow` 2.5 -> 2 s: the 5 s of silence of each lull breaks the combo once the screen is cleared.
Kills per wave are ~350 at 2 min, ~700 at 5 min, ~1400 at 10 min, so the frenzy threshold (ArenaEvents) must be sized
on that, otherwise FRÉNÉSIE still fires every wave.

## Final tuning after bot tests (lead, 2026-11-06)

The pacing pass above, combined with the builds pass (weaker level-1 weapons, nerfed healing), first made runs far too
hard (bot dead at 1:20-3:50). Final adjustments, measured with the human-like "kite" test bot (skill 0.75, flees crowds,
dodges shots, picks gems, prioritises evolutions and owned weapons):

| Knob | File | Value |
|---|---|---|
| Base damage (everyone) | PlayerStats.damageMult | 1.0 -> 1.15 |
| XP curve base / linear | PlayerXP | 3 / 1.5 -> 2 / 1.1 (cubic 0.07 kept) |
| Stream growth / early ramp | RunDirector | 2.0 -> 1.7 per min, 0.8 -> 0.5 |
| Peak multiplier | RunDirector.peakMult | 1.5 -> 1.35 |
| Horde / assault size | RunDirector | 14+7/min -> 12+6/min, 10+4/min -> 8+3.5/min |
| Spawns ahead of the player | RunDirector.aheadChance | 0.3 -> 0.15 (they punished movement) |
| Endless damage growth | RunDirector.endlessDamagePerMinute | 0.06 -> 0.03 |
| I-frames after a hit | PlayerHealth.invulnerability | 0.25 -> 0.35 s |
| Meat drops | Loot | 0.8% / 18 s -> 1% / 12 s |
| Enemy contact damage | builder | zombie 6, runner 5, brute 15, boss 22; spitter 9 every 3 s; zombie hp 9 |
| Comfort pressure | WorldEvents | unlock 60 -> 150 s, comfortHigh 0.62 -> 0.66, hold 10 -> 14 s, cooldown 12 -> 20 s, meteor 14 -> 9, burn 4 -> 3, safe zone 4 -> 3, ambush base 10 -> 6 |

Measured (endless, kite bot): L7 at 1:00, L15 at 3:10, 1st evolution 4:30 (L21), L26 at 6:30, 3 evolutions by ~8:30 (L30),
full slots 9:26, death 9:46 (L31). HP oscillates 45-100 % from minute 5 (dips in peaks, recovers in lulls), pressure events
escalate to level 3 around 4:30 while the bot is comfortable. 60 fps (editor), 0 errors.
A human should last longer than the bot (~12-15 min). Next levers if real playtests say it's too hard late:
`endlessHealthPerMinuteSquared` (0.04) and `WorldEvents.comfortHigh`; too easy early: `PlayerStats.damageMult`.
The first evolution (~4:30) comes a bit before the 6-8 min target; acceptable for now (it is the first "wow").
