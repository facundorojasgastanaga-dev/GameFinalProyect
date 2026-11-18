# IR_Protagonist_Refined_v01 (protagonista v02)

Exportado por `Tools/IronRequiem/Blender/export_protagonist_v02.py` desde
`Tools/IronRequiem/Blender/Working/protagonist_v02/refined/IR_Protagonist_v02_refined.blend` (solo lectura).

- `IR_Protagonist_v02_Rigged.fbx`: IR_Rig (50 huesos v01) + Body/Forearm_L/Forearm_R/Hand_L/Hand_R LOD0-2 + sockets
  Wrist_R/L, ModuleSocket_R/L (hueso Hand_X) y Elbow_R/L (hueso Forearm_X), unicos, +Z distal, +Y dorso.
- `IR_Protagonist_v02_FPS_Rigged.fbx`: IR_Rig + Hand_R_FPS, Hand_L_FPS, Forearm_R_FPS, Forearm_L_FPS + los mismos sockets.
- Clips: IR_v02_Open, IR_v02_Relaxed, IR_v02_Fist, IR_v02_Grip, IR_v02_Inspect, IR_Idle, IR_Walk (poses v02: 2 fotogramas). Retirados de v01: IR_HandFist, IR_HandGrip, IR_HandNormal, IR_PunchPosePreview.
- Texturas: las mismas imagenes del atlas v01 (copiadas en `Textures/`); materiales Material.001-.004 como v01.
- Convencion FBX de v01: axis_forward -Z, axis_up Y, FBX_SCALE_UNITS, sin leaf bones, armature raiz.
  Importar en Unity igual que v01: useFileScale 1, globalScale 1, bakeAxisConversion 0, Generic.
- Diferencias con v01: mismos nombres de objetos, huesos y jerarquia; cabezas de dedos/pulgar desplazadas
  (recentrado, pulgar reajustado, falanges 2-3 x0,72) -> regenerar HandPoseLibrary y bind poses;
  sockets unicos (v01 Rigged tenia .001/.002, v01 FPS no tenia sockets). Clips con el mismo formato que v01 (IR_Rig|IR_Rig|<accion>): una toma por accion, solo el armature lleva curvas.
- Verificacion (reimportado en Blender): tris = refined: True; huesos = v01: True; 5 clips: True; SHA protegidos sin cambios: True; mano L espejo exacto de R en LOD0/1/2/FPS: True.
- Iteracion 4 (solo limpieza): corte del codo sin laminas ni islas (cuello del cuerpo re-elegido, tapa del
  antebrazo plana a 1,5 mm de la del cuerpo en todos los LOD), aletas de la palma < 1 mm colapsadas, UV estiradas
  de la palma re-proyectadas a la placa clara de las falanges, espiga de la muneca con chaflan de raiz y ranura
  en V (sin cambiar diametros ni posiciones de IR-ACOPLE-01), mano L espejo de R tambien en LOD1/LOD2.
  Estado anterior en `iter3/`.
- `metrics.json`: verificacion completa, cuaterniones de las poses (Blender local y Unity absoluto),
  cambios de longitud de huesos y resumen del refinado; `renders/`: renders Workbench de la iteracion 4.
