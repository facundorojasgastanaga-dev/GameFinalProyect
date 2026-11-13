# Pistola y carga visual del cañón

Actualización 30/10: la variante equipada optimizada utiliza ahora la boca medida y la carga magenta descritas en [CANNON_CHARGE.md](CANNON_CHARGE.md). Este informe conserva el estado anterior de la primera versión y sus pruebas.

29 de octubre de 2026. Complementa DESIGN_V02_UPDATE.md; sustituye la representación provisional de la pistola y su intervalo anterior de 0,5 s.

## Cambios

- Weapon_IR_Pistol sigue siendo variante de Weapon_Blaster y conserva WeaponController, PlayerWeaponsManager, Input System, proyectil de daño 22, audio, retroceso y disparo semiautomático. Ahora usa el prefab anidado PistolModule_1000_Visual y su socket Muzzle. La geometría antigua se desactiva en la variante y el Animator antiguo no anima la representación nueva. El movimiento del arma y el retroceso continúan en el administrador existente.
- Cadencia de la pistola: **0,4 s entre disparos**, frente a 0,5 s (2,5 frente a 2 disparos/s teóricos). Mantener el botón no dispara repetidamente.
- CannonChargePresentation muestra un núcleo cian creciente y partículas orbitales al mantener el disparo del cañón. El máximo tiene un pulso pequeño. Cancela y limpia las partículas al soltar, cambiar, pausar, morir, lanzar o entrar en estado crítico, siguiendo CannonChargeShot. No emite humo.
- ChargedProjectileDamage recibe la potencia antes de los callbacks de impacto existentes. Interpola daño 25–65 y radio de colisión **0,04–0,14 m**; la esfera visible tiene el diámetro correspondiente **0,08–0,28 m**. La potencia sigue limitada por la energía disponible. El rastro corto mejora su lectura y no cambia la velocidad existente de 300 m/s.
- El proyectil derivado tiene escala raíz uno y Tip/Root coincidentes: evita heredar el offset longitudinal de 10 m de la representación original. ProjectileStandard conserva movimiento, barrido de colisión, impactos y daño.
- El humo sigue reservado exclusivamente al cañón crítico que va a explotar. No se modifica la lógica de peligro, drenaje, energía, explosión ni la pistola vacía sin humo.

## Assets y herramientas

Los cambios de prefab se aplican con PrefabUtility desde `Tools > Iron Requiem > Weapons > Apply Pistol Module and Charge Feedback` (Ctrl+Shift+F11). Rechaza Play Mode y escenas modificadas. Solo guarda las tres variantes propias de pistola, cañón equipado y proyectil cargado. El generador anterior de diseño v02 también conserva la integración al volver a ejecutarse.

Se reutilizan el modelo/material URP y sockets ya entregados, la máscara local ParticleSoftCircle y los sistemas existentes de armas y proyectiles. Dos materiales URP compartidos, un núcleo de malla y un emisor persistente de hasta 32 partículas por arma cargando. Sin luces adicionales ni instancias de material por frame o proyectil. Esto es un límite de implementación; no acredita rendimiento de oleadas.

Skills aplicadas: iron-requiem-fps, iron-requiem-modular-weapons, iron-requiem-performance y herramientas Unity CLI/Editor. Computer Use no pudo conectarse al helper nativo tras su recuperación documentada. Al no haber Editor abierto (proceso ausente y lock libre), se aplicaron los prefabs con el Unity 6000.5.5f1 instalado en modo batch. La verificación final se ejecutó en un Editor normal mediante CharacterVerificationRunner y Test Framework. Se comprobó que no hubiera otro Editor antes de cada arranque. No se instala Pipeline ni ningún paquete. La skill VFX consultada contiene un harness para Mac/Unity 6.3 que no se incorpora a este proyecto Windows/6.5.

Respaldo: `Tools/IronRequiem/Backups/20261029-weapon-feedback/`, hashes de 1.869 archivos y copias de assets propios, configuración, documentación e informes anteriores. Los modelos fuente, prefab visual entregado, MainScene, escenas de prueba y configuración se comparan por hash. Scripts reproducibles: `run_weapon_feedback.ps1` y `verify_weapon_feedback.ps1` en Tools/IronRequiem.

## Verificación

Verificación final del **29 de octubre de 2026, 14:37 America/La_Paz (18:37 UTC): 28/28 pruebas aprobadas**, 21 PlayMode y 7 EditMode, incluidos los seis escenarios originales. Entradas virtuales del Input System, avance de frames y bloqueo real del cursor del Editor; no se relajaron las condiciones de entrada del juego. Informes: `weapon-feedback-play.xml`, `weapon-feedback-edit.xml` y `weapon-feedback-verification.json`, en Tools/IronRequiem/Reports. El Editor quedó en CharacterLab, fuera de Play Mode, escena limpia, sin scripts ausentes ni errores C# capturados. No fue una sesión manual de sensaciones ni un benchmark de oleadas.

Se abrieron y revisaron visualmente `weapon-feedback-pistol.png` y `weapon-feedback-charge.png` (2560 × 1440), y `weapon-feedback-projectile-size.png` (960 × 540). La última compara los extremos de potencia con cámara fija y el mismo prefab; no representa la velocidad de vuelo. Se ve el módulo nuevo equipado, energía cian en el cañón y la diferencia de diámetro. La presentación es una primera versión; el pulido artístico y sonoro puede continuar con una sesión manual.

Diagnóstico conservado: la primera aplicación detectó un componente ausente por la semántica de null de Unity en Editor, corregido antes del guardado final del cañón. La aplicación posterior compiló y guardó los tres prefabs, pero Unity sufrió un cierre nativo en SerializationCache/UnloadDomain al descargar el dominio (`weapon-feedback-apply.log`). No se atribuye ese cierre a una causa no demostrada; queda pendiente investigar su reproducción. El Editor normal posterior completó las 28 pruebas y permanece abierto. El intento batch de gameplay obtuvo 1/21: no podía activar el cursor FPS; se conservan `weapon-feedback-play-batch-failed.xml` y su log. Se sustituyó esa ejecución por la suite completa del Editor normal, sin omitir casos ni cambiar el input gate. La herramienta de ejecución ya evita ofrecer PlayMode en batch.

Pruebas añadidas o ampliadas: cadencia real con pulsaciones nuevas; representación y socket del modelo; crecimiento visual y radio del disparo parcial/completo; partículas reales durante carga y limpieza al cancelar; un objetivo lateral que el proyectil pequeño no alcanza y el grande sí, con daño real. Se conservan las pruebas de movimiento, cámara, inventario, IA, peligro y UI existentes.

La comprobación final detectó únicamente un reordenamiento automático de los mismos símbolos Standalone al iniciar el Editor. Se compararon ambos conjuntos y se restauró el archivo ProjectSettings.asset exacto del respaldo; ningún símbolo se añadió o eliminó. La verificación final de las **14:47 America/La_Paz** confirmó `allChecksPass: true` frente a los 1.869 archivos: solo los siete cambios existentes previstos, sin cambios inesperados ni metadatos faltantes. MainScene, CharacterLab, modelos, prefab visual fuente, scripts base FPS, Input System, URP, Packages y ProjectSettings conservan sus hashes originales.
