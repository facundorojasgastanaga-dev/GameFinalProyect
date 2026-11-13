# Escopeta secundaria, cooldown del slide, umbral de ejecución y rendimiento (04/11/2026)

Alcance: las cinco tareas pedidas el 04/11/2026. Laboratorio `Assets/IronRequiem/Scenes/SecondaryShotgunLab.unity` (copia de CombatFollowupLab) y escena de medición `SecondaryShotgunBenchmark.unity`. MainScene, CombatFollowupLab, prefabs de plantilla, Player_ModularArm_v07, Packages y ProjectSettings.asset sin cambios (hash comparado con `Tools/IronRequiem/Backups/20261104-shotgun-slide-exec`). EditorBuildSettings incluye el laboratorio nuevo (intencional). No se instalaron paquetes ni se controló el escritorio.

## 1. Escopeta secundaria

Origen: `Assets/FPS/Prefabs/Weapons/Weapon_Shotgun.prefab` (la escopeta conservada fuera del inventario desde v02). Variantes nuevas en `Assets/IronRequiem/Prefabs/SecondaryShotgun/`: `Weapon_Shotgun_Secondary` y `Projectile_Shotgun_Secondary`. Mismo WeaponController, modelo, audio, fogonazo y proyectil heredados; no hay otro sistema de armas.

Inventario del jugador del laboratorio: ranura 1 arma principal `Weapon_IR_ArmModule_v07`, ranura 2 escopeta. Las armas robadas ocupan el primer hueco libre (ranura 3). Teclas 1/2, rueda, E, F y G sin cambios.

| Valor | Anterior (plantilla) | Final |
|---|---|---|
| Perdigones | 24 | 10 |
| Daño por perdigón | 7 | 9 |
| Daño máximo por disparo | 168 | 90 |
| Dispersión | 7° | 6° |
| Intervalo entre disparos | 0,2 s | 0,85 s |
| Cargas / recarga automática | 2 · 1,1/s tras 1 s | 4 · 1/s tras 1 s |
| Alcance | ~750 m nominales (150 m/s × 5 s) | 32 m exactos |
| Caída de daño | ninguna | 100 % hasta 6 m → 30 % desde 18 m; 0 más allá de 32 m |

Daño medio medido sobre T-07 (128 HP), 3 disparos por distancia: 3 m = 90; 12 m = 26,2; 22 m = 2,7; 40 m = 0. Mata un T-07 con dos disparos a quemarropa; a media distancia rinde menos que la principal (6,5 × 10/s).

Código: `ProjectileDistanceFalloff` (nuevo) + gancho opcional `IProjectileHitDamageModifier` en `ProjectileStandard.OnHit` (sin componente = comportamiento anterior).

## 2. Cooldown del slide

`AdvancedPlayerMovement`: `slideCooldown` (por defecto 0 = comportamiento anterior; 1,5 s en el laboratorio) contado desde el final del slide, y `slideRequiresRelease` (activo en el laboratorio). Durante el cooldown, una pulsación que cumpliría el resto de requisitos se consume: no hay slide ni agachado accidental en carrera. Requisitos de Shift + C, velocidad mínima y suelo sin cambios; animaciones, piernas y chispas intactas. `MechanicInput` expone el estado de C.

Medido: slide 0,696 s; cooldown restante al terminar 1,500 s; C mantenida 1,8 s sin segundo slide; 12 pulsaciones en 1,3 s sin slide ni agachado; primer slide válido a 1,59 s. Agacharse caminando y Shift + C en el aire siguen igual.

## 3. Ejecución con E

La regla ya existía en `EnemyExecutionSource` (vida > 0 y estrictamente < 75 %). Verificado en T-07 y Big Bot: a exactamente 75 % (96/128, 720/960) no hay aviso y E no hace nada; por debajo aparece el aviso; si vuelve a 75 % antes de pulsar E, se rechaza; una sola entrega aunque se pulse E varias veces. No hizo falta cambiar código.

## 4. Animaciones

- Escopeta: la mano derecha flotaba encima del arma y la atravesaba. Ahora se acopla al antebrazo modular como el arma principal (`ProtagonistHandsPresentation`, representación `Mounted`, lista `mountedWeaponPrefabs`). La variante tiene un `Attachment` trasero en el eje del cañón, escala 0,4 respecto a la plantilla y AimOffset (0; 0,055; 0,1). Con ADS, el borde superior queda en 0,34 del viewport (principal 0,29): ya no tapa la mira.
- Cambio de arma: el movimiento lineal frenaba en seco. Nuevo `PlayerWeaponsManager.SmoothWeaponSwitch` (smoothstep, activo solo en el laboratorio). Llegada medida a 60 Hz: 0,15 → 0,10 → 0,03 → 0 de viewport por frame.
- Slide y ejecución con la escopeta equipada revisados mediante capturas; no hizo falta corregir nada. Capturas en `Tools/IronRequiem/Reports/SecondaryShotgun/pose-*.png`.

## 5. Rendimiento

Carga repetible (`CombatLoadBenchmark`): oleadas de 20 s con 8 T-07 y 4 Big Bots con IA, navegación y disparos activos; el jugador dispara la principal y la escopeta mediante el Input System; daño por etapas al 73/48/23 % (destrucción progresiva) y muertes con explosión. 1 oleada de calentamiento + 15 medidas (300 s). Player Release, D3D11, 1920×1080 en pantalla completa exclusiva, VSync 0, sin límite de FPS, RTX 4050 Laptop, conectado a la corriente. El blur sigue desactivado.

**Antes (Release, 300 s, 43.494 frames, 0 frames con resolución inválida):** frame medio 6,90 ms · mediana 6,77 · p95 10,60 · p99 11,99 · máximo 66,2. El 26,6 % de los frames supera 8,33 ms, el 0,06 % supera 16,67 ms; 15 frames > 33 ms. CPU 6,90 ms (hilo principal 6,47) y GPU 3,15 ms (p99 4,97): limita la CPU. Con 12 enemigos vivos: media 7,79 · p95 10,97 · 37,7 % > 8,33 ms. Ventanas de 1 s: mediana 131 FPS; 1 % bajo 104 FPS. 42 recolecciones de GC.

Perfil (build Development, oleada 3, de 6 a 11 s): envío del render en el hilo principal ~3,2 ms/frame (dos cámaras, Main y WeaponCamera, cada una con prepass DepthNormal/SSAO); scripts Update 0,89 ms, de los cuales `EnemyMobile.Update` = 0,26 ms (pide `SetDestination` a cada enemigo en cada frame); NavMesh 0,21; Animators 0,30; Canvas 0,37.

**Optimización aplicada:** `EnemyController.DestinationUpdateThreshold` / `DestinationRefreshInterval`. Por defecto 0, así que no cambia nada en los prefabs anteriores. En las variantes `*_NavThrottle` vale 0,3 m / 0,25 s, y las usan las oleadas del laboratorio y el benchmark. Persecución verificada en el NavMesh real: el destino sigue al jugador a los 0,4 s y el enemigo se acerca 4,5 m en 1,2 s.

**Después (misma carga y condiciones, 300 s, 43.022 frames, 0 inválidos, mismos 825/872 disparos y 180 muertes):** media 6,97 ms · mediana 6,89 · p95 10,43 · p99 11,67 · máximo 66,9 · 26,4 % > 8,33 ms · 0,05 % > 16,67 ms · 15 frames > 33 ms. CPU 6,97 / GPU 3,11 ms. Con 12 vivos: media 7,76 · p95 10,78 · 37,2 % > 8,33 ms. Ventanas de 1 s: mediana 132 FPS, 1 % bajo 98.

**Conclusión:** el throttling de navegación no produce una mejora medible (diferencias dentro del ruido: p95 −0,17 ms, p99 −0,32 ms, media +0,07 ms). **120 FPS sostenidos NO se alcanzan** (~26 % de los frames supera 8,33 ms, limitado por la CPU del hilo principal). **60 FPS:** solo el 0,05 % de los frames pasa de 16,67 ms, pero hay 15 pausas > 33 ms en 5 min (máximo ~67 ms, coinciden con 42 GC y con las apariciones de oleada). Se considera casi estable, no estable. No se redujeron enemigos ni mecánicas.

Siguientes costes medidos, no tocados porque afectan a URP o a sistemas globales: el prepass/SSAO de la WeaponCamera y los Canvas de vida por enemigo.

## Pruebas

Play Mode, `SecondaryShotgunTests`: 7/7 aprobadas (inventario + ejecución + lanzamiento, caída de daño, cooldown del slide, umbral T-07, umbral Big Bot, navegación con throttling, capturas). Regresión `FastCombatTests`: 6/6. Sin errores de compilación; laboratorio limpio y fuera de Play. Dispositivos virtuales del Input System, sin foco de ventanas. XML en `Tools/IronRequiem/Reports/SecondaryShotgun`; benchmark en `Tools/IronRequiem/Reports/CombatLoad`.

## Archivos

Nuevos: `ProjectileDistanceFalloff.cs`, `IProjectileHitDamageModifier.cs`, `CombatLoadBenchmark.cs`, `SecondaryShotgunAuthoring.cs`, `SecondaryShotgunTests.cs`, `RunCombatLoad.ps1`, `summarize_combat_load.js`, `run_secondary_op.sh`, prefabs `SecondaryShotgun/*`, las dos escenas.

Modificados, con campos opcionales que por defecto mantienen el comportamiento anterior: `AdvancedPlayerMovement`, `MechanicInput`, `ProjectileStandard`, `PlayerWeaponsManager`, `EnemyController`, `ProtagonistHandsPresentation`, `PerformanceComputeStripper`.

Pendientes: revisión manual del aspecto de la escopeta montada y de los cuadrados verde claro que aparecen cerca de la cámara en la captura del Player cuando el jugador invulnerable recibe muchos impactos (no se observaron en el Editor; origen sin confirmar).
