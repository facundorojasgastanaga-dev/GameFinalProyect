# Trabajo 2 — Enemigo T-07 nuevo v0.1

Fuentes suministradas el 29 de octubre de 2026: `T-07.fbx` y `T-07(Texture).glb` en la carpeta vistasROBOT del usuario. Son distintas del Rustborn Sentinel anterior, que permanece intacto.

| Inspección real | FBX nuevo | GLB nuevo |
|---|---:|---:|
| Triángulos | 50.000 | 50.000 |
| Vértices importados por Blender | 24.944 | 40.820 |
| Objetos de malla | 1 | 1 |
| Armatures / huesos / vértices con pesos / acciones | 0 / 0 / 0 / 0 | 0 / 0 / 0 / 0 |
| Altura mundial de origen | 1,126616 m | 1,126616 m |
| Islas de índices | 2 | 1.907 |

Las islas del GLB están fragmentadas por su representación de índices; no equivalen a piezas mecánicas separables. Los bounds de ambos formatos coinciden. La inspección y las vistas frontal, lateral y posterior están en `Tools/IronRequiem/Reports/new-t07-fbx.json`, `new-t07-glb.json` y sus PNG. El FBX contiene albedo, normal, roughness y metallic de 4096². El GLB usa metallic/roughness empaquetados. No hay un rig o clips ocultos detrás de la configuración Generic.

Se trabajó con el FBX inspeccionado en Blender 5.2.3 LTS. `Tools/IronRequiem/Blender/prepare_enemy.py` protege su SHA-256 y prepara únicamente una salida derivada. Extrae las texturas, empaqueta metallic en R y smoothness = 1 − roughness en A, escala la copia a 2,05 m y separa el conjunto de antebrazo derecho y cañón mediante una selección espacial revisada visualmente. No separa por el nombre supuesto de una isla.

La salida `Assets/IronRequiem/Art/Enemies/T07New/T07_Prepared.fbx` tiene cuerpo de 42.617 triángulos y módulo de 7.696: 50.313 incluyendo las tapas añadidas. El corte y las tapas son provisionales; requieren limpieza artística para producción. El archivo editable está en `Tools/IronRequiem/Blender/Working/T07_Working.blend`, fuera de Assets para evitar conversiones automáticas. Las fuentes externas y el T-07 anterior no se sobrescribieron.

El material explícito T07_URP usa URP/Lit con las tres asignaciones correctas. Las imágenes extraídas mantienen 4096² en disco, con importación a máximo 2048, mipmaps, normal map y datos metallic/smoothness lineales. Los mapas crudos quedan conservados.

`Assets/IronRequiem/Prefabs/Enemies/T07_IR_v01.prefab` es una variante de Enemy_HoverBot. Reutiliza EnemyController, EnemyMobile, DetectionModule, NavigationModule, NavMeshAgent, Health, Damageable, WeaponController, proyectiles, impactos y barra de salud de la plantilla. El cuerpo visual nuevo es independiente del jugador.

Parámetros de la prueba: salud corporal 160, integridad de unión 35, detección 16 m, ataque 10 m, pérdida del objetivo tras 2 s sin visión, navegación a 3,5 m/s y cadencia del cañón de 0,85 s. El punto naranja señala la unión. La unión consume su propia salud; no aplica el mismo daño también al cuerpo. DetachableCannon desprende el módulo una vez, lo vuelve físico y suspende el ataque de EnemyMobile. DisarmedEnemyRetreat reutiliza el mismo agente y percepción para retirarse. La muerte del portador también desprende el módulo.

Los sockets de cabeza, hombros, codos, caderas y rodillas son guías de preparación. No son un esqueleto importado ni un rig de deformación. El Animator contiene el contrato MoveSpeed/Attack/Alerted/OnDamaged sin fingir clips fuente. La locomoción visual es estática: el enemigo se desplaza con NavMesh, todavía no camina con animación.

CharacterLab usa `Unity.AI.Navigation.NavMeshSurface` 2.0.14, recoge solo la geometría hija de su arena y tiene su propio NavMesh guardado. La plantilla conserva `UnityEngine.AI.NavMeshSurface` en Assets/NavMeshComponents; no se migró ni eliminó. MainScene no se rebakeó.

Pendiente: limpiar cortes y pivotes con revisión de artista, rig verificable, pesos si se opta por skinning, ciclos de caminar/ataque/reacción y pruebas con múltiples enemigos. La recogida, equipamiento temporal, energía, OVERDRIVE y expulsión del arma no forman parte de esta primera preparación; el módulo desprendido es físico y desaparece a los 30 s.

Pruebas y límites finales: consultar `character-play-visible.xml`, `character-edit-visible.xml`, `character-verification.json` y el informe conjunto. Una captura o una compilación no demuestra por sí sola todas las transiciones.
