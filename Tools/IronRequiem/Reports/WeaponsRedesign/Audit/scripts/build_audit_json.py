"""Read-only: merge probe-*.json, transitive-consumers.json and the audited facts into weapons-audit.json."""
import json
from pathlib import Path

A = Path(__file__).resolve().parents[1]
probe = {p.stem[6:]: json.loads(p.read_text(encoding='utf-8')) for p in A.glob('probe-*.json')}
trans = json.loads((A / 'transitive-consumers.json').read_text(encoding='utf-8'))

meta_common = {'globalScale': 1, 'useFileScale': 1, 'useFileUnits': 1, 'bakeAxisConversion': 0, 'isReadable': 0,
               'meshCompression': 0, 'addColliders': 0, 'preserveHierarchy': 0}
weapons = {
    'PistolModule_1000': {
        'fbx': 'Assets/IronRequiem/Art/Weapons/BasicPistolModule/PistolModule_1000.fbx', 'guid': '60c069240a47c1248b8576ed5920ca5f',
        'meta': dict(meta_common, materialImportMode=2, importAnimation=1, animationType='2 (Generic)', lODScreenPercentages=[]),
        'authoring': {'script': 'Tools/IronRequiem/Blender/create_pistol_module.py',
                      'blend': 'Tools/IronRequiem/Blender/Working/BasicPistolModule/PistolModule_1000.blend',
                      'export': "axis_forward='-Z', axis_up='Y', apply_unit_scale=True; empties Attachment (0,0,0), Muzzle (0,-0.379,0)"},
        'lods': {'LOD0': 532}, 'vertices': 1000,
        'textures': ['PistolModule_BaseColor.png 768x768', 'PistolModule_Emission.png 768x768'],
        'unity_materials': {'PistolModule_URP.mat': 'URP/Lit, _EmissionMap asignado, _EmissionColor 0.7, keyword _EMISSION AUSENTE (m_ValidKeywords: []) -> emision probablemente inactiva; verificar en Editor'},
        'emissive_note': 'Ranuras cian laterales x=+-0.058 m, z=0.17-0.23 m, y~0.01 (muestreo por centroide UV; 16.7% de texeles del atlas emisivos). Pestanas naranja no son emisivas en el atlas.',
        'geometry_note': 'Caras sueltas: 1000 bordes abiertos, cada cara es una isla (sombreado plano por separacion). Origen = acople trasero; malla de z=0 a z=0.379.',
        'unity_sockets_in_visual_prefab': {'Attachment': [0, 0, 0], 'Muzzle': [0, 0, 0.379], 'forward': '+Z', 'up': '+Y',
                                           'note': 'PistolModule_1000_Visual: raiz identidad; nodo FBX -90 X (x100 de escala de archivo), hijos Attachment/Muzzle con +90 X override'},
        'sockets_in_weapon_prefabs': {
            'Weapon_IR_Pistol (WeaponRoot)': {'visual_root': [0, 0.124, 0.077], 'Attachment': [0, 0.124, 0.077], 'Muzzle=WeaponMuzzle': [0, 0.124, 0.456]},
            'Weapon_IR_Pistol_CombatPolish': {'Attachment': [0, 0.124, 0.038], 'note': 'override de posicion del nodo FBX Attachment (CombatPolishAuthoring.cs:147), renderers del modulo desactivados, RoboticWristCoupler en z=0.048'},
            'Weapon_IR_PrimaryModule_v02 y derivados': {'note': 'PistolModule_1000_Equipped desactivado; Attachment/Muzzle renombrados LegacyAttachment_1000/LegacyMuzzle_1000 por override (PrimaryModuleAuthoring.cs:34)'}},
    },
    'IR_PrimaryModule_v02': {
        'fbx': 'Assets/IronRequiem/Art/Weapons/PrimaryModuleV02/IR_PrimaryModule_v02.fbx', 'guid': '899ab1d0bf4636a41a5549499212bc3e',
        'meta': dict(meta_common, materialImportMode=0, importAnimation=0, animationType='0 (None)', lODScreenPercentages=[]),
        'authoring': {'script': 'Tools/IronRequiem/Blender/primary_module_v02.py',
                      'blend': 'Tools/IronRequiem/Blender/Working/PrimaryModuleV02/IR_PrimaryModule_v02.blend',
                      'export': "axis_forward='-Z', axis_up='Y'; empties Attachment (0,0,0), Muzzle (0,-0.333,0); FBX de Working identico al de Assets (SHA 7839DEC7...)"},
        'lods': {'LOD0': 696}, 'vertices': 384,
        'textures': ['Reutiliza PistolModule_BaseColor.png y PistolModule_Emission.png de BasicPistolModule (768x768)'],
        'unity_materials': {'PrimaryModule_URP.mat': 'URP/Lit, _EMISSION activo, _EmissionMap = atlas de la pistola, _EmissionColor 0.7'},
        'emissive_note': 'Ranuras cian laterales x=+-0.054 m, z=0.125-0.19 m (muestreo por centroide UV).',
        'geometry_note': 'Malla cerrada (0 bordes abiertos), 20 islas. La malla sobresale 4.5 mm por detras del Attachment (z min = -0.0045).',
        'unity_sockets_in_weapon_prefab': {'Weapon_IR_PrimaryModule_v02 (WeaponRoot)': {'PrimaryModuleVisual': [0, 0.094, 0.077], 'Attachment': [0, 0.094, 0.077], 'GunMuzzle=WeaponMuzzle': [0, 0.094, 0.41]},
                                           'note': 'La visual se coloca haciendo coincidir el Muzzle del FBX con GunMuzzle (PrimaryModuleAuthoring.cs:35-37): anclaje por boca, no por acople.'},
    },
    'T07_CannonClean': {
        'fbx': 'Assets/IronRequiem/Art/Weapons/T07CannonClean/T07_CannonClean.fbx', 'guid': '48ac3df5d72f0eb44a98b3d277f045d0',
        'meta': dict(meta_common, materialImportMode=0, importAnimation=0, animationType='2 (Generic)', lODScreenPercentages=[0.25, 0.125, 0.01]),
        'lods': {'LOD0': 1702, 'LOD1': 906, 'LOD2': 412}, 'unity_reported_tris_source_import': {'LOD0': 1694, 'LOD1': 807, 'LOD2': 401},
        'textures': [], 'blender_materials': ['OriginalCannonSurface (LOD0 1494 tris)', 'CutInterior (LOD0 208 tris)'],
        'unity_materials': {'T07_URP_FPS.mat': 'URP/Lit, normal map, sin emision', 'ExposedMechanism_FPS.mat': '_EmissionColor naranja (1.08,0.31,0.05) pero sin keyword _EMISSION -> no emite'},
        'emissive_note': 'Ninguna en el FBX; la carga magenta es VFX aparte (CannonCharge_Magenta.prefab).',
        'pivot': 'Origen del robot completo: (-0.300,-0.027,1.092) Blender; 7.7 cm fuera del eje; canon inclinado ~31 grados',
        'firing_axis_blender': {'mouth_center': [-0.3154, -0.2148, 0.5363], 'mouth_normal': [-0.074, -0.513, -0.855], 'rear_attach': [-0.2661, 0.1260, 1.1045], 'length_m': 0.6644,
                                'source': 'Docs/IronRequiem/WEAPON_PIVOT_T07_CANNON.md (probe_cannon_pivot.py)'},
        'sockets': 'Ninguno en el FBX',
        'usage_in_prefab': {'Weapon_T07_Temporary_Optimized': 'CannonClean renderer m_Enabled=0; nodo con escala 100, pos (0.1247,0.1273,-0.0843), rot (x0.880,y-0.364,z0.150,w-0.265). Lo visible es VP_StolenShell; Attachment (0.0584,0.0558,-0.1742); muzzle base (0.015,0.065,0.48) recolocado en runtime por StolenChargeAnchor.'},
    },
    'T07_Cannon_PivotFixed': {
        'fbx': 'Assets/IronRequiem/Art/Weapons/T07CannonPivotFixed/T07_Cannon_PivotFixed.fbx', 'guid': '3167e5df9a314d44a9213ef269e96bf4',
        'meta': dict(meta_common, materialImportMode=0, importAnimation=0, animationType='0 (None)', lODScreenPercentages=[0.25, 0.125, 0.01]),
        'lods': {'LOD0': 1702, 'LOD1': 906, 'LOD2': 412},
        'textures': [], 'unity_materials': {'T07_URP_FPS.mat': 'sin emision', 'ExposedMechanism_FPS.mat': 'emision configurada sin keyword'},
        'lodgroup_in_visual': [0.18, 0.06, 0.003],
        'geometry_note': 'Derivado sin seguimiento git (??). Mesh sobresale 3.2 mm detras del acople. LOD1/LOD2: boca desviada 1-4 cm.',
        'unity_sockets_in_visual_prefab': {'WeaponAttachment': [0, 0, 0], 'WeaponMuzzle': [0, 0, 0.66442], 'forward': '+Z', 'up': '+Y',
                                           'fbx_empties': 'Attachment (0,0,0) y Muzzle (0,0,0.6644) hijos de CannonClean_LOD0 (rotacion importada -90 X)'},
        'regenerated_by': 'Assets/IronRequiem/Editor/WeaponPivotAuthoring.cs desde Tools/IronRequiem/Exports/T07_Cannon_PivotFixed/T07_Cannon_PivotFixed.fbx (copia identica, SHA C9F8132E...)',
    },
}
for k, w in weapons.items():
    p = probe.get(k)
    if p:
        w['source_sha256'] = p['sha256_before']
        w['source_unchanged_by_audit'] = p['source_unchanged']
        w['blender_measure'] = {'empties': p['empties'], 'meshes': [{x: o[x] for x in ('name', 'vertices', 'triangles', 'size_unity_xyz_m', 'unity_bounds_min', 'unity_bounds_max', 'origin_blender_world', 'materials', 'emissive') if x in o} for o in p['objects']]}
        w['renders'] = [str(Path(r).relative_to(A.parents[4])).replace('\\', '/') for r in p.get('renders', [])]
    w['transitive_consumers'] = trans.get(Path(w['fbx']).name, {})

contract = {
    'receiver': 'ProtagonistHandsPresentation (Assets/IronRequiem/Scripts/Player/ProtagonistHandsPresentation.cs)',
    'arm_follows_weapon': 'El arma se coloca con PlayerWeaponsManager (WeaponParentSocket + posicion por defecto/ADS); el antebrazo se ajusta al arma, no al reves.',
    'weapon_interface': ['Transform llamado exactamente "Attachment" bajo WeaponRoot (se toma el primero de GetComponentsInChildren; ModularArmTests exige uno solo)',
                         'WeaponController.WeaponMuzzle: su +Z (forward) es el eje de disparo y el eje del antebrazo'],
    'port_formula': 'rightPort = Attachment.position + WeaponMuzzle.forward * 0.003 (ProtagonistHandsPresentation.cs:256); solo si actionsEnabled, representacion distinta de Normal y Attachment encontrado',
    'fallback_without_attachment': {'pistolWristOffset': [0, -0.015, -0.23], 'cannonWristOffset': [0.015, -0.04, -0.38], 'mountedWristOffset': [0, -0.03, -0.42], 'space': 'rotacion de WeaponMuzzle, desde WeaponMuzzle.position (lineas 25-33, 246)'},
    'modular_arm_v07': 'Con modularRightArm la mano avanza (rightLength - retainedForearmLength 0.14 m) a lo largo de muzzle.forward; RightModuleSocket = (rightPort, WeaponMuzzle.rotation) (linea 323); collar de muneca radio 0.046 m, largo 0.032 m en el puerto.',
    'player_transforms': {'Player_IR_Protagonist_v01': ['ModuleSocket_R', 'ModuleSocket_L'], 'Player_ModularArm_v07': ['RightArm_ModuleSocket (cuerpo, junto al codo)', 'Forearm_R_Receiver (SkinnedMeshRenderer)', 'RightArm_UniversalCoupling'],
                          'runtime_property': 'ProtagonistHandsPresentation.RightModuleSocket (campo rightModuleSocket)'},
    'representation': 'StolenCannon si carrier.Held.EquippedWeapon==active; Pistol si SourcePrefab==pistolPrefab; Mounted si esta en mountedWeaponPrefabs; Normal en otro caso (linea 204).',
    'stolen_fit': 'StolenShellFit coloca VP_StolenShell con la cara trasera de su AABB (centro x/y, min z) en RightModuleSocket con 0.02 m de solape; StolenChargeAnchor lleva la boca al centro frontal del AABB + 0.015 m.',
    'mount_station': 'WeaponMountStation usa Attachment del proxy, MountedHandLead (= rightLength-0.14+0.003) y bounds de renderers activos para el agarre.',
    'requirements_for_zero_manual_adjustment': [
        'Origen del FBX en el centro de la cara trasera de acople; disparo por Blender -Y (Unity +Z), arriba Blender +Z (Unity +Y); escala 1 m; exportar con axis_forward=-Z, axis_up=Y.',
        'Vacios "Attachment" (0,0,0) y "Muzzle" (0,-L,0) dentro del FBX, o sockets equivalentes con identidad en la raiz del prefab visual; un unico "Attachment" por arma.',
        'Ninguna geometria detras de z=0 salvo el solape previsto (3 mm del puerto, 20 mm en armas robadas); la cara trasera centrada en x=0,y=0.',
        'Muzzle exactamente en el extremo del eje +Z y la caja AABB del arma alineada con +Z (StolenShellFit/StolenChargeAnchor miden por AABB).',
        'Importacion: useFileScale=1, globalScale=1, bakeAxisConversion=0 (activar bake invirtio el canon); prefab visual con raiz identidad.',
        'Colocacion en WeaponRoot por Attachment (hoy se hace por Muzzle contra GunMuzzle heredado): fijar una posicion canonica del Attachment, p.ej. (0, y, 0.077).',
        'Conservar nombres de nodos FBX (fileID derivados del nombre) si se reemplaza un FBX existente.'],
}
out = {'generated': '2026-11-16', 'mode': 'solo lectura', 'blender': '5.2.3 LTS --background --factory-startup',
       'unity_convention': 'Unity = (-Xb, Zb, -Yb) para estos FBX (bakeAxisConversion 0)', 'weapons': weapons, 'attachment_contract': contract}
(A / 'weapons-audit.json').write_text(json.dumps(out, indent=1, ensure_ascii=False), encoding='utf-8')
print('ok', len(weapons))
