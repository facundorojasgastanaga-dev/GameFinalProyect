# Contexto de animaciones de IRON REQUIEM

Actualizado: 15/11/2026, America/La_Paz. Informe de trabajo: `HANDS_HUD_CAMPAIGN_DIAGNOSTIC_20261115.md`.

## Autoridad de poses

`HandPoseController` (orden 1000) aplica rotaciones locales absolutas de `HandPoseLibrary`: Open, Relaxed, Fist, Grip e Inspect. Las bibliotecas FPS L/R y cinemáticas L/R están en `Assets/IronRequiem/Art/Player/HandPoses/`. Se obtienen de las matrices de bind de cada skin; no se mezclan ejes entre ProtagonistV01 FPS y CapsuleRobotRig/Mixamo.

`ProtagonistHandsPresentation` (110) conserva inventario, visibilidad, sockets, IK de antebrazos y overrides de estación. En la campaña delega los dedos al controlador; el fallback `HandFistRig` queda para laboratorios anteriores. `ProtagonistMelee` (100) decide carga/ataque: M1 sin arma usa derecha; F sin arma usa izquierda; F con arma usa antebrazo derecho sin retirar el arma ni sumar calor. La izquierda queda oculta si no actúa.

El rig FPS original mezcla pesos entre piezas mecánicas y deformaba el puño. Se adoptó el Plan B autorizado: puño rígido mecánico generado localmente con Blender, malla izquierda reflejada y collar de muñeca. Archivo de autoría: `Tools/IronRequiem/Blender/build_closed_fist.py`; exportación y métricas en `Tools/IronRequiem/Exports/PlayerRigidFist/`. No se sobrescribió ningún FBX fuente ni se usó Meshy.

`FirstPersonUpperArms` (120) coloca hombro, brazo superior y articulación de codo extraídos de CapsuleRobotRig entre la guía fuera del cuadro y el codo FPS. La escala se hornea en copias de la malla; huesos a escala uno. También funciona con el antebrazo sustituido por cañón/módulo. `FistChargePresentation` sigue la mano activa o el antebrazo montado.

## Cinemática y estación

`WakeUpRobotPose` (200) restaura el cuerpo y resuelve IK; `HandPoseController` en modo timeline aplica muestras deterministas de poses absolutas. Inspect y Grip usan transiciones suaves. La ventana de puño también entra/sale suavemente. No se usa deltaTime para muestrear los dedos de la cinemática.

`HangarIntroCinematic` sigue la dirección del cuerpo desde detrás del hombro tras mirar la cinta, revela los Carroñeros y conserva el vuelo a la cabeza. Prueba posiciones con SphereCast; si ninguna es segura conserva el plano de autoría. Reproducción completa y salto comparten la salida existente.

`WeaponMountStation` mantiene el override del brazo FPS. `StationArmRig` controla otro robot: el brazo mecánico de la estación. No es un escritor duplicado de los dedos del protagonista.

## HUD y entrada

`RetroStatusBar`, `RetroDigitsGraphic` y `RobotPortraitGraphic` usan el Canvas uGUI existente: energía/calor, salud, nueve slots, visor, carga y OVERDRIVE. Números bitmap mediante geometría 5x7; no se necesita una fuente dinámica para ellos. Se conservan retícula, prompts y banners. El visor responde a salud, dirección del daño, extracción/ejecución y OVERDRIVE.

La pantalla principal antigua de `RequiemCampaign` fue retirada a cuarentena por petición posterior explícita del usuario. IntroMenu animado es la entrada normal de Play y la primera escena ya existente del build. No ejecutar herramientas de autoría antiguas del menú para reparar manos o HUD. Las herramientas de preview/pruebas restauran `playModeStartScene` al terminar.

## Verificación y mantenimiento

Menús de herramientas: `Tools > Iron Requiem > Hands HUD Campaign`. Autoría exige salir de Play y no tener escenas modificadas. Las pruebas se ejecutan en el Editor ya abierto, sin un segundo Unity. Los resultados reales y pendientes se registran en el informe; compilación sola no certifica controles ni calidad visual.

Pruebas EditMode de poses: 3/3 superadas (rotaciones absolutas, ausencia de acumulación, vuelta de blend, escala y muestreo determinista). La revisión de capturas y PlayMode sigue en curso en esta intervención. No usar capturas previas con HUD ausente como evidencia de entrega.
