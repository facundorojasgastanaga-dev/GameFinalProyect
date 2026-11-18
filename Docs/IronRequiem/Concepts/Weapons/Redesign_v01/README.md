# Armas — Redesign v01 (fase B1): direcciones visuales

16/11/2026. Bocetos 3D propios (blockouts procedurales) para que el usuario **elija** una dirección por arma. No se eligió ninguna. No se tocaron assets de Unity, FBX, `.meta`, prefabs ni escenas; las armas actuales solo se importaron en Blender, en una sesión aparte y en segundo plano, para medir escala y forma. No se usó Meshy, generación de imágenes, descargas ni compras.

## Convenciones comunes

- Origen = acople trasero: collar circular de antebrazo de **Ø 0,09 m** (grafito, labio de acero, dos cierres naranja). Disparo hacia **−Y** en Blender (+Z en Unity), arriba +Z.
- Escala: largo desde el acople hasta la boca medido sobre la malla evaluada (`report.json`). Todas quedan en ±2 % del actual: pistola 0,379 m, módulo principal 0,333 m y cañón T-07 0,664 m.
- Paleta: placa clara, grafito, naranja industrial (pintura) y cian (estado), como el protagonista v02. **Calor/OVERDRIVE = naranja-rojo emisivo**, coherente con el gradiente cian→naranja→rojo de `OverheatBehavior`. El cañón T-07 usa **magenta** para la carga (`CANNON_CHARGE.md`) y el blindaje gris azulado de `T07_BaseColor.png`; los cortes son de acero desnudo, con un borde rojo caliente e interior oxidado.
- Renders con Eevee a 1200 px: 3/4 frontal (lado −X), perfil desde +X (el lado que ve la cámara FPS, con una regla del largo actual) y vista FPS aproximada (cámara a 0,35 m detrás y 0,12 m por encima del acople, desplazada 0,13 m, FOV horizontal de 60°, 16:9).
- Límites de la vista FPS: no hay brazo ni mano. El disco oscuro inferior es la cara trasera del collar, que en juego taparía el antebrazo. No reproduce el AimOffset ni el FOV de la cámara de armas del proyecto.
- Triángulos: cuenta de la malla evaluada del boceto, incluidas las piezas de un solo color. Es orientativa: una malla final con atlas y piezas fusionadas debería quedar igual o por debajo.

## Láminas

| Arma | Lámina | Script |
|---|---|---|
| Pistola | `Pistol/Pistol_lamina.png` | `Pistol/pistol_concepts.py` |
| Módulo principal | `Primary/Primary_lamina.png` | `Primary/primary_concepts.py` |
| Cañón T-07 | `T07Cannon/T07Cannon_lamina.png` | `T07Cannon/t07cannon_concepts.py` |

Cada lámina tiene una columna por dirección y tres filas: 3/4, perfil y FPS. Las vistas sueltas se llaman `<ID>_<Nombre>_34.png`, `_perfil.png` y `_fps.png`. `report.json` recoge triángulos, límites y largo. `_labels/` contiene rótulos intermedios.

### Regenerar

```
"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" --background --factory-startup --python-exit-code 1 --python Pistol\pistol_concepts.py -- P-B
```

Sin IDs se regeneran todas las direcciones del arma y su lámina. `ir_concept_lib.py` contiene las primitivas biseladas, anillos, prismas dentados, cables, rebabas, el collar, la iluminación, las cámaras, el recuento de triángulos y la composición con numpy de Blender, sin dependencias externas. Cada render tarda unos 30 s.

## Pistola (actual: PistolModule_1000, 0,379 m, 532 tris)

| Dir. | Idea en una frase | Pros | Contras | Riesgo acople/animación | Tris boceto → estimado final |
|---|---|---|---|---|---|
| **P-A Monolito** | Evolución del bloque recto actual con rejilla de calor en el lomo. | Continuidad con el modelo y el concepto v02. La rejilla queda justo en la línea de visión FPS. Es la opción más barata. | Silueta poco distinta de la actual: más cuidada, pero no sorprende. | **Bajo**: forma rígida y simétrica. Mismo acople y boca en el eje. | 1.444 → ~700–1.000 |
| **P-B Tambor de celdas** | Tambor hexagonal de seis celdas que gira y se pone al rojo. | Silueta inconfundible. El tambor puede girar al disparar y el calor se lee por celda, de 6 a 0. | El tambor ensancha el centro y el puente superior tapa parte de la vista FPS. Exige una pieza móvil. | **Medio**: añade un pivote de giro sobre el eje Y. La boca sigue en el eje. Animación procedural sencilla. | 1.760 → ~900–1.300 |
| **P-C Bobina en horquilla** | Horquilla abierta con una bobina expuesta entre dos patas. | Lectura de arma de energía y punto de calor muy visible de perfil y en 3/4. Buen soporte para un VFX de OVERDRIVE. | Desde atrás, en FPS, la bobina queda casi oculta por el cuerpo. Rompe con el bloque recto del v02. | **Bajo-medio**: rígida. Las patas largas pueden atravesar paredes cercanas. Sin piezas móviles obligatorias. | 1.840 → ~900–1.300 |

## Módulo principal (actual: IR_PrimaryModule_v02, 0,333 m, cápsulas de calor)

| Dir. | Idea en una frase | Pros | Contras | Riesgo acople/animación | Tris boceto → estimado final |
|---|---|---|---|---|---|
| **PR-A Corona de cápsulas** | Tres cápsulas de calor inclinadas sobre el lomo, que se expulsan al saturarse. | Continúa la mecánica de cápsulas de v02 y la exagera. Calor muy visible en FPS. La expulsión hace de feedback de OVERDRIVE. | Las cápsulas sobresalen 4 cm por arriba y entran en la línea de mira. Habría que bajarlas o inclinarlas más. | **Medio**: hay que reubicar las ranuras actuales de `WeaponFuelCellHandler`/`PrimaryModuleHeatPresentation` y añadir una animación de expulsión. | 1.920 → ~1.000–1.500 |
| **PR-B Rotativa** | Minirrotativa de tres cañas con un anillo de calor entre el motor y las cañas. | Fuego rápido evidente sin explicación. El anillo de calor se ve desde cualquier ángulo y el giro de las cañas comunica la cadencia. | Lenguaje de arma genérica. Desde atrás, el motor oculta las cañas. Perfil más pesado al frente. | **Medio**: grupo de cañas con pivote y giro continuo. Boca múltiple: el socket `Muzzle` debe alternar o usar un centro común. | 1.584 → ~900–1.300 |
| **PR-C Espina radiadora** | Cuerpo bajo con aletas de radiador que se encienden de atrás hacia delante. | Silueta baja y despejada en FPS. Las aletas son una barra de calor diegética por pasos. Coste geométrico bajo. | Las aletas finas parpadean o desaparecen a distancia y necesitan LOD. Lectura menos inmediata que PR-A. | **Bajo**: rígido. El calor por aleta requiere solo un MaterialPropertyBlock por ranura, como ahora. | 1.428 → ~700–1.100 |

## Cañón T-07 (actual: T07_Cannon_PivotFixed, 0,664 m, 1.702 tris LOD0)

Las tres direcciones mantienen la boca octogonal, el blindaje gris azulado, las varillas o pistones y las bandas del T-07. Añaden una abrazadera del jugador, con pernos y flejes naranja soldados al resto arrancado. Los cables cuelgan **hacia abajo y hacia delante**, sin invadir el espacio detrás del acople.

| Dir. | Idea en una frase | Pros | Contras | Riesgo acople/animación | Tris boceto → estimado final |
|---|---|---|---|---|---|
| **C-A Arrancado crudo** | El cañón actual casi literal, con corte, rebabas, cables y abrazadera soldada. | Máxima continuidad con el enemigo: se reconoce al instante como su arma. Ventana magenta lateral visible en FPS y carga en el ánima. | El extremo arrancado (Ø ~0,18 m) domina la vista FPS. Los cables colgantes alcanzan 0,25 m bajo el eje. | **Bajo-medio**: rígido. Los cables pueden ser huesos simples o ir sin física. Revisar recortes con el suelo al lanzar o recoger. | 3.140 → ~1.800–2.400 |
| **C-B Núcleo expuesto** | Carcasa reventada que deja a la vista la celda de carga magenta. | Punto de carga y OVERDRIVE inequívoco. El núcleo puede pulsar con la carga existente y aporta drama de pieza robada. | La placa pelada y el núcleo sobresalen por arriba y tapan parte de la mira. El enemigo debería mostrar ese núcleo antes de arrancarlo para que sea coherente. | **Medio**: el núcleo sugiere animación o escala con la carga. La placa pelada puede chocar con el AimOffset actual. | 3.440 → ~2.000–2.600 |
| **C-C Antebrazo completo** | También se arrancan la concha del antebrazo y restos de la mano del T-07. | Es la lectura más clara de "se lo arranqué del brazo". Las rejillas magenta de la concha quedan arriba, a la vista. Silueta asimétrica memorable. | Es la más voluminosa (0,24 m de ancho). En FPS la concha y su borde de corte llenan el cuadro. Los dedos colgantes pueden resultar confusos. | **Alto**: masa descentrada respecto del eje y del collar. Puede requerir otro AimOffset o una pose de cámara. La concha cubre el acople y complica la transición del antebrazo modular v07. | 3.144 → ~2.000–2.800 |

## Pendiente para la siguiente fase

- Elección del usuario: una dirección por arma, o combinaciones.
- Modelado final con presupuesto, atlas, LOD y sockets `Attachment`/`Muzzle` exportados como en `WEAPON_PIVOT_T07_CANNON.md`. Comprobación en Unity con la cámara de armas real y el AimOffset de cada prefab.
- No se ha verificado nada en Unity ni en Play Mode: estos archivos son solo bocetos.
