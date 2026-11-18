# Comparación Player — primera medición posterior

GPU RTX4050, i5-13420H; ambos recorridos a 1600×900 reales. Development, vSync 0, profiler desactivado. La baseline y la versión reparada incluyen sus respectivas presentaciones. Una ejecución no prueba estabilidad universal.

| Transición | Antes, pico ms | Después inicial, pico ms |
|---|---:|---:|
| Room00b_AlarmCorridor | 25.6 | 41.4 |
| Room01_Foundry | 21.0 | 21.7 |
| Room02_Breach | 36.3 | 16.4 |
| Room03_Assembly | 18.2 | 14.5 |
| Room04_Reactor | 19.0 | 20.5 |
| Pursuit05 | 19.4 | 21.9 |

| Sala | Antes media / p99 ms | Después inicial media / p99 ms |
|---|---:|---:|
| Room00_Hangar (no combat) | 8.73 / 13.60 | 8.80 / 13.65 |
| Room00b_AlarmCorridor | 8.68 / 12.81 | 8.17 / 12.45 |
| Room01_Foundry | 10.00 / 14.05 | 9.09 / 13.13 |
| Room02_Breach | 8.80 / 12.39 | 9.35 / 14.11 |
| Room03_Assembly | 9.25 / 14.81 | 8.93 / 13.01 |
| Room04_Reactor | 6.80 / 26.92 | 9.94 / 14.01 |
| Pursuit05 (no combat) | 5.08 / 9.54 | 7.13 / 10.55 |

El pico de 41,4 ms del primer cambio incumplió el objetivo de 33 ms. El perfilado adicional también registró unos 36 ms en descarga/activación: no se elimina esa evidencia. Se ajustó el teardown para repartir cada GameObject de las jerarquías grandes y separar OnDisable de la destrucción; pendiente repetir tests y Player.

El intento -irFullHD terminó con código 3: solicitó 1920×1080 y recibió 1600×900. No produjo una medición Full HD válida. Log: Logs/HandsHudCampaign-Player-after.log.
