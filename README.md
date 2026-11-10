# ScreenLink V1.3 @F5PBG 2026

**Français** · [English below](#english)

Voir et commander l'écran d'un smartphone Android depuis un PC Windows, **image + son**, par **Wi-Fi** (même réseau) ou par **câble USB**. Pensé pour réaliser des vidéos de présentation d'applications smartphone avec **OBS Studio** (capture de la fenêtre ScreenLinkPC).

Deux programmes :

| Programme | Rôle |
|---|---|
| `ScreenLink-1.3.apk` | à installer sur le smartphone (Android 8 minimum, son : Android 10 minimum) |
| `ScreenLinkPC.exe` | à lancer sur le PC Windows 10/11 (un seul fichier, rien à installer) |

## Installation
1. Smartphone : installer l'APK (autoriser les sources inconnues). Lancer ScreenLink.
2. Toucher **Démarrer le partage** et accepter la capture de l'**écran entier**.
3. Toucher **Activer la commande à la souris**, ouvrir *Applications installées* → **ScreenLink** → activer. C'est ce service d'accessibilité qui permet à la souris du PC de toucher l'écran. Sans lui, vous voyez l'écran mais ne pouvez pas le commander.
4. PC : lancer `ScreenLinkPC.exe`.
   - **Wi-Fi** : saisir l'adresse IP et le code à 6 chiffres affichés sur le téléphone, puis *Connecter*.
   - **USB** : activer le débogage USB sur le téléphone, brancher, choisir *USB*, saisir le code, *Connecter*. Le PC cherche `adb.exe` (à côté de l'exe, dans le PATH ou `C:\Android`), crée le lien et lance l'application ; il reste à accepter la capture sur le téléphone.

Sur le téléphone, le **bouton rouge d'arrêt** (à droite de « PC connecté ») arrête le partage et ferme complètement l'application.

## Utilisation
- Le bouton **Connecter** est rouge tant qu'il n'y a pas de connexion (orange pendant l'essai, vert une fois connecté). La case **Auto** à côté relance la connexion toute seule toutes les 15 secondes si elle est perdue (jamais après un code incorrect ni après un clic sur Déconnecter).
- **Clic gauche** = doigt (toucher, glisser, appui long). **Clic droit** = Retour, **clic milieu** = Accueil, boutons latéraux = Retour / Récents.
- **Molette** = défilement ; **Maj + molette** = horizontal ; **Ctrl + molette** = zoom (pincement).
- **Clavier** : le texte tapé, Ctrl+V (collage), Entrée, Effacer, Échap (= Retour) vont dans le champ de texte actif du téléphone.
- Boutons : Retour, Accueil, Récents, Notifications, Réglages rapides, Vol +/−, Réveil, Verrou, **PNG** (capture d'écran).
- Réglages à chaud : taille maximale (1280 à native), images/s (24/30/60), débit (2 à 30 Mbit/s), son, **Mute tel** (coupe le haut-parleur du téléphone, le son reste envoyé au PC), **Pas de notif** (mode « Alarmes seulement » pendant la prise, plus de bandeaux dans l'image ; à autoriser une fois sur le téléphone avec le bouton « Autoriser Ne pas déranger »), sortie audio (par exemple un câble audio virtuel pour OBS), muet.
- La fenêtre se rouvre à la taille et à la position où vous l'avez laissée (sauf barres masquées).
- **OBS Studio** : **F10** masque les barres, il ne reste que l'écran du téléphone, dans une fenêtre qui garde ses proportions → source « Capture de fenêtre ». La case *Montrer le doigt* dessine un cercle sous la souris pendant le clic.

## Sécurité
Le téléphone n'accepte une connexion qu'avec le code à 6 chiffres (échange par défi/réponse SHA-256, le code ne circule pas), une seule connexion à la fois, blocage d'une minute après 5 erreurs. *Nouveau code* dans l'application le change. Le trafic Wi-Fi n'est pas chiffré : à utiliser sur un réseau de confiance. Le service d'accessibilité est puissant : désactivez-le quand vous ne vous en servez pas.

## Limites
- Contenu protégé (applications bancaires, certaines vidéos) : écran noir. Certaines applications interdisent la capture du son.
- Les touchers passent par un service d'accessibilité (pas de droits root/adb) : les glissements sont un peu moins fluides qu'avec scrcpy.
- Android redemande l'autorisation de capture à chaque démarrage du partage.
- Le son du téléphone sort aussi sur le téléphone, sauf si vous cochez « Mute tel » (le volume média du téléphone est alors mis à 0 pendant la prise, puis rétabli).

## Compiler
- Android : `android\build.ps1` (JDK, Gradle et SDK Android portables ; le chemin de la chaîne d'outils de PhoneSDRLink est en tête du script). Clé de signature `screenlink.jks` non publiée (l'APK n'est signé que si elle existe).
- PC : `pc\build.ps1` (compilateur C# fourni avec Windows, aucune dépendance). Icônes : `tools\makeicon.ps1`.

Protocole et notes de projet : [LISEZMOI.md](LISEZMOI.md). Licence GPL v3.

---

## English

View and control an Android phone screen from a Windows PC, **video + audio**, over **Wi-Fi** (same network) or **USB**. Made for recording app demo videos with **OBS Studio** (window capture of ScreenLinkPC).

- `ScreenLink-1.3.apk` – install on the phone (Android 8+, audio needs Android 10+).
- `ScreenLinkPC.exe` – run on Windows 10/11 (single file).

**Setup**: install the APK → *Start sharing* (accept entire-screen capture) → *Enable mouse control* and turn on the **ScreenLink** accessibility service → on the PC enter the phone IP and the 6-digit code (Wi-Fi) or choose USB (needs `adb.exe`, USB debugging on) → *Connect*.

**Use**: the Connect button is red while not connected; the **Auto** checkbox reconnects every 15 s after a lost connection. The window reopens with the size and position you left it. Left click = finger (tap/drag/long press), right click = Back, middle click = Home, wheel = scroll (Shift = horizontal, Ctrl = pinch zoom), typed text and Ctrl+V go to the focused text field. **F10** hides the toolbars for a clean OBS window capture. Live settings: size, fps, bitrate, audio, audio output device, **Mute tel** (phone silent, audio still sent), **No notif** (phone in “Alarms only” during capture, allow “Do not disturb” once in the app). A red stop button on the phone stops sharing and quits the app.

**Security**: 6-digit code (SHA-256 challenge/response), one connection at a time, 1-minute lockout after 5 failures. Wi-Fi traffic is not encrypted: use a trusted network.

**Limits**: protected content shows black; some apps block audio capture; gestures go through an accessibility service so drags are less smooth than scrcpy; Android asks for capture permission at each start.

GPL v3.
