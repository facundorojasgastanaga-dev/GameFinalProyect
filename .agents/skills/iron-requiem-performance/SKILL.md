---
name: iron-requiem-performance
description: Medir y optimizar el rendimiento para PC de IRON REQUIEM en geometría, LOD, materiales, draw calls, pooling, físicas, enemigos y efectos, a partir de un problema o presupuesto concreto.
---

Empieza por una captura reproducible en el hardware objetivo: resolución, calidad, cantidad de enemigos, armas y efectos, frame time CPU/GPU y asignaciones. Distingue Editor y Player. No atribuyas mejoras a FPS sin controlar condiciones y sincronización vertical.

Inspecciona Assets/FPS/Scripts/Game/MeshCombiner y MeshCombineUtility antes de duplicarlos. Conserva URP y materiales existentes. Usa Profiler, Frame Debugger y Profile Analyzer disponibles; no instales otro perfilador por defecto. Identifica si el cuello de botella es CPU, GPU, memoria o importación.

Deriva presupuestos de triángulos, LOD, sombras y draw calls de las medidas y visibilidad real; evita límites universales. Comprueba triángulos y submeshes con el informe T-07. Crea LOD y colisiones en copias o variantes, nunca modificando el modelo original sin autorización específica.

Para pooling reinicia munición, energía, eventos, partículas, NavMeshAgent, colliders y daño. Comprueba el comportamiento reutilizado. Evita instancias de material accidentales; considera MaterialPropertyBlock y verifica su efecto sobre SRP Batcher. Mide transparencia de partículas, frecuencia de rutas y físicas con varios enemigos.

Verifica la misma carga antes y después, compilación y mecánicas afectadas. Entrega medidas comparables, cambio aplicado y límites de la prueba. Preserva velocidad, extracción de armas y OVERDRIVE; no elimines una mecánica principal para obtener una optimización prematura.
