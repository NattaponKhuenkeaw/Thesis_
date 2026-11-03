"""Read shipped Unity serialized data without loading game assemblies or changing assets."""
import sys, json, hashlib
from pathlib import Path
from collections import Counter
sys.path.insert(0, r'C:\Users\Apricha\AppData\Local\Temp\codex-crimson-movement-tools')
import UnityPy
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator

BUILD = Path(r'C:\Users\Apricha\Desktop\Crimson Orchard - Build September 2026\CrimsonOrchard_Data')
OUT = Path(__file__).parent
paths = [p for p in BUILD.iterdir() if p.is_file() and (p.suffix == '.assets' or p.name == 'globalgamemanagers' or (p.name.startswith('level') and not p.suffix))]
env = UnityPy.load(*(str(p) for p in paths))
print('Loaded serialized file inventory', flush=True)
generator = TypeTreeGenerator('6000.3.9f1')
generator.load_local_dll_folder(str(BUILD / 'Managed'))
env.typetree_generator = generator
print('Loaded managed metadata for type layouts', flush=True)
rows, errors, counts = [], [], Counter()
selected = {'MonoBehaviour', 'MonoScript', 'Rigidbody2D', 'PhysicsMaterial2D', 'Physics2DSettings', 'TimeManager', 'Animator', 'AnimatorController', 'AnimationClip', 'CapsuleCollider2D', 'BoxCollider2D', 'Transform', 'GameObject', 'ResourceManager'}
for obj in env.objects:
    counts[obj.type.name] += 1
    if obj.type.name not in selected:
        continue
    try:
        if obj.type.name == 'MonoBehaviour':
            head = obj.parse_monobehaviour_head()
            script = head.m_Script.deref_parse_as_object()
            name = script.m_ClassName
            if not (name.startswith('PState_') or name.startswith('Cinemachine') or name in {'PlayerData', 'PlayerController', 'CharacterStatSO', 'CrossSceneDataManager', 'MovementInputReaderSo', 'InputActionAsset', 'PlayerStateConditionManager'}):
                continue
        tree = obj.read_typetree()
        if obj.type.name == 'MonoBehaviour':
            # Use UnityPy's engine header layout; generated layouts can omit
            # alignment after m_Enabled while still reaching the script payload.
            tree['m_Script'] = {'m_FileID': head.m_Script.m_FileID, 'm_PathID': head.m_Script.m_PathID}
            tree['m_Enabled'] = head.m_Enabled
        row = {'file': obj.assets_file.name, 'path_id': obj.path_id, 'type': obj.type.name, 'data': tree}
        if obj.type.name == 'MonoBehaviour':
            row['script'] = name
        rows.append(row)
    except Exception as exc:
        errors.append({'file': obj.assets_file.name, 'path_id': obj.path_id, 'type': obj.type.name, 'error': str(exc)})
result = {'unitypy_version': UnityPy.__version__, 'artifacts': [{'file':p.name, 'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in paths], 'counts':dict(counts), 'objects':rows, 'errors':errors}
(OUT / 'serialized-assets.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
print(json.dumps({'counts':dict(counts), 'read':len(rows), 'errors':len(errors), 'error_preview':errors[:4]}))
for row in rows:
    d = row['data']
    if row.get('script') in {'PlayerData', 'PState_Jump', 'PState_AirJump', 'PState_Falling', 'PState_SlowFalling'}:
        print(row['file'], row['path_id'], row['script'])
