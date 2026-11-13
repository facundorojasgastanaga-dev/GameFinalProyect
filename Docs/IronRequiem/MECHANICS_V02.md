# IRON REQUIEM — mecánicas v0.2

**Informe histórico.** La actualización de diseño posterior en `DESIGN_V02_UPDATE.md` sustituye el inventario inicial, los 12 disparos fijos y la activación manual de OVERDRIVE. Consultar ese informe para la configuración vigente.

29 de octubre de 2026, America/La_Paz. Continuación del jugador/enemigo v0.1; representaciones provisionales conservadas. Los informes v0.1 son históricos y sus pendientes de recogida/OVERDRIVE se sustituyen por este informe.

## Uso y controles

Abrir `Assets/IronRequiem/Scenes/CharacterLab.unity` y pulsar Play. El cañón se desprende al destruir la unión naranja del T-07 o al morir su portador. Apuntar al módulo a menos de 2,5 m para ver la indicación de recogida.

| Acción | Control |
|---|---|
| Movimiento, salto y sprint | WASD, Espacio y Shift, conservados |
| Dash aéreo | Ctrl izquierdo; una carga, recuperada al tocar suelo válido |
| Agachado / slide | C; con sprint y velocidad suficiente inicia slide, en otros casos mantiene el agachado normal |
| Recoger módulo | F, apuntando a un cañón disponible dentro del alcance |
| Disparo y selección | Ratón izquierdo; 1/2 para armas iniciales y 3 para el primer cañón recogido; rueda/E/Q conservados |
| OVERDRIVE | X, con el cañón seleccionado; también funciona con cero disparos |
| Lanzar | G, con el cañón seleccionado; sin OVERDRIVE no explota, con OVERDRIVE detona al impactar o vencer el fusible |
| Pausa | P/Tab, conservados |

Las cuatro acciones nuevas son `Player/Dash`, `CollectWeapon`, `ThrowWeapon` y `Overdrive`, con bindings de teclado para esta primera versión PC. No se reasignaron acciones o bindings previos, incluidos sus GUID. Se evitó reutilizar E porque ya cambia armas. Los controles de mando anteriores permanecen; estas cuatro acciones nuevas aún no tienen bindings de mando.

El HUD indica carga del dash, disparos del cañón y estado de OVERDRIVE. La sobrecarga colorea y pulsa el arma y reproduce un sonido reutilizado de la plantilla. La escena usa el HUD y menú que ya contiene GameManager; se consolidaron las copias adicionales que habían quedado en la arena v0.1.

## Movimiento

PlayerCharacterController conserva cámara, rotación, gravedad, postura, consultas de suelo y CharacterController.Move. Se añadió un contrato de modificación de velocidad, `IPlayerMotionExtension`, en el ensamblado fps.Gameplay mediante un asmref situado bajo IronRequiem. AdvancedPlayerMovement propone la velocidad de dash/slide antes de la aplicación final; no mueve el transform ni añade Rigidbody o un segundo controlador.

El dash normaliza la dirección de movimiento; sin entrada usa la proyección horizontal de la cámara. Cancela ascenso al empezar, conserva descenso y mantiene la gravedad del controlador cada frame. Al terminar limita la velocidad horizontal a la velocidad aérea original y conserva velocidad descendente. Los golpes laterales lo cancelan y no restituyen la carga. La recarga depende del suelo detectado por la cápsula y del límite de pendiente existente, no de un temporizador.

El slide requiere sprint, contacto de suelo y velocidad mínima. Usa la postura/cápsula agachada, proyecta su dirección sobre la pendiente y desacelera durante su duración. Termina por tiempo, velocidad baja, pérdida de suelo u obstáculo. Intenta levantarse usando la consulta de obstrucciones original; bajo un techo permanece agachado. Un salto que consiga despejar la cápsula cancela el slide. Se puede hacer dash desde el siguiente frame aéreo; no se hace dash en el suelo ni se suman ambos impulsos.

La pausa, muerte y final de partida bloquean las acciones nuevas. La pausa cancela los estados de movimiento especial sin regenerar una carga en el aire.

## Cañón temporal

DetachableCannon conserva su transición única y el desprendimiento físico v0.1. Además vacía y desactiva la recarga del arma original del enemigo para impedir que vuelva a disparar incluso mediante una llamada directa a EnemyController.TryAtack. La IA mantiene navegación y detección con su retirada existente.

StolenCannon mantiene la instancia física y sus estados explícitos:

`Attached → Available → Equipped → Available` para recoger/soltar sin sobrecarga, o `Equipped → Thrown → Detonated` al lanzar con OVERDRIVE.

La sobrecarga se almacena por separado y solo puede activarse una vez. Un módulo sobrecargado arrojado no se puede recoger. La detonación se confirma antes de aplicar daño, de modo que varias colisiones o callbacks no pueden repetirla.

StolenWeaponCarrier consulta una interacción con línea de visión y alcance limitado. PlayerWeaponsManager añade la variante `Assets/IronRequiem/Prefabs/Weapons/Weapon_T07_Temporary.prefab` al primer slot libre; no sustituye las armas iniciales. CannonAmmo mantiene la munición en la instancia física y la restaura en cada representación equipada. Los disparos usan WeaponController, sus proyectiles, impactos, retroceso y daño existentes. Se desactivaron la recarga automática y la manual del cañón.

Cambiar de arma conserva el módulo guardado y su munición; X/G actúan cuando está seleccionado. Soltarlo y recogerlo no lo rellena. Cuando llega a cero deja de disparar pero sigue permitiendo sobrecarga y lanzamiento. El inventario lleno no consume ni oculta el módulo disponible. Al lanzar se recupera el arma anterior si continúa disponible, con alternativa a otra arma existente.

La representación física permanece independiente del GameObject del jugador mientras está guardada: renderer y collider desactivados, Rigidbody cinemático y sin gravedad. La representación FPS sigue el socket original. Al lanzar se restaura la física y se ignoran los colliders del portador durante 0,35 s. La muerte suelta el módulo, incluyendo el fusible si estaba armado. Una destrucción explícita del módulo elimina su representación del inventario. La descarga de escena no intenta operar sobre un HUD ya destruido.

Los módulos normales persisten para poder recogerlos de nuevo con su munición restante; se retiró la destrucción fija de 30 s únicamente para las instancias que incorporan este sistema. Un módulo que pierda a su portador por destrucción externa vuelve a quedar disponible.

## OVERDRIVE y daño radial

X convierte el cañón en un explosivo y bloquea sus disparos, incluso si aún tenía munición. No se puede cancelar o recargar la sobrecarga. El fusible empieza al lanzarlo, no mientras se sostiene. Utiliza tiempo de juego: la pausa suspende física y fusible.

CannonExplosion reutiliza DamageArea, que agrupa los colliders por Health antes de aplicar daño. El cuerpo y la unión conservan saludes independientes; varios colliders que apuntan a la misma salud no multiplican su daño. La explosión puede afectar también al jugador después del periodo de separación. Se reutiliza el VFX de explosión existente.

## Parámetros de Inspector

| Componente | Parámetro | Valor inicial |
|---|---|---:|
| AdvancedPlayerMovement, jugador | Dash Speed / Duration | 24 m/s / 0,18 s |
| AdvancedPlayerMovement | Slide Duration / Minimum Speed | 0,7 s / 11 m/s |
| AdvancedPlayerMovement | Slide Impulse / Deceleration / Maximum Speed | 4 m/s / 18 m/s² / 28 m/s |
| StolenWeaponCarrier, jugador | Interaction Range / Throw Speed | 2,5 m / 14 m/s |
| CannonAmmo, módulo del enemigo | Capacity | 12 disparos |
| StolenCannon, módulo | Pickup Delay | 0,3 s |
| CannonExplosion, módulo | Damage / Backup Fuse | 100 / 2,5 s |
| DamageArea, módulo | Area Of Effect Distance | 4,5 m |
| DamageArea | Damage Ratio Over Distance | 100 % en el centro → 25 % en el borde |
| Weapon_T07_Temporary | Delay Between Shots | 0,18 s |

Las velocidades de movimiento, sensibilidad, cámaras y parámetros de las armas iniciales no se cambiaron. La gravedad del dash usa GravityDownForce del controlador actual.

## Arquitectura y cambios de integración

Los scripts nuevos se reparten entre Player, Weapons y UI bajo `Assets/IronRequiem/Scripts`. No hay estado de munición mutable compartido en un ScriptableObject. Las referencias de los prefabs y el HUD se guardaron mediante APIs del Editor, con Undo en las operaciones interactivas, sin editar YAML de escena.

Cambios pequeños en cinco scripts base: contrato de velocidad/postura en PlayerCharacterController; restauración explícita de munición en WeaponController; recarga manual reservada a armas físicas en PlayerWeaponsManager; tolerancia a renderers desprendidos/destruidos en EnemyController; reconstrucción de los contadores iniciales y liberación de eventos en WeaponHUDManager. Estos últimos resuelven el ciclo de vida al destruir un cañón o descargar la arena.

Las seis pruebas originales se conservan en sus archivos. Se añadieron comprobaciones de dash/carga/aterrizaje/pared/dirección, slide/techo/salto/rampa de 15°, recogida única, inventario lleno, munición por instancia y ausencia de recarga, enemigo permanentemente desarmado, lanzamiento normal, impacto explosivo, daño único a una salud con tres hitboxes, fusible, pausa, muerte, final de partida, destrucción de módulo, retorno al arma anterior y contadores de HUD.

## Evidencia y reproducción

Usar **Tools > Iron Requiem > Mechanics > Verify Movement and Cannons** para ejecutar la suite en el Editor visible. El Test Runner usa teclado/ratón virtuales del Input System y confirma avance de frames. Son pruebas automatizadas; no equivalen a una sesión manual de juego ni a una escucha manual del sonido.

Resultados finales: `Tools/IronRequiem/Reports/character-play-visible.xml`, `character-edit-visible.xml` y `mechanics-verification.json`. El verificador independiente es `Tools/IronRequiem/verify_mechanics.ps1`. La captura `mechanics-overdrive-hud.png` corresponde a Play Mode con el cañón agotado y sobrecargado. La captura de rendimiento del Editor conserva resolución, calidad y 120 muestras en `character-editor-baseline.json`; no es una medición de Player compilado ni una comparación controlada de FPS.

Verificación final del 29 de octubre, 07:39 UTC: **17/17 pruebas aprobadas**, 12 de Play Mode y 5 de EditMode, incluidas las seis originales. Se conservaron sin cambios los dos archivos fuente de esas pruebas originales. El informe de integridad de las 07:40 UTC confirma compilación sin errores capturados, todas las referencias verificadas, ningún `.meta` faltante y ningún cambio imprevisto frente a los 1.790 archivos del respaldo. El Editor quedó fuera de Play Mode en CharacterLab, sin cambios de ejecución pendientes. Se copiaron también los XML finales como `mechanics-play-visible.xml` y `mechanics-edit-visible.xml`.

La muestra final del Editor registró 13,34 ms de media y 18,17 ms en percentil 95 durante 120 frames, a 2560 × 1440, calidad Default, VSync desactivado y un enemigo en una RTX 4050 Laptop. Estos datos describen únicamente esa ejecución automatizada y no acreditan rendimiento de un ejecutable o de oleadas.

Respaldo de los archivos anteriores: `Tools/IronRequiem/Backups/20261029-mechanics-v02/`, ignorado por Git. Incluye prefabs, escena, scripts anteriores, entrada, Packages, ProjectSettings, informes previos y hashes de 1.790 archivos. El verificador distingue los cambios solicitados de cualquier cambio imprevisto y compara todas las acciones/bindings anteriores por GUID y contenido.

Se mantuvieron MainScene, Build Settings, URP, Input System, modelos originales, materiales/meshes previos y representaciones provisionales. No se instalaron paquetes, usaron créditos, hicieron compras ni generaron modelos. Se usaron las cuatro skills de IRON REQUIEM, las skills pertinentes de Unity/uGUI y Computer Use, APIs del Editor y Test Framework ya instalado. Pipeline experimental sigue sin instalarse.

Límites: aún no hay rig/animaciones definitivas, brazos FPS nuevos, bindings de mando para las cuatro acciones añadidas, pruebas de oleadas, pooling de cañones o validación de rendimiento en un ejecutable PC. La geometría provisional del corte sigue necesitando trabajo artístico. Los módulos se mantienen únicamente durante la partida; no hay guardado entre sesiones.
