# Trabajo 1 — Jugador v0.1

Informe histórico de preparación. La configuración vigente de inventario y mecánicas está en `DESIGN_V02_UPDATE.md`: pistola única inicial y cañón robado con energía; la escopeta original se conserva fuera del inventario de prueba.

Preparado el 29 de octubre de 2026 sobre Unity 6000.5.5f1.

La variante `Assets/IronRequiem/Prefabs/Player/Player_IR_v01.prefab` hereda de `Assets/FPS/Prefabs/Player.prefab`. Conserva PlayerCharacterController, CharacterController, PlayerInputHandler, PlayerWeaponsManager, cámaras y sockets FPS. Añade la escopeta existente al inventario inicial para comprobar el cambio de arma. No incorpora otro controlador, Rigidbody, dash ni cambios a las acciones.

La respuesta de dirección artística del usuario se concretó en una representación provisional propia: robot de conciencia humana de 1,80 m, pose A, casco con visor horizontal, placas claras, estructura de grafito, naranja industrial, luces cian y cinco dedos por mano. No es un modelo final texturizado: usa formas simples y materiales planos, sin desgaste pintado ni animación corporal. Está en `Assets/IronRequiem/Art/Player/Prototype/SurvivorRobot_Visual.prefab` y tiene sockets de antebrazo para una integración posterior.

La geometría se construyó mediante APIs del Editor y se agrupó con MeshCombineUtility de la plantilla: 588 triángulos y cuatro materiales compartidos. Los meshes resultantes se guardan como assets nativos editables. No hubo generación remota, compras ni créditos.

PlayerBodyPresentation solo controla la presentación: oculta el cuerpo en ejecución conservando su sombra para evitar que el casco o la pose A tapen la vista FPS. El cuerpo es visible en el Editor y en la figura de inspección de CharacterLab. Las armas y su respuesta visual siguen siendo las de la plantilla.

La escena de prueba es `Assets/IronRequiem/Scenes/CharacterLab.unity`. Para abrirla basta con el asset; no requiere añadirla a Build Settings. Contiene el jugador funcional y una figura visual independiente de inspección, sin un segundo controlador.

La evidencia de pruebas está en `Tools/IronRequiem/Reports/character-play-visible.xml`, `character-edit-visible.xml` y `character-verification.json`. Solo sus resultados finales permiten afirmar controles comprobados. Las primeras ejecuciones en batch se conservan como diagnóstico y no son la validación final.

Pendiente: aprobar/refinar la silueta, biseles y desgaste, rig y animaciones corporales, brazos propios visibles en primera persona y adaptación de manos a cada arma. La representación provisional se puede sustituir conservando el controlador y los sockets FPS.
