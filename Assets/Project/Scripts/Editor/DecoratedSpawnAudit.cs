using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Project.Core.ZoneState;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class DecoratedSpawnAudit
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static readonly string[] Enemies={
        "Assets/Project/Character/enemy/enemy/Enm101/Enemy1.prefab",
        "Assets/Project/Character/enemy/enemy/Enm102/Enemy102.prefab",
        "Assets/Project/Character/enemy/enemy/Enm103/Enemy103.prefab"};
    [Serializable] public class Finding
    {
        public string layout,room,slot,enemy,scenario,problem,obstacle;
        public Vector3 authored,final;
        public Vector2 bodySize;
        public float penetration;
        public Vector3 physicsShift;
    }
    [Serializable] public class Result
    {
        public int slots,probes,nudges,rejectedNudges,decorativeSolidColliders;
        public List<Finding> findings=new List<Finding>();
    }
    private static Result result;

    public static void Run()
    {
        if(EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        var saved=Directory.GetFiles("Assets/Project/Resources/RoomLayouts","*.prefab",SearchOption.AllDirectories)
            .Concat(Directory.GetFiles("Assets/Project/Scenes","*.unity",SearchOption.AllDirectories))
            .ToDictionary(p=>p,File.ReadAllBytes);
        var random=UnityEngine.Random.state;
        result=new Result();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        try
        {
            for(int level=1;level<=10;level++) InspectLayout(BiomeLayoutAuthoring.LayoutPath(level));
            InspectLayout(LayoutRoadAuthoring.BossPath,true);
        }
        finally
        {
            UnityEngine.Random.state=random;
            foreach(var file in saved)
                if(!file.Value.SequenceEqual(File.ReadAllBytes(file.Key))) throw new InvalidOperationException("Audit changed saved artwork: "+file.Key);
        }
        Directory.CreateDirectory("Logs/DecoratedSpawnAudit");
        File.WriteAllText("Logs/DecoratedSpawnAudit/results.json",JsonUtility.ToJson(result,true));
        foreach(var issue in result.findings)
            Debug.Log($"[Spawn audit finding] {issue.layout}/{issue.room}/{issue.slot} {issue.enemy} {issue.scenario}: {issue.problem} {issue.obstacle} authored={issue.authored} final={issue.final}");
        Debug.Log($"[Spawn audit] slots={result.slots}, placements={result.probes}, accepted nudges={result.nudges}, safely rejected nudges={result.rejectedNudges}, decorative solids={result.decorativeSolidColliders}, findings={result.findings.Count}. All saved layouts and scenes byte-identical.");
    }

    private static void InspectLayout(string path,bool boss=false)
    {
        var fixture=new GameObject("Read-only decorated spawn fixture");
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var root=Object.Instantiate(source,fixture.transform,false);
        root.transform.localPosition=Vector3.zero;
        try
        {
            foreach(var composite in root.GetComponentsInChildren<CompositeCollider2D>()) composite.GenerateGeometry();
            Physics2D.SyncTransforms();
            result.decorativeSolidColliders+=root.GetComponentsInChildren<Collider2D>().Count(c=>c.enabled && !c.isTrigger
                && !Hierarchy(c.transform,root.transform).StartsWith("Terrain/")
                && !Hierarchy(c.transform,root.transform).StartsWith("OneWayPlatforms/")
                && !Hierarchy(c.transform,root.transform).StartsWith("Boundaries/"));
            if(boss)
            {
                var slot=root.GetComponentsInChildren<Transform>().First(t=>t.name=="BossSpawnPoint");
                result.slots++;
                Probe(root,"Boss",slot,slot.position,"Boss","Assets/Project/Character/enemy/Boss/Executioner/Boss_Executioner.prefab");
                return;
            }
            var waves=root.GetComponentInChildren<RoomWaveManager>();
            var occupied=(List<Vector3>)typeof(RoomWaveManager).GetField("livingEnemyPositions",Private).GetValue(waves);
            var chambers=root.GetComponentInChildren<CombatStageController>().Chambers;
            var boundary=root.GetComponentInChildren<SceneBoundaryController>();
            for(int index=0;index<chambers.Count;index++)
            {
                var chamber=chambers[index];
                // Earlier dividers stay open when a later encounter starts.
                for(int prior=0;prior<index;prior++)
                {
                    boundary.OpenDivider(chambers[prior].exitWall);
                    foreach(var wall in chambers[prior].additionalExitWalls ?? new Collider2D[0])
                        if(!chambers.Skip(index).Any(c=>(c.additionalExitWalls ?? new Collider2D[0]).Contains(wall))) boundary.OpenDivider(wall);
                }
                if(index>0) boundary.OpenDivider(chamber.entranceWall);
                chamber.exitWall.enabled=true; chamber.exitWall.isTrigger=false;
                foreach(var wall in chamber.additionalExitWalls ?? new Collider2D[0]) {wall.enabled=true; wall.isTrigger=false;}
                Physics2D.SyncTransforms();
                waves.BindSpawnSlots(chamber.spawnSlots);
                foreach(var slot in chamber.spawnSlots)
                {
                    result.slots++;
                    foreach(string enemy in Enemies) Probe(root,chamber.name,slot,slot.position,"Authored",enemy);
                    foreach(int side in new[]{0,-1,1})
                    {
                        occupied.Clear(); occupied.Add(slot.position);
                        if(side!=0) occupied.Add(slot.position+Vector3.right*side*1.8f);
                        object[] args={slot,Vector3.zero};
                        bool accepted=(bool)typeof(RoomWaveManager).GetMethod("TryResolveSeparatedPosition",Private).Invoke(waves,args);
                        if(!accepted) {result.rejectedNudges++; continue;}
                        result.nudges++;
                        foreach(string enemy in Enemies) Probe(root,chamber.name,slot,(Vector3)args[1],"Crowded_"+side,enemy);
                    }
                }
                if(index==0 && root.name.StartsWith("RoomLayout_LV_01",StringComparison.Ordinal))
                    ProbeActualPair(root,chamber,waves,occupied);
            }
            occupied.Clear();
        }
        finally {Object.DestroyImmediate(fixture);}
        Debug.Log("[Spawn audit] Inspected "+Path.GetFileName(path));
    }

    private static void ProbeActualPair(GameObject layout,CombatStageController.Chamber chamber,RoomWaveManager waves,List<Vector3> occupied)
    {
        Transform slot=null; Vector3 candidate=Vector3.zero;
        foreach(var point in chamber.spawnSlots)
        {
            occupied.Clear(); occupied.Add(point.position);
            object[] args={point,Vector3.zero};
            if(!(bool)typeof(RoomWaveManager).GetMethod("TryResolveSeparatedPosition",Private).Invoke(waves,args)) continue;
            slot=point; candidate=(Vector3)args[1]; break;
        }
        if(slot==null) return;
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Enemies[0]);
        var first=Object.Instantiate(prefab); var second=Object.Instantiate(prefab);
        try
        {
            first.transform.position=slot.position; second.transform.position=candidate;
            first.GetComponent<EnemyController>().rb.position=first.transform.position;
            second.GetComponent<EnemyController>().rb.position=second.transform.position;
            Physics2D.SyncTransforms();
            var a=first.GetComponent<EnemyController>().bodyCollider; var b=second.GetComponent<EnemyController>().bodyCollider;
            var distance=Physics2D.Distance(a,b);
            if(distance.isOverlapped && distance.distance<-.03f)
                result.findings.Add(new Finding{layout=layout.name.Replace("(Clone)",""),room=chamber.name,slot=slot.name,enemy="Enemy1 + Enemy1",
                    scenario="Crowded_actual_pair",problem="NUDGE_STACKS_REAL_BODIES",obstacle="Minimum separation="+Vector3.Distance(first.transform.position,second.transform.position).ToString("F3"),
                    authored=first.transform.position,final=second.transform.position,
                    penetration=-distance.distance,bodySize=a.bounds.size});
        }
        finally {Object.DestroyImmediate(first); Object.DestroyImmediate(second); occupied.Clear();}
    }

    private static void Probe(GameObject layout,string room,Transform slot,Vector3 position,string scenario,string path)
    {
        result.probes++;
        var actor=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        actor.transform.position=position;
        try
        {
            var enemy=actor.GetComponent<EnemyController>();
            var flight=actor.GetComponent<EnemyLocomotion>();
            if(enemy.rb!=null) enemy.rb.position=position;
            Physics2D.SyncTransforms();
            if(flight!=null) typeof(EnemyLocomotion).GetMethod("Awake",Private).Invoke(flight,null);
            bool flying=flight!=null && flight.UsesFixedHeightFlight;
            bool placed=flying ? flight.PrepareSpawnFlight() : enemy.TryPlaceSpawnOnGround();
            Physics2D.SyncTransforms();
            Action<string,string> report=(problem,obstacle)=>result.findings.Add(new Finding{
                layout=layout.name.Replace("(Clone)",""),room=room,slot=slot.name,enemy=Path.GetFileNameWithoutExtension(path),
                scenario=scenario,problem=problem,obstacle=obstacle,
                authored=position,final=actor.transform.position});
            if(!placed) {report("PLACEMENT_REJECTED",""); return;}
            var body=enemy.bodyCollider; Bounds bounds=body.bounds;
            int firstOverlap=result.findings.Count;
            var filter=new ContactFilter2D(); filter.SetLayerMask(Physics2D.GetLayerCollisionMask(body.gameObject.layer)); filter.useTriggers=false;
            var hits=new Collider2D[128];
            var size=Vector2.Max(Vector2.one*.01f,(Vector2)bounds.size-Vector2.one*.06f);
            int count=Physics2D.OverlapBox(bounds.center,size,0,filter,hits);
            foreach(var hit in hits.Take(count).Where(c=>c!=null && !c.transform.IsChildOf(actor.transform)
                && !Physics2D.GetIgnoreCollision(body,c)))
            {
                var distance=Physics2D.Distance(body,hit);
                if(!distance.isOverlapped || distance.distance>=-.03f) continue;
                report(hit.usedByEffector ? "BODY_OVERLAPS_ONE_WAY" : "BODY_IN_SOLID",
                    Hierarchy(hit.transform,layout.transform)+" layer="+LayerMask.LayerToName(hit.gameObject.layer)+" shape="+body.GetType().Name);
                var issue=result.findings[result.findings.Count-1]; issue.penetration=-distance.distance; issue.bodySize=bounds.size;
            }
            if(!flying && result.findings.Count>firstOverlap && enemy.rb!=null)
            {
                Vector3 before=actor.transform.position;
                var mode=Physics2D.simulationMode;
                try
                {
                    Physics2D.simulationMode=SimulationMode2D.Script;
                    enemy.rb.bodyType=RigidbodyType2D.Dynamic; enemy.rb.simulated=true; enemy.rb.WakeUp();
                    for(int frame=0;frame<60;frame++)
                        if(!Physics2D.Simulate(.02f)) throw new InvalidOperationException("Physics simulation did not advance.");
                    for(int index=firstOverlap;index<result.findings.Count;index++)
                        result.findings[index].physicsShift=actor.transform.position-before;
                }
                finally {Physics2D.simulationMode=mode;}
            }
            var groundFilter=new ContactFilter2D(); groundFilter.SetLayerMask(LayerMask.GetMask("Ground","OneWayPlatform")); groundFilter.useTriggers=false;
            var groundHits=new RaycastHit2D[64];
            var origin=new Vector2(bounds.center.x,bounds.min.y+.04f);
            int groundCount=Physics2D.Raycast(origin,Vector2.down,groundFilter,groundHits,flying ? 30f : .2f);
            var floors=groundHits.Take(groundCount).Where(h=>h.collider!=null && h.normal.y>.5f && !h.collider.transform.IsChildOf(actor.transform)).OrderBy(h=>h.distance).ToArray();
            if(floors.Length==0) report("NO_SUPPORTING_FLOOR","");
            else if(flying)
            {
                float altitude=bounds.min.y-floors[0].point.y;
                if(altitude>7.3f) report("HIGH_FLIGHT_CLEARANCE",altitude.ToString("F3"));
            }
            var boundaries=layout.transform.Find("Boundaries");
            foreach(var wall in boundaries!=null ? boundaries.GetComponentsInChildren<BoxCollider2D>() : new BoxCollider2D[0])
            {
                if(!wall.enabled || wall.isTrigger || wall.name=="Ceiling") continue;
                Bounds gate=wall.bounds;
                if(bounds.max.y<=gate.min.y || bounds.min.y>=gate.max.y) continue;
                bool crossed=position.x<gate.min.x && bounds.center.x>gate.max.x
                    || position.x>gate.max.x && bounds.center.x<gate.min.x;
                if(crossed) report("PLACEMENT_CROSSED_GATE",Hierarchy(wall.transform,layout.transform));
            }
        }
        catch(Exception ex)
        {
            result.findings.Add(new Finding{layout=layout.name,room=room,slot=slot.name,enemy=path,scenario=scenario,problem="AUDIT_EXCEPTION",obstacle=ex.ToString()});
        }
        finally {Object.DestroyImmediate(actor);}
    }

    private static string Hierarchy(Transform item,Transform root)=>AnimationUtility.CalculateTransformPath(item,root);
}
