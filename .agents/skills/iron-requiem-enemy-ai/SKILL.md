---
name: iron-requiem-enemy-ai
description: Desarrollar IA y daño localizado de robots de IRON REQUIEM con navegación, detección, patrulla, persecución y ataques, incluidos cambios de conducta por componentes perdidos.
---

Reutiliza Assets/FPS/Scripts/AI: EnemyController es dueño de Health, Actor y NavMeshAgent; EnemyMobile tiene Patrol, Follow y Attack; DetectionModule y NavigationModule controlan percepción y parámetros. Consulta EnemyTurret y PatrolPath antes de duplicarlos.

AI Navigation está instalado, pero la plantilla conserva Assets/NavMeshComponents. Sus componentes tienen namespaces distintos; no migres ni elimines superficies existentes sin comprobar referencias y datos de navegación. No rebakees MainScene por una tarea de código.

Define transiciones, alcance, línea de visión y pérdida de objetivo explícitos. Evita búsquedas globales por frame y solicitudes de ruta innecesarias. Separa conducta de integridad física: perder un cañón modifica capacidades mediante eventos, sin destruir el controlador de navegación. Respeta Health y Damageable y evita aplicar dos veces el mismo daño.

Antes de animar T-07, lee Docs/IronRequiem/T07_INSPECTION.md y los JSON en Tools/IronRequiem/Reports. Generic no demuestra rigging. No sustituyas el FBX original ni inventes huesos, clips o pivotes. EnemyMobile espera MoveSpeed, Attack, Alerted y OnDamaged; adapta un futuro Animator en una variante de prefab cuando se autorice.

Verifica compilación, referencias, patrulla, detección y pérdida de objetivo, rutas incompletas, ataque y muerte. Para cambios modulares comprueba la conducta antes y después de desprender cada pieza. Reporta pruebas reales y dependencias del rig o NavMesh ausentes.
