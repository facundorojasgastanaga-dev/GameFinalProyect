# Bláster nativo y eliminación de desenfoque

02/11/2026, America/La_Paz. Alcance: reparar la presentación v06 para acercarla al arma original de la plantilla, usar la paleta del protagonista y quitar el desenfoque.

## Resultado

Se actualizó `Assets/IronRequiem/Prefabs/Weapons/Weapon_IR_NativeVisual_v06.prefab`, que sigue siendo variante directa de `Weapon_Blaster`. Se restauraron las siete referencias de malla originales, con sus normales, UV, proporciones y piezas móviles. La carcasa deformada anterior y sus soportes quedan conservados pero sin renderizar. El modelo completo se traslada hasta la boca actual; el socket de unión del antebrazo queda en el extremo posterior.

Los materiales propios de `Assets/IronRequiem/Art/Weapons/NativeRepair` usan un atlas recoloreado a partir del atlas nativo. Se mantienen sus detalles y mapas originales de normales, oclusión, metal/suavidad y máscaras emisivas. Marfil y grafito proceden de la paleta medida del protagonista; la emisión usa cian y pasa a naranja según la envolvente de calentamiento original. No se proyecta el atlas del robot sobre UV ajenas. Se conserva la activación previa de partículas y efectos; no se añaden luces.

`WeaponFuelCellHandler` y `PlayerWeaponsManager` conservan la autoridad de movimiento por código: cápsulas, bob, ADS, retroceso, retirada y equipamiento. La fuente no tiene Animator ni clips. No hay cambios de código runtime, cadencia, daño, munición, proyectil, entradas o armas robadas.

## Desenfoque

- Motion Blur desactivado e intensidad cero en los tres perfiles existentes que lo contienen: DefaultVolumeProfile, CombatClarity y PostProcessingProfile.
- Depth of Field desactivado y en modo Off en el perfil que lo contiene.
- En `Player_NativeVisual_v06`, ambas cámaras quedan sin FXAA/TAA y sin resolución dinámica. Render scale verificado en 1. Se conserva el MSAA configurado en el pipeline, bloom e iluminación.
- El cambio de perfiles afecta a sus usuarios actuales; el ajuste de cámaras corresponde a la variante v06. No se modificaron los prefabs originales de la plantilla ni se guardaron escenas.

## Verificación

Prueba final del Test Runner en Play Mode: **2 aprobadas, 0 fallidas**. Son pruebas con teclado/ratón virtuales del Input System, no una partida manual ni un build de Player.

Se verificaron las siete mallas, materiales propios, configuración de desenfoque, escala de render y parámetros públicos del arma frente al estado anterior. Las 44 poses de cápsulas en 11 niveles de munición coinciden con los transforms de la plantilla. Reposo, caminar, caminar apuntando, disparar, ADS disparando, caminar disparando, retirar y equipar: **240 frames alineados**, **18 disparos**, cero vértices visibles del corte proximal del antebrazo. Alineación de boca y muñeca comprobada durante la prueba. Se revisaron capturas de reposo, ADS/disparo, acople y cápsulas extendidas.

Auditoría final: compilación sin errores capturados, cero scripts ausentes, NativeWeaponVisualLab limpio y fuera de Play Mode. El servicio Unity AI dejó mensajes NoSubscription preexistentes en la salida del Test Runner; las dos pruebas terminaron aprobadas. No se modificaron cuentas ni servicios.

Comparación de 1.366 archivos protegidos: solo cambian los dos prefabs v06 y los tres perfiles previstos. Todas las escenas, Assets/FPS, Packages y ProjectSettings permanecen iguales al respaldo. No se midieron FPS ni se probaron enemigos/melee en esta tarea.

Evidencias: `Tools/IronRequiem/Reports/NativeWeaponRepair` (play.xml, rendering.txt, motion.txt, native-parity.txt, preservation.json y PNG). Respaldo: `Tools/IronRequiem/Backups/20261102-native-weapon-clarity`.

Herramientas: APIs nativas del Editor mediante una herramienta local con operaciones fijas, Test Runner, Computer Use para Refresh y manejo de la ventana, Python para hashes/respaldo; skills FPS, URP y Unity CLI consultadas. Pipeline no está instalado: la comprobación de CLI no encontró una conexión; no se instalaron paquetes, servidores, herramientas ni se consumieron créditos.
