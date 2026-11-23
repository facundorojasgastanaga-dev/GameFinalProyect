# INVENTARIO D: animación del Carroñero (solo lectura)

Fecha: 2026-11-20. Alcance: `CarroneroMotion`, prefab `Prefabs/Campaign/Carronero.prefab` y su cadena de variantes, y consumidores. Nada modificado. Lo no verificado por lectura se marca **sin comprobar**.

Checklist de referencia: skill `game-dev-combat-animation` (anticipación, contacto, recuperación, peso, silueta, tests por frame). Es un estándar pensado para humanoides con piel skinneada; aquí el rig es de piel dura por hueso (CHECKLIST_TRABAJO.md:866), así que no hay estiramiento de piel, sino segmentos rígidos que pueden separarse o atravesarse en las articulaciones. No hay tests de `CarroneroMotion` en `Assets/**/Tests` (grep sin resultados).

---

## 1. Cómo funciona hoy la animación

Archivo: `Assets/IronRequiem/Scripts/Enemies/CarroneroMotion.cs` (105 líneas). Sin Animator propio. Se ejecuta en `LateUpdate` con `[DefaultExecutionOrder(150)]` (L10), después de SawBotCombat (orden 80).

### 1.1 Huesos

Lista de 22 huesos (`CarroneroRigInfo.bones`, prefab Carronero.prefab:1817-1846): Hips, Spine, Spine1, Spine2, Neck, Head, LeftShoulder, LeftArm, LeftForeArm, LeftHand, RightShoulder, RightArm, RightForeArm, RightHand, LeftUpLeg, LeftLeg, LeftFoot, LeftToeBase, RightUpLeg, RightLeg, RightFoot, RightToeBase. Más `SawSpin` (sierra) y BladeStart/BladeEnd (referencias de contacto, no posados).

Huesos que mueve CarroneroMotion (10 + sierra), asignados en el prefab (Carronero.prefab:4672-4704):

| Campo | Hueso | Movimiento |
|---|---|---|
| hips | Hips | bob vertical, pitch de inclinación (L74-76) |
| spine | Spine1 | yaw ±5° al paso, +6° ataque, +30° agachado, yaw de hurgar (L77) |
| leftUpLeg / rightUpLeg | LeftUpLeg / RightUpLeg | flexión ±26° + elevación de rodilla (L99-101) |
| leftLeg / rightLeg | LeftLeg / RightLeg | pantorrilla con elevación (L102) |
| sawArm | LeftArm | ataque: -38° base + sierra ±14° + balanceo ±6° (L84) |
| sawForeArm | LeftForeArm | como sawArm, sierra ×1,5 (L85) |
| otherArm | RightArm | balanceo ±14°, alcanzar -62°, hurgar -26° (L86) |
| head | Head | cabeceo 3° al paso, +12° agachado, balanceo 6° (L87) |
| saw | SawSpin | giro en eje sawAxisLocal (L89-93) |

Huesos NO movidos por CarroneroMotion (12): Spine, Spine2, Neck, LeftShoulder, RightShoulder, **RightForeArm**, LeftHand, RightHand, LeftFoot, RightFoot, LeftToeBase, RightToeBase. Quedan con la pose de bind o con lo que escriba el Animator (ver 1.6).

### 1.2 Paso (andar y correr)

- Velocidad real: `flat = ProjectOnPlane(pos - lastPos, up)` (L57). **La velocidad que usa el paso sale del desplazamiento real del transform**, no de `NavMeshAgent.velocity` ni de una constante. `target = flat/dt` (L58), `speed` suavizado con `Lerp(…, 1-exp(-10dt))` (L59), es decir, constante de tiempo ~0,1 s.
- Fase: `phase += (speed > 0.1) ? flat.magnitude / stride : dt*0.4` (L60). Si hay movimiento, la fase avanza por **distancia recorrida**, no por velocidad. Ciclo completo = `stride` metros = **0,575 m** (valor del prefab, Carronero.prefab:4680; valor de código 0,55 en L15). Eso son 1,74 ciclos/m. A 7,2 m/s (velocidad del prefab) son **12,5 ciclos/s = 25 pasos/s**; a 1 m/s, 3,5 pasos/s.
- Amplitud: `move = clamp01(speed/3)` (L69). **Satura a 3 m/s**: andar y perseguir a 7,2 m/s tienen la misma amplitud de piernas (26° cadera, 30° rodilla).
- Piernas (L96-103): `swing = sin(a)*26*move`; `lift = max(0,cos(a))*30*move` (rodilla solo en la mitad de fase de avance); muslo `-swing - lift*0,5 - 42*squat`; pantorrilla `-swing + lift*0,6 + 34*squat`. Pierna derecha con fase +π y squat ×0,85 (L80).
- Bob: `|sin(a)|*0.035*move` (L75): dos rebotes por ciclo.
- Brazo derecho (L86): `-sin(a)*14*move` (contrabalanceo).
- Patinaje: no hay IK ni anclaje de pie. La longitud de pierna no se usa en ningún cálculo; el avance del pie depende de la amplitud angular y de la longitud real del hueso (sin comprobar), mientras el paso depende de la distancia y de `stride` fijo. **Probable deslizamiento de pie**, sobre todo a velocidades altas. Sin comprobar visualmente.
- Fase inicial: `float phase` no se aleatoriza (L27 deja 0). Solo `seed` (L36) entra en el respiro (L75), en el giro de hurgar (L74), en la sierra (L83) y en la cabeza (L87). **Los bots de una horda que arrancan a la vez comparten fase de paso** y solo se separan por diferencias de distancia recorrida.

### 1.3 Brazo de sierra, agacharse, hurgar, alcanzar

- Ataque: `attackBlend` sube hacia `combat.IsAttacking` a 6/s (L61), es decir, ~0,17 s para completar. Brazo: `attackRaise*blend + sawing + sin(a)*6*move` con `sawing = sin(18t+seed)*14*blend` (≈2,9 Hz) (L83-85). Cadena: hips `+6*blend` (L76), spine `+6*blend` (L77). Sierra: `spin` a `Lerp(720, 2200, blend)` °/s (L91).
- Agacharse: `crouch` hacia `CrouchTarget` a 2,4/s (L62). Efecto: hips −0,16 m (L75), muslos 42°, pantorrillas 34°, cabeza +12°, inclinación 30·0,4 (L76).
- Hurgar: `dig` a 4/s (L63), `digWave = sin(7,5t+seed)*dig` (L74). Brazo derecho: `-26*(0,5+0,5*digWave)*dig` (L86).
- Alcanzar: `reach` a 3/s (L64), brazo derecho `-62*reach` (L86).
- **Ruta de cabeza**: `head` yaw ±6° a 1,7 rad/s (L87).

### 1.4 Gate de cámara ("no posa fuera de cámara")

- L66-68. `seen = ∃ v ∈ views: v.enabled && v.isVisible`. `views` = `SkinnedMeshRenderer` de `hips.root` (L49, una sola vez en `Bind`). Si ninguno cumple, `return` antes de escribir rotaciones.
- Lo que sigue corriendo fuera de cámara: `lastPos`, `speed`, `phase`, `attackBlend`, `crouch`, `dig`, `reach` (L56-64). Lo que se congela: las rotaciones escritas la última vez que el bot fue visible. **Al volver a cámara, la primera pose sale de golpe** (sin mezcla), con un salto de hasta una amplitud completa. Defecto probable, sin comprobar en play.
- `Renderer.isVisible` es del frame anterior y cuenta cualquier cámara que lo renderice (cámaras auxiliares o CCTV incluidas): sin comprobar.
- El Animator, si está activo, tiene override `m_CullingMode: 2` (CullCompletely, Carronero.prefab:4360) en un componente que no he podido confirmar; si es el Animator, también se congela fuera de cámara (sin comprobar).

### 1.5 LOD

LODGroup en `Carronero_Visual` (Carronero.prefab:1783-1816), umbrales de altura en pantalla 25% / 10% / 4% / 1,2% (L1796-1811):

| LOD | Umbral | Renderers |
|---|---|---|
| 0 | 0,25 | SMR 963515… + MeshRenderer 5511987… |
| 1 | 0,10 | SMR 7410603… + MeshRenderer 5511987… |
| 2 | 0,04 | SMR 1836165… + MeshRenderer 7357919… |
| 3 | 0,012 | SMR 7403272… |

Cada nivel tiene un SMR, así que el gate de visibilidad funciona en todos los LOD. Los SMR de niveles no activos quedan `enabled=false` por LODGroup y el gate los ignora. Tamaños de malla por checklist: v 16381 / 3317 / 1563 / 473; t 10653 / 7175 / 3873 / 1361 (CHECKLIST_TRABAJO.md:866). A LOD2-3 el paso y la sierra ya casi no se ven: el coste de posar importa poco ahí.

### 1.6 Lo que escribe el Animator (sin confirmar)

Carronero.prefab **no declara** Animator, pero hereda uno habilitado de `SawBot_AI_v01.prefab` (Animator fileID 1730922581048303400, L6577, `m_Enabled: 1`, sin override en Carronero.prefab). Controller: `Art/Enemies/SawBot/SawBot.controller`.

- Parámetros del controller: MoveSpeed (float), Alerted (bool), Attack / CancelAttack / OnDamaged (trigger), AttackSpeed (float). **No existe `DriveSpeed`**, que sí escribe `SawBotCombat.cs:160` (SetFloat sin efecto).
- Estados: IdleDrive (blend tree por MoveSpeed, umbrales 0 y 0,4: SawBot_Idle / SawBot_Drive), Attack (1,15 s, velocidad = AttackSpeed), Death (1 s), Hit (0,18 s, capa HitReaction).
- Clips: `SawBot_Idle.anim`, `SawBot_Drive.anim` y `SawBot_Hit.anim` animan rutas **`Rig/Body`, `Rig/Body/UpperArm_L`, `Rig/Body/UpperArm_R`** (y hashes). Carronero.prefab no tiene ningún GameObject llamado `Body` ni `UpperArm_*` (grep de m_Name: solo Hips y Rig). **Probable: las clips del SawBot no resuelven en el Carronero, y el Animator solo procesa estados y parámetros.** Sin comprobar en play.
- `SawBot_Death.anim` no tiene rutas legibles en el archivo de texto (sin comprobar qué curvas usa).

**Discrepancia de documentación**: CHECKLIST_TRABAJO.md:869 y PROMPTS_CHATS.md:604 dicen "sin Animator". Es cierto para `CarroneroMotion`, no para el objeto: el Animator existe, está habilitado y se evalúa.

### 1.7 Estados

| Estado | ¿Existe? | Dónde |
|---|---|---|
| Andar / Patrulla | Sí | fase por distancia, L57-69 |
| Perseguir (correr) | Parcial: misma cadencia que andar, sin pose propia; amplitud saturada a 3 m/s | L69 |
| Girar | No hay pose. Solo yaw sinusoidal de la columna (L77) y el giro del agente (SawBotCombat.cs:151-156) | — |
| Atacar | Sí: brazo, sierra, inclinación. Sin telegrafía ni recuperación (ver §3) | L61, L76, L83-93 |
| Recibir daño | No en CarroneroMotion. Solo el trigger `OnDamaged` del Animator (SawBotCombat.cs:189), sin comprobar que el clip Hit llegue a algún hueso | — |
| Hurgar / agacharse / alcanzar | Sí (ScavengerBehaviour, CctvActor) | L62-64, L74-76, L86 |
| Morir | Solo congelar: targets a 0 y pose vuelve a bind. La sierra para en seco | L56, L91 |
| Reposo | Respiración: bob 0,006 m a 3 rad/s (L75), cabeza (L87). Sin fase de paso | — |
| Arranque / frenada | No propios. Solo la inclinación sigue la velocidad suavizada (L76, lag ~0,1 s) | L59 |
| Anticipación de salto, caída, levantarse, alerta | No existen | — |

---

## 2. API pública de CarroneroMotion

### 2.1 Miembros

| Miembro | Tipo | Línea | Quién lo usa |
|---|---|---|---|
| `CrouchTarget` | propiedad get/set float | L21 | ScavengerBehaviour.cs:83, 151; CctvActor.cs:16 |
| `DigTarget` | propiedad get/set float | L22 | ScavengerBehaviour.cs:83, 152; CctvActor.cs:18 |
| `ReachTarget` | propiedad get/set float | L23 | ScavengerBehaviour.cs:83, 153, 162; CctvActor.cs:17 |
| `Configure(Transform×11, Vector3, float)` | método público | L30-31 | VisualPassCarronero.cs:264-265 (editor, C3). **No re-enlaza**: `Bind()` solo se llama en Awake (L38) |
| Campos `[SerializeField]` | ver 2.2 | L13-19 | HangarSectorAuthoring.cs:475-476 lee `"otherArm"` por SerializedObject; HangarMenuAuthoring.cs:433 lo incluye en KeepTypes |
| `enabled` (propiedad de Behaviour) | — | — | BridgeWalker.cs:53 (`procedural.enabled = !carried`); HordeCostProbe.cs:21 |
| `GetComponent<CarroneroMotion>()` | — | — | BridgeWalker.cs:28, 52; IndustrialOptionsAuthoring.cs:235; HangarSectorAuthoring.cs:475; VisualPassCarronero.cs:264 |

Privados (no tocar sin revisar callers): `Awake`, `Bind`, `LateUpdate`, `Leg`, `agent`, `combat`, `health`, `views`, rest rotations.

### 2.2 Campos serializados (valores del prefab Carronero.prefab:4672-4704 y defaults del código)

Prefab: stride 0,575; legSwing 26; kneeLift 30; bob 0,035; idleSpin 720; attackSpin 2200; attackRaise -38; attackSaw 14; sawAxisLocal (-1,0,0). Resto de campos (crouchDrop 0,16; crouchLean 30; crouchThigh 42; crouchShin 34; reachArm -62; digSwing 26) en default de código (L19).

### 2.3 Quién espera qué aspecto (no proponer cambiarlo)

- **ScavengerBehaviour (HangarIntro, ZONA PROTEGIDA)**: Rummage → crouch 1, dig 1, reach 0. Tear → crouch 1, reach 1. Throw → reach 0,8. Pause → crouch 0,15. Carrying → reach 0,45 (L150-162). Espera agacharse y hurgar, y que la cabeza y la columna cambien con el giro de hurgar.
- **CctvActor (Menu/CCTV, ZONA PROTEGIDA)**: crouch = 0,55 + 0,25·sin(2,1t); reach 1 (k<0,65) o 0,3; dig 1 (k<0,55). **Reach y dig a 1 a la vez** → brazo derecho hasta -62 - 26 = **-88°** (CctvActor.cs:17-18; posible atravesado de malla, sin comprobar).
- **BridgeWalker (Menu, ZONA PROTEGIDA)**: solo activa/desactiva el componente (`enabled = !carried`); el paso lo lleva el controller propio del T-07 (cabecera del archivo).
- **HordeCost / menú**: el probe apaga CarroneroMotion (HordeCostProbe.cs:21).

---

## 3. Defectos probables (deducidos del código)

| # | Defecto | Evidencia | Por qué |
|---|---|---|---|
| D1 | **Patinaje de pies** | CarroneroMotion.cs:60 (fase por distancia), L99-102 (ángulos fijos), L10-15 (stride fijo) | La cadencia sigue a la distancia, pero el pie no se ancla ni la amplitud depende de la longitud de pierna. Sin IK. |
| D2 | **Cadencia irreal a velocidad alta** | L60 y L15: 25 pasos/s a 7,2 m/s | Amplitud saturada a 3 m/s (L69), frecuencia lineal con la velocidad: parece zumbido, no carrera. |
| D3 | **Mitad del brazo derecho sin animar** | RightForeArm y RightHand no en la lista de motion (L86 solo mueve RightArm) | Alcance y hurgar mueven solo el hombro-brazo; el antebrazo derecho queda rígido. El izquierdo sí se mueve (L85). |
| D4 | **Rigidez general** | 12 de 22 huesos sin control (§1.1); sin cambio de peso lateral; la cadera solo se inclina en X (L76) | Silueta plana, sin transferencia de peso ni giro de cadera. |
| D5 | **Sin anticipación real en el ataque** | SawBotCombat.cs:87: preparación 0,35 s sin daño; CarroneroMotion: raise en 0,17 s (L61) | La subida dura la mitad de la preparación y no hay pausa de carga ni retroceso. |
| D6 | **Sin recuperación ni retroceso** | SawBotCombat.cs:183: al salir del rango, `CancelAttack` y cooldown 0,25 s; blend a 0 en 0,17 s (L61) | El brazo vuelve en ~0,17 s y el contacto termina en seco. |
| D7 | **Duración del ataque no está acotada** | SawBotCombat.cs:79-84 y 99: el ataque sigue mientras `dist ≤ exitReach (1,35)` y `ángulo < 100°` | No hay ciclo de ataque fijo: el "golpe" dura lo que dure la persecución cuerpo a cuerpo. |
| D8 | **Clip Attack desincronizado** | Attack.anim 1,15 s; AttackSpeed = 0,25/0,35 = 0,714 (SawBotCombat.cs:166) → 1,61 s; el daño empieza a 0,35 s (L87) | El contacto no sigue el fotograma del clip. Además el clip puede no resolver (1.6). Sin comprobar. |
| D9 | **Sin reacción al daño en la pose** | SawBotCombat.cs:189 dispara solo `OnDamaged` del Animator; CarroneroMotion no lee vida ni recibe evento | Sin flinch visible. Probable que el clip Hit (0,18 s) no llegue a los huesos (1.6). |
| D10 | **Giro brusco** | OrientTowards: `RotateTowards` a 300°/s en patrulla (SawBotCombat.cs:155) y 140°/s en ataque (`attackTurnSpeed`, L52, sin override en Carronero); `agent.updateRotation=false` (L71); velocidad escalada por alineación (L160) | No hay aceleración angular ni inclinación al girar. Gira en sitio cuando el ángulo es grande. Velocidad angular del agente en el prefab: 300 (heredada, SawBot_Sustained_v02.prefab). |
| D11 | **Sin arranque ni frenada** | agente: aceleración 26 m/s² (Carronero.prefab:4110) → 0→7,2 en 0,28 s; la pose solo sigue la velocidad suavizada (L59) | La inclinación es siempre hacia delante (L76), sin signo de la aceleración. |
| D12 | **Muerte: pose congelada y sierra parada en seco** | L56: `dead` pone todos los targets a 0 (L62-64) → la pose vuelve al bind en ~0,4 s (2,4/s); L91: `spin` deja de avanzar al instante | El clip Death (1 s) se mezcla con la pose de bind en los huesos controlados. Además MechDeathExplosion hace explotar el cuerpo (ver §5). |
| D13 | **Pop al volver a cámara** | L66-68: el gate salta la escritura y no hay mezcla al volver | Salto de hasta 26-30° en un frame. |
| D14 | **Sincronía de horda** | L27: `phase` sin semilla; L36: seed solo en respiro, hurgar, sierra y cabeza | Bots que arrancan juntos comparten fase de paso. Sí hay desfase en la sierra y en el respiro. |
| D15 | **Brazo que atraviesa malla** | L86: `reachArm*reach - digSwing*dig` → hasta -88° con reach y dig a 1 (CctvActor.cs:17-18); hombro rígido; piel dura (CHECKLIST:866) | Sin comprobar en malla. Los segmentos rígidos pueden separarse o atravesarse en la articulación. |
| D16 | **Ruedas y cadena heredadas corren en combate** | SawBotCombat.cs:199 → `SustainedWheels()` (L120-135): escribe leftWheel/rightWheel y cadenas cada frame | Trabajo por bot sobre objetos heredados del SawBot (sin comprobar si están activos ni visibles). |

---

## 4. Coste

Por frame, por bot (`CarroneroMotion.LateUpdate`):
- Un `Quaternion.AngleAxis` por rotación escrita: 4 piernas, 2 brazos, cabeza, columna, cadera, sierra → ~11-13. Unos 25 productos de cuaterniones y ~12 `Sin`/`Cos`.
- Bucle sobre `views` (4 SMR como mucho) con `enabled` e `isVisible`.
- Sin asignaciones: la función local `About` captura `right`, pero no crea delegados. Sin verificar con Profiler (GC Alloc).
- Fuera de cámara sale en L68 tras unas 12 operaciones.

Coste además del motion (por bot, si está activo):
- **Animator** habilitado (§1.6): evalúa IdleDrive y estados en pantalla, y se congela con CullCompletely (sin comprobar qué componente lleva el override).
- **SawBotCombat.LateUpdate → SustainedWheels** (L199): ruedas y cadenas cada frame.
- **SawBotCombat.Update → TickSustainedAttack / Visible** (L147-148): raycast de visión `Clear` cada frame (L33, L186) y `Physics.OverlapCapsuleNonAlloc` solo al atacar (L106).
- **DetectionModule** a 0,1 s (Carronero.prefab:4075) y **NavigationModule** destino a 0,3 s / 0,6 m (Carronero.prefab:4050-4055).
- **SawMotorPresentation** habilitado (1915637320647593123): chains y smoke a tamaño 0; el `motor` es un AudioSource (5864162741362017830, tipo 82). Sin conflicto visual con la sierra.

Cifras medidas:
- Checklist (CHECKLIST_TRABAJO.md:871): **LateUpdate 3,7 → 1,25 ms con 24 bots en Editor**; scripts totales +2,4 ms para 24 en Editor. El 1,25 ms es el LateUpdate de la versión actual.
- Pendiente (CHECKLIST:870): "Medir en build de desarrollo": no hay cifra de build.

**HordeCostProbe**: `Assets/IronRequiem/Scripts/Diagnostics/HordeCostProbe.cs`.
- Menú: `Tools/Iron Requiem/Visual Pass/C6 Diag horde cost breakdown (play)` → `VisualPassCarronero.cs:334` (solo en Play) → crea copias llamadas `DIAG_Carronero_i` (VisualPassCarronero.cs:350) → `HordeCostProbe.Run("DIAG_Carronero")` (HordeCostProbe.cs:12).
- Secuencia (HordeCostProbe.cs:14-35): espera 2 s reales; toma las instancias cuyo nombre empieza por el prefijo; muestrea 3 s por paso con `unscaledDeltaTime` (media y p99). Pasos: todo activo → sin CarroneroMotion → sin audio → sin Animator → LOD3 forzado → sin renderers → sin MonoBehaviour (salvo Health) ni NavMeshAgent.
- Mide **tiempo de frame completo**, no solo LateUpdate: el ruido de otros sistemas se mete en cada paso. No hay resultado registrado en Docs (grep sin hits).

---

## 5. Datos del prefab

Herencia: `Carronero.prefab` → `SawBot_SawPolish_v04.prefab` → `SawBot_Arsenal_v03.prefab` → `SawBot_Sustained_v02.prefab` → `SawBot_AI_v01.prefab`.

| Componente | Valor | Origen |
|---|---|---|
| NavMeshAgent | speed **7,2**; acceleration **26**; radius **0,32**; height 1,15; obstacleAvoidanceType 1 (LowQuality) | Carronero.prefab:4095, 4110, 4105, 4100, 4115 |
| NavMeshAgent | angularSpeed **300**; stoppingDistance **0,55** | heredado de SawBot_Sustained_v02.prefab (líneas 317, 322); AI_v01 tenía 180 y 0,7 |
| NavMeshAgent (runtime) | updateRotation=false, angularSpeed=turnSpeed | SawBotCombat.cs:71 |
| avoidancePriority | 40-59 aleatorio | CarroneroMotion.cs:37 |
| Health | MaxHealth **26** | Carronero.prefab:4295 (base 140, AI_v01:5356) |
| SawBotCombat | damagePerSecond **4,5**; damageInterval 0,12; contactRadius 0,24 | Carronero.prefab:4185, 4176, 4166 |
| SawBotCombat | reach **1,05**; exitReach **1,35**; preparation **0,35**; moveSpeed 7,2; acceleration 26 | Carronero.prefab:4131, 4141, 4151, 4146, 4156 |
| SawBotCombat | sustainedSaw 1; turnSpeed 300; attackTurnSpeed 140 (default) | SawBot_Sustained_v02.prefab:262, 282; SawBotCombat.cs:52 |
| SawBotCombat | recovery 0,55 (no se usa en modo sostenido) | Carronero.prefab:4136; SawBotCombat.cs:172-182 |
| DetectionModule | DetectionInterval 0,1 | Carronero.prefab:4075 |
| NavigationModule | DestinationRefreshInterval 0,3; DestinationUpdateThreshold 0,6 | Carronero.prefab:4050, 4055 |
| AudioSource | priority 200; MaxDistance 30 | Carronero.prefab:4085, 4091, 4196 |
| Animator | SawBot.controller; habilitado; culling CullCompletely (override, sin comprobar componente) | SawBot_AI_v01.prefab:6577; Carronero.prefab:4360 |
| LODGroup | 4 niveles, 25/10/4/1,2 % | Carronero.prefab:1783-1816 |
| Componentes añadidos | EnemyPowerIndicators, HeavyDeathFeel, MechDeathExplosion, CarroneroMotion | Carronero.prefab:4545, 4560, 4577, 4672 |
| Componentes quitados | cadenas y humo de SawMotorPresentation (size 0) | Carronero.prefab:3960-4037 |
| CarroneroRigInfo | 22 huesos + sierra + BladeStart/End | Carronero.prefab:1817-1846 |
| Desprendible / ejecución | sin DetachableCannon. Ejecución: **discrepancia** (ver abajo) | — |

Discrepancia de ejecución: `Docs/IronRequiem/Map01/03_ENCUENTROS.md:68` dice "No ejecutable (`executionEnabled = 0` en Carronero.prefab)". `Docs/IronRequiem/Map01/00_INVENTARIO.md:466` dice "NO CONCLUYENTE: EnemyExecutionSource habilitado y heredado". No he leído el valor de `executionEnabled` en el prefab. **Sin comprobar.**

Discrepancia de GUID: 00_INVENTARIO.md:466 da `f6b6432993420e5409d267ac7782cdd0` para Carronero.prefab. No lo he comprobado en el .meta.

---

## 6. Mejoras priorizadas (sin tocar IA ni balance; sin cambiar HangarIntro, Menu ni CctvActor)

Restricción que cambia el plan: **Carronero.prefab es el mismo prefab que los scavengers de Room00 (checklist:906, HangarSectorAuthoring.cs:471-476)**. Cualquier cambio en el prefab o en `CarroneroMotion` afecta a HangarIntro. Para mejorar solo el combate hace falta uno de estos caminos: (a) variante de prefab de combate (`Carronero_Map01.prefab` existe en `Prefabs/Map01/Enemies/`, sin investigar); (b) componente nuevo añadido solo a las instancias de ronda, leyendo `SawBotCombat` (solo lectura) y escribiendo en `CarroneroMotion` a través de un flag nuevo que por defecto deje el comportamiento actual. Lo que sigue es el orden recomendado.

| Prioridad | Mejora | Defecto | Timing de referencia | Riesgo de coste | Nota |
|---|---|---|---|---|---|
| 1 | Subida de sierra y brazo en la preparación completa (rampa de blend hasta el contacto) | D5 | Anticipación ≥ 0,25 s (contacto a 0,35 s) | Nulo (matemática) | Solo cambia el tiempo de `attackBlend` en L61. No toca `preparation`. |
| 2 | Fase de paso aleatoria por bot (`phase = Random.value` en Awake) | D14 | Sin timing (una vez) | Nulo | Un cambio de una línea. Riesgo de romper la sincronía de hordas: ninguno. |
| 3 | Cadencia ligada a velocidad: amplitud ∝ sqrt(move), tope de cadencia (~3 Hz) y mezcla de amplitud a 3 m/s | D1, D2 | Ciclo de paso ~0,6-0,9 s | Nulo | No cambia el avance real. |
| 4 | Brazo derecho completo: mover RightForeArm y RightHand con flexión de codo 20-60° al alcanzar y hurgar | D3 | Mezcla 0,15 s | +2 rotaciones por bot | Afecta a HangarIntro si es el mismo prefab: ver restricción. |
| 5 | Reacción al daño: inclinación aditiva de columna y cabeza 0,18 s con evento `Health.OnDamaged` (solo lectura) | D9 | 0,18 s (duración de SawBot_Hit.anim) | Nulo | Evento existente; no cambia la IA. |
| 6 | Inclinación al girar (roll de cadera ∝ velocidad angular) y arranque/frenada por aceleración | D10, D11 | 0,15-0,25 s de construcción | Nulo | Velocidad angular: diferencia de yaw del transform. |
| 7 | Muerte: desacelerar la sierra (0,3 s) y relajar la pose (0,25 s) en vez de congelarla | D12 | ≤ 0,28 s (delay de MechDeathExplosion, default) | Nulo | Coordinar con la explosión; si la explosión oculta el cuerpo antes, la mejora no se ve. |
| 8 | Mezcla al volver a cámara (guardar última pose y blendear 0,2 s) | D13 | 0,2 s | Bajo (un Slerp por hueso al volver) | Evita el salto. |
| 9 | Aplicar la sección "Spine2, Neck y hombros" al peso del torso | D4 | Sin timing | +3-4 rotaciones por bot | Mejor después de 4. |
| 10 | Coste: saltar `SustainedWheels` y Animator en LOD2-3 o fuera de cámara (medir antes) | D16, §4 | — | Riesgo de desincronía al volver a cámara si no se hace con cuidado | Medir primero con HordeCostProbe en build de desarrollo (checklist:870). |
| 11 | Recuperación del ataque: retroceso del brazo 0,25 s tras contacto | D6 | Recuperación ≥ 0,25 s | Nulo | Solo si la IA no cambia la cadencia (cooldown 0,25 s, SawBotCombat.cs:183). |

Pruebas: ninguna existe para este componente. Antes de cambiar nada conviene añadir un EditMode que compruebe que `attackBlend` alcanza 1 en ≥ 0,25 s y que la fase arranca distinta entre dos instancias, y una prueba de frame del brazo derecho con la piel dura (sin deformación, pero con separación en el codo).

---

## Resumen

1. `CarroneroMotion` (105 líneas, sin Animator propio) posa 10 huesos + sierra; 12 de 22 huesos quedan sin control.
2. El paso avanza por distancia real con `stride` 0,575 m fijo; amplitud saturada a 3 m/s; 25 pasos/s a 7,2 m/s; patinaje probable.
3. Fase de paso sin semilla: los bots de una horda comparten fase (L27).
4. Ataque: raise en 0,17 s, preparación 0,35 s sin daño, sin recuperación; duración no acotada (IA).
5. Sin reacción al daño, sin arranque/frenada, giro sin inclinación, muerte congelada con sierra parada en seco.
6. Gate de cámara: `views[i].enabled && isVisible` (L66-68); al volver a cámara hay un salto de pose.
7. Brazo derecho: RightForeArm y RightHand sin controlar (D3). CctvActor pone reach y dig a 1 a la vez (-88°).
8. Animator habilitado heredado de SawBot_AI_v01 (Carronero.prefab sin declararlo): sus clips apuntan a `Rig/Body/UpperArm_*` que no existen en el prefab (probable, sin comprobar). Los docs dicen "sin Animator".
9. API: 3 propiedades de pose (Crouch/Dig/Reach) para ScavengerBehaviour y CctvActor; `Configure` solo en VisualPassCarronero.cs:264; `otherArm` leído por nombre en HangarSectorAuthoring.cs:476.
10. Coste: 1,25 ms LateUpdate para 24 en Editor (checklist:871); HordeCostProbe (C6 en Play) mide frame completo; sin cifra de build.
11. Carronero.prefab es el mismo prefab que los scavengers de Room00: cualquier cambio de pose afecta a HangarIntro. Mejorar solo combate exige variante o flag.
12. Prioridad 1: subida de sierra durante la preparación (≥ 0,25 s). Prioridad 2: fase aleatoria (sin riesgo). Prioridad 3: cadencia ligada a velocidad.
13. Muerte: MechDeathExplosion hace explotar el cuerpo (delay 0,28 s), así que la animación de muerte se ve poco o nada (sin comprobar el orden exacto).
14. Discrepancias de documentación: "sin Animator" (CHECKLIST:869, PROMPTS_CHATS:604), ejecución (03_ENCUENTROS:68 vs 00_INVENTARIO:466), GUID del prefab (00_INVENTARIO:466).
15. Sin comprobar: longitud de pierna y patinaje real, clips del Animator en play, componente del culling override, si RobotDamageProgression o PooledEnemyDeathBurst están en el prefab.
16. Nada se ha modificado; solo se escribió este archivo.
