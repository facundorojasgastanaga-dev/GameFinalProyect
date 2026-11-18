# Registro de llamadas a Tripo3D (Composio CLI 0.6.2, toolkit tripo3d 20261026_00)

| # | Hora UTC | Herramienta | Parámetros | Resultado | Créditos |
|---|---|---|---|---|---|
| 0 | 17/11 | `TRIPO3D_GET_BALANCE`, `LIST_USAGE` | — | saldo 0, sin historial | 0 |
| 1 | 2026-11-18 00:00:35 | `TRIPO3D_UPLOAD_FILE` | `--file input/T07Mech_TripoInput.glb` | `file_53cb5d73-34cc-4025-bbde-07b30f8521f4`; Tripo creó la tarea implícita `import_model` `4fcd6d20-1ffb-4788-b0cc-e0886b4d5bf2` | 0 |
| 2 | 2026-11-18 00:00:42 | `TRIPO3D_CHECK_MODEL_RIGGABILITY` | `{"input": "file_53cb…"}` | tarea `24315c55-fe7b-4fd2-a714-dbb2346bbfe0`: **riggable: false**, rig_type biped, normalize_scale 1.002 | 0 |

Saldo después del paso 2: 0 (verificado con `LIST_USAGE`). Las respuestas completas están en `01_upload_file.json`, `02_check_riggability_*.json`.
