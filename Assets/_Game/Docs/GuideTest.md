# Guide de test — nuit du 6 novembre 2026

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
