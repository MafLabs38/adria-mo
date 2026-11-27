# Adria MO : spécification (v0.6, 2026-11-27)

> Outil de **maîtrise d'ouvrage citoyenne** pour l'immeuble Adria : constater, dater et suivre les
> défauts des parties communes (malfaçons, dégradations, insalubrité, pannes à répétition), suivre
> les travaux, chiffrer ce que coûtent les interventions répétées, et en rendre compte au bailleur.
>
> Nom : **Adria MO** (« Adria maîtrise d'ouvrage »), dépôt `adria-mo`, licence MIT, dépôt public.
> Les questions encore ouvertes sont au § 12.

---

## 1. Pourquoi

Le bailleur ne contrôle pas les travaux qu'il commande : une peinture laissée en plan depuis trois
mois (bandes de masquage encore en place, bas des murs pas repeints), une porte de hall réparée
trois fois par un technicien et toujours cassée. Personne ne trace ces faits. Sans trace, on ne peut
pas dire **depuis quand** ça dure, **combien de fois** on est intervenu, ni **combien ça a coûté**.

L'application fait ce travail de maîtrise d'ouvrage à la place de celle qui manque :

1. **Constater** : une photo ou un texte, daté, rangé dans un dossier par problème.
2. **Suivre** : l'historique du dossier (signalé, intervention, « réparé », cassé de nouveau…).
3. **Chiffrer** : estimer le coût cumulé des interventions, pour le mettre en balance avec une
   vraie solution (« 7 passages de technicien sur la porte = ~1 000 € ; une caméra = ~400 € »).
4. **Rendre compte** : une page publique où tous les locataires voient l'état d'avancement, des
   tableaux de bord, et des mails réguliers au bailleur.

**Périmètre : les parties communes uniquement.** Jamais les logements privés, jamais les personnes.

## 2. Deux portes : la page publique et l'administration

- **Page publique** (sans connexion) : les constats et leur état d'avancement, consultables par tous
  les locataires (lien, QR code dans le hall). Rien d'autre n'y est visible.
- **Administration** (derrière une connexion, jamais liée depuis la page publique) : tout le reste.

| Rôle | Qui | Connexion | Ce qu'il peut faire |
|---|---|---|---|
| **Visiteur** | n'importe quel habitant | aucune | Lire les constats publiés. Plus tard (§ 6) : proposer une photo ou un constat, soumis à une **modération stricte par l'IA** et à validation. |
| **Administrateur** | un ou deux habitants impliqués dans la vie de l'immeuble | identifiant + mot de passe, ou téléphone appairé (§ 4.1) | Tout sur le contenu : créer, modifier, fusionner, clore un constat ; ajouter des pièces ; valider les propositions des visiteurs ; saisir les interventions et les signalements au bailleur ; préparer les mails. |
| **Super-administrateur** | toi | identifiant + mot de passe (+ TOTP en option) | Tout ce que fait un admin, plus : comptes et rôles, réglages (barème, modération, mails, SMTP), journal de modération, suppression définitive. |

- **Pas de connexion Google** : un voisin peu à l'aise avec l'informatique pourrait hésiter à donner son compte Google, et un compte local
  suffit pour deux ou trois personnes. Le super-admin crée les comptes des administrateurs.

### 2.1 Comptes et mots de passe

Le super-admin crée le compte d'un administrateur avec un **mot de passe provisoire** qu'il lui donne
de vive voix ; ensuite, c'est l'administrateur qui gère son mot de passe, seul.

- **Premier passage obligé** : à la première connexion avec un mot de passe provisoire, l'administrateur
  doit en choisir un nouveau ; rien d'autre n'est accessible avant.
- **Changer son mot de passe** à tout moment (« Mon compte »), en redonnant l'ancien. Changer ou
  réinitialiser un mot de passe **ferme toutes les autres sessions** (un téléphone perdu ne reste pas
  connecté).
- **Mot de passe oublié** : le super-admin le réinitialise (nouveau mot de passe provisoire, même
  passage obligé). Personne ne peut lire un mot de passe, pas même le super-admin ; l'application
  n'envoie jamais de mot de passe par mail.
- **Règles** (recommandations NIST SP 800-63B et OWASP) : au moins 12 caractères, jusqu'à 128, espaces
  et accents permis (une phrase se retient mieux qu'un mélange de symboles) ; pas d'obligation de
  majuscule ou de chiffre ; refus des mots de passe trop courants, faits d'un seul caractère répété ou
  contenant l'identifiant ; pas d'expiration périodique.
- **Stockage** : jamais en clair ni dans un journal ; empreinte salée et lente (PBKDF2-HMAC-SHA512,
  210 000 itérations, sel propre à chaque compte), par ASP.NET Core Identity ; une empreinte ancienne
  est recalculée toute seule à la connexion suivante si le réglage se durcit.
- **Contre les essais en série** : compte bloqué 15 minutes après 5 échecs ; nombre de tentatives
  limité par adresse ; même message d'erreur que l'identifiant existe ou non.
- **Session** : cookie `HttpOnly`, `Secure`, `SameSite=Strict`, valable 14 jours glissants ; les
  requêtes qui modifient quelque chose portent en plus un en-tête propre à l'application.
- **Premier compte** : le super-admin est créé au premier démarrage à partir d'un identifiant et d'un
  fichier secret monté dans le conteneur, jamais du dépôt.

## 3. Le modèle : ce qu'on manipule

- **Constat** (le dossier d'un problème) : titre, catégorie, emplacement, gravité, statut,
  **date de première constatation** (= date de prise de vue de la plus ancienne photo, pas la date de
  saisie), « ouvert depuis N jours », équipement concerné (facultatif), visible ou non sur la page
  publique.
- **Pièce** (une observation versée au dossier) : photo(s) et/ou texte, **date de prise de vue**
  (lue dans l'EXIF), date de dépôt, auteur, statut de modération. On garde l'**empreinte SHA-256**
  du fichier original et l'original lui-même : la pièce est horodatée et non modifiable, ce qui lui
  donne du poids face au bailleur (ce n'est pas un constat d'huissier, mais c'est traçable).
- **Équipement** (facultatif) : « porte du hall bât. B », « ascenseur cage A ». Il relie les constats
  successifs sur un même objet : c'est lui qui fait apparaître la **récurrence**.
- **Intervention** : date, qui (prestataire, technicien du bailleur), nature, résultat
  (« réparé », « provisoire », « sans effet »), **coût estimé** (§ 7).
- **Signalement au bailleur** : date, canal, référence éventuelle, réponse obtenue. Les mails envoyés
  par l'application (§ 9) s'y inscrivent tout seuls.
- **Chantier** : un ensemble de travaux suivis (« réfection peinture cage B ») qui regroupe des
  constats, avec ses étapes, ses réserves et sa réception.

**Catégories de départ** (modifiables) : peinture et finitions, portes et fermetures, ascenseur,
éclairage, électricité, propreté, encombrants, poubelles, nuisibles, humidité et fuites, sécurité et
incendie, dégradations, extérieurs.

**Emplacements** : bâtiment / cage / étage / zone (hall, palier, escalier, ascenseur, local poubelles,
caves, parking, extérieurs). Listes réglables dans l'administration ; la structure réelle de
l'immeuble est saisie dans la base, jamais dans le dépôt.

**Statuts d'un constat** : `ouvert` → `signalé au bailleur` → `intervention prévue` → `en cours` →
`résolu (vérifié)` → `clos`, avec `rouvert` quand ça recasse. Seul un admin passe à « résolu »,
**après vérification sur place** : on ne croit pas l'entreprise sur parole.

## 4. Le parcours principal : constater depuis un téléphone

C'est la fonction de base, faite d'abord pour le mobile.

1. Bouton **« Constater »** toujours visible.
2. **Prendre une photo** (appareil photo du téléphone, directement depuis la page) ou en choisir
   plusieurs dans la galerie.
3. L'application propose : **ajouter à un constat existant** (les constats ouverts du même
   emplacement, avec leur vignette) **ou créer un nouveau constat**.
4. Catégorie et emplacement en quelques touches, un commentaire facultatif (la dictée du clavier
   suffit).
5. Envoi. L'**original** part tel quel (c'est lui qui porte l'empreinte et la date de prise de vue) ;
   le serveur en tire la **version publiée** (réduite, ≈ 300 Ko, sans aucune métadonnée, donc sans
   GPS) et une vignette. Une réduction dans le navigateur avant l'envoi, pour la 4G, pourra venir
   avec l'envoi différé (§ 4.1).

### 4.1 Faire venir les photos d'un téléphone sans manipulation

Un administrateur sans forfait de données prend ses photos avec l'appareil photo de son téléphone,
hors ligne, et retrouve sa wifi chez lui. Le but : qu'il n'ait ni mot de passe à taper sur le téléphone, ni fichier à transférer à la main.

- **Appairer son téléphone par QR code** (une seule fois) : dans l'administration, sur un
  ordinateur, « Ajouter mon téléphone » affiche un QR code. Il le scanne avec l'appareil photo :
  le téléphone s'ouvre sur l'application, déjà connecté (un jeton propre à cet appareil, révocable
  depuis l'administration). Il n'a jamais tapé son mot de passe sur le téléphone.
- **« Ajouter à l'écran d'accueil »** depuis Chrome : le site s'installe avec son icône, comme une
  appli (PWA). **Ce n'est pas une appli du Play Store** : c'est le site lui-même, consultable dans
  n'importe quel navigateur ; l'installation est facultative et ne fait qu'ajouter l'icône, le
  partage depuis la galerie et l'envoi au retour de la wifi.
- **Partager depuis la galerie** : il sélectionne ses photos dans la galerie → *Partager* →
  **Adria MO**. Elles partent dans une **boîte « À classer »**. (Fonction de Chrome sur Android, la
  cible. Sur iPhone, rien de spécifique : le bouton « Ajouter des photos » ouvre la galerie.)
- **Envoi automatique dès la wifi** : si la photo est prise depuis l'application sans réseau, elle
  attend dans le téléphone et part toute seule au retour de la wifi.
- **Classer ensuite, au calme** (sur le téléphone ou l'ordinateur) : la boîte « À classer » trie les
  photos **par date de prise de vue** et les regroupe en **séries** (photos prises à quelques minutes
  d'écart = probablement le même problème) ; il rattache chaque série à un constat existant ou
  nouveau d'un geste. La date retenue est celle de la photo : un constat fait lundi et déposé jeudi
  reste daté de lundi.
- **QR codes d'emplacement** (pour tout le monde) : une petite étiquette par lieu (hall B, palier 3,
  local poubelles). La scanner ouvre la page de ce lieu : les constats en cours à cet endroit, et
  « Constater ici » avec l'emplacement déjà rempli.

## 5. La page publique

- **Liste des constats** (cartes avec vignette, statut en couleur, « ouvert depuis 94 jours »),
  filtres par catégorie, emplacement, statut. Mobile d'abord.
- **Fiche d'un constat** : frise chronologique des pièces, interventions et signalements ; galerie de
  photos ; coût estimé cumulé.
- **Tableau de bord** (§ 8).
- Français d'abord ; la traduction à la lecture du forum d'Adria pourra être reprise plus tard.

## 6. Contributions des visiteurs et modération (verticale ultérieure)

Un visiteur peut ajouter une photo ou proposer un constat, sans compte. Garde-fous, repris du forum
d'Adria qui les a déjà éprouvés :

- **Anti-robot** (Cloudflare Turnstile) et **limite de fréquence** par adresse IP (hachée).
- **Modération IA de chaque photo et de chaque texte** avant toute publication :
  - contenu inacceptable (nudité, obscénité, insulte, violence) → **rejeté**, avec le bannissement
    progressif du forum ;
  - **personnes reconnaissables, noms sur les boîtes aux lettres, plaques d'immatriculation**
    → file de validation (protection des personnes, RGPD) ;
  - **hors sujet** (la photo ne montre ni une partie commune ni un défaut) → rejeté ;
  - **doublon** : l'IA compare avec les constats ouverts et propose de rattacher la pièce au bon
    dossier.
- Une pièce **ajoutée à un constat existant** est publiée si l'IA n'y trouve rien à redire ; **un
  nouveau constat proposé par un visiteur attend toujours** la validation d'un admin.
- L'IA est celle du forum d'Adria : **l'API OpenAI**, appelée par le back-end (la clé est un secret
  du back-end, jamais dans le dépôt ni dans le navigateur). `omni-moderation-latest` (gratuit) juge
  le texte et les images (nudité, violence, haine) ; `gpt-4o-mini` juge le hors-sujet, les personnes
  reconnaissables et les doublons, pour quelques centimes par mois à ce volume. L'abonnement Claude
  n'est pas utilisé par l'application.
- Le texte public reste **factuel** : on décrit l'état des lieux, on ne nomme personne. C'est ce qui
  protège contre un reproche de diffamation.

## 7. Chiffrage des interventions

- Un **barème** réglable : déplacement d'un technicien, heure de main-d'œuvre, remplacement d'une
  serrure, d'un ferme-porte, d'un luminaire, nettoyage d'un tag, etc.
- Chaque intervention reçoit un **coût estimé** : par défaut celui du barème, ajustable ; l'IA peut
  proposer une fourchette, toujours affichée comme **estimation**.
- Par constat et par équipement : **coût cumulé**, nombre de passages, intervalle moyen entre deux
  pannes ; comparaison avec une solution durable saisie par l'admin.

## 8. Tableaux de bord (KPI)

- Constats ouverts, et **depuis combien de temps** (médiane, le plus ancien, répartition par âge).
- Par catégorie, emplacement, équipement : où ça casse le plus.
- Délais : constat → signalement → intervention → résolution vérifiée.
- **Taux de réouverture** et **coût cumulé estimé** des interventions à répétition.
- Courbe des constats ouverts au fil des mois.

## 9. Mails au bailleur

Une **tâche planifiée dans le back-end**, au modèle de **Mailarr** (l'outil fait pour le dossier des
poubelles) :

- un **envoi** = des **destinataires** (À, Cc, Cci) + un **modèle de mail** à variables + une
  **fréquence** réglable (chaque mois, chaque semaine, chaque jour ouvré…) et une heure ;
- le contenu se remplit depuis la base : nouveaux constats, constats ouverts avec leur âge,
  interventions de la période, coût cumulé, photos nouvelles en pièces jointes, lien vers chaque
  fiche publique ;
- **aperçu fidèle** avant envoi (expéditeur, destinataires, copies, photos), envoi immédiat possible ;
- envoyé **à ton nom par ton serveur SMTP** (mot de passe lu dans un fichier monté, comme Mailarr) ;
- chaque mail envoyé s'inscrit dans l'historique des constats concernés (« signalé au bailleur le… ») ;
- plus tard : un **rapport PDF** d'un constat ou d'une période, pour un recommandé, une réunion ou un
  huissier.

## 10. Architecture

### 10.1 Le besoin : la page publique ne tombe jamais

Le back-end est hébergé chez toi : il peut s'arrêter (redémarrage, mise à jour, coupure de courant
ou de box). **La page publique doit rester visible quand il est arrêté** : le tableau des constats,
leurs photos et leur avancement. L'administration, elle, peut être indisponible pendant ce temps.

D'où deux parties qui vivent à deux endroits :

| | **Partie publique** | **Partie administration** |
|---|---|---|
| Contenu | tableau des constats, fiches, photos publiées, avancement, tableau de bord | saisie, CRUD, comptes, boîte « À classer », mails, modération |
| Où | **chez Cloudflare** : front sur Cloudflare Pages, données sur R2 | **chez toi** : un conteneur .NET + sa base |
| Si ta machine est arrêtée | **toujours visible** (données de la dernière publication) | indisponible ; les photos prises dans l'appli attendent dans le téléphone |

### 10.2 Publier plutôt que mettre en cache

Un simple cache du CDN devant le back-end ne garantit rien : Cloudflare peut retirer une page de son
cache quand il veut, et la page n'y est pas encore si personne ne l'a visitée récemment. Quand le
back-end tombe, une partie du site tombe avec lui, au hasard.

On fait donc l'inverse : **le back-end publie**. À chaque changement d'un constat publié, il écrit
une **photographie des données publiques** chez Cloudflare :

- `constats.json` : les constats publiés, leur frise, leur statut, les indicateurs du tableau de
  bord, et la date de publication (« à jour au 27/11 à 14:05 ») ;
- les photos publiées (réduites, sans GPS) et leurs vignettes, nommées par leur empreinte : elles ne
  changent jamais, donc se mettent en cache pour toujours.

Publication déclenchée après chaque modification (regroupée sur une trentaine de secondes), plus une
publication complète chaque nuit. Le front public ne lit **que** cette photographie, jamais le
back-end : il marche pareil, que ta machine soit allumée ou non.

Seules les données publiques sortent : rien de l'administration, aucun compte, aucune pièce
non validée, aucun original de photo.

### 10.3 Les briques

| Brique | Choix |
|---|---|
| Front | **Astro + Tailwind v4 + TypeScript**, comme Adria (§ 10.5), PWA (§ 4.1). Partie publique : pages statiques qui lisent `constats.json`, plus des composants web à intégrer ailleurs. Partie admin : îlots React qui appellent l'API. |
| Hébergement du front | **Cloudflare Pages** (gratuit), déployé par la CI |
| Données publiques | **Cloudflare R2** (10 Go gratuits, aucun frais de sortie), écrit par le back-end par l'API S3 |
| Back-end | **C# / .NET**, API minimale ASP.NET Core, ASP.NET Core Identity ; contient aussi la publication, les mails planifiés (§ 9) et plus tard la modération (API OpenAI, § 6) |
| Base | **SQLite** dans un volume (comme Mailarr), par Entity Framework Core et ses migrations (qu'ASP.NET Core Identity utilise déjà) ; PostgreSQL possible plus tard |
| Photos | originaux (avec empreinte SHA-256) et versions publiées dans le volume ; seules les versions publiées partent sur R2 |
| Exposition de l'API | **tunnel Cloudflare** : connexion sortante, aucun port ouvert, adresse IP cachée, HTTPS |
| Livraison | image `ghcr.io/maflabs38/adria-mo` pour le back-end ; front sur Pages, par la CI |

Adresses (production) : la partie publique sous `adria-residents.fr/constats` (§ 10.5) ou sur sa
propre adresse ; l'administration et l'API sur des sous-domaines d'`adria-residents.fr` (la zone est
déjà chez Cloudflare), pour que la connexion admin fonctionne sans bricolage.

Coût : 0 € (Pages, R2, tunnel en formule gratuite). Les clés d'écriture R2 sont un secret du
back-end (fichier monté), jamais dans le dépôt.

### 10.4 Bac à sable

- Deux conteneurs sur la machine de test, comme en production : le **back-end** et un **serveur statique**
  (nginx) qui sert le front et la photographie publiée dans un dossier local (à la place de Pages et
  de R2). On peut y vérifier la promesse : **on arrête le back-end, la page publique reste en ligne**.
- Une adresse fixe sur le réseau local, et un sous-domaine en HTTPS derrière le proxy de la maison (HTTPS : nécessaire pour l'appareil photo, le partage depuis la galerie et
  l'installation sur le téléphone) à envoyer aux premiers testeurs.
- Comptes : un super-admin et un admin, mots de passe simples (bac à sable seulement).
- Données : de vraies photos de l'immeuble, chargées dans la base du bac à sable.
- Mails : vers un attrape-mails (rien ne part chez le bailleur pendant les essais).

### 10.5 Intégration dans Adria Résidents : un seul site en apparence

Objectif : un habitant qui navigue sur `adria-residents.fr` ne voit **qu'un site**, alors que les
parties « constats » viennent d'Adria MO, déployé à part. Trois conditions :

**1. Le même front des deux côtés.** La partie publique d'Adria MO est faite comme Adria :
**Astro + Tailwind v4**, mêmes couleurs (`brand`, `accent`), même police (Baloo 2), même en-tête,
même largeur de page, mobile d'abord. L'administration est dans le même projet Astro, en composants
React chargés seulement sur ces pages (les « îlots » d'Astro).

**2. Un habillage commun, écrit une seule fois.** Le thème (variables Tailwind), l'en-tête, le menu
et la mise en page sortent d'Adria dans un **petit paquet partagé** (`adria-ui`) que les deux sites
utilisent. Le menu y est décrit une fois : Vivre ensemble, Salubrité, Forum, **Constats**. Changer
une couleur ou ajouter un onglet change les deux sites.

**3. Deux façons d'intégrer, à combiner :**

| | **Par chemin** (pages entières) | **Par composant** (morceaux dans une page d'Adria) |
|---|---|---|
| Ce que voit l'habitant | `adria-residents.fr/constats`, `/constats/42` : des pages d'Adria comme les autres, onglet « Constats » dans le menu | un bloc « Derniers constats » ou « Où en sont les travaux » sur l'accueil d'Adria, ou dans la page Salubrité (constats de nuisibles) |
| Comment | Cloudflare envoie le chemin `/constats/*` du domaine d'Adria vers le front d'Adria MO (règle de routage dans la zone, ou quelques lignes dans le Worker d'Adria). Adria MO est construit avec le chemin de base `/constats`. | Adria MO fournit des **composants web** (`<adria-constats-recents limite="3">`, `<adria-constats-tableau>`) : Adria ajoute une balise `<script>` et la balise du composant, rien d'autre |
| Données | la photographie publiée sur R2 (§ 10.2) | la même photographie |
| Si ta machine est arrêtée | toujours visible | toujours visible |

Les composants web marchent dans n'importe quelle page, quel que soit son outil : ils pourront aussi
servir ailleurs (un autre site, l'affiche du hall avec un écran…). Écarté : l'**iframe**, qui se voit
(hauteur fixe, double défilement sur mobile, styles différents).

Ce qui reste à décider le jour de l'intégration (sans incidence sur le code d'Adria MO, qui est
prêt pour les deux) : où vit le paquet `adria-ui` (dans le dépôt d'Adria MO, public, publié sur npm,
est le plus simple pour qu'Adria, privé, l'utilise) et la langue (Adria a 13 langues sous
`/<langue>/` ; Adria MO commence en français, avec des adresses prévues pour les langues).

En attendant l'intégration, Adria MO a sa propre adresse et le même habillage : on peut le montrer à
un premier testeur tel quel.

### 10.6 Production, plus tard

Front sur Cloudflare Pages, données publiques sur R2, back-end sur ton cluster ou une VM derrière le
tunnel, sauvegarde du volume (base + photos originales) vers le serveur de sauvegarde de la maison.

## 11. Dépôt public, qualité et contribution

Le dépôt est **public** : un étudiant en informatique du voisinage pourra lire le code et
proposer des PR. On le traite comme un vrai projet, au niveau de Portarr.

- **Création par `github-template`** : dépôt, branches `main` et `develop` protégées, gitflow
  (`feat/*`, `fix/*` → `develop` → `main`), modèles de PR et d'issues, Dependabot, labels.
- **Fichiers d'un projet ouvert** : `LICENSE` (MIT), `README.md` (lancer le projet en local en une
  commande), `CONTRIBUTING.md`, `SECURITY.md`, `CLAUDE.md`.
- **CI exigeante** (une PR n'est fusionnable que tout au vert) :
  - .NET : build sans avertissement avec les règles SonarAnalyzer, `dotnet format` ;
    tests xUnit v3 (unitaires et d'intégration avec `WebApplicationFactory`), seuil de couverture ;
  - front : ESLint (SonarJS, a11y), Prettier, TypeScript strict, `astro check`, tests Vitest ;
  - **tests de bout en bout** Playwright en écran mobile sur les parcours clés (constater, partager
    puis classer, publier) ;
  - **tests de mutation** (Stryker.NET et Stryker JS, modèles `mutation-dotnet` / `mutation-node`) ;
  - SonarQube (quality gate), **CodeQL**, gitleaks ;
  - les **quatre agents de revue** (`ai-review.yml` : tests, qualité, sécurité, architecture) et `review-decisions`, comme sur Portarr.
- **Ce qu'impose un dépôt public** :
  - **le dépôt ne contient que le code et la structure** : aucune donnée, aucun secret, aucune
    adresse email, aucune vraie photo, aucun rapport. Tout cela vit dans la base et le volume. Les
    données de test sont fictives ;
  - **un contributeur extérieur passe par un fork**. Une branche de ce dépôt exécute ses propres
    workflows avec les secrets (jeton Claude, jeton SonarQube) : un collaborateur pourrait modifier un
    workflow sur sa branche et les lire avant toute relecture. Les PR d'un fork ne reçoivent aucun
    secret : la CI y tourne sans SonarQube ni agents de revue. Pour les lancer, le mainteneur relit
    les workflows de la PR, puis pousse la branche dans le dépôt. Le droit de collaborateur est réservé à
    quelqu'un à qui l'on confierait ces jetons ;
  - `CODEOWNERS` : toi sur la CI, les workflows et le déploiement.

## 12. Décisions prises et questions ouvertes

Décidé le 2026-11-27 : modération par l'**API OpenAI** comme le forum d'Adria (pas l'abonnement
Claude, réservé aux agents de revue de la CI) ; nom **Adria MO** (dépôt `adria-mo`), licence MIT, dépôt public ; back-end
.NET chez toi, partie publique publiée chez Cloudflare (§ 10) ; front Astro comme Adria, intégrable
(§ 10.5) ; **Android d'abord** (le téléphone du premier administrateur), rien de spécifique pour l'iPhone ; un site
consultable dans le navigateur, **pas d'appli du Play Store**.

Encore ouvert :

1. Mails : intégrer le mécanisme de Mailarr dans Adria MO (proposé), ou faire envoyer les mails par
   l'instance Mailarr existante ? (verticale 4)
2. La structure de l'immeuble (bâtiments, cages, étages, équipements) : à saisir dans le bac à sable,
   pas dans le dépôt.
3. Les destinataires des mails chez le bailleur (dans l'administration, verticale 4).

## 13. Découpage en verticales (une démo et une PR chacune)

1. **Constater**, en deux PR successives :
   - **1a** : comptes admin et super-admin avec la gestion des mots de passe (§ 2.1) ; constats et
     pièces (photos depuis le téléphone, date de prise de vue lue dans l'EXIF, empreinte de
     l'original) ; publication de la photographie publique (§ 10.2) ; page publique (liste et
     fiche, « ouvert depuis ») ; bac à sable, où la page publique reste en ligne back-end arrêté.
   - **1b** : appairage du téléphone par QR code, installation sur l'écran d'accueil, partage depuis
     la galerie, envoi différé, boîte « À classer » avec les séries. → *C'est ce qu'on envoie au
     premier administrateur.*
2. **Suivi** : interventions, signalements, chantiers, statuts, équipements et récurrence ; QR codes
   d'emplacement.
3. **Chiffrage et tableaux de bord** : barème, coûts cumulés, KPI.
4. **Mails** : envois planifiés au modèle de Mailarr, aperçu, historique ; rapport PDF.
5. **Contributions des visiteurs** : Turnstile, modération IA par OpenAI (comme le forum), doublons,
   file de validation.
6. **Production** : front sur Cloudflare Pages, publication sur R2, tunnel vers le back-end,
   sauvegardes.
7. **Intégration dans Adria Résidents** : paquet `adria-ui` partagé, onglet « Constats » servi sous
   `/constats`, composant « Derniers constats » sur l'accueil d'Adria.
