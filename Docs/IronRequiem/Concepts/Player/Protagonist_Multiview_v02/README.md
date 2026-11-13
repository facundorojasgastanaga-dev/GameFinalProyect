# Protagonista — referencias de vistas, sin pistola

Ocho PNG individuales generados con la herramienta integrada image_gen. El robot base tiene ambas manos completas; el arma se conserva como un asset separado. Estas imágenes no modifican el jugador ni otros assets de Unity.

## Casillas de la referencia adjunta

| Casilla | Archivo |
|---|---|
| Frontal, imagen principal | 01_frontal.png |
| Frontal a 45° por la izquierda | 02_frontal_45_izquierda.png |
| Frontal a 45° por la derecha | 03_frontal_45_derecha.png |
| Lateral izquierda | 04_lateral_izquierda.png |
| Lateral derecha | 05_lateral_derecha.png |
| Posterior / detrás | 06_posterior.png |
| Superior / desde arriba | 07_superior.png |
| Inferior / desde abajo | 08_inferior.png |

Izquierda/derecha se refieren al lado anatómico del robot: el perfil izquierdo mira hacia la izquierda de su imagen y el derecho hacia la derecha. Usa la frontal sin arma como imagen principal y sube cada PNG individual en su casilla. La frontal armada del concepto inicial no forma parte de este conjunto.

## Uso y límites

Son referencias conceptuales generadas por IA, no proyecciones calibradas de una única malla 3D. Se revisaron visualmente y se corrigió la posterior para diferenciar pelvis y rodillas traseras. Pueden existir diferencias de paneles, manos, escala aparente y encuadre entre imágenes. Las vistas superior e inferior son aproximaciones con cierta inclinación residual: no tratarlas como planos ortográficos exactos. Si el generador interpreta mal esas dos vistas, priorizar la frontal, posterior, perfiles y ángulos de 45° como referencias principales.

No hay un modelo 3D nuevo ni rig creado en esta entrega. Los prompts de las imágenes seleccionadas y las rutas originales figuran en prompts.json. Se conservan los archivos generados originales; no se editaron por código ni se sobrescribió ningún concepto anterior.

## Corrección v02
Solo se corrigió 08_inferior.png: un pulgar exterior y cuatro dedos largos por mano. Las otras siete imágenes conservan sus bytes originales. Se mantiene v01 como respaldo.
