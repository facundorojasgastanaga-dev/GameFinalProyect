# Antebrazo modular, recogida y calentamiento v07

02/11/2026, America/La_Paz. Alcance autorizado: corregir la recogida del cañón enemigo, reducir el arma principal, sustituir la parte distal del antebrazo derecho por módulos y mostrar calentamiento progresivo. Referencia visual: acople junto al codo señalado por el usuario.

## Assets y comportamiento

- `Assets/IronRequiem/Prefabs/Weapons/Weapon_IR_ArmModule_v07.prefab`: variante de la presentación nativa v06. Modelo al 62 % del tamaño anterior; empuñadura eliminada de una copia de la carcasa, conservando UV y normales. Cápsulas, pivotes y movimientos siguen usando WeaponFuelCellHandler y PlayerWeaponsManager. Daño 6,5, intervalo 0,1 s, munición y proyectil conservados.
- `Assets/IronRequiem/Prefabs/Player/Player_ModularArm_v07.prefab`: conserva unos 14 cm junto al codo, sustituye visualmente el resto del antebrazo y oculta la mano al equipar. Receptor de 500 triángulos basado en la malla y los pesos reales; extremo proximal extendido detrás de cámara en esta copia FPS. Manguito mecánico cubre la transición. Al retirar el módulo vuelve el antebrazo/mano original. El puñetazo izquierdo se conserva.
- `Attachment` es la interfaz trasera del arma. El receptor sigue esa interfaz para el módulo principal, T-07 y Big Bot. Se añadió el punto trasero real al prefab equipado del T-07, sin cambiar su geometría, boca, carga o estadísticas.
- El rig del cuerpo conserva las piezas originales y un `RightArm_ModuleSocket` junto al codo para futuras animaciones. La sustitución visible implementada aquí es la representación FPS; no se añadieron nuevas animaciones ni un sistema de vista en tercera persona.
- `Assets/IronRequiem/Scenes/ModularArmLab.unity`: laboratorio separado, con la variante nueva del jugador y los enemigos existentes. Las escenas anteriores se conservan.

## Recogida del Big Bot

La causa estaba en su configuración: BigBot_FallenCannon era un resto decorativo, sin StolenCannon, energía ni prefab equipado. La variante `Assets/IronRequiem/Prefabs/Weapons/BigBot_CollectibleCannon.prefab` conserva las mallas reales y los tres LOD y añade los componentes existentes de armas robadas. `Weapon_BigBot_Temporary.prefab` usa el modelo real del cañón equipado y reutiliza carga, disparos, energía, humo crítico y explosión existentes.

BigBot_Destructible_v01 referencia el nuevo módulo; sus variantes heredan el cambio. El desprendimiento sigue siendo único y activa el comportamiento de emergencia existente. No se crea una segunda IA o inventario.

Los cañones recogibles tienen energía por instancia. No se reciclan ni vencen mientras están equipados. Permanecen hasta 120 segundos en el mundo; se limitan a ocho instancias y se retira primero la más antigua que no esté equipada. El pool decorativo original permanece como alternativa para prefabs sin módulo recogible.

Controles conservados: **E recoge, F golpea, G lanza**. El disparo robado mantiene la carga y liberación existentes.

## Calentamiento

OverheatBehavior conserva la relación entre consumo, enfriamiento, movimiento de cápsulas y vapor. El gradiente pasa de cian a naranja y rojo. PrimaryModuleHeatPresentation aplica la misma señal a todas las ranuras de las cápsulas, incluyendo las superficies opacas que ocultaban el cambio del cristal. Usa MaterialPropertyBlock y restaura colores al enfriar; no modifica materiales compartidos ni añade daño o mecánicas de sobrecalentamiento. Vapor limitado a 20 partículas; sin luces adicionales.

## Verificación

Última pasada completa del Test Runner en Play Mode: **5 aprobadas, 0 fallidas**, 02/11/2026 a las 15:47, America/La_Paz. Las pruebas comprueban:

1. Tamaño del módulo, movimiento y ADS, disparo, interfaz única por arma y vuelta al antebrazo/mano original al retirar el módulo. 169 frames alineados, 14 disparos y cero vértices del corte proximal dentro de cámara.
2. Disparo sostenido, cápsulas opacas y transparentes con emisión roja, vapor y retorno al color frío tras enfriar. Señal roja medida: RGB aproximado (3,14; 0,032; 0,004); enfriada: (0,07; 0,65; 0,85). Capturas revisadas.
3. Desprendimiento real del Big Bot al 25 % de vida: recoger con E, disparar con carga, consumir energía, lanzar con G, recoger de nuevo sin restablecer energía, humo crítico y una única explosión al lanzarlo.
4. Desprendimiento real del T-07: recogida, F reservado al golpe, rechazo detrás de paredes y fuera de alcance, cambio entre principal y robada conservando energía y acople.
5. Muerte del jugador: liberación del módulo del Big Bot con su energía intacta y ocultación de los brazos FPS.

La comprobación inicial detectó que las superficies opacas mantenían las cápsulas cian aunque el cristal recibía el color caliente; se corrigió mediante la presentación por ranura. También se alargó el manguito y el extremo proximal oculto para cubrir los cortes. Los dos fallos posteriores pertenecían a la prueba: leer índices de una malla no legible en Play Mode y permitir que el controlador deshiciera la inclinación de cámara hacia el módulo del suelo. La prueba usa ahora la malla horneada y una pose de cámara fija para la consulta de recogida.

Las pruebas usan dispositivos virtuales y poses reproducibles, sin activar ventanas ni llamar GameView.Focus. Las pruebas de recogida fijan la cámara para impedir que el controlador sobrescriba el ángulo hacia el suelo; movimiento y ADS se prueban por separado con el controlador activo. No son una partida manual ni un build de Player.

Auditoría tras la pasada completa: sin errores de compilación capturados ni scripts ausentes; ModularArmLab limpio y fuera de Play. Comparación de 2.114 archivos existentes: cambiaron únicamente BigBot_Destructible_v01, el prefab equipado del T-07 y los tres scripts de integración de destrucción/restos/presentación del jugador. Las escenas anteriores, v06, plantilla Assets/FPS, modelos fuente, Packages y ProjectSettings se conservaron. No se midieron FPS ni se añadió comportamiento de combate nuevo.

Revisión adicional del T-07: una prueba específica aprobada, con cámara al frente para inspeccionar el acople y captura lateral. Renderer equipado activo y dirección de boca coincidente con la cámara (producto escalar 1). Se conservan su malla y materiales existentes; esta tarea no rehace el arte del cañón robado. Evidencias `play-t07-view.xml`, `t07-view.txt`, `t07-equipped.png` y `t07-equipped-joint.png`. Auditoría final a las 15:52:45: laboratorio limpio, fuera de Play, sin errores de compilación capturados ni scripts ausentes. Se retiraron las solicitudes automáticas completadas.

Evidencias: `Tools/IronRequiem/Reports/ModularArm`. Respaldo: `Tools/IronRequiem/Backups/20261102-modular-arm-pickup`. La comprobación C# aislada usa el compilador y las referencias existentes de Unity; sus DLL quedan en Tools y no sustituyen las del Editor.

Herramientas: APIs nativas del Editor, Test Runner, dispositivos virtuales del Input System, Python para respaldos y hashes. Skills FPS, armas modulares y caveman. No se instalaron paquetes, consumieron créditos ni modificaron los modelos fuente.
