# IRON REQUIEM — actualización de diseño v0.2

29 de octubre de 2026. Este informe sustituye las reglas de 12 disparos y OVERDRIVE manual de MECHANICS_V02.md. Se mantienen dash, navegación, desprendimiento, cámara, inventario y daño de la implementación existente.

Actualización posterior: [WEAPON_FEEDBACK.md](WEAPON_FEEDBACK.md) integra PistolModule_1000 en el arma principal, cambia su intervalo a 0,4 s y añade efectos de carga y tamaño variable al proyectil del cañón. Las cifras y capturas de verificación que siguen corresponden a la revisión anterior.

## Uso y controles

Abrir `Assets/IronRequiem/Scenes/CharacterLab.unity` y entrar en Play. El jugador empieza con **una pistola**. Destruir la unión naranja del T-07 desprende su cañón; apuntar al módulo dentro de 2,5 m y pulsar F para recogerlo.

| Acción | Control |
|---|---|
| Mover / saltar / correr | WASD / Espacio / Shift |
| Dash aéreo | Ctrl izquierdo; una carga hasta aterrizar |
| Slide | Pulsar C mientras se mueve con Shift, sobre suelo y con velocidad suficiente |
| Agacharse normalmente | C cuando no se cumplen las condiciones del slide |
| Pistola | Un disparo por pulsación de ratón izquierdo |
| Cañón | Mantener ratón izquierdo para cargar; soltar para disparar |
| Recoger / lanzar | F / G |
| Selección | 1 pistola, 2 cañón recogido; rueda/E/Q conservados |
| Pausa | P/Tab |

**OVERDRIVE es automático al alcanzar el umbral crítico. X deja de activar la sobrecarga.** La acción antigua de entrada se conserva por compatibilidad, sin consumidor de gameplay. No se añadieron ni cambiaron bindings.

## Pistola y conservación de la escopeta

`Weapon_IR_Pistol.prefab` es una variante nueva de Weapon_Blaster; la plantilla no trae una pistola dedicada. Conserva su representación provisional de bláster y utiliza un proyectil derivado independiente con daño moderado. WeaponController sigue siendo dueño del disparo, efectos, audio, cadencia y retroceso.

Player_IR_v01 y CharacterLab tienen únicamente esta pistola en StartingWeapons. La escopeta no se añade al inventario ni hay un pickup de ella en la arena. Su prefab, geometría, materiales, proyectil, scripts y funcionamiento originales se conservan. La recarga por regeneración de la pistola sigue el comportamiento heredado del bláster; no se aplica al cañón robado.

## Carga y energía por instancia

CannonAmmo conserva su archivo/GUID y ahora representa energía flotante, inicializada una sola vez en la instancia física. Bind/Unbind sincronizan la representación equipada y nunca reponen energía. El contador del inventario muestra las unidades disponibles; el HUD específico muestra energía y porcentaje de carga.

CannonChargeShot adapta las entradas a WeaponController mediante un contrato opcional. La única ruta de disparo sigue creando el proyectil y aplicando efectos y retroceso existentes. ChargedProjectileDamage fija su daño antes de que el proyectil evalúe impactos, incluso si el cañón está junto a una pared. Los prefabs originales de proyectil y bláster no se editan.

- Una pulsación nueva inicia la carga. Mantener alcanza el máximo y espera a soltar; no hay liberación automática.
- Soltar por debajo del mínimo cancela sin disparo ni gasto. Cargar por sí mismo no consume energía.
- Entre el mínimo y el máximo, potencia y coste interpolan linealmente. Si falta energía para la potencia elegida, se limita a la potencia asequible; nunca se produce energía negativa.
- Cambiar de arma cancela inmediatamente al solicitar el cambio. Pausar, morir, perder el módulo o lanzarlo también cancela la carga. Volver al slot con el botón aún mantenido exige soltar y pulsar de nuevo.
- El cañón no recarga. El estado crítico bloquea nuevas cargas y continúa su cuenta atrás.

## Peligro crítico, humo y explosión

**El humo de peligro pertenece exclusivamente al cañón robado que puede explotar. La pistola principal no humea ni explota por agotarse.** Se desactivaron el comportamiento OverheatBehavior y su vapor heredado del bláster solo en las variantes Weapon_IR_Pistol y cañón temporal, para evitar avisos falsos. El prefab y el código originales del bláster conservan ese efecto. Las partículas del slide son independientes y aparecen junto al suelo.

Al llegar a energía ≤ 20 % se entra una única vez en OVERDRIVE crítico: humo desde la carcasa, pulso naranja, aviso de peligro con tiempo restante y señal sonora breve de la plantilla a volumen reducido. La duración configurada determina el drenaje desde la energía que quedaba al entrar: `drenaje = energía al entrar / margen`. El margen inicial es 5 s, aunque un disparo haya dejado menos de 20 unidades.

El drenaje continúa en el arma equipada, guardada, disponible o lanzada. Cambiar de slot no suspende el peligro. El estado y la energía viven en la instancia física, nunca en un ScriptableObject compartido. La pausa usa tiempo de juego: suspende drenaje, fusible y simulación; cancela cargas y desactiva emisión nueva de humo. Al reanudar vuelve a emitirse humo si sigue siendo crítico.

El humo usa escala local del emisor y eje vertical del mundo: el FBX del módulo tiene escala heredada 100 y no debe ampliar cien veces el radio de emisión. El efecto queda concentrado junto a la carcasa.

Al agotarse detona en su posición real. Mientras está equipada, la instancia sigue el punto del arma; guardada, usa un punto de porte próximo a la cámara. Se retira el clon del inventario y se restaura el arma anterior disponible. La explosión también daña al jugador según Damageable y su sensibilidad a daño propio heredada.

Un lanzamiento normal permanece recogible y no inicia explosión ni fusible. Un lanzamiento crítico pasa a Thrown y detona por contacto sólido válido, energía cero o fusible de respaldo de 6 s. El primer plazo de fusible se conserva entre lanzamientos: recogerlo no lo reinicia. Se ignoran temporalmente los colliders del portador durante 0,35 s; después el jugador puede volver a recibir daño por proximidad.

Se permite recoger un módulo ya humeante si sigue existiendo, está dentro del alcance y ha vencido el retardo de recogida. **No se reinician energía, drenaje ni fusible**: es una recogida peligrosa, indicada con el tiempo restante en el prompt. El contacto durante el vuelo puede hacerlo explotar antes de alcanzarlo. La muerte suelta el módulo con su peligro restante y cancela su carga.

Las transiciones siguen Attached → Available → Equipped → Available/Thrown → Detonated. MarkDetonated se confirma antes de los callbacks de daño. DamageArea existente agrupa hitboxes por Health, evitando daño duplicado a una misma salud. La unión del robot conserva su salud independiente. La detonación no se puede repetir.

## Slide y partículas

La entrada sigue siendo la acción Crouch original. El inicio requiere pulsación de C, Shift mantenido, entrada de movimiento, postura no agachada, suelo válido y velocidad mínima. C sin esas condiciones conserva el agachado normal. Salto, obstáculos, final de duración y pérdida de suelo terminan el slide; la cápsula original comprueba si puede levantarse. El dash conserva su única autoridad CharacterController y puede iniciarse una vez en el aire tras el salto.

SlideContactEffects controla un único Particle System persistente por jugador. Sigue el contacto del pie y el normal del suelo; solo emite durante slide válido. Al terminar deja una cola de como máximo 0,3 s; no se instancian emisores por frame. El humo usa otro emisor persistente por módulo. Ambos usan material URP/Particles/Unlit compartido y una máscara suave procedural local de 32², sin generación remota.

## Valores ajustables

| Componente / prefab | Parámetro | Valor inicial |
|---|---|---:|
| Weapon_IR_Pistol / WeaponController | Shoot Type / Bullets Per Shot / Spread | Manual / 1 / 0° |
| Weapon_IR_Pistol | Delay Between Shots / Recoil Force | 0,5 s / 0,12 |
| Projectile_IR_Pistol / ProjectileStandard | Damage | 22 |
| CannonAmmo, instancia del enemigo | Capacity | 100 unidades |
| CannonChargeShot, arma temporal | Maximum Charge Duration / Minimum Charge Fraction | 1,2 s / 20 % (0,24 s) |
| CannonChargeShot | Minimum / Maximum Energy Cost | 12 / 30 |
| ChargedProjectileDamage, proyectil derivado | Minimum / Maximum Damage | 25 / 65 |
| Weapon_T07_Temporary | Cadencia mínima entre liberaciones | 0,5 s |
| CannonOverdrive, instancia del enemigo | Critical Threshold / Grace Duration | 20 % / 5 s |
| CannonExplosion / DamageArea | Damage / Radius / Backup Fuse | 100 / 4,5 m / 6 s |
| DamageArea | Atenuación centro → borde | 100 % → 25 % |
| StolenWeaponCarrier | Interaction Range / Throw Speed | 2,5 m / 14 m/s |
| StolenCannon | Pickup Delay / protección de colisión del portador | 0,3 s / 0,35 s |
| AdvancedPlayerMovement | Dash Speed / Duration | 24 m/s / 0,18 s |
| AdvancedPlayerMovement | Slide Duration / Minimum Speed / Impulse | 0,7 s / 11 m/s / 4 m/s |
| AdvancedPlayerMovement | Slide Deceleration / Maximum Speed | 18 m/s² / 28 m/s |
| CriticalSmoke | Máximo de partículas / emisión | 48 / 22 por segundo |
| SlideContactParticles | Máximo de partículas / emisión | 36 / 45 por segundo |

## Verificación y reproducción

El menú `Tools > Iron Requiem > Mechanics > Apply Design Update v02` (Ctrl+Shift+F6) configura variantes mediante APIs del Editor y rechaza escenas previamente modificadas por el usuario. `Verify Movement and Cannons` (Ctrl+Shift+F7) ejecuta PlayMode seguido de EditMode en el Editor visible, con entradas virtuales del Input System y comprobación de frames.

Se conservan los seis escenarios originales y los once añadidos anteriormente. Las expectativas de inventario de dos armas, disparo automático de 12 tiros y X manual se actualizaron por contradicción explícita con este diseño; sus versiones previas completas están respaldadas. No se ocultaron ni ignoraron esos escenarios. Se añaden casos de pistola, carga/potencia/coste, cancelación por cambio/pausa/lanzamiento, daño al portador por agotamiento, energía cero en vuelo, recogida humeante y peligro guardado, muerte crítica y partículas del slide.

Los dos fixtures de Play Mode limitan temporalmente InputSystem.actions.devices al teclado y ratón virtuales que crean y restauran el filtro al terminar. Esto evita que el ratón físico dirija la cámara mientras se ejecutan las entradas automatizadas. No cambia bindings ni ajustes persistentes. Se comprueba también que una pistola vacía no emite vapor ni tiene CannonExplosion y que el cañón no humea antes del estado crítico.

Verificación final del **29 de octubre de 2026, 10:53 America/La_Paz (14:53 UTC): 27/27 pruebas aprobadas**, 20 de Play Mode y 7 de EditMode. Incluye los seis escenarios originales con las expectativas actualizadas que exige este diseño. Las pruebas usaron entradas del Input System y avance real de frames en el Editor visible. No fueron una sesión manual de juego ni una escucha manual del audio.

El resultado final está guardado en `Tools/IronRequiem/Reports/design-v02-play-visible.xml` y `design-v02-edit-visible.xml`; los informes `character-*-visible.xml` contienen la misma última ejecución. La auditoría posterior dejó CharacterLab fuera de Play Mode, sin cambios pendientes, sin scripts ausentes y sin errores de compilación capturados. El verificador de integridad de las **10:56 America/La_Paz** (`design-v02-verification.json`) confirmó `allChecksPass: true`: ningún cambio imprevisto frente a los 1.825 archivos respaldados, acciones y bindings preservados y ningún `.meta` faltante. MainScene, Build Settings, Packages, ProjectSettings, modelos, bláster y escopeta originales permanecen intactos.

Las capturas `design-v02-critical-hud.png`, `design-v02-charge-hud.png` y `design-v02-slide-particles.png` muestran el cañón crítico, la carga y las chispas de contacto. Se revisaron visualmente el humo concentrado y el HUD. Los casos nuevos comprueban pistola vacía sin humo/explosión, cañón sin humo antes del crítico y humo real durante su cuenta atrás.

La muestra de rendimiento del Editor (`character-editor-baseline.json`, copiada también como `design-v02-editor-baseline.json`) registró 120 frames, 16,03 ms de media y 18,88 ms en percentil 95, a 2560 × 1440, calidad Default, VSync desactivado, un enemigo y RTX 4050 Laptop. Describe esa ejecución, sin atribuir una mejora de FPS ni acreditar rendimiento de oleadas o de un ejecutable PC.

Los informes de las ejecuciones previas se conservan como diagnóstico. Se corrigió un conteo de proyectiles realizado después de su impacto y se aislaron las entradas virtuales ante fallos intermitentes de recogida; la suite final completa vuelve a pasar esos casos. Durante las recargas hubo demoras y avisos de red/licencias del Unity AI ya instalado y de UPM; no se modificaron paquetes ni cuentas para resolverlos.

Respaldo previo: `Tools/IronRequiem/Backups/20261029-design-v02/`, con hashes de 1.825 archivos y copias de scripts, configuración, prefabs, escena, tests e informes anteriores. `Tools/IronRequiem/verify_design_v02.ps1` compara el respaldo, configuración, acciones originales, metadatos y resultados. Los cambios pequeños adicionales de integración son la política opcional de entrada de WeaponController, cancelación al cambiar arma y recepción de potencia en ProjectileBase.

La aclaración de humo exclusivo del explosivo tiene un respaldo adicional de las variantes y archivos afectados en `Tools/IronRequiem/Backups/20261029-smoke-only-explosive/`.

Herramientas: cuatro skills locales de IRON REQUIEM, Unity/uGUI, CLI instalada para abrir el Editor, Computer Use, APIs de prefabs/escenas, Particle System y Test Framework instalados. No se instalaron paquetes, no se usaron servicios de generación ni créditos, y no se modificaron MainScene, URP, Input System ni los modelos originales.

Límites de alcance: representaciones provisionales y sonido reutilizado; falta ajuste artístico/sonoro y sesión manual de sensaciones, mappings de mando de las acciones nuevas previas, guardado entre sesiones, pooling y perfilado de oleadas en un ejecutable PC. Las pruebas automatizadas del Editor no acreditan rendimiento del ejecutable.
