# CORDON — Bestiaire et catalogue des menaces

Version 1 — 6 novembre 2026. Complément de `Univers.md` (le monde, les factions, la DA). Ici : ce que chaque ennemi **fait** au joueur, comment il le **signale**, comment on s'en **sort**, **quand** il arrive, et pourquoi il existe.

Les comportements déjà codés : `Melee`, `Ranged`, `Boss`, `Kamikaze`, `Thief`, `Prop`. Les affixes d'élite déjà codés : RAPIDE, BLINDÉ, EXPLOSIF, VAMPIRE, INVOCATEUR. Les lignes marquées **(existe)** sont des assets actuels à renommer. Tout le reste est listé par priorité en section 5. Les chiffres (dégâts, délais) sont des ordres de grandeur ; l'équilibrage reste dans `Balance.md` et `Pacing.md`.

Sommaire
0. Les neuf règles qui font qu'un ennemi est « pro »
1. Les six rôles
2. Lire de dessus : la bosse et les silhouettes
3. Le catalogue (Ambrés, animaux, Charognards, Fervents, REGEN, élites, Souches, props)
4. La variété : grille d'apparition, composition des vagues, variantes, plafonds
5. Ce qu'il faut coder, par priorité

---

## 0. Les neuf règles

1. **Une attaque, un signal, une parade.** Toute attaque qui n'est pas un simple contact a un télégraphe visible d'au moins 0,5 s (gonflement, cercle au sol, ligne au sol, lueur, posture) et un son dédié. Le joueur meurt de ses erreurs, jamais d'une surprise.
2. **Un rôle par ennemi.** Six rôles (section 1). Un ennemi n'en cumule pas deux. Une élite ajoute un accent, pas un rôle.
3. **Une silhouette par ennemi, lisible de dessus.** Chez les Ambrés c'est la **bosse** (la croissance dorsale). Chez les humains c'est l'**objet porté**.
4. **Une explication par ennemi.** Dose, temps, hôte. Rien d'arbitraire.
5. **Une nouveauté à la fois.** En survie infinie, un nouveau type environ toutes les 90 s pendant les 10 premières minutes. En campagne, chaque chapitre introduit deux types et un hazard.
6. **Même ennemi, autre peau.** Trois textures par type et des variantes par carte : la variété perçue double sans une ligne de code.
7. **Pas de dégâts hors écran.** Les tireurs ne tirent qu'à l'écran (déjà codé). Un saut atterrit là où tu étais au départ du saut. Une attrape se casse.
8. **Des plafonds.** Contrôle : 2 à l'écran. Attrapes : 1. Tireurs : 6. Au-delà, le jeu devient illisible, pas difficile.
9. **Imprévisible avant, lisible pendant.** Le joueur ne doit jamais pouvoir prédire **quand**, **quoi** ni **d'où** arrive une Crue. Mais une fois qu'elle arrive, il voit d'où elle vient 1,5 s avant le premier ennemi. L'imprévu fait l'adrénaline ; l'injustice fait désinstaller.

---

## 1. Les six rôles

| Rôle | Ce qu'il fait au joueur | Qui | Part d'une vague |
|---|---|---|---|
| **Pression** | occupe l'espace, force à bouger, nourrit le combo | Ambré, Fiévreux, Rejeton, Nuée, Meute, Fervent, Cogneur | 60 à 75 % |
| **Mur** | bloque et encaisse, force à contourner | Masse, Bouclier | 5 à 10 % |
| **Perçage** | te touche là où tu fuis | Cracheur, Bondisseur, Sanglier, Nuée noire, Fusilier, Opérateur, Drone | 10 à 20 % |
| **Déni** | rend une zone dangereuse | Kyste, Martyr, Lanceur, Rampant, Résurgence | environ 5 % |
| **Contrôle** | change la vague : appelle, accélère, attrape, régénère | Hurleur, Berger, Accrocheur, Greffé, élites INVOCATEUR et VAMPIRE | 1 ou 2 unités, jamais plus |
| **Opportunité** | récompense l'agressivité | Pilleur, Porteur, Cristallisé, Caisse, Baril, Prime | sur minuterie |

Une vague qui ne contient que de la Pression est ennuyeuse. Une vague avec trois unités de Contrôle est injuste. Les parts ci-dessus sont celles du palier 3 ; elles bougent avec la montée (4.3 b). Le flux permanent reste de la Pression avec un Cracheur de temps en temps ; les Crues, elles, sont tirées au sort (4.3).

---

## 2. Lire de dessus

La caméra regarde le dos des ennemis. Tout ce qui identifie un type doit donc être **sur le dos ou dans la silhouette**, jamais sur le visage.

**La bosse.** La Sève surconstruit là où le corps de l'hôte stockait de la tension : nuque, dos, épaules. Chaque stade et chaque type a une bosse d'une forme reconnaissable, en ambre sombre, avec une légère émission (visible de nuit, jamais clignotante).

| Type | Vu de dessus | Bosse ou signe |
|---|---|---|
| Ambré | ovale voûté | un **dôme** rond sur le haut du dos |
| Fiévreux | flèche penchée | une **crête** fine le long de la colonne |
| Masse | carré | deux **épaulières** massives |
| Cracheur | goutte | un **goitre** : le cou est plus large que la tête |
| Kyste | cercle | tout le torse est un **ballon** translucide |
| Colonie | trèfle | deux ou trois dômes |
| Rejeton | triangle | pas de bosse : trois membres, pas de tête |
| Bondisseur | trait long | la bosse est sur les **cuisses**, le corps est allongé |
| Hurleur | ovale ouvert | la bosse est une **cage** thoracique ouverte qui s'éclaire |
| Accrocheur | croix | les **bras** sont plus longs que le corps |
| Rampant | trait plat | pas de jambes, une traînée d'ambre |
| Cristallisé | ovale clair | tout le corps est **translucide** |
| Humains | silhouette droite | l'objet : sac, batte, fusil, bouteille, cloche, bouclier, diodes |
| Animaux | silhouette naturelle | plaques dorsales |

---

## 3. Le catalogue

Colonnes : **Attaque** = ce que tu subis (faible / moyen / fort, par rapport à la griffe d'un Ambré). **Télégraphe** = ce que tu vois avant. **Parade** = ce que le joueur apprend à faire. **Apparition** = minute en survie infinie / premier chapitre de campagne.

### 3.1 Les Ambrés (infectés humains)

| Nom | Asset | Rôle | Attaque | Télégraphe | Parade | Apparition |
|---|---|---|---|---|---|---|
| **Ambré** | Zombie **(existe)** | Pression | contact : griffes de croûte, faible, toutes les 0,8 s | aucun, c'est la base ; il marche droit sur toi | reculer, frapper en cône | 0:00 / ch. 1 |
| **Fiévreux** | Runner **(existe)** | Pression | contact : morsure, faible, rapide | il court en ligne droite, on l'entend souffler | ne pas se laisser flanquer, cônes et portée | 0:30 / ch. 1 |
| **Masse** | Brute **(existe)** | Mur | contact : coup lourd, fort ; résiste au recul | lente, bruit de pas, arrive après les autres | ne jamais être coincé contre elle ; la brûler, la croûte fond | 2:30 / ch. 1 |
| **Cracheur** | Spitter **(existe)** | Perçage | à distance : crachat de sève, moyen, qui durcit à l'impact et laisse une **plaque de poisse** 3 s (−30 % de vitesse dedans) | le sac du cou gonfle et s'éclaire 0,5 s, puis un hoquet | pas de côté au gonflement, le tuer en priorité | 1:30 / ch. 2 |
| **Kyste** | Gonflé **(existe)** | Déni | explosion au contact, fort, flaque d'ambre ; blesse aussi les Ambrés | gonfle, sifflement, 0,7 s | le tuer à distance pour déclencher la chaîne | 2:00 / ch. 1 |
| **Colonie** | Scindeur **(existe)** | Pression | contact : trois coups rapprochés, moyen ; à sa mort, trois Rejetons | ses torses s'agitent | la tuer loin de soi, finir les Rejetons avant qu'ils se dispersent | 3:30 / ch. 2 |
| **Rejeton** | Rejeton **(existe)** | Pression | contact : morsure, faible, très rapide, peu de PV | aucun | zone d'effet, marcher dans son propre feu | avec la Colonie |
| **Bondisseur** | nouveau | Perçage | saut de 6 m, moyen, dans un petit rayon à l'atterrissage | s'accroupit 0,4 s, puis un **cercle d'atterrissage** 0,6 s là où tu étais | sortir du cercle ; il est vulnérable 1 s en se relevant | 5:00 / ch. 3 |
| **Hurleur** | nouveau | Contrôle | ne frappe pas : son cri fait **converger** les Ambrés dans 12 m et leur donne +25 % de vitesse 5 s | la cage thoracique s'éclaire 1 s, puis une onde visible | cible prioritaire, le tuer pendant la lueur | 6:00 / ch. 3 |
| **Accrocheur** | nouveau | Contrôle | s'accroche à toi : −50 % de vitesse et petits dégâts par seconde, 2 s max ; lâche quand il a perdu 15 % de ses PV | bras levés, puis un bond court | le découper (lames, scie) ; ne jamais rester à portée de ses bras | 7:00 / ch. 4 |
| **Rampant** | nouveau | Déni | contact, moyen ; très lent, énormément de PV ; presque invisible dans l'herbe, l'eau, les gravats | une ondulation du sol, une traînée d'ambre | contourner, ou brûler | ch. 7 (carrière, forêt, marais) |
| **Cristallisé** | Zombie doré **(existe)** | Opportunité | contact, faible, lent ; lâche beaucoup d'éclats et d'or | il brille doucement | le chasser | vague « Veine d'ambre » / ch. 2 |

Pourquoi ils existent :
- **Ambré** : stade II. La croûte aux mains fait des griffes. Il ne voit plus (yeux scellés) mais il te **sent** (la sève concentrée de ton injecteur) et il t'**entend**. Il a faim en permanence. Sa bouche n'est jamais scellée : il mange.
- **Fiévreux** : stade I, le métabolisme emballé. C'est le voisin d'hier, encore habillé.
- **Masse** : stade III sur un hôte costaud (ouvrier, soldat du Jour 6). La Sève a surconstruit les bras et plaqué le torse.
- **Cracheur** : stade III, la Sève a envahi la gorge. Le sac est sous pression, comme chez un cobra cracheur ; le crachat durcit à l'air comme la croûte.
- **Kyste** : stade III sur un hôte qui a bu beaucoup d'eau contaminée. Le corps est devenu un réservoir sous pression ; la chaleur ou un choc le fait céder.
- **Colonie** : stade III par bourgeonnement. L'hôte a « poussé » des torses partiels, comme une bouture. Les Rejetons sont des bourgeons détachés : trois membres, pas de tête.
- **Bondisseur** : stade III sur un hôte jeune et sportif. Cuisses et tendons surconstruits.
- **Hurleur** : stade III, poumons et cage surconstruits. Le cri est un jet d'air chargé de phéromone de sève. Il ne commande pas, il **excite** : la Sève n'a pas de volonté, elle a des stimuli.
- **Accrocheur** : stade III, bras surconstruits, crochets de croûte au bout des doigts.
- **Rampant** : stade II sur un hôte aux jambes brisées pendant la Crue. La Sève a soudé ce qui restait.
- **Cristallisé** : cas rare, tout a cristallisé. Un bloc d'ambre vivant. Les Guichets paient très cher.

Le mot des Charognards (palier 1 000 du Carnet, voir `Univers.md` 7.2) :
- Ambré : « Vise la bosse. Le reste, c'est de la croûte. »
- Fiévreux : « Ils courent droit. Un pas de côté, et laisse-les traverser ton feu. »
- Masse : « Tu la contournes ou pas, elle s'en fout. Brûle-la. »
- Cracheur : « Quand la gorge brille, tu bouges. »
- Kyste : « Un Kyste dans une horde, c'est un cadeau. Tire dedans. »
- Colonie : « Tue-la loin de toi. Sinon t'as trois bestioles dans les jambes. »
- Rejeton : « Ça mord les chevilles. Marche dans le feu. »
- Bondisseur : « Il saute où t'étais. Pas où t'es. »
- Hurleur : « Tu l'entends avant de le voir. Tue-le avant qu'il finisse. »
- Accrocheur : « Garde une lame pour lui. Toujours. »
- Rampant : « Si l'herbe bouge et qu'il n'y a pas de vent, c'est pas le vent. »
- Cristallisé : « Ça, c'est ta paye. »

Variantes de peau gratuites (même mesh) : Ambré et Fiévreux existent en **civil**, **ouvrier** (casque, veste bleue), **soignant** (blouse), **soldat du Jour 6** (treillis, plus de PV, 10 % des spawns après la minute 4). Voir 4.4 pour les variantes par carte.

### 3.2 Les animaux

| Nom | Asset | Rôle | Attaque | Télégraphe | Parade | Apparition |
|---|---|---|---|---|---|---|
| **Nuée** | Rat **(existe)** | Pression | contact : morsures, très faible, en meute de 8 à 12 | bannière, couinements | zone d'effet | 1:00 / ch. 1 |
| **Meute** | nouveau | Pression | contact : morsure, faible à moyen ; 3 à 5 chiens qui arrivent **en arc** pour te prendre de flanc | aboiements, on les voit se déployer | tourner avec eux, cône | 4:30 / ch. 1 |
| **Nuée noire** | nouveau | Perçage | piqué depuis le haut, faible, puis elle s'envole ; harcèle | une **ombre** au sol 0,5 s | ignorer, ou abattre au passage | ch. 7 (carrière, forêt, marais) |
| **Sanglier** | nouveau | Perçage | charge en ligne, fort, avec recul ; tourne lentement après | gratte le sol 0,6 s, une **ligne** au sol | pas de côté, le frapper pendant qu'il tourne | ch. 8 (forêt, lisière) |
| **L'Ours de Hlad** | boss | voir 3.7 | | | | ch. 8 |

Pourquoi : les rats du métro ont été les premiers contaminés par l'eau. Les chiens sont ceux qu'on a abandonnés le Jour 6 (comme les chiens de Tchernobyl) ; leurs **crocs** sont d'ambre, la mâchoire reste libre. Les corneilles ont mangé des Ambrés. Le sanglier a bu à une résurgence et sa croûte en fait un bélier. Aucun animal ne vole grâce à la Sève : les corneilles volent parce que ce sont des corneilles.

Le mot des Charognards : Nuée « Si tu prends le temps de les compter, t'es mort. » Meute « Ils tournent. Tourne plus vite. » Nuée noire « Elles visent les yeux. Baisse la tête et continue. » Sanglier « Il va tout droit. Toi, pas. »

### 3.3 Les Charognards

| Nom | Asset | Rôle | Attaque | Télégraphe | Parade | Apparition |
|---|---|---|---|---|---|---|
| **Pilleur** | Thief **(existe)** | Opportunité | ne frappe pas : ramasse tes éclats et ton or au sol, et file | bannière, sac qui brille | lui courir après, il lâche tout quand il est touché | 4:00 / ch. 1 |
| **Porteur** | variante du Pilleur | Opportunité | ne frappe pas : porte une caisse, fuit | bannière « Un porteur ! » | le rattraper | événement |
| **Cogneur** | nouveau | Pression | contact : coup de batte, moyen | lève la batte 0,3 s, il crie | comme un Ambré, mais il tient mieux | 7:30 / ch. 2 |
| **Fusilier** | nouveau (réutilise `Ranged`) | Perçage | tir, moyen, lent et précis | s'arrête, épaule 0,5 s, un reflet sur le canon | bouger pendant l'épaulement, le tuer en premier | 7:30 / ch. 2 |
| **Lanceur** | nouveau | Déni | cocktail en cloche : **flaque de feu** 4 s, moyen par tic ; brûle aussi les Ambrés | l'arc du lancer s'affiche 0,6 s | sortir de l'arc, retourner son feu contre la horde | 8:30 / ch. 3 |
| **Le Ferrailleur** | boss | voir 3.7 | | | | ch. 5 |

Pourquoi : des gens, pas des monstres. Ils t'attaquent parce que tu es un concurrent et que REGEN a mis une prime sur les Passeurs. Ils tiennent mieux qu'un Ambré (ils esquivent), mais ils sont moins nombreux. Ils ne sont jamais démembrés et tombent sans agonie.

Le mot des Charognards sur eux-mêmes : Pilleur « Un Pilleur, c'est de l'or qui court. » Cogneur « Il a une famille à la casse. Toi aussi t'as une famille. » Fusilier « Il tire une fois. Fais en sorte qu'il n'y ait pas de deuxième. » Lanceur « Son feu brûle tout le monde. Pense-y. »

### 3.4 Les Fervents

| Nom | Asset | Rôle | Attaque | Télégraphe | Parade | Apparition |
|---|---|---|---|---|---|---|
| **Fervent** | nouveau (réutilise le Fiévreux) | Pression | contact : couteau, faible, rapide | il chante en courant | cône | 9:00 / ch. 9 |
| **Berger** | nouveau | Contrôle | ne frappe pas : à chaque sonnerie, **3 Ambrés émergent** près de lui ; les Ambrés dans 10 m l'ignorent et te visent | cloche levée 0,8 s | cible prioritaire | 10:00 / ch. 9 |
| **Martyr** | nouveau (réutilise `Kamikaze`) | Déni | explosion, fort, plus large qu'un Kyste | court vers toi en gonflant, un chant aigu 0,9 s | à distance, et loin des autres | ch. 9 |
| **La Mère des Sources** | boss | voir 3.7 | | | | ch. 9 |

Pourquoi : l'Onction (sève diluée sur la peau) fait que les Ambrés les prennent pour des leurs. La cloche est un son que les Ambrés suivent (ils entendent). Le Martyr a bu la sève pure : il est un Kyste volontaire. C'est la faction qui justifie les **vagues mixtes** humains + infectés.

Le mot des Charognards : Fervent « Ils chantent. C'est pour pas entendre ce qu'ils font. » Berger « Tue la cloche, le troupeau se perd. » Martyr « Un gars qui court vers toi en souriant, c'est jamais bon. »

### 3.5 REGEN

| Nom | Asset | Rôle | Attaque | Télégraphe | Parade | Apparition |
|---|---|---|---|---|---|---|
| **Opérateur** | nouveau | Perçage | rafale de 3, moyen ; se replie pour garder la distance | s'arrête, la visière s'allume 0,5 s | lui foncer dessus : il recule lentement, il est fragile de près | 11:00 / ch. 4 |
| **Bouclier** | nouveau | Mur | contact : coup de bouclier, moyen ; **bloque les projectiles de face** | avance lentement, bouclier levé | contourner, zones, feu, lames dans le dos | 12:00 / ch. 4 |
| **Drone** | nouveau | Perçage | fléchette, faible, toutes les 2 s ; **vole** : ignore les flaques et les scies | un projecteur cyan sur toi | l'abattre, il a peu de PV | 11:00 / ch. 4 |
| **Greffé** | nouveau | Contrôle (élite) | contact rapide, fort ; **régénère** 5 % par seconde tant qu'il ne brûle pas | diodes cyan dans le dos, qui clignotent quand il régénère | le brûler, puis le finir | 12:00 / ch. 4 |
| **Déréglé** | mini-boss | hybride | frappe **tout ce qui bouge**, Ambrés compris ; projette des éclats de croûte autour de lui | diodes rouges, la croûte éclate à travers l'armure | le laisser faire le ménage, rester loin, finir | ch. 6 puis ch. 7 et suivants |
| **Greffé Prime** | boss | voir 3.7 | | | | ch. 11 |

Pourquoi : des mercenaires équipés, sans visage. Ils veulent les injecteurs (voir `Univers.md` 7.4). Le Greffé régénère parce qu'il a la Sève sous contrôle d'un Régulateur ; le feu fait fondre la croûte plus vite qu'elle ne pousse. Le Déréglé est un Greffé dont le Régulateur a lâché.

Le mot des Charognards : Opérateur « Il recule en tirant. Avance plus vite qu'il recule. » Bouclier « Devant, rien ne passe. Derrière, tout passe. » Drone « Quand la lumière est sur toi, c'est que quelqu'un regarde. » Greffé « Il se referme comme une porte. Fous le feu à la porte. » Déréglé « Lui, il est plus à personne. Laisse-le travailler. »

### 3.6 Les élites (Alphas)

Une élite garde la silhouette de son type. L'affixe est un accent visible, pas un nouveau modèle.

| Affixe | Explication | Signe de dessus | État |
|---|---|---|---|
| RAPIDE | métabolisme de stade I conservé | traînée ambre, posture penchée | existe |
| BLINDÉ | croûte deux fois plus épaisse | plaques anguleuses, reflets | existe |
| EXPLOSIF | poche de sève interne | ventre luisant | existe |
| VAMPIRE | absorbe la sève des tués alentour, régénère | filaments ambre vers les cadavres | existe |
| INVOCATEUR | phéromone : deux Rejetons bourgeonnent sur lui toutes les 6 s | halo ambre pulsé | existe (le spawn) |
| **POISSEUX** | laisse une traînée de sève qui ralentit 3 s | traînée luisante au sol | à coder |
| **BOSSU** | une bosse énorme qui absorbe les 25 premiers % de dégâts avant de craquer | la bosse est deux fois plus grosse, puis vole en éclats | à coder |
| **ENRAGÉ** | sous 30 % de PV, +40 % de vitesse et il hurle | la croûte vire au rouge sombre | à coder |

Trois « appels » différents, pour ne pas se marcher dessus : le **Hurleur** fait converger et accélère (pas de spawn), le **Berger** fait émerger des Ambrés près de lui (humain), l'élite **INVOCATEUR** bourgeonne des Rejetons sur elle-même.

### 3.7 Les Souches (boss)

Chaque Souche a deux ou trois attaques télégraphiées, des phases, et une **escorte** qui raconte d'où elle vient. En survie infinie elles se succèdent (Boucher, Maire, Contremaître, Sujet 01, puis les autres au fur et à mesure), chacune plus résistante que la précédente (déjà codé : +60 % par boss).

| Boss | Chapitre | Qui c'était | Attaques et phases | Escorte |
|---|---|---|---|---|
| **Le Boucher** **(existe)** | 1, Galeria | le boucher du rayon viande, premier à avoir mangé de la viande contaminée | anneaux d'éclats de croûte (existe) ; charge au hachoir (existe) ; à 50 % : anneaux plus serrés et il appelle des Fiévreux | Fiévreux en tablier |
| **Le Maire** | 2, place de la mairie | a refusé l'évacuation pour « garder sa ville » | Kyste géant à écharpe, lent ; lâche des Rejetons toutes les 10 s ; à 50 % **éclate** (grand anneau à fuir) et devient « le Maire dégonflé », rapide | Colonies |
| **Le Contremaître** | 3, la centrale | chef d'équipe du Combinat, a enfermé ses hommes dedans | charge en ligne (ligne au sol) ; frappe au sol (cercle) ; jette des débris (projectiles lents) ; à 30 % appelle deux Masses | Masses en bleu de travail |
| **Sujet 01, « le Premier »** | 4, laboratoire de l'aile Nord | Milan Doubek, mineur volontaire, quatre ans de Sève | phase 1 : rapide, griffes ; phase 2 : crache (Cracheur) ; phase 3 : saute (Bondisseur) et crache ; il est le plus « évolué » | Greffés, qui le gardent |
| **Le Ferrailleur** | 5, la casse | « Ferro », le receleur des Charognards | cône de fusil à pompe (il épaule) ; lance un baril (cercle, explosion) ; appelle 4 Pilleurs qui volent tes éclats au sol ; humain : jamais démembré | Cogneurs, Fusiliers |
| **Sujet 04, « le Déréglé »** | 6, la Dalle | le premier Greffé dont le Régulateur a lâché, abandonné sur la Dalle | frappe tout ce qui bouge ; éclats de croûte en anneau ; à 50 % les diodes passent au rouge et il double de vitesse | Opérateurs qui tirent sur lui autant que sur toi |
| **Le Foreur** | 7, la carrière | conducteur de foreuse de Belsk | s'enfouit, un cercle apparaît sous toi, il ressort (sortir du cercle) ; balayage de tarière (arc) | Rampants dans les gravats |
| **L'Ours de Hlad** | 8, la forêt | un ours brun, stade IV | charge longue (ligne) ; coup de patte (arc) ; rugissement qui appelle la Meute | Meute |
| **La Mère des Sources** | 9, le barrage | Dr Ilona Marek, l'ancienne directrice du site REGEN (voir `Univers.md` 7.4) | ne frappe pas ; trois **cloches** autour d'elle font émerger des Ambrés, il faut les briser pour percer sa garde ; des Martyrs en défense | Bergers, Martyrs |
| **La Résurgence Mère** | 10, les Galeries | pas une personne : le point où la boue de la Crue s'est accumulée sous terre | immobile ; quatre **pores** à détruire ; chaque pore détruit libère une bouffée (cercle) ; Fiévreux en continu | tout stade III |
| **Greffé Prime, Sujet 07** | 11, le Berceau | Anton Rask, ex-soldat, le seul Greffé stable | phase 1 : contact rapide et régénération (brûle-le) ; phase 2 : deux Drones ; phase 3 : les diodes passent au rouge, il devient Déréglé et frappe tout, REGEN compris | Opérateurs, Boucliers |

### 3.8 Les props et les nids

| Nom | Asset | Ce que c'est | Effet |
|---|---|---|---|
| **Baril** | **(existe)** | fût de carburant, ou de réactif REGEN (blanc à bande cyan) | explose, blesse tout le monde |
| **Caisse** | **(existe)** | caisse de ravitaillement larguée par REGEN pour ses équipes | butin : le joueur l'intercepte |
| **Résurgence** | nouveau, `Prop` vivant | une flaque d'ambre qui pulse ; un **Fiévreux en émerge toutes les 6 s** tant qu'elle n'est pas détruite ; brille (le seul ambre vraiment lumineux hors boss) | la détruire lâche un coffre ; en laisser une est un choix (elle nourrit le combo, mais elle nourrit la Crue) |

---

## 4. La variété

### 4.1 Grille d'apparition en survie infinie (proposition)

L'ordre compte plus que les minutes exactes. Une nouveauté toutes les 90 s environ jusqu'à la minute 10, puis le mélange et les paliers de danger font le travail.

| Minute | Nouveauté | Ce que le joueur apprend |
|---|---|---|
| 0:00 | Ambré | bouger, frapper en cône |
| 0:30 | Fiévreux | ne pas se faire flanquer |
| 1:00 | Nuée | la zone d'effet |
| 1:30 | Cracheur | regarder les gorges qui brillent |
| 2:00 | Kyste | tirer dans la horde |
| 2:30 | Masse | brûler ce qui ne recule pas |
| 3:20 | Souche 1 | le boss, l'escorte |
| 3:30 | Colonie | tuer loin de soi |
| 4:00 | Pilleur | courir après l'or |
| 4:30 | Meute | tourner |
| 5:00 | Bondisseur | sortir du cercle |
| 6:00 | Hurleur | la cible prioritaire |
| 6:40 | Souche 2 | |
| 7:00 | Accrocheur | garder une lame |
| 7:30 | Cogneur, Fusilier | les humains tirent |
| 8:30 | Lanceur | le feu est à tout le monde |
| 9:00 | Fervent, Martyr | les vagues mixtes |
| 10:00 | Berger, Souche 3 | tuer la cloche |
| 11:00 | Opérateur, Drone | REGEN entre en scène |
| 12:00 | Bouclier, Greffé | contourner, brûler |
| 13:20 | Souche 4 | |
| 14:00 | Déréglé | laisser faire |
| ensuite | tout, et les paliers de danger | survivre |

### 4.2 Par chapitre de campagne

| Chapitre | Nouveautés | Hazard introduit | Souche |
|---|---|---|---|
| 1 Ostrov-Sud | Ambré, Fiévreux, Nuée, Kyste, Masse, Pilleur, Meute | embuscade, barils | Le Boucher |
| 2 Ostrov-Centre | Cracheur, Colonie, Cristallisé, Cogneur, Fusilier | tir du Cordon, la fontaine est une Résurgence | Le Maire |
| 3 Le Combinat | Bondisseur, Hurleur, Lanceur | largage incendiaire, barils en chaîne | Le Contremaître |
| 4 Hôpital Central | Accrocheur, Opérateur, Bouclier, Drone, Greffé | drone traqueur, nuage de sève | Sujet 01 |
| 5 Gare de triage et casse | tout le roster Charognard, Porteurs | pièges, primes | Le Ferrailleur |
| 6 Les Bassins | Déréglé | la Dalle : REGEN partout, tir du Cordon dense | Sujet 04 |
| 7 La Carrière | Rampant, Nuée noire | éboulements, pas de couvert | Le Foreur |
| 8 La Forêt de Hlad | Sanglier | visibilité, Rampants dans l'herbe | L'Ours |
| 9 Le Barrage | Fervent, Berger, Martyr | cercles et autels des Fervents | La Mère des Sources |
| 10 Les Galeries | Résurgences partout, Cristallisés | nuages de sève, effondrements | La Résurgence Mère |
| 11 Le Berceau | tout REGEN | tout | Greffé Prime |

### 4.3 Les Crues : tirage, variété, montée

Ce que fait le code aujourd'hui : les pics alternent strictement anneau (vagues impaires) et colonne (vagues paires), toutes les 50 s pile, et la composition est par positions fixes (un coureur tous les 3, une brute tous les 9, un Kyste tous les 11). Un joueur attentif sait au bout de trois parties ce qui arrive, quand, et d'où. L'escalade n'est que numérique (plus de PV, plus d'ennemis) : la **nature** des vagues ne change jamais. Voilà ce qui remplace ça.

**Le principe en trois phrases.** Une Crue, c'est une **forme** (d'où et comment ils arrivent) tirée au sort, un **fond** (qui) tiré par budgets de rôles, et une fois sur trois un **thème**. Rien n'est fixé à l'avance, mais tout est tiré dans un sac qui se remplit avec le temps : c'est la **montée**. Et tout ce qui est tiré s'annonce 1,5 s avant par une lueur au bord de l'écran, du côté d'où ça vient.

#### a) La forme : neuf formations

| Formation | Comment ils arrivent | Ce que ça force | Palier | Poids | Bannière |
|---|---|---|---|---|---|
| **Anneau** | tout autour de l'écran (la horde actuelle) | choisir une brèche | 1 | 3 | « CRUE ! » |
| **Colonne** | une colonne dense : devant toi 60 %, de côté 25 %, dans ton dos 15 % (l'assaut actuel, mais la direction est tirée) | trancher dedans | 1 | 3 | « ASSAUT ! » |
| **Rafale** | trois salves d'un tiers chacune, à 4 s d'intervalle, de trois directions différentes | se replacer trois fois | 2 | 2 | « RAFALE ! » |
| **Tenaille** | deux colonnes, de deux côtés opposés | sortir par le côté | 3 | 2 | « TENAILLE ! » |
| **Pluie de Kystes** | 8 à 20 Kystes posés au bord, puis des Fiévreux derrière pour te pousser dedans | déclencher la chaîne au bon moment | 3 | 1 | « KYSTES ! » |
| **Marée** | tout un bord de l'écran avance comme un mur, deux rangs de profondeur, lent | bouger latéralement pendant 20 s | 4 | 2 | « MARÉE ! » |
| **Chasse** | que du rapide (Fiévreux, Meute, Bondisseurs) venant de ton dos, plus un Hurleur | se retourner et tenir | 5 | 2 | « CHASSE ! » |
| **Nids** | deux ou trois Résurgences posées au bord, qui pompent des Fiévreux pendant 20 s | aller les détruire (coffre) ou tenir | 6 | 1 | « RÉSURGENCE ! » |
| **Vague mixte** | un Berger qui fait émerger, deux Martyrs, des Fervents | tuer la cloche | 5, et seulement là où il y a des Fervents | 1 | « LES CLOCHES ! » |

Tirage pondéré parmi les formations débloquées au palier courant, en excluant la précédente.

#### b) Le fond : des budgets de rôles, pas des positions

Chaque Crue reçoit un budget par rôle qui dépend du palier. Chaque place est ensuite piochée dans le **pool débloqué** de ce rôle, avec les poids du flux (`Pick`). Deux Crues au même palier n'ont donc jamais le même fond : une Colonne de palier 4 peut être Fiévreux + Cracheurs + une Masse, la suivante Ambrés + Fusiliers + un Bouclier.

| Palier | Pression | Mur | Perçage | Déni | Contrôle |
|---|---|---|---|---|---|
| 1 | 85 % | 5 % | 10 % | 0 | 0 |
| 2 | 80 % | 5 % | 10 % | 5 % | 0 |
| 3 | 70 % | 10 % | 12 % | 8 % | 1 unité |
| 4 | 65 % | 10 % | 15 % | 10 % | 1 unité |
| 5 | 60 % | 10 % | 18 % | 12 % | 2 unités |
| 6 | 58 % | 12 % | 18 % | 12 % | 2 unités |
| 7 et plus | 55 % | 12 % | 20 % | 13 % | 2 unités |

Chaque formation applique son accent par-dessus : l'Anneau ajoute du Déni, la Colonne du Perçage, la Marée du Mur, la Chasse ne prend que de la Pression rapide.

#### c) Le thème : une Crue sur trois a une dominante

| Thème | Contenu | Palier | Bannière |
|---|---|---|---|
| Crue de Fiévreux | que des Fiévreux, −30 % de PV, +50 % de nombre | 2 | « CRUE DE FIÉVREUX ! » |
| Veine d'ambre | des Cristallisés (existe) | 2 | « VEINE D'AMBRE ! » |
| Crue de Kystes | la moitié de Kystes | 3 | « CRUE DE KYSTES ! » |
| Crue de Cracheurs | un anneau de Cracheurs : il faut aller au contact | 4 | « CRUE DE CRACHEURS ! » |
| Crue de Masses | que des Masses, nombre divisé par trois | 4 | « CRUE DE MASSES ! » |
| Crue de Colonies | des Colonies : la Crue se dédouble quand on la tue | 5 | « CRUE DE COLONIES ! » |
| Nuée | des rats (existe, en phase de montée) | 1 | « NUÉE ! » |

Jamais le même thème deux fois de suite. Les thèmes sont les Crues dont on se souvient : « la fois où quarante Kystes sont arrivés ». Au-delà d'une sur trois, elles deviennent la norme et ne surprennent plus.

#### d) La montée : huit paliers de danger

Tous les 150 s en survie infinie (en campagne, plafonné par l'étape). Chaque palier ajoute une **règle**, pas seulement des chiffres. Le nom s'affiche sous le chrono : le joueur voit le jeu changer de nature.

| Palier | Temps | Nom | Ce qui change |
|---|---|---|---|
| 1 | 0:00 | **Suintement** | Anneau, Colonne. Silence de l'accalmie 5 s. |
| 2 | 2:30 | **Montée** | + Rafale. Thèmes possibles (Fiévreux, Veine). Silence 4 s. |
| 3 | 5:00 | **Crue** | + Tenaille, Pluie de Kystes. Une unité de Contrôle par Crue. Le pic dure 14 s. |
| 4 | 7:30 | **Débordement** | + Marée. Thèmes Masses, Cracheurs. Silence 3 s. 15 % des pics sont **doubles** (deux formations à 5 s d'écart). |
| 5 | 10:00 | **Déferlante** | + Chasse, Vague mixte. Deux unités de Contrôle. Un Alpha dans chaque Crue. L'accalmie passe de 0,3× à 0,45×. |
| 6 | 12:30 | **Submersion** | + Nids. Silence 2 s. 30 % de pics doubles. Pic 16 s. |
| 7 | 15:00 | **Noyade** | Tout est débloqué. L'escorte des Souches double. Pression 55 %. |
| 8 | 17:30 | **Apocalypse** | +12 % de nombre et de PV par palier supplémentaire, tous les 150 s, sans fin. |

Par-dessus, les leviers existants continuent de monter : nombre par minute, PV et dégâts par minute, débit du flux, affixes d'élite, mélange du flux.

#### e) Les règles anti-pattern

- Le pic ne tombe jamais à la seconde près : son moment bouge de ±6 s à chaque vague.
- Jamais la même formation deux fois de suite, jamais le même thème deux fois de suite.
- La direction d'une Colonne, d'une Tenaille ou d'une Marée est tirée, jamais déduite de la précédente.
- Un pic double n'enchaîne jamais deux Anneaux.
- Les Crues à thème ne dépassent pas une sur trois.
- Le seul pattern volontaire est **la Souche** : elle tombe sur son minuteur, elle est annoncée, et c'est au joueur de s'y préparer. C'est l'horloge que le joueur garde en tête pendant que tout le reste bouge.

#### f) L'équité

- Lueur au bord de l'écran 1,5 s avant le premier ennemi, du côté d'où ils viennent, dans la couleur de la source.
- Les plafonds de 4.5 s'appliquent aux Crues : pas plus de deux Contrôles, six tireurs, un Accrocheur.
- Dans l'Appel du jour et la météo de la Crue, tous les tirages (forme, fond, thème, direction, moment) viennent de la graine du mode (`RunConfig.Seed`) : même Crue pour tout le monde, c'est ce qui rend le classement juste. Ailleurs, hasard pur.
- Le silence de l'accalmie ne descend jamais sous 2 s : le combo peut toujours casser, l'écran peut toujours respirer.
- La Crue de Cracheurs ne tombe jamais en pic double. La Chasse ne tombe jamais pendant une Souche.

#### g) Ce que ça change dans le `RunDirector`

- `WaveIndex % 2` devient un tirage pondéré de formation parmi celles débloquées, en excluant la précédente.
- Les positions fixes de `SpawnHorde` et `SpawnAssault` (`i % 3`, `i % 9`, `i % 11`, `i % 4`) deviennent un tirage par budget de rôles, chaque place piochée dans le pool du rôle avec les poids du flux.
- Un `DangerTier` calculé depuis les minutes (survie) ou l'étape (campagne) pilote les débloquages, les budgets, `lullSilence`, `peakTime`, `lullMult`, les pics doubles et le nom sous le chrono.
- Le début du pic est jitté de ±6 s autour de `buildTime`.
- Un `System.Random` initialisé avec `RunConfig.Seed` quand il est non nul, pour tous les tirages de Crue.
- Un effet de bord lumineux 1,5 s avant (`GameFX`), et une bannière par formation et par thème.

Les autres vagues ne changent pas : la **Nuée**, la **Veine d'ambre**, le **Pilleur** restent en phase de montée sur leurs minuteurs, l'**escorte de Souche** accompagne le boss, flux réduit.

### 4.4 Variantes de peau par carte

Même mesh, autre texture, parfois un accessoire. C'est la façon la moins chère de faire sentir que chaque carte a « ses » Ambrés.

| Carte | Ambrés et Fiévreux | Détail |
|---|---|---|
| Ostrov-Sud | civils, un peu de sport, pyjamas | ils ont été pris chez eux |
| Ostrov-Centre | costumes, uniformes de la gare, du métro, de la mairie | |
| Le Combinat | ouvriers, casques, vestes bleues | plus de Masses |
| Hôpital | blouses, patients en chemise, un soignant sur deux | |
| Les Bassins | **couverts de boue d'ambre séchée**, craquelés | les plus proches de l'origine |
| La Carrière | poussière blanche, presque calcaire | |
| La Forêt | mousse, chasseurs | |
| Le Barrage | trempés, draps blancs des Fervents convertis trop tard | |
| Les Galeries | plus cristallisés, plus lumineux | la seule carte où l'ambre domine |
| Jour de Crue (`Univers.md` 6.8) | tout le monde a de la boue fraîche, trempés | Cristallisés ×3, Kystes +50 %, Rampants dans l'eau |
| Saison de Crue (automne) | boue fraîche, résurgences partout | la variante saisonnière |

### 4.5 Plafonds de simultanéité (équité et lisibilité)

| Quoi | Maximum à l'écran |
|---|---|
| Unités de Contrôle (Hurleur, Berger, Accrocheur, Greffé) | 2 |
| Accrocheur | 1 |
| Tireurs (Cracheur, Fusilier, Opérateur, Drone) | 6 |
| Déni (Kyste, Martyr, Lanceur) | 8 |
| Bondisseur | 3 |
| Masses hors Crue | 2 |
| Souches | 1 (plus son escorte) |

---

## 5. Ce qu'il faut coder, par priorité

Chaque ligne = un comportement ou un effet. Les ennemis qui « réutilisent » un comportement existant ne coûtent qu'un prefab et une `EnemyData`.

**P1, le plus de variété pour le moins de code**
1. Variantes de texture par type et par carte (4.4).
2. **Plaque de poisse** du Cracheur : une zone au sol qui ralentit (réutilise la logique des flaques).
3. **Hurleur** : cri = convergence + vitesse temporaire des Ambrés dans un rayon.
4. **Bondisseur** : saut avec cercle d'atterrissage, dégâts à l'atterrissage, 1 s de vulnérabilité.
5. **Meute** : déplacement en arc (offset latéral pendant l'approche).
6. Renommer les assets et les textes (table dans `Univers.md` 6.5).

**P2, la profondeur**
7. **Accrocheur** : attrape, ralentissement, rupture à 15 % de PV perdus.
8. **Lanceur** : projectile en cloche qui laisse une flaque de feu ennemie (réutilise `FirePool`).
9. **Berger** : spawn périodique près de lui, Ambrés proches qui l'ignorent.
10. **Bouclier** : blocage des projectiles venant de face.
11. **Résurgence** : prop à PV qui spawne, coffre à la destruction.
12. Phases de boss (seuils de PV, changement d'attaques) et escortes.
13. Compteurs de kills par type pour le Carnet (`Univers.md` 7.2).

**P3, les cartes futures**
14. Drone (flag « vole » : ignore flaques et scies).
15. Rampant (invisible hors d'un rayon, traînée).
16. Nuée noire (piqué avec ombre).
17. Sanglier et charge en ligne (partagé avec le Contremaître et l'Ours).
18. Déréglé (IA qui vise la cible la plus proche, joueur ou ennemi).
19. Affixes POISSEUX, BOSSU, ENRAGÉ.
20. Les nouvelles Souches.
