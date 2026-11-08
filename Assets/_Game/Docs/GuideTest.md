# Guide de test — soir du 6 novembre 2026 : difficulté qui monte + se dépasser

## Nouveau

1. **Paliers de DANGER** (modes classés, pas la campagne) : toutes les 2:30 le danger monte d'un cran, avec une bannière
   et une nouvelle règle. Le palier est affiché sous le chrono (« CLASSIQUE · DANGER 3 »), du jaune au rouge.
   | Palier | À | Nom | Règle |
   |---|---|---|---|
   | 2 | 2:30 | Les coureurs | plus de coureurs |
   | 3 | 5:00 | Pluie d'acide | plus de cracheurs |
   | 4 | 7:30 | Élites enragées | élites plus fréquentes, +1 affixe |
   | 5 | 10:00 | La marée | hordes et assauts +30% |
   | 6 | 12:30 | Les colosses | les brutes déferlent |
   | 7 | 15:00 | Nuit rouge | ennemis +10% de vitesse |
   | 8+ | 17:30 | Apocalypse | +12% PV et dégâts par palier |
2. **Contre ton record, en direct** : en haut à droite, sous « Record », une ligne te dit si tu es devant
   (« +3 200 vs record », vert) ou derrière (rouge) ton meilleur run au même moment. Comme un fantôme en course.
3. **Écran de fin** : « NOUVEAU RECORD ! +12 300 » ou « il t'a manqué 4 300 », ton palier de danger contre ton meilleur,
   ta partie la plus longue et tes derniers scores. Les mêmes records sont affichés dans les règles de chaque mode classé.
4. **Défi du jour plus juste** : les affixes des élites sont maintenant les mêmes pour tout le monde (comme les cartes,
   reliques et pactes).
5. **Perf mobile** : les balles, scies et flaques de feu ne testent plus que les ennemis proches (~4 000 tests par image
   au lieu de ~55 000 avec 350 ennemis).

# (Avant) Guide de test — après-midi du 6 novembre 2026 : le STUFF de 20 niveaux

## Ce qui change en jeu

1. **Ton stuff a 20 niveaux max.** Chaque niveau d'arme ou de bonus compte (ton arme de départ = 1/20). Une jauge de
   20 cases colorées sous les cartes montre où sont partis tes niveaux ; la barre d'XP affiche « Stuff 12/20 ».
   - Les **évolutions sont gratuites** (hors stuff) : 3 armes au Nv 5 + leurs 3 partenaires = 18 niveaux = 3 évolutions.
   - Prendre un peu de tout = beaucoup d'objets faibles (des synergies I/II, mais pas d'évolution). Se spécialiser paye.
   - Chaque main de cartes propose **toujours au moins une carte pour monter une de tes armes**, et une arme au Nv 5
     voit **toujours son bonus partenaire** proposé (pour pouvoir te spécialiser et évoluer).
   - Les cartes rares/épiques donnent toujours +2/+3 niveaux, mais coûtent autant dans le stuff (c'est de l'avance, pas plus de puissance).
2. **Stuff complet = « primes de niveau ».** Plus d'écran de cartes : chaque niveau fait une onde de choc qui nettoie autour
   de toi, soigne 10 % et donne des points (40 × ton niveau). La barre d'XP devient dorée. Si une évolution est prête,
   l'écran s'ouvre avec seulement l'évolution.
3. **3 armes absentes à chaque partie** (« ARSENAL DE LA PARTIE » au début, rappel dans le menu pause et à la fin).
   Jamais ton arme de départ. En défi du jour / de la semaine elles sont les mêmes pour tout le monde et affichées dans les règles.
4. **Étiquettes sur les cartes** : chaque carte porte ses familles (FEU, EXPLOSIF, LAME…) ou UTILITAIRE.
   L'évolution s'écrit « Évolution : Nv 5 + Adrénaline », chaque condition déjà remplie en vert ; un bonus partenaire dit
   « Fait évoluer : Revolver (Nv 3/5) ».
5. **Nouvelle menace (NG+)** : « Bagage léger », stuff limité à 16 niveaux, +20 % de score.

## Mesures avec le bot (Classique, il joue comme un humain moyen)

| Façon de choisir | Mort à | Score | Stuff complet | Évolutions |
|---|---|---|---|---|
| Spécialisé (3 armes + partenaires) | 13:00 – 16:56 | 216 000 – 332 000 | 3:30 – 5:12 | 2 – 3 |
| Large (4 armes) | 13:42 | 217 000 | 4:28 | 3 |
| Au hasard | 3:40 – 7:05 | 16 000 – 62 000 | – | 0 |

Le hasard ne mène jamais aux grosses améliorations, un stuff cohérent fait 5 à 20x plus de points : c'est la règle que tu voulais.
À tester toi-même : est-ce que le stuff complet arrive trop tôt ou trop tard pour toi (réglage : `PlayerXP.xpLate`),
et est-ce que les primes de niveau sont assez fun.

## Corrigé pendant la passe « cohérence »

- **Boss garé hors écran** : tant qu'un boss vit il n'y a plus de hordes ; le semer permettait de farmer le score du temps.
  Il te rattrape maintenant après 6 s hors écran. Et deux boss ne se superposent plus.
- **Défi du jour** : reliques et pactes proposés sont maintenant les mêmes pour tout le monde (comme les cartes).
- **Les événements de pression** ne tombent plus pendant un boss ou un pic de vague. Les **autels de pacte** ne se prennent
  plus par accident en courant (1,5 s pour lire). La **zone sûre** ne peut plus apparaître hors de la carte.
- **XP perdue** : quand il y a trop de gemmes au sol, l'XP va dans la gemme la plus proche (avant : une gemme au hasard sur la carte).
- **Cadavres explosifs** profitent enfin d'Artificier (zone, réplique) et des réactions, comme les grenades.
- **Chargeur étendu** accélère la Gatling du Diable (avant : aucun effet). **Rage** donne vraiment +12 %.
- Étoile « santé » de la campagne : plus gagnable après une mort + revive (pub) ou la plume de phénix.
- Pièces / viande / aimants oubliés très loin disparaissent (perf mobile). Les coffres restent.
- ~25 textes corrigés (descriptions qui ne disaient pas la vérité : Fil de cuivre, Balles incendiaires, Lame dentelée,
  Peste noire, Grenades, Famine, camp « NG+ bientôt »…) et un seul mot partout : **stuff**.

# (Avant) Guide de test — nuit du 6 novembre 2026

Tout compile, 0 erreur pendant les parties de test. Rien n'est commité dans git (tout est dans ta copie de travail).

## Lancer le jeu

1. Ouvre Unity, scène `Assets/_Game/Scenes/Game.unity`.
2. Menu **Survivor > Game View Portrait (1080x1920)** pour l'affichage téléphone.
3. **Play**. Clavier : ZQSD / WASD / flèches. Souris : clique-glisse n'importe où = joystick.

Si un jour la scène semble cassée : **Survivor > Build Prototype Scene** la reconstruit (l'équilibrage est dans le code).

## Ce qui a changé cette nuit

### 1. Modes classés (le pilier compétitif)
Sur l'accueil, sous la campagne, 4 tuiles **MODES CLASSÉS** (mêmes règles pour tous) :

| Mode | Règles | Classement |
|---|---|---|
| **Classique** | Survie infinie, règles pures | `classic` (ton record 510 est conservé) |
| **Défi du jour** | Même graine pour tout le monde : même arme de départ, mêmes 2 menaces, mêmes cartes si tu fais les mêmes choix. **1 seul essai classé par jour**, les suivants = « Entraînement » | un par jour |
| **Défi de la semaine** | Un élément par semaine (Feu, Poison, Électrique, Explosif, Lame, Balle) : arme de départ de cet élément, ses cartes sortent 2x plus, 1-2 menaces fixes | un par semaine |
| **Menaces (NG+)** | Tu actives les menaces que tu veux, chacune ajoute au multiplicateur de score (jusqu'à x2.80) | `menaces` |

Les **10 menaces** : Horde dense (+25 %), Chair épaisse (+25 %), Crocs (+20 %), Sprinteurs (+20 %), Élites enragées (+15 %),
Boss pressé (+20 %), Famine (+15 %), Sans filet (+10 %), Fragile (+20 %), Verre canon (mutateur : +30 % dégâts / -50 % PV, +10 %).
Menaces se débloque quand le camp est au max — pour tester tout de suite : tuile Menaces > DÉTAILS > **Débloquer (test)**.

**Médailles** (objectifs honnêtes au lieu de faux joueurs) : Bronze / Argent / Or / Diamant / Légende.
Classique : 10 000 / 40 000 / 100 000 / 200 000 / 400 000 (le bot a fait 270 000 en 15:44 : Légende demande ~18 min). L'écran de fin montre la médaille et « Prochain palier ».
Classement : bouton **CLASSEMENTS** avec un onglet par mode. Pour l'instant local (prêt pour un serveur en ligne : `ILeaderboardService`).

### 2. Monétisation (pubs simulées, aucun pouvoir en jeu)
Les pubs sont **factices** (écran « PUBLICITÉ (test) » de 3 s) pour tester les parcours. Un vrai SDK se branche à un seul endroit (`Ads.CreateProvider`).
- **Seconde chance** (campagne uniquement, jamais en classé) : à la mort, « Revivre (pub) » = 50 % de vie + onde de choc. Au plus 1 proposition toutes les 2 parties.
- **x2 or (pub)** sur l'écran de fin, tous modes (l'or ne donne aucune puissance).
- **Camp** : « Production x2 pendant 2 h » et « Livraison express +1 h » (2 par jour).
- **Pub forcée** : j'ai appliqué ta règle à la lettre (« annoncée avant la partie, jamais après la première partie du jour ») →
  **au maximum une par jour, annoncée avant la première partie du jour**, jamais pour un nouveau joueur (3 premières parties).
  Si tu voulais dire l'inverse (aucune pub sur la 1re partie, puis 1 toutes les 3), c'est 2 lignes dans `Scripts/Monetization/Ads.cs` (`InterstitialBlocker`).
- Achats simulés : « Sans pub » 1,99 €, Pack Néon 2,99 €, Pack Fondateur 4,99 €.

### 3. Cosmétiques (Profil)
Onglets **SKINS / TITRES / BOUTIQUE / STATS**. 12 skins (couleur + accessoire : casquette, chapeau, crête, couronne, auréole, cornes…)
et ~13 titres. Sources : or (2 000 à 25 000), succès (ex. « Le Boucher » à 10 000 kills), premium (simulé).
Le skin est visible en jeu, le titre s'affiche sous ton pseudo au classement.
Outils de test : Profil > STATS > PUBS DE TEST (activer/désactiver, simuler « aucune pub », forcer les pubs à la prochaine partie).

### 4. Gameplay : bugs corrigés (audit complet des 10 armes, 25 bonus, 7 ensembles, 11 réactions)
- Les ennemis replacés devant toi perdaient brûlure/poison et déclenchaient de faux effets de mort hors écran.
- Le combo **Plasma** (feu + arcs électriques de Fil de cuivre / Électricien II) ne se déclenchait jamais.
- **Adrénaline** n'accélérait pas les Machettes.
- Les brûlures des explosions de cadavres / barils ignoraient Huile de vidange, Pyromane et les réactions.
- Le gel (relique) était annulé par le ralentissement du gaz ; tout ralentissement compte maintenant pour Surcharge.
- Pression niveau 3 : le 2e événement était coupé trop tôt.
- Une relique ramassée quand on les a toutes ne donnait rien (maintenant de l'or).
- Textes de cartes corrigés (Chargeur étendu « sauf le gaz », évolution du Revolver).

### 5. Équilibrage (fait par l'autre session, avec un bot qui joue comme un humain)
Bot en Classique : niveau 7 à 1:00, 15 à 3:10, 1re évolution vers 4:30, toutes les cases ~9:30, vie qui oscille entre 45 et 100 %
dans les pics. Selon les parties, le bot meurt entre ~3:20 (mauvais build) et 15:44 (niveau 39, 3 évolutions, 270 000 points, 0 erreur).
Un humain devrait tenir 12-15 min. Réglages si c'est trop dur en fin de partie : `RunDirector.endlessHealthPerMinuteSquared` (0.04),
`WorldEvents.comfortHigh` (0.66) ; trop facile au début : `PlayerStats.damageMult` (1.15). Détails : `Docs/Pacing.md`.

### 6. Prise en main
Astuces de première partie (une seule fois chacune) : bouger, gemmes, choix de cartes, évolutions, bonus d'ensemble, vie basse, boss,
relancer/bannir, pause. Missions du jour : 2 nouvelles (« Joue le défi du jour », « Joue N parties classées »).

## Parcours de test conseillé (20-30 min)

1. Accueil : vérifie les 4 tuiles et le compte à rebours du défi du jour.
2. **Campagne 1-1** : astuces en bas de l'écran ; meurs exprès → « SECONDE CHANCE » → Revivre (pub) → tu reprends à 50 %.
3. **Classique** : une vraie partie, essaie de viser une évolution (arme Nv 5 + son bonus partenaire, indiqué sur la carte).
4. Fin de partie : score, médaille, « Prochain palier », bouton x2 or (pub), REJOUER relance le même mode.
5. **Défi du jour** : ta 1re partie est classée ; la 2e affiche « Entraînement » partout.
6. **Menaces** : Débloquer (test), active 3-4 menaces, regarde le multiplicateur.
7. **Classements** : un onglet par mode.
8. **Profil** : achète un skin avec ton or, équipe-le, lance une partie et regarde ton perso.
9. **Camp** : boost x2 et livraison express (pubs de test).
10. Dis-moi ce qui est trop dur / trop facile / pas clair : on ajuste.

## Pas vérifié / à savoir
- Pas testé sur iPhone cette nuit (uniquement l'éditeur, 60 fps).
- Pub forcée et livraison express : la logique compile et suit les règles, mais je ne les ai pas vues tourner sur plusieurs heures réelles.
- L'accessoire des skins vu de dessus en partie : à juger avec tes yeux.
- Tes données de test ont été remises propres : pseudo « Mwn », record 510. L'or et le camp ont été remis à zéro
  (ils venaient presque entièrement des parties du bot). Sauvegarde de ton état de 6 h 20 dans mon dossier temporaire si besoin.
- Outils dev laissés (éditeur uniquement, n'existent pas dans un build) : `ZzTestBot` (bot de test), `Tools/check_compile.sh`
  (compile les scripts hors Unity en ~15 s). « Débloquer (test) » et les outils PUBS DE TEST sont à retirer avant une sortie publique.
