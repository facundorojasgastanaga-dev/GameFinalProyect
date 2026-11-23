# Audio · cinemática de inicio (Room00_Hangar · despertar del T-07)

Hoja de cues y plan de mezcla del 18/11/2026. Petición del usuario: «mejora los sonidos de la cinemática de inicio».
La mejora se hace **añadiendo capas**. La línea de tiempo `Room00_WakeUp.playable`, `WakeUpCinematic`, `CinematicAudio`, los scripts
`Scripts/HangarIntro/*` y la imagen, el ritmo y la duración de la cinemática (27,8 s) **no se tocan**.

| Pieza | Ruta |
|---|---|
| Síntesis reproducible (biblioteca estándar, semilla fija) | `Tools/IronRequiem/Audio/synth_hangar_intro.py` |
| Sonidos nuevos (48 kHz, 16 bits) | `Assets/IronRequiem/Audio/SFX/HangarIntro_v2/IR2_*.wav` (18) |
| Runtime | `Assets/IronRequiem/Scripts/Audio/HangarIntroSoundscape.cs` |
| Autoría | `Assets/IronRequiem/Editor/Map01/HangarIntroAudioAuthoring.cs` · menú `Tools/Iron Requiem/Map01/Hangar/Build cinematic soundscape` |
| Informes | `Tools/IronRequiem/Reports/Map01/Hangar/synth_hangar_intro.txt` y `soundscape_build.txt` |

## 1. Idea sonora

El robot despierta **desde el silencio**: en negro solo hay un zumbido eléctrico y una sala que respira. Con el visor llega su
propio cuerpo (arranque, latido del reactor), y el hangar se abre alrededor (goteo, maquinaria lejana, viento metálico).
Los dos golpes al cristal se preparan con un **vacío** (la sala y la música se apartan) y caen con un sub-impacto. La salida es
pesada: anclajes que se sueltan, vapor y una cuna que cruje. La mano se escucha de cerca (escaneo, ambiente más bajo). Los
Carroñeros aparecen con un **acento grave y disonante**, un rebusque metálico real desde su chatarra y el «fijado» del visor.
Al final la cámara entra en la cabeza con una ráfaga que acelera y se corta en seco: «visor en línea», y el ambiente sigue en
juego más bajo, sin pisar la música.

## 2. Estado medido antes del cambio

- Línea de tiempo: duración fija **27,8 s** (`m_FixedDuration`). La pista `Sfx` tiene **15 clips**, no 9:
  `IR_Hangar_PowerUp` 0,70 · `Servo` 3,30 / 5,15 / 6,90 / 9,00 / 11,05 / 14,30 / 15,80 / 18,40 / 22,00 · `Hydraulic` 6,30 / 7,90 / 8,05 / 13,30 ·
  `Spark` 6,55. El servo suena **9 veces** y es el clip más fuerte (RMS -10,6 dBFS); `GlassCrack` (-26) y `PowerUp` (-27) son
  los más flojos. De ahí el efecto «repetitivo y plano».
- Tiempos de la pose (`WakeUpRobotPose.Timing` en la escena): visor 0,8 · dedos 2,4–3,4 · **puñetazos 4,0 (grieta) y 5,6 (estallido)** ·
  anclajes 6,3 · cables 6,55 · cuna 6,9–8,2 · sale 8,0 · pasos 8,9–12,05 · agachado 12,05 · de pie 13,6 · **mano 14–18** ·
  mira la cinta 18–20,5 · **se gira hacia los Carroñeros 22–24** · entrada en la cabeza 25–27,8.
- Planos (`Shots`): A_Visor 0–3,6 · B2_Glass 3,6–6,3 · B3_Release 6,3–8,3 · C_Exit 8,3–14 · D_Hand 14–18 · D2_Belt 18–22 ·
  E_Reveal 22–25,6 · F_ToHead 25–27,8.
- Debajo de la cinemática ya suenan, desde la escena de sesión: la música de exploración `CombatMusic` (grupo Music, 0,8) y
  `Wind Ambience` de la plantilla (GameManager, grupo Ambient). Room00 no tenía ningún ambiente propio en bucle.

## 3. Capas, grupos y espacialización

| Capa | Contenido | Grupo del `MainAudioMixer` (sin modificar) | Espacio |
|---|---|---|---|
| ambiente | tono de sala, zumbido de lámpara, goteo, maquinaria | Ambient | 2D (tono) y 3D (resto) |
| robot | arranque, latido, pitidos, escaneo | General | 2D: es el cuerpo/visor del protagonista |
| cápsula | anclajes, cerrojo, vapor, crujido, cables | General | 3D en la cápsula (spatial 0,75) |
| impactos | puños, sub-impactos, succión, lluvia de cristal, pisadas con peso | Impact | sub/succión 2D; el resto 3D |
| revelación | acento grave + rebusque y chapa en la chatarra + fijado | General | acento y pitidos 2D; chatarra 3D |
| transición | ráfaga de entrada en la cabeza, «visor en línea» | General | 2D |

Emisores (hijos de `HG_CinematicSoundscape`, colocados en la posición leída de la escena; nada se reparenta):
`Emit_CapsuleGlass` (= `Capsule_C04/CanopyBreak/GlassAudio`), `Emit_CapsuleClamps` (= `Capsule_C04`),
`Emit_ScavengerScrap` (centroide de `HangarSector/ScrapPoints` + 1 m), `Bed_LampHum` (= `OverheadLamp`),
`Bed_Machinery` (= `ConveyorPit/UnderBeltGantry`), `Bed_Drip` (el `ScrapPile*` más cercano al `StandPoint` a ≥ 4 m).
Doppler 0 en todo (la cámara vuela en F_ToHead).

## 4. Hoja de cues

Volumen = `AudioSource.volume` (0–1) del cue; los WAV vienen normalizados (one-shots a -1/-3 dBFS de pico).

| t (s) | Momento | Qué se oye | Capa | Vol | 2D/3D · objeto | Por qué |
|---|---|---|---|---|---|---|
| 0–0,8 | negro | solo zumbido de lámpara (0,25) y tono de sala entrando | ambiente | — | 3D lámpara · 2D | arrancar del silencio; el oído se abre antes que el ojo |
| 0,85 | el visor parpadea | **arranque**: relé, tono 70→420 Hz con tartamudeo, 3 pitidos, «listo», datos | robot | 0,75 | 2D | se encadena con `PowerUp` (0,7) y da carácter de máquina |
| 1,0→ | — | **latido del reactor** (40 lpm) entra y crece | robot | cama | 2D | pulso vital del protagonista, hilo conductor hasta la revelación |
| 2,45 · 3,2 | dedos | pitidos de visor | robot | 0,30 · 0,26 | 2D | microrreacción de los sistemas al primer movimiento |
| 3,6–4,0 | prepara el golpe | la sala baja -6 dB | dinámica | — | — | silencio antes del golpe |
| 4,0 | **puñetazo 1 (grieta)** | puño pesado (V4 `IR_Fist_HeavyImpact` a 0,9) + sub corto (pitch 1,3) | impactos | 0,65 · 0,40 | 3D cristal · 2D | el crack existente es débil (-26 dBFS RMS) |
| 4,95–5,6 | **vacío** | sala -14 dB, música -14 dB, el latido cae | dinámica | — | — | contraste máximo para el estallido |
| 4,98 | carga | **succión** inversa que se corta justo a 5,6 | impactos | 0,75 | 2D | anticipa el golpe |
| 5,6 | **estallido del cristal** | **sub-impacto** 85→30 Hz + puño (pitch 0,76) | impactos | 1,0 · 0,85 | 2D · 3D cristal | pico de la cinemática; ducking -9 dB de camas y música |
| 5,68 | | **lluvia de cristales** que rebotan | impactos | 0,85 | 3D cristal | da cola y materia al shatter existente |
| 5,95 | | `IR_Hangar_Clatter` (pitch 0,9) | impactos | 0,45 | 3D cristal | restos metálicos al caer |
| 6,3 | anclajes | **anclaje** pesado + cerrojo (V4 `IR_Mount_Lock` a 0,8) + vapor corto | cápsula | 0,9 · 0,45 · 0,4 | 3D anclajes | peso: se oye que sujetaban algo grande |
| 6,46 · 6,62 | | dos anclajes más (pitch 0,92 / 1,06) | cápsula | 0,75 · 0,62 | 3D anclajes | secuencia mecánica, no un único golpe |
| 6,55 | cables | `IR_Hangar_Tear` (pitch 1,15) | cápsula | 0,4 | 3D anclajes | los conectores de la espalda se arrancan |
| 6,9–8,2 | la cuna se inclina | **crujido** de metal bajo tensión | cápsula | 0,65 | 3D anclajes | el peso del robot sobre la cuna |
| 7,88 | sale (vapor de `podSteam`) | **vapor** presurizado con tartamudeo final | cápsula | 0,9 | 3D anclajes | sincronizado con las partículas a `leave`-0,1 |
| 8,2 | tope de la cuna | `Clatter` (pitch 0,7) + golpe de peso | cápsula/impactos | 0,5 · 0,6 | 3D anclajes | la cuna llega al final con masa |
| 8,9–13,6 | pasos | **peso** sub en cada contacto real de pie (`FootContactFx.Contact`) | impactos | 0,7 | 3D en el pie | las pisadas metálicas existentes no tenían graves |
| 13,8–18 | la mano | sala -4 dB, latido más presente (0,62) | dinámica | — | — | plano íntimo |
| 14,15 · 14,5 · 16,25 · 17,7 | estudia la mano | pitido · **escaneo** · escaneo (pitch 1,12) · pitido de fin | robot | 0,35 · 0,5 · 0,38 · 0,32 | 2D | el robot analiza su cuerpo |
| 18–22 | mira la cinta | maquinaria más presente (0,7) | ambiente | cama | 3D cinta | la cámara mira hacia ella |
| 21,7 | se va a girar | **acento** (dron E1/F1 disonante, metal frotado) crece; camas -6 dB | revelación | 0,8 | 2D | anticipación |
| 22,95 | se gira hacia ellos | rebusque fuerte (`IR_Hangar_Rummage2`) | revelación | 0,9 | 3D chatarra | los Carroñeros se oyen antes de verse del todo |
| 23,2 | **los ve** | golpe del acento (campana grave + sub), latido 0,75 y pitch 1,15 | revelación | — | 2D | el pico emocional de la segunda mitad |
| 23,2 · 23,42 | | doble pitido grave de «fijado» (pitch 0,75) | revelación | 0,4 | 2D | el visor los marca: son una amenaza |
| 23,65 | | chapa arrancada (`IR_Hangar_Tear` a 0,85) | revelación | 0,7 | 3D chatarra | siguen rebuscando, no lo han visto |
| 25,0 | la cámara vuela a la cabeza | **ráfaga** que acelera, riser tonal; camas -8 dB, música abajo | transición | 0,85 | 2D | túnel sonoro que acompaña la aceleración k² |
| 27,78 | entra en la cabeza | «sellado» del casco: golpe + clic (dentro del mismo WAV); el latido ya se ha ido (26,6) | transición | — | 2D | corte en seco = estamos dentro |
| fin (27,8) | primera persona | **visor en línea** (clic + arpegio suave) | transición | 0,5 | 2D | primer sonido del control del jugador |

## 5. Camas (bucles)

| Cama | Clip | 2D/3D · objeto | Curva en la cinemática (s:vol) | Juego / combate |
|---|---|---|---|---|
| `Bed_RoomTone` | `IR2_Amb_HangarTone_Loop` (20 s, estéreo) | 2D | 0:0 · 0,6:0,08 · 3,6:0,42 · 18:0,42 · 21,5:0,4 · 25:0,32 · 27,8:0,2 | 0,30 / 0,10 |
| `Bed_LampHum` | `IR2_Amb_ElecHum_Loop` (6 s) | 3D lámpara (2–18 m) | 0:0,25 · 0,9:0,35 · 3,6:0,6 · 25:0,55 · 27,8:0,35 | 0,50 / 0,20 |
| `Bed_Drip` | `IR2_Amb_Drip_Loop` (11 s) | 3D montón de chatarra (3–25 m) | 0:0 · 1,5:0 · 3,6:0,6 · 27,8:0,6 | 0,55 / 0,20 |
| `Bed_Machinery` | `IR2_Amb_MachineFar_Loop` (12 s, prensa cada 1,5 s) | 3D cinta (8–60 m, spread 60) | 0:0 · 3:0 · 6:0,55 · 18:0,7 · 21,5:0,6 · 27,8:0,45 | 0,60 / 0,20 |
| `Bed_ReactorHeart` | `IR2_Robot_Heart_Loop` (1,5 s) | 2D (grupo General) | 1:0 · 2,6:0,32 · 4,9:0,42 · 5,45:0,12 · 6,3:0,45 · 14,5:0,62 · 18:0,45 · 23,2:0,75 · 25,6:0,3 · 26,6:0 · pitch 0,9→1→1,15 | 0 / 0 |

Las camas arrancan en un punto aleatorio del bucle. Los bucles se cierran sin costura (ver §8).

## 6. Curva de dinámica

```
nivel relativo de las camas (dB)            · = cama   # = golpe/acento
  0 |            ......           ...................   ....... ....
 -4 |      ......      ...       .                 ....       .    ...      (mano: -4)
 -6 |    ..               .     .                                       .  (revelación: -6)
-14 |  ..                  .   .                                           (vacío 4,95-5,6: -14; entrada cabeza -8)
    +--#-----------#-----##----#--------------------------------#-------#--> t
       0,85        4,0   5,6   6,3                              23,2    27,8
       arranque   grieta  estallido anclajes                   Carroñeros  1ª persona
```

Ventanas de dinámica (deterministas en el tiempo de la cinemática; si coinciden manda la más profunda, no se suman):

| Ventana | Inicio | Ataque / mantener / soltar | Profundidad | Música |
|---|---|---|---|---|
| D1 antes de la grieta | 3,60 | 0,3 / 0,1 / 0,5 | -6 dB | no |
| D2 vacío antes del estallido | 4,95 | 0,5 / 0,15 / 1,9 | -14 dB | sí |
| D3 mano | 13,80 | 1,0 / 3,0 / 1,2 | -4 dB | no |
| cue I02 grieta | 4,00 | 0,02 / 0,1 / 0,9 | -5 dB | no |
| cue I04 estallido | 5,60 | 0,02 / 0,35 / 1,6 | -9 dB | sí |
| cues C01 / C08 anclajes y vapor | 6,30 / 7,88 | cortos | -3 dB | no |
| cue V01 acento | 21,70 | 1,2 / 1,0 / 2,5 | -6 dB | sí |
| cue T01 ráfaga | 25,00 | 0,8 / 1,8 / 0,4 | -8 dB | sí |

Música de campaña: mientras dura la cinemática, `CombatMusic` queda a **-9 dB** (más bajo en las ventanas marcadas «música»)
para que existan los silencios y los golpes. Se hace multiplicando en `LateUpdate` el volumen que `CombatMusic` escribe en sus
fuentes cada `Update` (sin acumulación, sin tocar la clase ni el mezclador). Al terminar vuelve a 0 dB en 3 s. Se desactiva
con `duckMusicDuringCinematic`.

## 7. Saltar, terminar, reiniciar y jugar

- **Saltar** (E/Espacio/Sur mantenido; `WasSkipped` pasa a true): no se disparan más cues; los programados que aún no habían
  sonado se cancelan; los que suenan se funden en **0,3 s** (la imagen funde a negro en 0,25 s); las camas pasan del valor de la
  curva al nivel de juego en **1 s** con curva suave; el latido se apaga en ese tiempo; «visor en línea» suena más bajo (0,32)
  a los 0,25 s, cuando vuelve la imagen. Nunca hay un `Stop` en seco de algo audible.
- **Terminar** (sale de `IsPlaying` sin salto): los cues suenan hasta su cola y se funden en 1,5 s; «visor en línea» a 0,5;
  las camas bajan al nivel de juego en **2,5 s**; la música vuelve en 3 s.
- **Reiniciar tras morir** (`CombatLabMenu.ResumedAfterRestart`: la cinemática va directa al estado final sin reproducirse):
  el componente nunca ve `IsPlaying`, solo `WasSkipped`/`Finished`, y entra directamente en el ambiente de juego (fundido de
  entrada 1,5 s). Ningún cue de cinemática suena. Lo mismo si falta la cinemática.
- **En juego**: las camas siguen a nivel de juego (tabla §5); si `CombatMusic.InCombat`, bajan al nivel de combate en 1,5 s
  para no pisar la música de combate. Fuera del hangar (límites del sector + 8 m de fundido, medido en la cámara del jugador)
  se apagan, porque el tono de sala es 2D y si el sector sigue cargado no debe acompañar al jugador a otras salas.
- **Pausa** (`Time.timeScale` 0 y `AudioListener.pause`): el componente no avanza; las fuentes se congelan con el listener.

## 8. Sonidos nuevos (`IR2_*`, 48 kHz / 16 bits, normalizados)

| Archivo | Dur. | Canales | Importación | Descripción |
|---|---|---|---|---|
| IR2_Amb_HangarTone_Loop | 20,0 s | estéreo | Vorbis 0,6 · Streaming · fondo | zumbido 47 Hz (batido L/R), retumbo marrón, viento metálico en 3 resonancias, aire, golpes y crujidos lejanos reverberados |
| IR2_Amb_Drip_Loop | 11,0 s | mono | Vorbis 0,7 · Compressed In Memory | gotas en charco (subida de tono) y en chapa (modal) con sala |
| IR2_Amb_MachineFar_Loop | 12,0 s | mono | Vorbis 0,7 · CIM | prensa cada 1,5 s, cadena, motor 92 Hz, todo oscuro y lejano |
| IR2_Amb_ElecHum_Loop | 6,0 s | mono | Vorbis 0,7 · CIM | zumbido 100 Hz rico en impares, aleteo y chisporroteo |
| IR2_Robot_Boot | 3,4 s | mono | Vorbis 0,7 · CIM | ver §4 |
| IR2_Robot_Heart_Loop | 1,5 s | mono | ADPCM · Decompress On Load | lub-dub 78→42 Hz con saturación suave |
| IR2_Robot_Blip | 0,25 s | mono | ADPCM · DOL | dos notas 1760/2349 Hz |
| IR2_Robot_Scan | 1,6 s | mono | Vorbis 0,7 · CIM | barrido filtrado con trémolo de escáner y datos |
| IR2_Robot_StepWeight | 0,7 s | mono | ADPCM · DOL | sub + resonancia de suelo metálico |
| IR2_Capsule_Steam | 3,6 s | mono | Vorbis 0,7 · CIM | válvula + chorro que se apaga tartamudeando |
| IR2_Capsule_Clamp | 1,1 s | mono | ADPCM · DOL | chasquido, golpe modal inarmónico, pestillo, asiento |
| IR2_Capsule_Creak | 2,4 s | mono | Vorbis 0,7 · CIM | fricción stick-slip por resonancias estrechas |
| IR2_Impact_SubBoom | 2,4 s | mono | Vorbis 0,7 · CIM | 85→30 Hz, transitorio, cola oscura |
| IR2_Impact_PreSuck | 0,62 s | mono | ADPCM · DOL | cola de impacto invertida |
| IR2_Impact_GlassDebris | 2,6 s | mono | Vorbis 0,7 · CIM | trozos y ~85 esquirlas que se asientan |
| IR2_Reveal_Sting | 5,0 s | estéreo | Vorbis 0,7 · CIM | dron E1/F1/B1 (batido 2,4 Hz), metal frotado, campana grave y sub a 1,5 s |
| IR2_Trans_Whoosh | 3,0 s | estéreo | Vorbis 0,7 · CIM | ráfaga 180→8100 Hz con aceleración, riser, corte y sellado a 2,78 s |
| IR2_Trans_HudOnline | 0,9 s | mono | ADPCM · DOL | clic + E5-B5-E6 |

Regla de importación (patrón de `unity:optimize-audio`): ≤ 1,5 s ADPCM + Decompress On Load; ≤ 12 s Vorbis 0,70 + Compressed
In Memory con precarga; > 12 s Vorbis 0,60 + Streaming con carga en segundo plano. Mono forzado salvo las tres capas 2D estéreo;
frecuencia de muestreo preservada. Los clips reutilizados (`IR_Fist_HeavyImpact`, `IR_Mount_Lock`, `IR_Hangar_Clatter`,
`IR_Hangar_Tear`, `IR_Hangar_Rummage2`) no cambian su importación.

Calidad: procesado sobre búferes largos antes de cerrar el bucle; bucles cerrados plegando las colas de los eventos
(convolución periódica) y con fundido de potencia constante en el ruido; tonos con ciclos enteros en la longitud del bucle;
one-shots que empiezan y acaban en cero; sin continua; dither TPDF. Salto en la costura medido ≤ 1,8× el salto medio entre
muestras (inaudible). Ejecutar dos veces da los mismos SHA-256.

## 9. Riesgos y pendientes

- **No escuchado en Unity**: falta comprobar en Play Mode (JUGAR → cinemática completa, salto y reinicio tras morir) los niveles
  relativos frente a la pista existente y la música, y que la ráfaga cae con la entrada en la cabeza.
- El acento de los Carroñeros se ancla a 23,2 s (mitad del giro 22–24): ajustar `V01`–`V05` si en imagen se ven antes o después.
- La pista existente sigue repitiendo el servo 9 veces; reducirlo exige editar `Room00_WakeUp.playable` (zona protegida).
  Las capas nuevas lo enmascaran pero no lo eliminan.
- El ducking de la música multiplica las fuentes de `CombatMusic` en `LateUpdate`; si algún día otra cosa escribe esas fuentes,
  revisar o desactivar `duckMusicDuringCinematic`.
- `Wind Ambience` de la plantilla sigue sonando en el grupo Ambient por debajo de todo; no se toca.
