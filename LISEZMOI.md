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
Serveur vers PC : 1 config vidéo (L,H sur 2 octets + SPS/PPS), 2 image (pts 8 + clé 1 + Annex B), 3 config audio, 4 audio PCM, 5 infos texte `clé=valeur`.
PC vers serveur : 10 toucher (action, x, y flottants 0-1), 11 molette, 12 touche, 13 texte UTF-8, 14 réglages (taille, fps, débit, son), 15 image clé, 16 ping (2 s ; coupure après 8 s de silence).
Dimensions encodées multiples de 16 (pas de bordure de décodage) ; les coordonnées sont normalisées.

## État V1.0 (2026-11-08)
Code complet, compile (APK 1.0 signé, exe x64). **Pas encore essayé de bout en bout sur le téléphone** au moment de la publication : à valider (service d'accessibilité, fluidité des glissements, couleurs BT.709 vs 601 : `VideoView.Bt709`, son, rotation, mode USB). Corriger puis publier une 1.1.

## Chaîne d'outils
`android\build.ps1` utilise `J:\claude\SDRLINK Smartphone\toolchain` (JDK, Gradle, SDK) ; `android\local.properties` (non publié) donne `sdk.dir`. Clé `screenlink.jks` (mot de passe `screenlink`) hors dépôt.
