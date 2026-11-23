# IRON REQUIEM · Controles v0.1 (F1, propuesta en papel)

19 de noviembre de 2026. Solo propuesta: no se ha cambiado ningún binding, script, escena ni prefab. El usuario elige un esquema y después se implementa. No se tocan daño, tiempos, cadencias ni valores de movimiento.

Fuentes verificadas: `Assets/FPS/InputSystem_Actions.inputactions` (vía inventario A), `PlayerInputHandler.cs`, `PlayerCharacterController.cs:234-347`, `PlayerWeaponsManager.cs:140-197`, `MechanicInput.cs`, `AdvancedPlayerMovement.cs:150-200`, `ProtagonistMelee.cs:127-138`, `StolenWeaponCarrier.cs:64-82`, `WeaponMountStation.cs:149-181`, `RequiemSettings.cs`, ARMS_FIX_20261119 y MAP01_DESIGN.

## 0. Hechos que condicionan el diseño

| Hecho (código) | Consecuencia |
|---|---|
| Agacharse es **alternar** hoy: `GetCrouchInputDown` → `SetCrouchingState(!IsCrouching)` (PCC:236-239). Sprint (mantener) cancela el agachado (PCC:321-323). | El valor por defecto actual de agachado ya es alternar. |
| Slide = pulsar Agacharse en suelo, con Sprint mantenido, moviéndose y con velocidad mínima (APM:189). Una segunda pulsación lo corta (APM:187). | Slide y Agacharse comparten acción por diseño; Sprint debe seguir "mantenido" (o alternado que el handler reporte como mantenido). |
| Dash solo en el aire, una carga hasta aterrizar (APM:158,170). Salto solo en suelo (PCC:347). | Salto y Dash **nunca son válidos a la vez**: pueden compartir botón con prioridad por estado (esquema Ágil). Excepción: `Jetpack.cs:78` usa salto en el aire si existe en el jugador. |
| E contextual: ejecutar (Ready) > recoger (Ready, sin arma en mano) > estación (`ActionFree`, WMS:176-181). E gana a F en el mismo frame (ProtagonistMelee:133). | Ya existe la prioridad; solo falta llevarla al mando. |
| Recargar (R) tiene consumidor, pero exige `HasPhysicalBullets` (PWM:152) y ningún prefab de `Assets` lo tiene a 1. | Recargar no hace nada con el contenido actual: puede compartir botón en mando como último recurso contextual. |
| `NextWeapon > 0` (Q, rueda arriba, cruceta arriba) devuelve -1 → `SwitchWeapon(false)`, orden descendente (PIH:231-237). | Q hoy es "anterior"; el teclado no tiene la dirección contraria salvo la rueda. |
| Mapa 01 exige sprint (huecos de 11 m), dash (12 m) y slide (carriles de 12 m). | Sprint, Dash y Slide deben estar en mando sin soltar el stick izquierdo. |

## 1. Esquemas

Notación de mando por posición: **South (A/Cruz), East (B/Círculo), West (X/Cuadrado), North (Y/Triángulo)**, LB/RB, LT/RT, L3/R3, cruceta. Input System mapea por posición, así que PlayStation y Switch Pro conservan la posición física.

### 1A. Esquema CLÁSICO (cercano al actual, sin conflictos)

| Acción | Teclado y ratón | Mando | Cambio respecto a hoy y motivo |
|---|---|---|---|
| Mover | WASD (+flechas) | Stick izq | Sin cambio |
| Mirar | Ratón | Stick der | Sin cambio (ver §6 sobre dt del stick) |
| Disparar (mantener = carga en el cañón robado) | M1 | RT | Sin cambio |
| Apuntar | M2 | LT | Sin cambio |
| Saltar | Espacio | South (A) | Sin cambio |
| Sprint | Shift izq | L3 | Sin cambio |
| Agacharse / Slide | C | East (B) | Sin cambio |
| Dash (aire) | Ctrl izq + M4 (lateral trasero) | **LB** | Mando: hoy no existe. M4 como secundario por el riesgo Ctrl+Shift (§7) |
| Interactuar contextual (ejecutar > recoger > estación) | E | **West (X)** | Une Interact y CollectWeapon. Mando: hoy no existe. X sale de Attack (sin consumidor) |
| Recargar | R | West (X), solo si Interactuar no tiene objetivo | Mando: X compartido con prioridad documentada (§3). Hoy Recargar no tiene efecto |
| Golpe (toque) / carga (mantener) | F | **RB** | Mando: hoy no existe. Gatillo superior: no suelta ningún stick durante la carga |
| Expulsar arma robada | G | **Mantener North (Y) 0,4 s** | Mando: hoy no existe. Mantener evita expulsiones accidentales |
| Arma siguiente / anterior | Rueda abajo / Q y rueda arriba | North (Y) toque · cruceta abajo / arriba | Se separa en dos acciones de botón (§4c). Y pasa a "toque" (interacción Tap) para convivir con mantener = expulsar |
| Seleccionar arma 1–9 | 1–9 | Cruceta izq = 1, der = 2 | Nuevas acciones SelectWeapon1..9 (§4c). Hoy lectura directa |
| Saltar secuencia (estación) | Mantener E o Espacio | Mantener West (X) o South (A) | Se lee de las acciones Interactuar y Saltar, no de `Keyboard.current` (WMS:160-161) |
| Overdrive | (sin binding) | (sin binding) | Acción conservada sin tecla; se libera X. Borrarla requiere aprobación |
| Pausa | Esc (fija) + P | Start, Select | Tab **se quita**: está junto a Q y pausa en combate. Esc entra en la acción UI/Menu |
| UI: navegar / aceptar / cancelar | WASD, flechas, Enter, Esc, ratón | Stick, cruceta, South, East | Sin cambio |

### 1B. Esquema ÁGIL (movimiento rápido, estilo ULTRAKILL)

Solo se listan las filas que difieren del Clásico; el resto es idéntico.

| Acción | Teclado y ratón | Mando | Motivo |
|---|---|---|---|
| Dash (aire) | **Espacio en el aire** + M5 | **South (A) en el aire** + LB | Salto y Dash son excluyentes por estado: el pulgar no se mueve para encadenar salto → dash. Se elimina Ctrl (sin chord Ctrl+Shift) |
| Agacharse / Slide | C + **M4** | **R3** | Slide sin levantar el pulgar del stick derecho: se puede deslizar apuntando |
| Expulsar arma robada | G | **East (B)** toque | B queda libre al mover Slide a R3; expulsar sin retardo |
| Arma siguiente | Q, rueda | North (Y) toque | Sin "mantener": cambio inmediato |
| Sprint | Shift (mantener) | L3 **alternar** por defecto | Mantener L3 cansa y bloquea el pulgar; ver §5 |

## 2. Mapas de calor de manos

### Teclado, mano izquierda en reposo (WASD)

| Dedo | Teclas en reposo | Acciones | Alcance sin soltar movimiento |
|---|---|---|---|
| Meñique | Shift izq (Ctrl izq) | Sprint (Dash en Clásico) | Shift: reposo. Ctrl: baja el meñique; Shift+Ctrl a la vez es incómodo |
| Anular | A | Izquierda | Q por encima: arma (suelta A un instante) |
| Corazón | W | Adelante | 1–3: selección directa (suelta W un instante) |
| Índice | D | Derecha | E, R, F, G, C: el índice sube o baja desde D. **G y C sueltan D**; Slide con C mientras se esquiva a la derecha no es posible sin el ratón |
| Pulgar | Espacio | Saltar (Ágil: + Dash) | Reposo |

Acciones que obligan a mover la mano: 4–9 (selección rara, no combate), P/Esc (pausa, permitido). Ninguna acción de combate exige soltar W (adelante), que es la dirección dominante.

Mano derecha (ratón): M1 disparar, M2 apuntar, rueda arma, M4/M5 (pulgar) Dash/Slide. Todo en reposo.

### Mando

| Dedo | Clásico | Ágil | ¿Suelta un stick? |
|---|---|---|---|
| Índice izq | LT apuntar, LB dash | LT, LB dash | No |
| Índice der | RT disparar, RB golpe | RT, RB golpe | No |
| Pulgar izq | Stick + L3 sprint | Stick + L3 sprint (alternar) | No (cruceta sí: solo selección 1/2 y ciclo alternativo) |
| Pulgar der | Stick der; A, B, X, Y | Stick der; A, B, X, Y, R3 slide | Botones frontales sueltan **mirar** un instante, nunca **mover**. Ágil saca Slide de los frontales |

## 3. Conflictos

| Conflicto actual | Resolución |
|---|---|
| E en Interact + CollectWeapon | Una sola acción contextual (§4a) |
| West (X) en Attack + Reload | Attack se elimina. X = Interactuar, con Recargar como último recurso (prioridad documentada) |
| North (Y) en Interact + NextWeapon | Interact se elimina. Y = arma siguiente (toque); en Clásico, mantener Y = expulsar |
| M1 en Attack + Fire | Attack se elimina |
| Enter en Attack | Desaparece con Attack; Enter queda solo en UI/Submit |
| Tab en UI/Menu junto a Q | Se retira Tab |

Contextos con la misma entrada, intencionados y documentados:

| Entrada | Prioridad | Por qué no chocan |
|---|---|---|
| E / X | Ejecutar (Ready) > recoger (Ready y sin arma) > estación (`ActionFree`) > [solo mando] recargar | Un solo consumidor decide por frame; la estación ya cede la E (WMS:176) |
| E + F mismo frame | Interactuar gana (ProtagonistMelee:133) | Sin cambio |
| Espacio / A (Ágil) | Suelo → salto; aire con carga → dash | Estados excluyentes. **Riesgo**: si `AdvancedPlayerMovement.Update` corre después del salto en el mismo frame, `IsGrounded` ya es falso y el dash saldría en el frame del salto. La implementación debe ignorar Dash cuando `HasJumpedThisFrame`. Incompatible con Jetpack de plantilla si se activa |
| Mantener Y (Clásico) | Toque = arma; 0,4 s = expulsar | Toque añade ~0,1 s de latencia al cambio de arma |
| Mantener E/Espacio/X/A en estación | Saltar secuencia | `PlayerInputLock` activo: ninguna otra acción lee esas teclas |
| Player y UI comparten WASD, Enter, P | Mapas distintos; no verificado cuándo se habilita cada uno (inventario A §7) | Pendiente de comprobar en Play Mode |

Quedan sin resolver: Ctrl izq (Clásico) con Shift a la vez; latencia de Y en Clásico; C suelta D.

## 4. Acciones que se unifican o desaparecen

**(a) Interactuar contextual.** Conservar `Player/CollectWeapon` (sus GUID y nombre, que buscan `MechanicInput.cs:37` y `WeaponMountStation.cs:129`) como **la** acción de interacción y mostrarla como "Interactuar". Añadir binding de mando (West). Eliminar `Player/Interact` (plantilla, sin consumidor, con interacción Hold). Prioridad: la de `StolenWeaponCarrier.cs:76` (ejecutar antes que recoger) y la estación solo si `ActionFree`. La ejecución bloquea M1, F, 1, 2 y G hasta el impacto (ARMS_FIX); esos bloqueos evalúan estado, no teclas, y siguen valiendo con cualquier binding. Atención: `ProtagonistActionsAuthoring.cs:62-67` y `MechanicsLabUpgrade.cs:64-80` reescriben el asset y vuelven a añadir acciones por nombre; no ejecutarlas tras el cambio o actualizarlas.

**(b) Otras acciones.**

| Acción | Propuesta |
|---|---|
| Attack | Eliminar (sin consumidor; ocupa M1, Enter y X) |
| Interact | Eliminar (ver a) |
| Previous / Next | Eliminar (sin consumidor; ocupan 1, 2 y cruceta izq/der) |
| Overdrive | **Solo propuesta**: dejar la acción sin bindings (OVERDRIVE es automático, DESIGN_V02_UPDATE) y libera X. Borrarla requiere aprobación del usuario |
| NextWeapon (Axis con una sola mitad en teclado) | Sustituir por dos botones: `WeaponNext` (rueda abajo, cruceta abajo, Y) y `WeaponPrevious` (Q, rueda arriba, cruceta arriba), usando `<Mouse>/scroll/down` y `scroll/up`. Conserva el sentido actual de Q |

**(c) Selección 1–9.** Nueve acciones Button `SelectWeapon1`…`SelectWeapon9` (teclado `<Keyboard>/1`…`/9`; mando solo 1 y 2 en cruceta izq/der). Motivo frente a una acción con composite: cada slot es reasignable por separado y `GetBindingDisplayString` da una etiqueta por slot. `GetSelectWeaponInput` devuelve el primer índice cuya acción `WasPressedThisFrame` dentro de `CanProcessInput()`, igual que hoy.

## 5. Mantener o alternar

| Acción | Opción | Por defecto Clásico | Por defecto Ágil | Motivo |
|---|---|---|---|---|
| Sprint | Mantener / alternar (se apaga al soltar el movimiento) | Mantener (teclado y mando) | Teclado mantener, mando alternar | Slide exige sprint "mantenido": el alternado debe reportarse como mantenido en `GetSprintInputHeld` |
| Agacharse | Alternar / mantener | Alternar | Alternar | Es el comportamiento actual; el slide es independiente (pulsación) |
| Apuntar | Mantener / alternar | Mantener | Mantener | Disparo de cadera dominante en un FPS rápido; alternar como opción de accesibilidad |
| Slide | Pulsación (sin opción) | — | — | Duración fija en código; `SlideRequiresRelease` ya controla la repetición |
| Dash | Pulsación (sin opción) | — | — | Acción puntual |
| Golpe cargado | Mantener (sin opción en v0.1) | — | — | Toque/mantener es la mecánica; un modo alternado queda como pendiente de accesibilidad |

## 6. Sensibilidad e invertir Y

| Dónde vive hoy | Valor |
|---|---|
| `RequiemSettings.Sensitivity()` (PlayerPrefs `IronRequiem.LookSensitivity`, límite 0,1–5, por defecto 1) | Se copia en `PlayerInputHandler.LookSensitivity` (PIH:40) y desde el menú (`RequiemMenuView.cs:55`) |
| `PlayerInputHandler.InvertYAxis` / `InvertXAxis` | Campos públicos, sin opción de menú ni guardado |
| Procesadores Look | Ratón ×0,001; stick ×0,01 |

Un único multiplicador sirve para ratón y stick. Además el stick se multiplica por `RotationSpeed` **sin `deltaTime`** (PCC:300-308), así que el giro con mando depende de los FPS (a 144 FPS gira 2,4 veces más que a 60). Es un defecto de entrada, no un valor de balance; corregirlo es requisito para que una sensibilidad de mando signifique algo.

Propuesta: Sensibilidad ratón (la clave actual, por defecto 1,0); Sensibilidad mando (clave nueva, por defecto el equivalente a unos 180°/s horizontal a 60 FPS, validable en Play); invertir Y separado para ratón y mando (por defecto no); zona muerta radial por defecto de Input System (0,125–0,925). El multiplicador de apuntado existente se mantiene.

## 7. Riesgos

| Riesgo | Clásico | Ágil | Nota |
|---|---|---|---|
| Ctrl+Shift = cambio de distribución de Windows (si hay 2 idiomas o distribuciones) | **Alto**: Dash en Ctrl mientras se esprinta | Ninguno | Ctrl+Esc abre Inicio; Alt+Mayús cambia idioma; Alt+Enter alterna pantalla completa en el reproductor de Unity: ningún esquema usa Alt |
| Teclas especiales (Shift 5 veces) | Toques rápidos de Shift pueden abrir el diálogo de Teclas especiales | Igual | Sprint es mantener; avisar en Opciones |
| Ghosting de teclados de membrana (W+Shift+C+Espacio, A+W+C) | Medio | Menor: Slide en M4 y Dash en Espacio | Probar con un comprobador de ghosting; M4/M5 evitan la matriz |
| Win, Caps, F12 | No se usan. F12 está en `TeleportPlayer.cs:21` (depuración sin compuerta) | Igual | F12 además es captura de Steam |
| AZERTY / QWERTZ | `<Keyboard>/w` etc. es posición física (US); en AZERTY WASD queda en ZQSD físicos y 1–9 funcionan sin Shift. `GetBindingDisplayString` muestra la letra local (Z, Q). No usar bindings de carácter `#(x)` | Igual | Los textos horneados "[E]" serían falsos en AZERTY: razón extra para §8 |
| Ratón sin botones laterales | Dash conserva Ctrl | Dash conserva Espacio; Slide conserva C | Los laterales son siempre secundarios |
| Dash en el frame del salto | — | Ver §3 | Requiere guard `HasJumpedThisFrame` |
| Dash pulsado un instante antes de aterrizar | — | Se gasta el dash en vez de saltar | Sin buffer de salto en la plantilla |
| Latencia de Y (Tap/Hold) | ~0,1 s en cambio de arma | Ninguna | |
| R3 en Ágil | — | Clic accidental de R3 al girar fuerte = slide | Menos grave que expulsar el arma |
| Tests | 45 tests PlayMode simulan E, F, G, 1, 2, Esc, Espacio con teclado virtual | Igual | Siguen valiendo con bindings de teclado iguales; los de 1–9 y Esc deben pasar por las acciones nuevas |

## 8. Pendientes y decisiones del usuario

1. **Zona protegida**: `HangarIntroCinematic.cs:264-268` salta con mantener E, Espacio o South (A) por lectura directa. Ambos esquemas conservan Interactuar=E, Saltar=Espacio/A, así que sigue siendo coherente. No se migra.
2. **Menú**: `BridgePatrol.cs` y `MenuCameraRig.cs` leen el ratón directamente (zona protegida, solo anotado).
3. **Textos horneados que requieren Editor** (todos con cambios sin commit, no se pueden guardar ahora):
   - Placa CONTROLES "[F] GOLPE · MANTENER [F] · [M1]… [ESC] VOLVER" en `IntroMenu.unity:75264-75270` (protegida) y `RequiemCampaign.unity:49067-49073`.
   - "MANTÉN E / ESPACIO PARA SALTAR" en `Room00_Hangar.unity:147590`.
   - Prompt y mensaje "[E] ACOPLAR…", "[F] GOLPE MECÁNICO" en `MountStation_C04.prefab:1106-1108`.
   - Unos 26 textos con [E]/[F]/[G]/CTRL/M1 más en escenas de laboratorio y Map01 (inventario C).
4. **Textos de jugador a `GetBindingDisplayString`**: 25 cadenas runtime en 3 archivos (`MechanicHud.cs` 18, `RetroStatusBar.cs` 3, `WeaponMountStation.cs` 4 como valores por defecto serializados). Las de `ProtagonistMelee`, `EnemyExecutionSource` y `GameConstants` son comentarios o nombres de eje, no texto visible.
5. **Pausa**: Esc fija y no reasignable (vía de vuelta garantizada). Añadir `<Keyboard>/escape` a UI/Menu y quitar las lecturas directas de `InGameMenuManager.cs:94,100,106` y `RequiemMenuView.cs:49`. Decidir si Select del mando sigue pausando o se reserva.
6. **Lecturas directas a migrar**: `WeaponMountStation.cs:160-161` (saltar secuencia), `PlayerInputHandler.cs:247-263` (1–9). `TeleportPlayer.cs:21` (F12): decidir eliminar, proteger con `#if UNITY_EDITOR || DEVELOPMENT_BUILD` o dejar.
7. **Aprobaciones**: borrar Overdrive (o solo dejarla sin binding); eliminar Attack, Interact, Previous y Next; convertir NextWeapon en dos acciones.
8. **Documentación obsoleta**: `DESIGN_V02_UPDATE.md:9,19` dice que F recoge; hoy recoge E (MODULAR_ARM_AND_PICKUP).
9. **Defecto del stick sin `deltaTime`** (§6): decidir si se corrige en la implementación.
10. **Pantalla de reasignación**: no existe. Propuesta mínima: `PerformInteractiveRebinding` con detección de conflictos dentro del mapa Player y guardado con `SaveBindingOverridesAsJson` en PlayerPrefs, con "restablecer".

## 9. Recomendación

Elegiría el **Ágil**. Es el que mejor sirve al núcleo del juego: el Mapa 01 pide encadenar sprint, salto, dash y slide, y Ágil los encadena sin mover el pulgar (Espacio/A → dash) ni levantarlo del stick de mirar (slide en R3/M4). Además elimina el único riesgo serio de Windows (Ctrl+Shift). El coste es un guard de una línea para el dash en el frame del salto y verificar que el Jetpack de plantilla no está activo; el Clásico queda como alternativa seleccionable porque comparte todas las demás teclas.

---

## 10. Estado implementado (20/11/2026, esquema Ágil)

**Hecho y verificado** (EditMode 18/18 y PlayMode de controles 4/4, ver `Tools/IronRequiem/Reports/Controls/`):

- Bindings Ágil en `Assets/FPS/InputSystem_Actions.inputactions` (respaldo en `Tools/IronRequiem/Backups/20261119-controls/`). Cada acción reasignable tiene dos casillas por familia de dispositivo (se añadieron 45 bindings vacíos para poder rellenarlos con overrides).
- Reasignación: `ControlsRebinder` (captura con `PerformInteractiveRebinding`, conflictos, intercambio, restablecer) y panel `ControlsPanel` (lista A, pestañas Teclado y ratón / Mando / Ajustes). Se abre con el botón CONTROLES junto a VOLVER en las opciones de la pausa.
- Guardado: `SaveBindingOverridesAsJson` en PlayerPrefs (`IronRequiem.InputOverrides`), cargado en `BeforeSceneLoad` sobre el mismo `InputSystem.actions` que lee `PlayerInputHandler`.
- Textos: `ControlHints` (nombres cortos por ruta, no por idioma del sistema). `BakedHintFixer` reescribe en ejecución la placa CONTROLES horneada en escenas con jugador, sin editar la escena.
- Esc y P no se pueden asignar; Esc cancela primero la captura y después cierra el panel (`PauseMenuBackHook`).
- Permitido compartir: Saltar y Dash (estados excluyentes). Cualquier otro choque pide intercambiar o cancelar.

**Capturas** en `Docs/IronRequiem/Concepts/UI/Controls/`: lista, esperando tecla, conflicto, restablecer, mando, ajustes, botón en la pausa, placa horneada con la V y HUD con el golpe en V.

**Pendiente / decisiones**
- Botón CONTROLES en el menú principal (IntroMenu): zona protegida, solo propuesta.
- Textos horneados que no son la placa (cartel «MANTÉN E / ESPACIO PARA SALTAR» en Room00_Hangar, textos de laboratorio y Map01) siguen con la tecla escrita a mano.
- No verificado a mano con mando físico ni con el panel en una compilación; las capturas son del Editor.
- La rueda del ratón no se puede capturar como nueva tecla (solo se asigna por defecto); el stick sin `deltaTime` y F12 de depuración siguen sin tocar.

## 11. Menú principal (aprobado por el usuario, 2026-11-20)

El botón CONTROLES también aparece en las opciones del menú principal (IntroMenu). Se clona en ejecución desde `ControlsPanelInstaller`; la escena no se edita ni se guarda. Esc cierra el panel allí mediante un respaldo en `ControlsPanel.LateUpdate`. Capturas: `ctrl_menu_principal_boton.png`, `ctrl_menu_principal_panel.png`. Verificado en Play (carga de IntroMenu, clic en CONTROLES, panel abierto). Esc y el ajuste de etiquetas autoajustables tras el último cambio no se han vuelto a comprobar a mano.
