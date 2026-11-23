# Estación de montaje con el modelo segmentado (19/11/2026)

## Estado

**Aplicado a producción el 19/11/2026**, con la aprobación del usuario («Sí, aplícalo»). El prefab `Assets/IronRequiem/Prefabs/Levels/MountStation_C04.prefab` ya lleva el modelo segmentado. Lo usan RequiemCampaign, Map01 S1–S4 y dos escenas de laboratorio (ProtagonistV02Lab, WeaponsRedesignLab).

- Ninguna escena se guardó: todas son instancias del prefab y recogen el cambio solas. Sus hashes coinciden con los de antes de aplicar.
- El `.meta` del prefab (GUID) no cambió.
- La variante de prueba `MountStation_C04_Segmented.prefab` se retiró: con el arte ya en su base habría llevado los brazos dos veces. Se movió a la papelera del sistema y hay copia en `Tools/IronRequiem/Backups/20261119-mount-station-art/apply-production/`, junto al prefab de producción anterior (SHA-256 `17bb0f80…`), las herramientas y la prueba tal como estaban.

La animación es la misma de antes. `WeaponMountStation` sigue mandando con los mismos tiempos (5,6 s y los mismos golpes de sonido); solo cambia qué piezas mueve. No hay commits.

## Fuente

- **Archivo:** `387f2048-85be-491f-8b59-25669c731e02.fbx`, entregado por el usuario.
  - SHA-256 `6807ce8e2717df4de7eda8d3ec08a76623faa9741190d01cc0ee401724d0b4da`.
  - Copia de trabajo en `Tools/IronRequiem/Sources/MountStation/`.
  - Copia de respaldo en `Tools/IronRequiem/Backups/20261119-mount-station-art/source/`.
  - La fuente no se modifica; el hash se comprobó al final.
- **Contenido:** 256 piezas de Tripo, sin UV ni texturas, normalizadas a 1 m de ancho.
  - Frente en −Y y arriba en +Z (en Blender).
  - Es un pórtico con 2 postes, viga, carro y **cuatro** brazos colgantes: dos delanteros y dos traseros. Cada brazo tiene hombro, eslabón largo, codo cilíndrico (eje delante-atrás), herramienta corta y punta en forma de pinza.
  - Hay además dos campanas colgantes.

## Preparado (Blender, sin tocar la fuente)

**Script:** `Tools/IronRequiem/Blender/mount_station_prep.py`. Se ejecuta con Blender 5.2 en modo `-b`.

Cada isla de cada pieza se clasifica según su caja, comparada con los centros de articulación medidos:

| Punto | Qué es |
|---|---|
| P0 | Pivote del carro |
| P1 | Hombro |
| P2 | Codo |
| P3 | Punta de la herramienta (muñeca) |
| P4 | Punto de agarre |
| P5 | Punta de la pinza |
| P6 | Cabeza de la pinza |

**Grupos exportados:**

| Grupo | Contenido |
|---|---|
| `Static` | Pórtico, carro y detalles |
| `FR_/FL_Hanger` | Anilla de cuelgue de cada brazo |
| `FR_/FL_Upper` | Hombro, eslabón y codo |
| `FR_/FL_Fore` | Herramienta |
| `FR_/FL_ClawN` / `ClawP` | La pinza partida por su propio eje en dos mitades, con el corte rellenado. Abren y cierran como tenazas |
| `BR_/BL_Arm` | Brazos traseros y campanas, aparcados (no se mueven) |

**Otros datos del preparado:**
- **Punto de agarre:** al 72 % de la pinza, cerca de la punta; así la punta no atraviesa la muñeca.
- **UV:** proyección cúbica, 1 unidad = 1 m a la escala final.
- **Materiales por pieza:** cinco ranuras según tamaño y forma: acero, pintura, tornillería, manguera y latón.
- **Marcadores exportados como empties:** articulaciones, volúmenes de postes y dintel, y el marco completo.
- **Salida:** `Assets/IronRequiem/Art/Levels/MountStation/MountStation_Segmented.fbx`.
- **Materiales:** `Art/Levels/MountStation/Materials/IR_MS_*.mat`.
  - Son copias de `HG_PlateDark`; los `HG_*` no se tocaron.
  - La pintura de los brazos activos va sin la textura de suciedad, para que se lea en el vestíbulo oscuro de Map01.
  - Los brazos aparcados usan `IR_MS_PaintParked`, más apagada y con suciedad.

## Montaje en Unity

### Constructor

[MountStationArtBuilder.cs](../../Assets/IronRequiem/Editor/MountStationArt/MountStationArtBuilder.cs), menús en `Tools/Iron Requiem/Mount Station Art/`:

| Paso | Menú |
|---|---|
| 1 | Importar modelo y materiales |
| — | «Apply to production prefab», con diálogo de confirmación. Es idempotente: rehace el arte `SA_*` desde el modelo |
| — | «Apply to production prefab (approved 19-11, no dialog)»: el mismo paso sin diálogo, para el puente de órdenes; retira la variante si sigue existiendo |
| 3 | Vistas fijas del prefab de producción en modo edición |

Los menús de la variante («2 Build preview variant», sus vistas fijas y «Swap station to segmented variant») se quitaron al aplicar.

**Qué hace al construir:**
- Coloca las articulaciones del rig sobre las del modelo.
- Cuelga cada segmento de su transform:
  - la anilla en la raíz del brazo;
  - el eslabón en `Upper`;
  - la herramienta en `Fore`;
  - las dos mitades de la pinza en `SA_JawA` y `SA_JawB`, que pasan a ser las mandíbulas del rig.
- Oculta los cubos de relleno, pero conserva los colisionadores de los postes, reajustados a los postes del modelo.
- Recoloca la lámpara de estado, el cartel (ahora cabe dentro del pórtico) y el soporte del cañón.

**Ajuste** (`MountStationArtBuilder.cs:32-45`):

| Parámetro | Valor | Motivo |
|---|---|---|
| Pórtico | ×3,8 (3,8 m de ancho, 3,26 m de alto), centrado a z = 0,95 | Cabe entre los muros laterales de S1, S3 y S4 (a 2,0 m) y delante del muro trasero de S2 (a 2,0 m) |
| Brazos | 0,70 del pórtico, escalados sobre su pivote de carro; los cuatro por igual | Cobertura del encuadre |
| Cuelgue de los brazos delanteros | 12 cm más hacia fuera, siguen bajo el carril | El brazo A entra en la vista por el borde |
| Reposo de las pinzas | Aparcadas más arriba, hacia fuera y hacia atrás | En reposo no ocupan el centro de la vista |
| Codo | Preferencia hacia atrás y hacia fuera | Se aleja de la cámara al trabajar en la muñeca |
| Soporte del cañón | (−0,78; 1,16; 0,25), cañón apuntando hacia dentro | El brazo B lo toma por la boca desde atrás |

### Rig

[StationArmRig.cs](../../Assets/IronRequiem/Scripts/Levels/StationArmRig.cs) tiene dos opciones nuevas, **desactivadas por defecto** (la estación actual se comporta igual):

- **`hingeAligned`** (l.40, 91-96): el eje X local de cada segmento es el eje de flexión. El disco del hombro y el cilindro del codo giran sobre su eje y no voltean al colgar en vertical.
- **`gripLength`** (l.43, 74, 104): el cálculo alcanza la muñeca, y la pinza queda esa distancia por delante, a lo largo de la aproximación. Las mandíbulas, no la muñeca, caen sobre el objetivo.

## Verificación

### Compilación

0 errores.

### Pruebas EditMode

[StationArmRigSegmentedTests.cs](../../Assets/IronRequiem/Tests/EditMode/StationArmRigSegmentedTests.cs): **4/4** tras aplicar. Antes de aplicar, la prueba de producción fallaba («dressed once: was 0»), así que detecta una estación sin vestir.

| Prueba | Qué comprueba |
|---|---|
| Valores por defecto | Comportamiento v7 sin cambios |
| `gripLength` | Mandíbulas en el objetivo y muñeca detrás, sobre la aproximación |
| `hingeAligned` | Barrido bajo el hombro sin volteo |
| Producción | Vestida una sola vez; dos brazos, cada uno con una anilla, un eslabón, una herramienta y dos medias pinzas; cubos ocultos, colisionadores de postes y dentro de los muros de Map01 |

### Regresión PlayMode

Campaña, hangar y las 4 pruebas de la pasada de armas: **11/12**, antes y después de aplicar a producción.
- La que falla es `IntroMenuStartsCampaignAndRestartReturnsToRoomOne`.
- Ya fallaba antes de esta pasada, con el mismo mensaje: IntroMenu arranca ahora `Map01_Campaign_S1` en vez de `RequiemCampaign`.

### Encaje del pórtico

Cajas reales del marco contra los colisionadores de cada escena. Informe en `Reports/MountStationArt/frame-fit.txt`, medido de nuevo sobre el prefab de producción tras aplicar, con el mismo resultado.

| Escena | Estación actual (cubos) | Modelo segmentado |
|---|---|---|
| RequiemCampaign | Libre | Libre |
| Map01 S1 | Poste y dintel dentro de `SideW` | Libre |
| Map01 S2 | Libre | Libre |
| Map01 S3 | Poste y dintel dentro de `SideE` | Libre |
| Map01 S4 | Poste y dintel dentro de `SideE` | Libre |

### Cobertura y choques en Play

Ejecución completa a 0,1×, capturada cada 0,05 s, con el mismo filtro para todas las estaciones. La columna «Producción» es la estación real de cada escena tras aplicar, sin sustituir nada en Play. Se cuentan las piezas que se mueven (brazos, abrazaderas y anillo). El pórtico estático no cuenta. La mano soltada se informa aparte, porque es la mano del jugador copiada por la estación desde 1,55 s.

| Escena | Cubos: brazos / con mano | Variante: brazos / con mano | Producción: brazos / con mano | Choques con muros |
|---|---|---|---|---|
| Vestíbulo | 15,2 % / 18,1 % | 23,0 % / 26,1 % | **23,0 %** / 26,1 % | Ninguno |
| Map01 S1 | 13,0 % / 15,9 % | 19,7 % / 22,2 % | **19,6 %** / 22,2 % | Cubos: poste, franja, dintel y cartel en `SideW`. Variante y producción: ninguno |

En las dos escenas de producción el montaje se concede (`granted=True`).

- **Brazos solos:** por debajo del 25 % en las dos escenas.
- **Con la mano soltada en el vestíbulo:** pasa del 25 % entre 1,55 y 1,65 s.

### Capturas

En `Tools/IronRequiem/Reports/MountStationArt/`:
- `compare_RequiemCampaign.png` y `compare_Map01_Campaign_S1.png`: antes/después en aproximación, sujeción, bloqueo, retirada y final.
- `witness_*.png`: vista lateral de testigo.
- `stills_sheet.png`: vistas fijas del modelo.
- `current/` y `segmented/`: fotogramas y CSV de los cubos y de la variante.
- `production-lobby/` y `production-s1/`: los mismos datos con la estación de producción; resumen en `production_play_sheet.png`.

## Refinamiento de la garra (19/11/2026, tarde)

Pedido del usuario: al quitar la mano, la garra se veía separada del resto del brazo. También pidió sustituir los cubos de rayas negras y amarillas.

### Diagnóstico

Se midió en Play con una sonda nueva, que escribe `station-probe-<escena>.csv` junto a cada captura. La sonda mide cuatro cosas:
- la distancia entre las mallas de eslabón y herramienta, y entre herramienta y garra;
- la flexión de la muñeca;
- el giro de la garra sobre su eje, comparado con la relación que tiene en el modelo.

| Medida | Antes |
|---|---|
| Separación herramienta-garra | 2 cm: no se separaban físicamente |
| Giro de la garra respecto a la herramienta | Hasta 132° (brazo A) y 175° (brazo B) |
| Flexión de la muñeca en reposo y llevándose la mano | 142-145° (en el modelo, unos 60°) |

**Causa 1, el giro y la flexión:** la garra tomaba la orientación que pide la coreografía sin tener en cuenta la herramienta. Su cabeza miraba hacia otro lado, por eso se leía suelta.

**Causa 2, el reposo:** el reposo subía la pinza 0,34 m. Eso dejaba la muñeca más cerca del hombro de lo que el brazo puede plegarse (0,6 m). El brazo se cerraba del todo y la garra quedaba colgando hacia atrás, junto al eslabón.

**Causa 3, la apertura:** las dos mitades de la pinza se deslizaban 11 cm hacia los lados, como dos piezas sueltas.

### Cambios

**`StationArmRig`** tiene dos opciones nuevas, desactivadas por defecto, así que la estación de cubos se comportaría igual:
- **`modelWrist`**: la cabeza de la garra queda en la punta de la herramienta (`wristLocal`). El codo se inclina hacia donde lo pone el modelo para esa pose de garra (`elbowLocal`, con peso 0,8 frente a `elbowHint`). La garra gira sobre su eje para mirar a la herramienta; ese giro se desvanece cuando la herramienta está alineada con la garra.
- **`jawHingeOpen` / `jawHingeClosed`**: cada mitad gira sobre su pasador en la cabeza de la garra, como unas tenazas (15,8° abierta, 3,5° cerrada), en vez de deslizarse.

**Constructor:**
- Calcula `wristLocal`, `elbowLocal` y el pasador de cada garra a partir del modelo.
- Coloca las mitades de la pinza en su pasador.
- Cambia el reposo de las pinzas: `ParkOffset` pasa de (0,16; 0,34; 0,12) a (0,22; 0,10; 0,18), hacia fuera y atrás en lugar de arriba.
- Sustituye los tres bloques de rayas del anillo de bloqueo de la muñeca (`WristLocks/Lock*/Clamp`, material `HG_Hazard`) por pernos de acero (`IR_MS_Bolt`) con un tapón naranja (`IR_MS_Paint`).

`WeaponMountStation` no cambió: mismos tiempos, misma coreografía, mismos sonidos.

### Resultado

Vestíbulo, ejecución completa en Play. Las fases del brazo A son: reposo, alcance y agarre, tirón y retirada, sujeción, y vuelta.

| Medida | Antes | Después |
|---|---|---|
| Giro de la garra respecto a la herramienta (A / B) | 132° / 175° | **0° / 0°** |
| Flexión de muñeca A: reposo / agarre / retirada / sujeción | 145 / 61-145 / 61-144 / 142° | 83 / 60-90 / 60-114 / 104° |
| Separación herramienta-garra (máx.) | 2,3 cm | 1,8 cm |
| Cobertura en el vestíbulo: brazos / con mano | 23,0 % / 26,1 % | **22,0 %** / 25,2 % |
| Cobertura en Map01 S1: brazos / con mano | 19,6 % / 22,2 % | **18,4 %** / 21,6 % |
| Choques con muros (vestíbulo y S1) | Ninguno | Ninguno |
| Montaje concedido | Sí | Sí |

**Capturas** en `Tools/IronRequiem/Reports/MountStationArt/`:
- `refine_fps_sheet.png` y `refine_side_sheet.png`: antes y después.
- `refine_final_sheet.png`: versión final en el vestíbulo y en S1.
- `production-probe-before-lobby/`, `production-probe-after-lobby/`, `production-final-lobby/` y `production-final-s1/`: fotogramas, primeros planos del brazo A y CSV de la sonda.

### Verificación

| Comprobación | Resultado |
|---|---|
| EditMode | **6/6**. Pruebas nuevas: muñeca del modelo (cabeza en la punta de la herramienta y giro hacia ella) y pinzas que giran sobre su pasador. La prueba de producción también comprueba la muñeca, las pinzas, que no queden bloques de rayas en el anillo y los tres tapones. Antes de aplicar falló, como se esperaba |
| Regresión PlayMode | 11/12; el único fallo es el previo de IntroMenu |
| Escenas | Las 7 que usan el prefab, sin guardar y con el mismo hash |
| Zona protegida | 39/39 |
| FBX fuente, `WeaponMountStation.cs` y `.meta` del prefab | Sin cambios |

**Respaldo** previo en `Tools/IronRequiem/Backups/20261119-mount-station-refine/`.

### Pendiente

- **Prueba manual con teclado y ratón:** no hecha.
- **Mano al retirarse:** la pinza la sujeta por la punta, igual que antes, no entre las mandíbulas.
- **Codo del brazo A:** se abre hasta 6 cm entre eslabón y herramienta mientras alcanza la muñeca. Está lejos de la cámara y no se nota en las capturas.

## Puertas blindadas en lugar de rayas (19/11/2026, aprobado por el usuario: «Sí, cámbialas»)

Las cajas de puerta con rayas negras y amarillas de las cinco escenas de la estación ahora usan `IR_BlastDoor`. Es una puerta blindada de acero:
- dos hojas con junta central;
- tres refuerzos y riostras diagonales;
- remaches;
- cerradura central;
- marco naranja desgastado, la misma pintura que los brazos de la estación.

Solo cambia el material. Los objetos, sus colisionadores, scripts y animaciones siguen igual, y ningún script cambia el material de una puerta en juego.

**Herramienta:** [StationSceneDoors.cs](../../Assets/IronRequiem/Editor/MountStationArt/StationSceneDoors.cs), en los menús `Tools/Iron Requiem/Mount Station Art/Doors/`:

| Menú | Qué hace |
|---|---|
| 1 | Genera las texturas por código: albedo y normal de 1024 px, sin assets externos. Crea `Art/Levels/Doors/IR_BlastDoor.mat` (URP Lit, metálico 0,2, suavidad 0,34) |
| 2 | Cambia el material de rayas por el nuevo y guarda las escenas. Se niega si la escena abierta tiene cambios sin guardar |
| 3 | Vista fija: `Reports/MountStationArt/doors/door-still.png` |

**Puertas cambiadas: 86**

| Escena | Puertas |
|---|---|
| RequiemCampaign | 9 |
| Map01 S1 | 20 |
| Map01 S2 | 27 |
| Map01 S3 | 19 |
| Map01 S4 | 11 |

Son dos grupos:
- `SourceDoor` / `DestinationDoor` de las salas de Map01 (`Bunker_Hazard`);
- en las cinco escenas, `InitialLoadingDoor` del vestíbulo, las puertas de las esclusas de transferencia, `Room01ExitDoor` y `Room02EntryDoor` (`VO_Env_Hazard`).

**Se dejaron con rayas:**
- las marcas de borde de la escalera (`Staircase/StepMark`);
- en la cámara de tránsito del hangar, las molduras laterales (`Trim`) y la franja de la persiana (`ShutterHazard`). Sus puertas se cambiaron después, ver la sección siguiente.

Las escenas de laboratorio no se tocaron.

**Respaldo:** las cinco escenas con su `.meta` y hashes están en `Tools/IronRequiem/Backups/20261119-doors/`.

**Diferencias en las escenas:**
- **Map01 S1–S4:** solo cambian las líneas del material de cada puerta.
- **RequiemCampaign:** además, Unity escribió 16 líneas con los valores por defecto de campos de HUD añadidos a sus scripts después del último guardado de esa escena (`RetroStatusBar.clusterWidth` 0,53 y `barHeight` 180, `IndustrialPanelGraphic.style` 0, `FistChargeHud.radial` vacío). Son los valores que ya usaba al cargar, así que no cambia nada.

**Verificación:**

| Comprobación | Resultado |
|---|---|
| Play, vestíbulo | Brazos 22,0 % / con mano 25,2 %, sin choques, montaje concedido |
| Play, Map01 S1 | 18,4 % / 21,6 %, sin choques, montaje concedido. El primer intento se detuvo en t = 2,05 s con Unity en segundo plano; el segundo terminó bien |
| EditMode | 6/6 |
| Regresión PlayMode | 11/12 |
| Zona protegida | 39/39 |
| Escenas de laboratorio | Mismo hash |
| FBX fuente | Mismo hash |

Detalle de la regresión:
- La única prueba que falla es la previa de IntroMenu. En esta pasada falló un paso antes, porque no pudo reescribir su captura de evidencia.
- En una primera pasada fallaron además dos pruebas, por el mismo motivo: Windows tenía bloqueados en ese momento dos PNG de evidencia en `Reports/HandsHudCampaign`. Al repetir pasaron, y los archivos se pueden escribir.

**Capturas:** `doors_sheet.png` (antes y después en el vestíbulo y en S1), `production-doors-lobby/`, `production-doors2-lobby/` y `production-doors-s1/`.

**Notas:**
- En el vestíbulo la puerta se ve marrón por la luz cálida de la sala, no por el material. Bajar el metálico de 0,45 a 0,2 no cambió el tono.
- Si alguien vuelve a ejecutar un generador de niveles que asigna el material de rayas, las rayas vuelven: Map01 `DoorAuthoring` («Doors/Build all») o la autoría de esclusas de la campaña. Basta con repetir el menú 2.

### Puerta del tránsito del hangar (19/11/2026, aprobado: «Sí, cámbiala»)

Es la puerta con cartel «TRÁNSITO C-04 · PUENTE NORTE», que se ve al terminar el montaje. Está en `PersistentAirlocks/HangarTransit/ConnectionChamber` de las cinco escenas y tiene dos partes:
- **Puerta lado hangar** (`ChamberHangarDoor`): una sola caja. Ahora usa `IR_BlastDoor`.
- **Puerta lado pasillo, con ojo de buey** (`ChamberCorridorDoor` y sus piezas `DoorRight`, `DoorAbove` y `DoorBelow`): ahora usa `IR_DoorPlate`. Es una placa de acero generada por código, con bisel, una fila de remaches en cada borde, suciedad y regueros. Si cada una de las cuatro piezas llevara el dibujo completo de la puerta blindada, se verían cuatro puertas. El ojo de buey y sus barras no cambian.

**Herramienta:** menú «Doors/4 Apply to the hangar transit chamber doors» de [StationSceneDoors.cs](../../Assets/IronRequiem/Editor/MountStationArt/StationSceneDoors.cs). Cambió 5 piezas por escena y guardó las escenas, con respaldo previo en `Tools/IronRequiem/Backups/20261119-transit-door/`.

**Qué no se tocó:**
- Las escenas solo cambian en esas 5 líneas de material.
- La escena IntroMenu no tiene esta cámara y queda intacta.
- Los generadores (`HangarTransitAuthoring`, `DoorAuthoring`) no se modificaron. Si se vuelven a ejecutar, repite los menús 2 y 4.
- Map01 `DoorValidation` comprueba los nombres de estas puertas, no su material.

**Verificación:**

| Comprobación | Resultado |
|---|---|
| Play, vestíbulo | Brazos 22,0 % / con mano 25,2 % |
| Play, Map01 S1 | 18,4 % / 21,3 % |
| Choques y montaje | Sin choques; montaje concedido en las dos |
| EditMode | 6/6 |
| Regresión PlayMode | 11/12, solo el fallo previo de IntroMenu |
| Zona protegida | 39/39 |
| Escenas de laboratorio y FBX fuente | Mismo hash |

**Capturas:** `transit_door_sheet.png` y `transit_door_zoom.png`.

## Pendiente o no verificado

- **Prueba manual con teclado y ratón:** no hecha. Las pasadas fueron con la secuencia disparada por el arnés.
- **Map01 S2–S4:** solo se comprobó el encaje estático; no hubo ejecución en Play. En esas sesiones la estación ya está concedida.
- **Escenas de laboratorio** (ProtagonistV02Lab, WeaponsRedesignLab): también usan el prefab y ahora muestran el modelo; no se abrieron.
- **Coste:** ≈ 31 k vértices y ~25 renderizadores más que los cubos (submallas por material). No se ha medido el rendimiento.
- **Rotación de la anilla de cuelgue:** no sigue la guiñada del brazo, porque es estática en la raíz. El rig gira la guiñada solo para la carcasa antigua, que ahora está oculta.
- **Volver atrás:** restaurar `MountStation_C04.prefab` desde `Tools/IronRequiem/Backups/20261119-mount-station-art/Prefabs/Levels/`; el `.meta` no cambió.
