"""Make a compact evidence index. All values come from the supplied build."""
import json, struct, math
from pathlib import Path

ROOT = Path(__file__).parent
source = json.loads((ROOT / 'serialized-assets.json').read_text(encoding='utf-8'))
objects = source['objects']
def select(script):
    return [o for o in objects if o.get('script') == script]
def object_at(file, path_id):
    return next(o for o in objects if o['file'] == file and o['path_id'] == path_id)
def ref(o):
    return {'file':o['file'], 'path_id':o['path_id']}

pd = select('PlayerData')[0]
rb = object_at('sharedassets1.assets', 401)
char = object_at(pd['file'], pd['data']['curCharacter']['m_PathID'])
state_names = ['Idle','Move','Jump','AirJump','Falling','SlowFalling','Dash','Crouch','DrinkPotion','BasicAttack1','BasicAttack2','BasicAttack3','Parry','UpperAttack','AirAttack1','AirAttack2','AirPlunge','CounterAttack','DashAttack','ChargeAttack','WallSlide','WallJump','Hurt','InstantDrop','DownAttack','UseSkill','VeinStackConsuming']
states=[]
for o in objects:
    if o.get('script') in {'PState_Idle','PState_Move','PState_Jump','PState_AirJump','PState_Falling','PState_SlowFalling'}:
        d=dict(o['data'])
        for k in ['m_GameObject','m_Script','m_Enabled']:
            d.pop(k, None)
        d['transitions']=[state_names[i] for i in d.pop('<canTransitionToState>k__BackingField')]
        states.append({**ref(o), 'script':o['script'], 'settings':d})

active_input=object_at('globalgamemanagers.assets',2770)
movement=next(m for m in active_input['data']['m_ActionMaps'] if m['m_Name']=='Movement')
bindings=[{k:b[k] for k in ['m_Name','m_Path','m_Action','m_Processors','m_Interactions','m_Flags']} for b in movement['m_Bindings'] if b['m_Action'] in ['Move','Jump'] and b['m_Path']]
clips=[]
for o in objects:
    d=o['data']
    if o['type']!='AnimationClip' or d.get('m_Name') not in {'Running','Jump','AirJump','Falling','Falling_OnGround','BeginSlowFalling','SlowFalling'}:
        continue
    m=d['m_MuscleClip']
    raw=m['m_Clip']['data']['m_StreamedClip']['data']
    payload=struct.pack('<'+'I'*len(raw),*raw)
    offset, keys=0,[]
    while offset<len(payload):
        time,count=struct.unpack_from('<fi',payload,offset); offset+=8
        for _ in range(count):
            index,*coefs=struct.unpack_from('<i4f',payload,offset); offset+=20
            if time>=0 and math.isfinite(time):
                keys.append({'time':time,'curve':index,'sprite_mapping_index':int(coefs[3])})
    clips.append({**ref(o),'name':d['m_Name'],'duration':m['m_StopTime']-m['m_StartTime'],'loop':m['m_LoopTime'],'sample_rate':d['m_SampleRate'],'sprite_keys':keys,'sprite_mapping':d['m_ClipBindingConstant']['pptrCurveMapping'],'events':d['m_Events']})

summary={
    'authority':'shipped artifact; static extraction, not runtime telemetry',
    'assembly_sha256':'93dccc21e9f046974a3729be256aa775b53daa711e99d5dcdd167a0fceefca38',
    'player_data':pd, 'character':char, 'rigidbody':rb,
    'physics':next(o for o in objects if o['type']=='Physics2DSettings'),
    'time':next(o for o in objects if o['type']=='TimeManager'),
    'materials':[o for o in objects if o['type']=='PhysicsMaterial2D'],
    'states':states, 'input_source':ref(active_input),'bindings':bindings,
    'animations':clips,
    'animator':object_at('sharedassets1.assets',431),
    'camera_composers':select('CinemachinePositionComposer'),
    'cameras':select('CinemachineCamera'),
    'coverage':{'selected_object_reads':len(objects),'selected_object_errors':source['errors'],'note':'Unselected MonoBehaviours and other object types are outside extraction scope.'},
}
(ROOT/'movement-tuning.json').write_text(json.dumps(summary,indent=2),encoding='utf-8')
print(json.dumps({'animations':clips,'player':ref(pd),'target_speed':char['data']['Speed'],'states':[(s['script'],s['settings']['transitions']) for s in states]},indent=2))
