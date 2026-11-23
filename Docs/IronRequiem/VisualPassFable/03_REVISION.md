# Revisión independiente: pase visual (F0 frente a F2_despues)

He revisado solo el material de `Tools/IronRequiem/Reports/VisualPassFable/F0` y `F2_despues`: las capturas, `zones.csv`, `robots.txt` y `carronero.txt`. No he leído `antes_despues.md` ni ningún documento de la propuesta. Las imágenes las amplié con recortes propios.

Imágenes revisadas:

- **Salas.** R01, R03, R06, R08, R11, R14, R17 y R18: `vp1_vacio` y `vp1_T07_15m`, antes y después. Las tomas de 25 m en R01, R03, R11 y R18, y además R14, R15 y R16. Como control, Room01_Foundry y Pursuit05.
- **Robots.** Los 7 robots en patrulla, ataque y alerta de frente (la torreta y el Carroñero solo tienen `spawn`), más la toma de 15 m.
- **Carroñero.** Las 8 tiras de antes y de después.

## 1. Datos de `zones.csv` (Map01, 36 puntos de vista)

| Medida | ANTES | DESPUÉS |
|---|---|---|
| `frame_mean` de los `vacio` (media aproximada) | ≈ 11 (de 3 a 19) | ≈ 36 (de 17 a 66) |
| Puntos de vista con `frame_mean` ≥ 45 (objetivo) | 0 / 36 | 5 / 36 (R04 vp2, R15 ×2, R16 ×2) |
| Peores salas | R18 3, R10 4,6, R17 5,9 | R10 vp1 16,7, R08 vp1 18,7, R10 vp2 22 |
| `ratio` del T07 a 15 m (típico) | 1,7 – 5 | 1,03 – 1,5 (casi siempre por debajo de 1,5) |
| T07 a 25 m sin oclusión: `legible` | 27 / 27 | 7 / 27 (20 pasan a ILEGIBLE o débil) |
| `eye_px` a 5 / 15 / 25 m (típico) | ≈ 1850 / 100 / 12 | ≈ 650 / 40 / 1–5 |

- **Luz de las salas.** Ha subido unas 3 veces, pero el objetivo de 45 solo se cumple en el 14 % de los puntos de vista.
- **Contraste del enemigo.** Se ha hundido: el fondo ha subido de ~14 a ~40 y el robot sigue en ~35. Si el veredicto sigue siendo «legible» a 15 m, es solo gracias a los ojos.
- **Por qué baja `eye_px`.** El ojo nuevo es rosa o magenta y tiene el centro blanco, así que el clasificador de rojo lo detecta peor. En Pursuit05 a 5 m, `eye_px` baja de 2044 a 2, y sin embargo el ojo se ve en la imagen.
  - Parte de la caída es, por tanto, un artefacto de la medida.
  - Aun así, eso confirma que el ojo **no es rojo**.
- **Salas de control.** Su `frame_mean` no cambia (bien). En cambio, el T07 se ve más oscuro en todas:
  - `enemy_val` baja de ~34 a ~18, y `ratio` baja de 4,9 a 3,1 en Room03 y de 2,1 a 1,0 en Room01 a 25 m.
  - Algún cambio en el material del robot o en su luz de recorte se ha llevado también a la otra campaña.
- **Defectos de la captura, presentes antes y después:**
  - A 15 y 25 m, el T07 queda tapado en R02, R06, R08, R14 y R16 (`tapado_por_…`).
  - Desde R08, el robot «se autodestruye» al caer por debajo de cierta Y (`se_autodestruye_y-…`).
  - Esas comparaciones no sirven para medir la legibilidad.

## 2. Puntuaciones

| Criterio | ANTES | DESPUÉS | Justificación |
|---|---|---|---|
| (a) Legibilidad del espacio | 3 | 7 | En `R18_vp1_vacio`, antes la imagen era casi negra (3/255) y después se ven columnas, suelo, rampas a franjas y muros. En `R14_vp1_vacio` y `R01_vp1_vacio` ahora se leen suelo, coberturas y techo. Por debajo de 8 porque R10 (17–22), R08 vp1 (19) y R17 (28) siguen oscuras, y el objetivo de 45 se cumple en 5 de 36. |
| (b) Legibilidad del enemigo (5/15/25 m) | 6 | 5 | Antes, el T07 se recortaba en blanco sobre negro a cualquier distancia (`R01_vp1_T07_25m`), aunque el cuerpo no se veía. **A 5 m** se lee bien después. **A 15 m** se ve como silueta oscura con un punto rosa (`R01_vp1_T07_15m`). **A 25 m** queda como un bulto pequeño casi del tono del muro (`R03_vp1_T07_25m`, `R11_vp1_T07_25m`), con 20 de 27 tomas ILEGIBLES según la propia métrica. En `R16_vp1_T07_5m` el robot se tiñe de azul verdoso y se pierde contra el suelo y el pilar del mismo color (enemigo 27 sobre fondo 46): ahí empeora claramente. |
| (c) Ambiente | 4 | 6 | Sigue pareciendo un búnker y las zonas se distinguen por color: cálida en R01 y R14, azul acero en R03 y R18, verde azulado en R11 y R16, y el halo rojo de R03 se conserva. Pero la subida viene sobre todo de una luz ambiental plana (`ambient` pasa de #574738 a #BF9D7B en `robots.txt`). No hay charcos de luz ni sombras: muros y techo tienen la misma intensidad (`R01_vp1_vacio`, `R15_vp1_vacio`, gris lavado a 66). |
| (d) Ojos y luces de los robots | 4 | 5 | **Mejoras:** el Carroñero ya no tiene el punto rojo flotando encima (`Carronero_spawn_frente`, y las filas 3 de `04_girar`); la torreta pasa de cian a rojo (`Turret_Current_spawn_frente`). **Lo que falla:** el ojo del T07, SawBot, T07_Mech y Carroñero se ve **rosa o magenta con el centro blanco**, no rojo (`T07_Current_Repaired_patrulla_frente`; el MPB es `em=(2;0.1;0.24)`, con componente azul). En T07_Mech el «ojo» sigue **en la barriga**, ahora más grande. Hay **chispas rosas flotando** junto a la cabeza del BigBot y del cristal de la torreta. El **HoverBot ha perdido los estados**: antes era cian en patrulla, rojo en ataque y amarillo en alerta; ahora es siempre rojo. T07, Mech y SawBot dan lecturas idénticas en todos los estados (`eyePeak` ≈ 204 en todos). Se mantienen los contornos blanco-azulados dentados en todas las armaduras. |
| (e) Carroñero | 3 | 5 | **Correr** (`01_correr`): el deslizamiento del pie baja de 6,2 a 1,95 m/s y ya se lee la zancada, aunque sigue patinando. **Daño** (`03_dano`): ahora hay un retroceso visible del torso en las celdas 4–7; antes no había nada. **Ataque** (`02_atacar`): un leve gesto de levantar el brazo y la cabeza (celdas 20–24) antes del golpe; la anticipación existe pero es poco marcada. **Giro** (`04_girar`): casi igual, sin peso apreciable. **Muerte** (`05_morir`): idéntica, explota al instante en trozos con chispas que son rectángulos blancos planos, sin caer antes. **Reposo** (`07_reposo`) y **hurgar** (`08_hurgar`): sin cambios. **Andar** (`06_andar`): la prueba no vale (pide 1,8 m/s y mide 5,92) y el pie sigue patinando a 4,58 m/s. |

## 3. Defectos del DESPUÉS, de más a menos grave

1. **Ojo rosa o magenta en lugar de rojo, con el centro blanco quemado.** Se ve en `robots/T07_Current_Repaired_*_frente`, `SawBot_Current_*_frente`, `T07_Mech_*_frente`, `Carronero_spawn_frente` y `zones/*_T07_15m`. Contradice directamente la queja 3.
   - *Sugerencia:* quitar el azul del color de emisión (B = 0, usar algo como (4; 0,15; 0)). Limitar la intensidad para que el núcleo no se queme a blanco o se coma el tono con el tonemapping. Revisar el umbral de bloom.
2. **El enemigo pierde contraste al iluminar las salas.** El `ratio` a 15 m baja de ~2 a ~1,1; a 25 m, 20 de 27 tomas son ILEGIBLES; en `R16_vp1_T07_5m` el robot se confunde con el suelo azul verdoso. Es un empeoramiento respecto al ANTES.
   - *Sugerencia:* separar valores. O se aclara el robot (un poco de luz propia o de recorte cálida que no se tiña con el ambiente de la sala), o se oscurecen las zonas de fondo a la altura del torso. Otra opción es un contorno o luz de recorte por estado, con intensidad que crezca con la distancia.
3. **Ojo de T07_Mech en el abdomen.** Se ve en `T07_Mech_patrulla_frente` y en el resto de estados. El componente `Head/Sphere` usa el esqueleto del T07 y cae en el pecho del Mech; el anillo real de la cabeza sigue apagado.
   - *Sugerencia:* engancharlo al hueso o socket de la cabeza del propio Mech, o reutilizar el anillo de la cabeza como emisor.
4. **Los estados no se distinguen por la luz.** El HoverBot ha perdido el cian, el rojo y el amarillo que tenía (`HoverBot_Current_patrulla/alerta/ataque_frente`), lo que es una regresión. En T07, Mech y SawBot, patrulla, alerta y ataque son idénticos tanto antes como después. Solo el BigBot conserva verde, rojo y amarillo.
   - *Sugerencia:* mantener el rojo como «ojo» y codificar el estado con la intensidad o el pulso (patrulla tenue y fija, alerta parpadeo, ataque al máximo y con destello antes del disparo), no con el tono.
5. **Puntos o chispas rosas flotando fuera del cuerpo.** En `BigBot_Current_*_frente` aparece uno arriba a la izquierda de la cabeza; en `Turret_Current_spawn_frente`, uno sobre el borde del cristal. Coincide con `haloPx` alto (BigBot 757, SawBot 1235, Carroñero 1217).
   - *Sugerencia:* localizar qué renderer o luz puntual los genera (¿un emisor de `EnemyStateLights` sin malla visible?) y eliminarlo o fijarlo a su malla.
6. **Iluminación plana y lavada.** Se ve en `R15_vp1_vacio` (66, gris) y `R01_vp1_vacio`. La luz ambiental alta hace que muros, techo y suelo tengan la misma intensidad; se pierde el «búnker oscuro con focos».
   - *Sugerencia:* bajar el ambiente y conseguir los 45 con luces puntuales o de área sobre la ruta y las coberturas. Dejar zonas en penumbra con contraste local, no un velo uniforme.
7. **Salas que siguen por debajo del objetivo.** R10 (16,7 y 22), R08 vp1 (18,7), R17 (28), R06 y R09 (28 en vp1), R11 vp1 (26).
   - *Sugerencia:* añadir luz de ruta en esas salas en concreto en lugar de subir el ambiente global.
8. **El robot se oscurece en la campaña de control, que en teoría no se tocó.** En `Room01_Foundry_vp1_T07_15m` y `Pursuit05_vp1_T07_5m`, `enemy_val` pasa de ~34 a ~18. El cambio de material o de recorte del robot afecta a todas las escenas.
   - *Sugerencia:* comprobar si es intencionado; si no, separar el ajuste por escena o por volumen.
9. **Carroñero: muerte y giro sin trabajar, y el pie sigue patinando.** En `05_morir` muere por explosión instantánea con chispas que son quads blancos sin textura; en `04_girar`, el giro de 180° en 0,8 s no tiene peso; en `01_correr` el pie de apoyo aún se desliza a 1,95 m/s.
   - *Sugerencia:* añadir 6–10 frames de tropiezo y caída antes de la explosión, darle a las chispas material con color y que se desvanezca, inclinar el cuerpo al girar y fijar el pie con IK o ajustando la velocidad del clip.
10. **Contornos blanco-azulados dentados en las armaduras.** Se ven en todos los `robots/*_frente`, sobre todo en T07 y Carroñero. Persisten; probablemente es la queja 2 («luces raras»).
    - *Sugerencia:* revisar el término de recorte o fresnel del shader `IronRequiem/HardSurface`: reducirlo o darle un tono cálido, y aplicar antialiasing.
11. **Capturas que no sirven para comparar.**
    - El robot queda tapado a 15 y 25 m en R02, R06, R08, R14 y R16.
    - Se autodestruye por Y desde R08.
    - En `06_andar` la velocidad forzada no se aplica (pide 1,8 y mide 5,92).
    - *Sugerencia:* reubicar esos puntos de vista y corregir la captura antes del próximo pase.

## 4. Lo que queda por debajo de 8 y por qué

- **(a) Espacio, 7.** Cumple el objetivo de 45 en solo 5 de 36 puntos de vista, y R10, R08 y R17 siguen oscuras.
- **(b) Enemigo, 5.** Ha empeorado. La sala sube de valor y el robot no, y a 25 m se pierde; además, el ojo, que es lo que lo salva, no es rojo.
- **(c) Ambiente, 6.** Se ha conseguido luz «de relleno» y no de focos: plana, sin jerarquía de luz ni sombras.
- **(d) Ojos, 5.** El color está mal (rosa) y hay un ojo en la barriga, chispas flotando y estados que no se distinguen; el HoverBot incluso ha retrocedido.
- **(e) Carroñero, 5.** Hay mejoras reales en correr y en el daño, pero la anticipación es débil y la muerte y el giro no se han tocado. El pie sigue patinando y la prueba de andar no es válida.

(La queja 4, decoración, no se puntúa. Con el greybox actual, los muros de paneles repetidos y la luz uniforme hacen que R01, R14 y R15 se parezcan mucho entre sí. La decoración y los focos locales deberían ir juntos.)
