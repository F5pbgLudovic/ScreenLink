# ScreenLink — notes de projet (F5PBG)

Demande du 2026-11-08 : application smartphone pour voir l'écran + le son du téléphone sur le PC (Wi-Fi ou USB) et le commander à la souris, pour filmer des présentations avec OBS.

## Architecture
- **Android** (`android\`, Java pur, package `fr.f5pbg.screenlink`, minSdk 26, targetSdk 34) :
  - `CaptureService` : service au premier plan (type mediaProjection), serveur TCP port **27183**, un seul client, gère le VirtualDisplay (créé une fois, redimensionné ensuite : Android 14 interdit d'en recréer un), rotation via DisplayListener.
  - `Session` : défi SHA-256(nonce + code), encodeur MediaCodec H.264 sur surface (1 image clé/s, répétition d'image 100 ms), capture audio AudioPlaybackCapture (PCM 48 kHz stéréo 16 bits, 20 ms), file d'envoi avec abandon des images en retard puis attente d'une image clé.
  - `InputService` : service d'accessibilité : doigt virtuel par traits continus `continueStroke`, molette = balayage/pincement, touches globales, saisie par ACTION_SET_TEXT.
  - `MainActivity` : IP, code, démarrage/arrêt ; extra `--ez start true` pour démarrer par adb.
- **PC** (`pc\src`, C# .NET Framework 4, csc, x64) : `Link` (protocole), `MfDecoder` (décodeur H.264 Media Foundation via COM, sortie NV12, faible latence), `VideoView` (NV12 vers BGRA BT.709 parallèle, souris vers touchers), `AudioOut` (waveOut, 60 ms de préremplissage, rattrapage au-delà de 300 ms), `Adb`, `MainForm`.

## Protocole (TCP, trames `[type:1][longueur:4 grand-boutiste][données]`)
Serveur : `"SLNK"`+version(1)+nonce(16) ; client : SHA-256(nonce+code) ; serveur : 0 OK, 1 code faux, 2 occupé, 3 bloqué.
Serveur vers PC : 1 config vidéo (L,H sur 2 octets + SPS/PPS), 2 image (pts 8 + clé 1 + Annex B), 3 config audio, 4 audio PCM, 5 infos texte `clé=valeur` (name, input, audio ; dnd=0|1 renvoyé après les réglages : « Ne pas déranger » autorisé ou non).
PC vers serveur : 10 toucher (action, x, y flottants 0-1), 11 molette, 12 touche, 13 texte UTF-8, 14 réglages (taille 2 o, fps, débit 4 o, son, Mute tel, Pas de notif : les deux derniers octets sont facultatifs), 15 image clé, 16 ping (2 s ; coupure après 8 s de silence).
Dimensions encodées multiples de 16 (pas de bordure de décodage) ; les coordonnées sont normalisées.

## État V1.3 (2026-11-10, publiée ; l essentiel des essais date de la V1.1)
V1.0 publiée sans essai de bout en bout. **Essais du 2026-11-08 sur le OnePlus (Android 12) avec un banc de test PC (mêmes Link/MfDecoder/VideoView) : connexion, image H.264 décodée (864x1920, couleurs BT.709 correctes), son capté (YouTube), touche Accueil et glissement exécutés par le service d accessibilité : OK.** V1.1 : case « Mute tel » (volume média du téléphone à 0 pendant la prise, son toujours transmis au PC, volume rétabli à la déconnexion) ; il manquait la permission MODIFY_AUDIO_SETTINGS (sans elle setStreamVolume est ignoré en silence : les boutons Vol +/− de la 1.0 ne marchaient pas non plus) ; setStreamVolume(0) puis repli sur ADJUST_MUTE. Reste à essayer : fenêtre PC réelle avec souris, mode USB complet, rotation, clavier/texte, Android 14.

## Chaîne d'outils
`android\build.ps1` utilise `J:\claude\SDRLINK Smartphone\toolchain` (JDK, Gradle, SDK) ; `android\local.properties` (non publié) donne `sdk.dir`. Clé `screenlink.jks` (mot de passe `screenlink`) hors dépôt.

**V1.1 — corrections vérifiées par F5PBG (connexion stable)** : fil de décodage séparé et double tampon côté PC, son waveOut avec 16 tampons ; côté téléphone file son prioritaire, image envoyée d'une traite (l'envoi par morceaux laissait s'intercaler du son au milieu d'une image : « trame invalide », déconnexion immédiate), AudioTrack silencieux contre le gel HANS du OnePlus ; fenêtre PC qui garde le ratio du téléphone à l'agrandissement comme à la réduction.

**V1.2 (2026-11-09)** :
- **Case « Pas de notif »** (PC, 10e octet des réglages) : le téléphone passe en « Alarmes seulement » (`INTERRUPTION_FILTER_ALARMS`) pendant la prise, plus de bandeaux dans l'image ; le filtre d'avant est mémorisé (`dndPrev` dans les préférences `sl`) et rétabli à la déconnexion, à l'arrêt du service ou au prochain arrêt. Permission `ACCESS_NOTIFICATION_POLICY` + bouton « Autoriser Ne pas déranger » dans l'appli (accès à donner une fois). Sans cet accès, le téléphone répond `dnd=0` et le PC affiche un avertissement. **Essayée par F5PBG (2026-11-09) : le son média reste bien transmis en mode Alarmes.**
- **Rotation portrait/paysage** : le décodage était correct (essais avec un banc `rot.exe`), le défaut était la fenêtre PC. `FitWindow` calcule la hauteur des barres avec la largeur cible (itération), `FixRows()` fixe la hauteur des rangées de boutons à chaque `OnResize` (les `FlowLayoutPanel` AutoSize ancrés en haut ne recalculent pas leur hauteur), second ajustement 250 ms après un changement de configuration vidéo. Essayé dans les deux sens sur le OnePlus.
- **Bouton rouge d'arrêt** sur le téléphone (à droite de l'état « PC connecté ») : arrête le service (volume et « Ne pas déranger » rétablis), ferme l'appli, retire la tâche des récents et termine le processus.
- Version 1.2 (code 3) affichée dans l'appli, l'APK et le titre de la fenêtre PC.
- À savoir : les antivirus (AVG, Norton) signalent l'APK comme « suspect » à chaque installation (capture d'écran + accessibilité + hors Play Store) ; il faut le déclarer sûr. Après `adb install -r` ou une désinstallation, le service d'accessibilité est à réactiver et un nouveau code est généré après désinstallation.
- Reste à essayer : mode USB complet, saisie de texte, Android 14.

**V1.3 (2026-11-10)** :
- **Bouton Connecter coloré** (`UpdateConnectUi`) : rouge sans connexion, orange pendant l'essai (« Annuler »), vert une fois connecté (« Déconnecter »).
- **Case « Auto »** (réglage `Auto` dans `%APPDATA%\ScreenLinkPC\settings.ini`) : reconnexion automatique toutes les 15 s après une perte (`RetryTick`, minuterie de 1 s, `RETRY_S` = 15, compte à rebours dans la barre d'état). `wantLink` = l'utilisateur a demandé la connexion et ne l'a pas coupée lui-même ; pas de nouvel essai après un clic sur Déconnecter/Annuler, après « Code incorrect » (le téléphone bloque 1 min après 5 erreurs) ni si le code ou l'IP manque. Essayé en vrai : arrêt du partage par le bouton rouge du téléphone, relance, le PC se reconnecte seul au prochain essai. Non essayé en mode USB.
- **Fenêtre PC mémorisée** : taille et position sont enregistrées à chaque fin de redimensionnement et à la fermeture (pas quand les barres sont masquées par F10) et reprises à l'ouverture (`restoredGeom`). À l'arrivée de la première image, `FitFirst` ne touche la fenêtre que si ses proportions ne correspondent plus à celles du téléphone (écart > 3 %) ; sans réglage mémorisé, la fenêtre est dimensionnée sur le téléphone et centrée comme avant. Largeur minimale 430 px.
- Version 1.3 (code 4) dans l'appli, l'APK et le titre de la fenêtre PC.
- Pièges de test : `adb` sous Git Bash réécrit `/sdcard` (utiliser `MSYS_NO_PATHCONV=1`) ; pas de redirection binaire par PowerShell ; rotation forcée par `adb shell cmd window user-rotation lock 0|1` (`free` pour rendre la main) ; le téléphone se reverrouille tout seul, vérifier `mWakefulness` avant un essai ; les antivirus (AVG, Norton) réagissent à chaque installation de l'APK.
- Reste à essayer : mode USB complet (et reconnexion en USB), saisie de texte, Android 14.
