---
name: iron-requiem-modular-weapons
description: Implementar la extracción y uso temporal de armas enemigas en IRON REQUIEM, incluidos puntos de unión, desprendimiento, recogida, energía, OVERDRIVE y expulsión al agotarse.
---

La mecánica central es destruir una unión del robot, desprender su módulo, recogerlo, equiparlo temporalmente, consumir energía o munición, activar OVERDRIVE y expulsar o destruir el módulo agotado. Implementa únicamente la parte solicitada y conserva el resto como límites de integración.

Inspecciona WeaponController, PlayerWeaponsManager, WeaponPickup, Health y Damageable. Usa adaptadores o composición antes de crear otra jerarquía de armas. Separa definición de arma, munición por instancia, integridad de unión y representación visual. No compartas estado mutable de munición mediante un ScriptableObject.

Define una transición única entre Attached, Detached, Available, Equipped y Exhausted, o un diseño equivalente adecuado al código real. Cada desprendimiento, recogida y expulsión debe ejecutarse una vez; libera eventos al desactivar o devolver objetos al pool. La IA recibe pérdida de capacidad mediante un contrato explícito, sin conocer al jugador.

Guarda sockets y pivotes en una variante de prefab. Lee el informe T-07 antes de asumir que su cañón es separable. Para edición 3D usa Tools/IronRequiem/Blender/blender_asset_tool.py sobre una copia. Una geometría fusionada requiere selección humana o identificación verificable, no separación por un nombre supuesto.

Verifica compilación, referencias, desprendimiento repetido, recogida simultánea, inventario lleno, consumo, agotamiento, muerte del portador, OVERDRIVE y retorno al pool cuando correspondan al cambio. Conserva las armas iniciales y cámara FPS. Reporta qué transiciones se probaron realmente.
