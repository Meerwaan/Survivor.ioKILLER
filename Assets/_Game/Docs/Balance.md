# Balance & synergies

Reasoning sheet for weapons, evolutions, bonuses, set bonuses and the card draw. Numbers are the serialized
defaults (rebuild the scene with *Survivor > Build Prototype Scene* to apply them: weapon level tables are serialized).
Code: `Scripts/Upgrades/Synergies.cs` (set bonuses, reactions, statuses), `UpgradeManager.cs` (bonuses, cards, rarity, banish).
Pacing targets and the XP / enemy curves live in `Docs/Pacing.md`.

## Stuff: the 20-level build (owner's rule, 2026-11-06)

Owner: "on ne doit pas pouvoir améliorer à l'infini... un stuff c'est ~20 niveaux et tu dois créer ton stuff avec ces 20
améliorations, sinon ça devient trop cheaté". And: random picks must never give the big upgrades, a coherent stuff must
help you score. Code: `UpgradeManager` (`buildCap`, `BuildLevels`, `BuildRoom`, `BuildComplete`, `HasChoices`),
`GameManager.LevelBurst`, `GameHUD` (gauge, labels).

- **Budget**: `buildCap` = 20 levels. Every weapon level and every bonus level counts (the starting weapon's Nv 1 too, so a
  run starts at 1/20). **Evolutions are free** (they don't use a level): depth is rewarded. Rare / epic cards still give
  +2 / +3 levels but cost the same in budget (= tempo, not more final power), clamped to the room left.
  Slots are unchanged (5 weapons, 6 bonuses), so the real choice is **wide** (many items at low level = set bonuses I/II)
  vs **deep** (3 weapons at Nv 5 + their partners = 18 levels = 3 evolutions) or a mix.
- **Draw rules for focus**: every hand has at least one card that levels up a weapon you own (while one can still level).
  Without it the bot's weapons stayed at Nv 1-3 (owned weapons were only ~18% of the draw weight). And a weapon at Nv 5
  always gets its partner bonus in the hand (else, with ~25 bonuses in the pool, the focused bot filled its stuff at 4:47
  without ever seeing the Grenades' partner: 2 evolutions instead of 3).
- **Full stuff**: no more cards. Each level-up is a **level burst** instead (no pause): a nova of 6.5 m x area, damage
  14 x enemy health scaling x damage bonus (clears normal enemies around you), heals 10% max HP (30% max per batch) and gives
  `40 x level` score points. A pending evolution still opens the card screen (evolution + heal + gold, no reroll/banish).
  XP (Cerveau frais, Boussole, Pacte de l'avare) therefore turns into score once the stuff is full.
- **Arsenal of the run**: `absentWeapons` = 3 weapons (of 10) are left out of the pool each run (never the starting weapon,
  never a weapon of the weekly element; daily / weekly: from the seed, stream 5). Announced at the start and listed in pause.
  Partners of absent weapons stay in the pool (they still count for sets).
- **Threat "Bagage léger"** (+20% score): stuff limited to 16.
- Labels: family chips stuck on the top edge of every card (FEU, EXPLOSIF... or UTILITAIRE for bonuses without family),
  evolution recipes with the met conditions in green ("Évolution : Nv 5 + Adrénaline"), partner hints with the weapon level
  ("Fait évoluer : Revolver (Nv 3/5)"), "Partenaire d'évolution : X" for weapons not owned yet, stuff gauge (one segment per
  level, colored by item) + "TON STUFF 12/20" on the level-up screen, "Stuff 12/20" in the XP bar (gold bar once full).

Other coherence fixes in the same pass: Rage +0.12 -> +0.14 (= +12% of the 1.15 base per level, as the card says);
corpse explosions (Cadavres explosifs, Mèche courte) now go through `Synergies.Explosion` (Artificier zone + réplique,
Fusion, Dispersion), like grenades; Gatling du Diable fires 35% faster per extra projectile (Chargeur étendu did nothing on it).

## What the enemies ask for

Endless HP mult is now `1 + 0.15m + 0.07m²` (see Pacing.md): 5 min 3.5x, 10 min 9.5x, 15 min 19x. The stream is
~1 pick/s at 0:00, ~6.6 at 2:00, ~12.6 at 5:00, ~22.6 at 10:00. So: one weapon at L2-3 (+ a 2nd at L1) holds minute 2
only if the player moves well, ~3 weapons around L3-4 hold minute 5, minute 8-10 needs an evolution or a set II,
and past minute 14 only multiplicative builds (evolution + set II + reactions) survive for a while.

## Weapons (base, no bonus)

"Single" = one target dps. "Crowd" = rough effective dps in a dense horde (hits counted from radius/pierce/chains).

| Weapon | Tags | L1 single (before → after) | L3 | L5 single / crowd | Evolution (partner) | Evo crowd | Role |
|---|---|---|---|---|---|---|---|
| Revolver | Balle | 21 → 19 | 65 → 60 | 229 → 238 / ~700 | Gatling du Diable (Adrénaline) | ~1700 | long range, best single target |
| Fusil à pompe | Balle | 22 → 19 | 52 → 50 | 136 → 144 / ~580 | Canon à viande (Rage) | ~1400 | close range, knockback wall |
| Machettes | Lame | 15 → 12.5 | 37 → 35 | 51 → 53 / ~580 | Hachoir cyclone (Gros calibre) | ~1500 | defensive orbit, bleed carrier |
| Cocktail Molotov | Feu, Explosif | 17 → 14.5 | 44 → 41 | 51 / 650-1300 | Napalm (Bidon d'essence) | ~2000 | area denial, weak vs bosses |
| Gaz toxique | Poison | 17 → 14.5 | 30 → 27 | 54 → 51 / ~760 | Peste noire (Sang froid) | ~2300 + small heal | aura, slow, poison carrier |
| Lance-flammes | Feu | 31 → 28 | 37 → 34 | 68 / 820 | Souffle du dragon (Huile de vidange) | ~1750 | short cone, always ignites |
| Batterie Tesla | Électrique | 12 → 10 | 23 → 20 | 42 / 425 | Tempête électrique (Chargeur étendu) | ~1900 | horde clear, chains |
| Grenades | Explosif | 14 → 11.6 | 42 → 40 | 122 / 850 | Bombe à sous-munitions (Cadavres explosifs) | ~2200 | bursts on crowds |
| Scie circulaire | Lame | 21 → 18 | 65 → 60 | 164 / 820 | Scie sanglante (Œil de lynx) | ~2900 | lines, crits |
| **Fléchettes (new)** | Poison, Balle | 7.5 + poison 4/s | ~20 + poison 7/s | 72 + poison 9/s / ~500 | Nuée d'aiguilles (Fiole d'acide) | ~1800 | spreads poison on screen, weak vs bosses |

Changes in this pass (why): the owner wants "pas direct invincible et ultra boosté dès le début, de la progression".
- **L1 -10 to -17%, L5 unchanged or slightly up**: the first minute is a real fight with one weapon, and each level is
  a visible step (level texts say +40-65% on damage levels instead of +30-45%). Ratio L5/L1 single goes from ~3-11x to ~4-12x.
- Evolutions unchanged: they stay the 2-3x "wow" over L5. They now arrive later because the XP curve is slower (Pacing).
- **Fléchettes** (new, `DartWeapon`): darts at random enemies on screen, pierce at L4-5, every hit poisons (dps does not
  stack, it refreshes). Poison was carried by Gaz only; this gives Alchimiste a ranged carrier and bridges it to
  Pistolero (darts are bullets: ricochets, Poudre noire, Pistolero II special bullets). Evolution with **Fiole d'acide**
  (a partner nobody used): a ring of 10 needles (+2 per extra projectile) that burst on their first hit, so Dispersion
  spreads the poison through the whole horde. Uses the bullet prefab (`PrototypeBuilder.Weapons`).

Watch list: Molotov L5 in a tight horde, Saw evo + crit, Tesla with Chargeur étendu 2/2 (+2 bolts = 3x targets, the
strongest single bonus-on-weapon interaction), Nuée d'aiguilles + Alchimiste II (cloud per poisoned death, limited 4/s).
If one is clearly dominant, lower its evo damage first, not its early levels.

## Bonuses (25, 6 slots)

| Bonus | Tags | Per level (before → after) | Max | Notes |
|---|---|---|---|---|
| Rage | Lame | +15% → **+12%** damage (+0.14 on the 1.15 base) | 5 | Shotgun evo. 5/5 = +61% |
| Adrénaline | Balle | x0.91 → **x0.92** cooldown | 5 | Revolver evo. 5/5 = +52% fire rate |
| Gros calibre | Explosif | +12% area | 5 | Machete evo |
| Sang froid | Survie, Poison | +0.7 HP/s → **+0.4 HP/s, +0.5 more after 4 s without a hit** | 5 | Gas evo. Base regen also pauses 2 s after a hit (PlayerHealth). 5/5: 0 in a brawl, 2 HP/s after 2 s, 4.5 HP/s after 4 s |
| Bidon d'essence | Feu, Poison | +15% duration | 5 | Molotov evo |
| Chargeur étendu | Balle | +1 projectile | 2 | Tesla evo |
| Cadavres explosifs | Explosif | +10% corpse explosion | 5 | Grenade evo |
| Œil de lynx | Lame, Balle | +8% crit, +15% crit dmg | 5 | Saw evo |
| Huile de vidange | Feu | +25% burn damage | 5 | Flamethrower evo |
| Fiole d'acide | Poison | +10% poison chance | 5 | **now Fléchettes evo** |
| Ricochet | Balle, Électrique | +1 bounce / +1 chain | 3 | |
| Balles incendiaires | Feu | +12% ignite chance | 4 | |
| Sangsue | Survie | +6% → **+5%** chance to heal 3 HP on kill | 5 | Combat caps it: 6 HP bucket refilling 2 HP/s (PACING) |
| Poudre noire | Explosif, Balle | +8% bullet explodes | 5 | |
| Fil de cuivre | Électrique | +8% shock 2 neighbours | 5 | |
| Lame dentelée | Lame | +40% bleed | 5 | |
| **Acide de batterie (new)** | Électrique, Poison | lightning poisons: 15% of the hit per s, 3 s | 5 | arcs included, so Fil de cuivre / Électricien II spread poison, and the next bolt gets Surcharge |
| **Lame chauffée (new)** | Lame, Feu | +12% chance for blades to ignite | 4 | bridge Boucher / Pyromane, sets up Fer rouge |
| Viande crue | Survie | +25 HP max **and full heal → heals 25** | 5 | a full heal on demand was an emergency button |
| Gilet pare-balles | Survie | -7% → **-5%** damage taken | 5 | 5/5 = -25% (was -35%) |
| **Dos au mur (new)** | Survie | up to +12% damage, scaled by missing HP | 5 | 5/5: +30% at 50% HP, +48% at 20%. Offensive Increvable path: rewards HP dips instead of preventing them |
| Bottes, Aimant | - / Électrique | unchanged | | |
| Cerveau frais | - | +12% → **+10%** XP | 5 | |
| **Patte de lapin (new)** | - | luck +1: rare +4%, epic +1.5%, 4th card +6%, **+1 reroll** on pick | 3 | the "card quality" pick |

## Set bonuses (2 items of a tag = tier I, 4 = tier II)

| Set | Tag | Items (weapons + bonuses) | I | II |
|---|---|---|---|---|
| Pyromane | Feu | Molotov, Lance-flammes, Balles incendiaires, Bidon, Huile, **Lame chauffée** | burn +40% | burning deaths ignite 3 neighbours |
| Artificier | Explosif | Grenades, Molotov, Gros calibre, Cadavres, Poudre noire | explosions +25% radius | lethal explosion → aftershock (60%) |
| Électricien | Électrique | Tesla, Ricochet, Fil de cuivre, Aimant, **Acide de batterie** | +2 chains, jumps +30% | every crit releases an arc |
| Boucher | Lame | Machettes, Scie, Rage, Œil de lynx, Lame dentelée, **Lame chauffée** | blades bleed (50%) | bleeding enemies take +30% |
| Pistolero | Balle | Revolver, Fusil, **Fléchettes**, Adrénaline, Chargeur, Œil, Ricochet, Poudre noire | +1 ricochet | every 6th bullet: exploding crit |
| Alchimiste | Poison | Gaz, **Fléchettes**, Fiole, Bidon, Sang froid, **Acide de batterie** | poison 2x longer, slows 25% | poisoned deaths release a toxic cloud |
| Increvable | Survie | Sang froid, Sangsue, Viande crue, Gilet, **Dos au mur** | riposte shockwave when hit (cd 1.2 s) | riposte x2, heals **0.5 HP per enemy hit, max 4** (was 1 / max 10 ≈ 8 HP/s) |

## Elemental reactions (always on)

| Reaction | Trigger | Effect | Cap |
|---|---|---|---|
| Explosion toxique | burning + poisoned | green burst r2, 8 + 3x the stronger DoT | 6/s, 1.5 s per enemy |
| Fusion | an explosion kills a burning enemy | fire explosion r x0.75, 60% | 5/s |
| Propagation | crit on a burning enemy | ignites 2 neighbours | 14/s |
| Surcharge | lightning on a slowed or poisoned enemy | +30% damage, jumps +50% | - |
| Balles traçantes | ricochet with Balles incendiaires | always ignites | - |
| Corrosion | poisoned enemy | +10% damage taken | - |
| Hémorragie | Lame dentelée + crit from any weapon | bleed at half power (now listed in the Codex) | - |
| **Électrocution (new)** | lightning on a bleeding enemy | the remaining bleed is dealt at once (x1.25) | consumes the bleed |
| **Plasma (new)** | lightning (bolts, arcs, sky strikes) on a burning enemy | plasma burst r1.6, 50% of the hit, NoProc | 5/s |
| **Dispersion (new)** | a primary explosion that hits a poisoned enemy (and ≥2 enemies) | poisons the whole blast at 70% of the strongest poison | 4/s |
| **Fer rouge (new)** | blade hit on a burning enemy | +25% damage | - |

New combos this opens: Machettes/Scie + Tesla (bleed → Électrocution), Lance-flammes + Tesla or Fil de cuivre (Plasma),
Grenades/Molotov + Gaz or Fléchettes (Dispersion), Machettes + Lame chauffée or Lance-flammes (Fer rouge),
Tesla + Acide de batterie (self-Surcharge: the 2nd bolt onward hits +30% and jumps further).
All constants are at the top of `Synergies.cs` (`FerRougeMult`, `ElectrocutionMult`, `PlasmaDamage`, `DispersionShare`...).

## Archetypes (key pieces)

- **Pyromane**: Lance-flammes + Molotov + Huile + Balles incendiaires (+ Bidon). Weak vs bosses: add Revolver.
- **Forgeron** (Lame + Feu): Machettes + Lance-flammes + Lame chauffée + Lame dentelée. Fer rouge + Propagation on crits.
- **Artificier**: Grenades + Molotov + Gros calibre + Cadavres / Poudre noire. Add Gaz or Fléchettes for Dispersion.
- **Alchimiste**: Gaz + Fléchettes + Fiole + Bidon. Clouds everywhere; Molotov for Explosion toxique.
- **Électricien**: Tesla + Ricochet + Fil de cuivre + Acide de batterie. Add Lance-flammes (Plasma) or Machettes (Électrocution).
- **Boucher**: Machettes + Scie + Lame dentelée + Œil de lynx + Rage.
- **Pistolero**: Revolver + Fusil + Ricochet + Poudre noire + Chargeur; Fléchettes make it Pistolero/Alchimiste.
- **Increvable**: Sang froid + Sangsue + Gilet + Viande crue = the safe build; with **Dos au mur** it becomes a
  berserker that wants to sit at 30-50% HP and riposte.

## Card draw

- Weights: owned weapon 3, new weapon 2.8 (was 2.5, offsets the 4 new bonuses), bonus 1, evolution partner 2.
  Weapons stay ~half of the draws. A new item whose tag the player already owns gets x1 to x1.8 (`Synergies.CardWeight`).
- **Late new items**: once 3 weapons are owned, new weapons x0.8; once 4 bonuses are owned, new bonuses x0.8
  (PACING request: slots should fill around ~10 min, owned items level up instead).
- **Forced new weapon** only while the player has fewer than 2 weapons (was 3): the 3rd weapon comes from the normal draw.
- **Rarity** (`UpgradeOption.rarity`, `levels`): rolled after the item is drawn, so it never changes which items come up
  (and `inPreset` never changes weights either). Commun = +1 level, **Rare = +2 levels, Épique = +3** (clamped to the levels left;
  a new item arrives at Nv 2 / Nv 3). Chance per card: rare 0 until level 6, +0.6%/level, max 8%; epic 0 until level 15,
  +0.3%/level, max 3%. Luck (Patte de lapin) adds +4% / +1.5% per level. Cards show `RARE · Nv 2→4` and a blue / orange frame.
- **4th card**: guaranteed once at levels 10, 20, 30... and 5% otherwise (+6% per luck level). Rerolls keep the hand size.
  For a fixed-size draw (chest / event screens), call `UpgradeManager.RollExact(n)`.
- **Bannir** (`TryBanish`): 3 per run (`PlayerStats.banishes`). Tap BANNIR then a card: the item leaves the pool for the run
  and only that card is replaced. An owned item keeps its levels (so a banished weapon never evolves). Evolutions and rewards
  can't be banished. `UpgradeManager.Banished` lists the ids.
- Rerolls: 1 per run (`PlayerStats.rerolls`), +1 per Patte de lapin level.

(Outdated since the 20-level stuff: see the Stuff section above and Pacing.md "Stuff pass".) Expected build timeline with the new XP curve (Pacing: ~25 picks at 5:00, ~36 at 8:00, ~44 at 10:00, incl. chests):
2 weapons by ~1:00-1:30, 3 weapons + first tier I by ~3:00, first rare cards from ~2:30, 4-5 weapons and the first
evolution ~6-8 min (5 targeted picks among ~20, plus rare jumps), full slots ~9-11 min, 2-3 evolutions by ~12 min.
If evolutions come before 6 min in tests: lower `OwnedWeaponWeight` (3 → 2.5) or `RareMax` first.

## Healing budget (target: HP dips to 40-70% in peaks, recovers in lulls)

| Source | Before | After |
|---|---|---|
| Sang froid 5/5 | 3.5 HP/s always | 0 while being hit, 2 HP/s after 2 s, 4.5 HP/s after 4 s |
| Sangsue 5/5 | 30% x 3 HP per kill, uncapped (often >10 HP/s) | 25% x 3 HP, ≤ 2 HP/s sustained (Combat bucket) |
| Peste noire | up to 3 HP per 0.3 s tick (~10 HP/s) | 0.12 per enemy, max 1 HP per tick (~3.3 HP/s), `evoHealCap` |
| Increvable II | up to 10 HP per riposte (~8 HP/s when swarmed) | 0.5 per enemy, max 4 (~3.3 HP/s) |
| Viande crue | +25 max + full heal, x5 | +25 max, heals 25 |
| Gilet 5/5 | -35% damage | -25% damage |

## Safety (mobile, no infinite combos)

- Secondary hits (arcs, fusion, plasma, aftershock, toxic bursts, riposte) use `HitFlags.NoProc`: they never roll more procs.
  Dispersion only applies poison (no hit). Only primary explosions create secondary ones; all chain effects go through
  token-bucket limiters and a 48-entry queue processed next frame (never inside an enemy loop).
- Status tracking: one dictionary entry per pooled enemy, at most 512 tracked, DoTs tick every 0.5 s. No per-frame allocation
  (the card draw allocates a few small lists per level-up only).

## Ranked modes, threats and medals

Code: `Scripts/Modes/` (`GameModes.cs` setup per mode, `Threats.cs`, `Medals.cs`, `ModeHUD.cs`, `UI/MenuScreen.Ranked.cs`).
Everyone plays with the same rules inside a mode; nothing is permanent. Score sent to a board = round(base score x multiplier),
base score = kill points + 2 per second + level bursts (40 x level for each level gained once the stuff is full, `GameManager.LevelBurst`).

| Mode | Board | Rules |
|---|---|---|
| Classique | `classic` (keeps the old `best_score` / `leaderboard_local_v1` keys) | pure endless, start weapon from the Codex |
| Défi du jour | `daily_yyyyMMdd` (UTC) | seed from the date: start weapon + 2 threats + card draws. 1st attempt of the day ranked, then practice (nothing saved) |
| Défi de la semaine | `weekly_yyyyWW` (ISO week, UTC) | element rotates every week (Feu, Poison, Électrique, Explosif, Lame, Balle); start weapon of that element, cards of that element x2 weight, 1-2 threats, seeded cards; every attempt counts |
| Menaces (NG+) | `menaces` | unlocked when the camp is maxed (or "Débloquer (test)", PlayerPrefs `dev_unlock_all`); the player toggles threats |

Seeded draws (`UpgradeManager.SeedDraw`): one `System.Random` per draw from (seed, stream, counter): level-up hand N,
reroll N of that hand, banish N, fixed-size draw N. Same picks = same cards. Enemies / loot stay `UnityEngine.Random`.

Threats (multiplier = 1 + sum, max x3.00 with the 11 threats):

| Threat | Effect | Bonus | Hook |
|---|---|---|---|
| Horde dense | spawns x1.3 (stream, hordes, assaults, rats) | +25% | `RunConfig.SpawnRateMult` |
| Chair épaisse | enemy HP x1.35 | +25% | `RunConfig.EnemyHealthMult` |
| Crocs | enemy damage x1.3 | +20% | `RunConfig.EnemyDamageMult` |
| Sprinteurs | enemy speed x1.15 | +20% | `RunModifiers.EnemySpeedMult` base |
| Élites enragées | elite timers x0.7 (first x0.8), elite HP x1.3, +1 affix | +15% | `RunModifiers.EliteHealthMult / ExtraEliteAffixes`, `RunDirector.eliteInterval` |
| Boss pressé | boss at 150 s then every 150 s (was 200) | +20% | `RunDirector.firstBossTime / bossInterval` x0.75 |
| Famine | no meat (normal kills, elites, crates); chests, heal cards and level bursts still heal | +15% | `Loot` chances = 0 for the run |
| Sans filet | no reroll, no banish | +10% | `UpgradeManager` |
| Fragile | max HP x0.7 | +20% | `PlayerStats.maxHealth` |
| Verre canon (mutator) | damage x1.3, max HP x0.5 | +10% | `PlayerStats` |
| Bagage léger | stuff limited to 16 levels instead of 20 (appended last: indexes never move) | +20% | `Threats.BuildCapCut` -> `UpgradeManager.BuildCap` |

Daily / weekly never pick the mutator. All hooks act on this run's instances only (tuned defaults untouched).

Medals (score thresholds; model: kills per 50 s wave ~350 at 2 min, ~700 at 5 min, ~1400 at 10 min, ~11 points per kill,
so ~10 k at 3 min, ~27 k at 5 min, ~65 k at 8 min, ~95 k at 10 min, ~250 k at 16 min):

| Board | Bronze | Argent | Or | Diamant | Légende |
|---|---|---|---|---|---|
| Classique | 10 000 | 40 000 | 100 000 | 200 000 | 400 000 |
| Jour / Semaine | 12 000 | 45 000 | 120 000 | 240 000 | 480 000 |
| Menaces | 15 000 | 60 000 | 150 000 | 300 000 | 600 000 |

(Values from `Medals.cs`, the source of truth.)

Target: a decent player (dies 8-12 min) gets Argent / Or in Classique. Retune in `Medals.cs` after real playtests.
