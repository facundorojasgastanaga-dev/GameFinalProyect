# T-07 Exterminador: inspección del modelo fuente

Inspección del 28 de octubre de 2026 con Unity 6000.5.5f1 y Blender 5.2.3 LTS. Fuente conservada: `Assets/IronRequiem/Art/Enemies/Exterminador/Models/Meshy_AI_Rustborn_Sentinel_20261028_texture.fbx`.

## Resultado

El modelo es una **malla estática única**, `Mesh_0`, con **10.370 triángulos**, un submesh y un material. No tiene armature, huesos, grupos de pesos, bind poses, SkinnedMeshRenderer, Avatar ni clips de animación. La configuración Generic del importador no constituye rigging.

Blender informa 5.089 vértices; Unity importa 9.003. Los recuentos se refieren a representaciones distintas de la misma superficie y el número de triángulos coincide; divisiones por normales/UV pueden aumentar vértices en el importador. Blender encontró **una sola isla conectada**, sin bordes de contorno ni aristas compartidas por más de dos caras. Esto no demuestra que la topología sea apta para deformación en articulaciones.

El cañón **no es un objeto separado**, ni una isla desconectada. Separar por piezas sueltas no lo extraerá. No se modificó la geometría ni se generó un esqueleto.

## Escala, orientación y pivote

Blender importa dimensiones aproximadas X/Y/Z de **1,167 / 0,871 / 1,897 m**, con Z vertical. Unity conserva en la raíz del FBX un giro X de **270°** y escala **100/100/100**; aplicando esa transformación a los bounds de la malla, el volumen en Unity es aproximadamente **1,167 m de ancho, 1,897 m de alto y 0,871 m de fondo**.

No interpretar el tamaño local de la malla sin esa transformación: sus bounds internos son cien veces menores. El origen está cerca del centro del volumen, no en los pies ni en una articulación del cañón. Para una futura variante conviene un root de gameplay en el suelo y sockets propios; conservar la transformación importada como hijo. La dirección visual del frente y los ejes de cada articulación requieren revisión al preparar el rig, no se deducen del nombre del archivo.

## Materiales

Unity carga `Material.001` con shader **Universal Render Pipeline/Lit**, compatible con la pipeline actual. Sin embargo, la auditoría del material no encontró texturas asignadas. La importación del material está incompleta para reproducir el aspecto texturizado.

Blender sí encuentra cuatro imágenes incrustadas de **2048 × 2048**: albedo `Image_0`, normal, roughness y metallic. Sus rutas `.fbm` en el informe son referencias del FBX; la bandera `packed: true` confirma que los datos están incrustados. No se extrajeron ni se reasignaron materiales al original durante esta tarea. Para una copia de trabajo habrá que extraerlas, marcar el normal map correctamente y preparar los canales que espera URP/Lit (la roughness no se conecta directamente como smoothness).

## Lo que falta para movimiento y arma desmontable

1. Crear una copia de trabajo y seleccionar geométricamente cuerpo, piernas, brazos, cañón y unión. Diseñar superficies de corte y tapas donde deban desprenderse; una separación automática por islas no resuelve esta malla.
2. Decidir articulación rígida por piezas para el robot o rig con pesos. Preparar pivotes de cadera, rodillas, hombro, cañón y boca de disparo. Revisar intersecciones y topología en las zonas de movimiento antes de animar.
3. Crear y validar el rig de la copia. Solo entonces producir el primer ciclo de locomoción, con pies estables y velocidad coherente con NavMeshAgent. Animation Rigging añade constraints; no crea por sí mismo huesos ni una animación de caminar.
4. Crear un Animator y una variante de prefab, reutilizando EnemyController/EnemyMobile cuando corresponda. La plantilla consulta MoveSpeed, Attack, Alerted y OnDamaged. No se conectó el T-07 a estos componentes en esta configuración.
5. Preparar el módulo de arma como prefab separado con socket, punto de daño de la unión, collider y estado de recogida. Integrarlo posteriormente con WeaponController y PlayerWeaponsManager; todavía no hay gameplay modular ni OVERDRIVE implementados.

## Evidencia y herramientas

- `Tools/IronRequiem/Reports/t07-blender.json`: estructura, topología, texturas y hash del original.
- `Tools/IronRequiem/Reports/unity-audit.json`: estructura y material tal como Unity realmente los importó.
- `Tools/IronRequiem/Reports/t07-export-verification.json` y `t07-export-reimport.json`: exportación de una copia temporal y reimportación con dimensiones y 10.370 triángulos conservados. El original mantuvo su SHA-256.
- `Tools/IronRequiem/Blender/blender_asset_tool.py`: inspección, selección de objetos, separación de islas, pivotes por coordenadas y exportación a un archivo nuevo. La separación y la edición de pivotes son opciones explícitas para copias; no se aplicaron al T-07 original.
