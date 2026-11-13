---
name: iron-requiem-fps
description: Desarrollar o ajustar movimiento y combate FPS de IRON REQUIEM, reutilizando el controlador y las armas de la plantilla existente. Usar para salto, dash, slide, control aéreo, disparos e impactos.
---

Trabaja sobre Unity 6000.5.5f1, Input System y URP existentes. Lee AGENTS.md y el informe de configuración en Docs/IronRequiem antes de cambiar arquitectura.

Revisa primero PlayerCharacterController y Managers/PlayerInputHandler en Assets/FPS/Scripts/Gameplay, Managers/PlayerWeaponsManager y Assets/FPS/Scripts/Game/Shared/WeaponController. El jugador ya usa CharacterController; no añadas otro controlador ni Rigidbody que compita con él. Las acciones actuales están en Assets/FPS/InputSystem_Actions.inputactions y se consultan mediante InputSystem.actions.

Implementa solo la mecánica solicitada. Prefiere componentes de extensión o una modificación pequeña del dueño actual del movimiento. Mantén una única autoridad para velocidad, gravedad y orientación. Para dash y slide define entrada, duración, salida y comportamiento en pendientes y colisiones. Conserva la cámara FPS y sus referencias; Cinemachine no autoriza reemplazarla.

Usa armas y proyectiles existentes antes de crear un segundo sistema. Separa detección de impactos, daño, retroceso y efectos; conserva la respuesta inmediata al disparo. El acoplamiento con armas extraídas debe pasar por PlayerWeaponsManager y WeaponController.

Verifica compilación, controles existentes, movimiento diagonal, suelo y aire, obstáculos, cambio de arma y pausa. Comprueba que MainScene y Player.prefab no tienen cambios ajenos a la tarea. Si haces una prueba en Play Mode, distingue prueba visual de prueba real de entrada y restaura el estado anterior sin guardar cambios de ejecución. Informa lo comprobado y los casos pendientes.
