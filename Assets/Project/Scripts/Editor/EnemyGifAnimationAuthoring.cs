using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEditor.Animations;
using UnityEngine;
using Project.AIBehaviour.Boss;
using Object = UnityEngine.Object;

public static class EnemyGifAnimationAuthoring
{
    public static void PrepareBossController()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Project/Character/enemy/Boss/Executioner/animation/ExecutionerAnimatorController.controller");
        var idle = controller.layers[0].stateMachine.states.Single(s => s.state.name == "Idle").state;
        idle.speed = 1f;
        EditorUtility.SetDirty(idle);
    }

    public sealed class Pose
    {
        public readonly int enemy, row;
        public readonly string name;
        public readonly float[] seconds;
        public Pose(int enemy, string name, int row, params float[] seconds)
        { this.enemy = enemy; this.name = name; this.row = row; this.seconds = seconds; }
        public string SpriteName(int frame) => $"Gif_{name}_{frame:00}";
        public float Duration => seconds.Sum();
    }

    // Frame holds measured from the supplied GIFs, not guessed from sheet spacing.
    public static readonly Pose[] Poses = {
        new Pose(101,"Idle",0,.15f,.15f,.15f,.15f,.15f,.15f),
        new Pose(101,"Bite",1,.2f,.2f,.1f,.1f,.1f,.1f),
        new Pose(101,"Walk",2,.12f,.12f,.12f,.12f,.12f,.12f),
        new Pose(101,"Hurt",3,.15f),
        new Pose(101,"Fall",4,.15f,.15f,.15f,.15f,.15f,.15f),
        new Pose(101,"Recover",5,.15f,.15f,.15f,.15f,.15f),
        new Pose(102,"Idle",0,.12f,.12f,.12f,.12f,.12f,.12f),
        new Pose(102,"Shoot",1,.15f,.15f,.15f,.15f,.15f,.15f),
        new Pose(102,"ShootStance",2,.15f,.15f,.15f,.15f),
        new Pose(102,"Hurt",3,.15f),
        new Pose(102,"Fall",4,.15f,.15f,.15f,.15f,.15f,.15f),
        new Pose(102,"Recover",5,.15f,.15f,.15f,.15f,.15f,.15f),
        new Pose(102,"Projectile",6,.15f,.15f,.15f),
        new Pose(103,"Idle",0,.15f,.15f,.15f,.15f,.15f,.15f),
        new Pose(103,"Guard",1,.15f,.15f,.15f,.15f),
        new Pose(103,"Dash",2,.15f,.15f,.15f,.15f,.15f,.15f,.15f),
        new Pose(103,"Walk",3,.1f,.1f,.1f,.1f,.1f,.1f),
        new Pose(103,"Hurt",4,.15f),
        new Pose(103,"Fall",6,.15f,.15f,.15f),
        new Pose(103,"Recover",7,.15f,.15f,.15f,.15f,.25f,.25f),
        new Pose(111,"Idle",0,.15f,.15f,.15f,.15f,.15f,.15f),
        new Pose(111,"Cleave",1,.15f,.15f,.15f),
        new Pose(111,"Charge",2,.15f,.15f,.1f,.1f,.1f,.1f,.1f,.1f,.1f,.1f,.1f,.1f,.1f,.1f),
        new Pose(111,"Walk",3,.2f,.2f,.2f,.2f,.2f,.2f,.2f),
        new Pose(111,"Jump",4,.1f,.1f,.1f,.1f,.1f)
    };

    public static IEnumerable<SpriteRect> Slices(int id, int height, Vector2 pivot)
    {
        int size = id == 111 ? 160 : 128;
        foreach (var pose in Poses.Where(p => p.enemy == id))
            for (int frame = 0; frame < pose.seconds.Length; frame++)
                yield return new SpriteRect { name = pose.SpriteName(frame),
                    rect = new Rect(frame * size, height - (pose.row + 1) * size, size, size),
                    pivot = pivot, alignment = SpriteAlignment.Custom };
    }

    public static Pose Resolve(int id, string key)
    {
        string pose = null;
        if (key.EndsWith("Idle",StringComparison.Ordinal)) pose = "Idle";
        else if (key.EndsWith("Walk",StringComparison.Ordinal)) pose = id == 102 ? "Idle" : "Walk";
        else if (id != 111 && key == "Enemy_Fall") pose = "Fall";
        else if (id != 111 && key == "Enemy_Knockdown") pose = "Fall";
        else if (id != 111 && key == "Enemy_GetUp") pose = "Recover";
        else if (id != 111 && (key == "Enemy_Hurt" || key == "Enemy_Launch" || key == "Enemy_Die")) pose = "Hurt";
        else if (key == "Enemy2_Crouch") pose = id == 101 ? "Bite" : id == 102 ? "Shoot" : "Guard";
        else if (key == "Enemy2_Kick" || key == "Enemy_Attack") pose = id == 101 ? "Bite" : id == 102 ? "ShootStance" : "Dash";
        else if (id == 111 && (key == "boss_AttackCleave" || key == "boss_Attack")) pose = "Cleave";
        else if (id == 111 && key == "boss_AttackCharge") pose = "Charge";
        else if (id == 111 && key == "boss_AttackJumpSmash") pose = "Jump";
        return Poses.FirstOrDefault(p => p.enemy == id && p.name == pose);
    }

    public static AnimationClip Build(int id, AnimationClip key, AnimationClip source,
        Dictionary<string, Sprite> sprites, string folder, char suffix, EnemyController enemy)
    {
        var pose = Resolve(id, key.name);
        if (pose == null) return null;
        string path = $"{folder}/Gif_{key.name}_Color{char.ToUpperInvariant(suffix)}.anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) { clip = Object.Instantiate(source); AssetDatabase.CreateAsset(clip, path); }
        else EditorUtility.CopySerialized(source, clip);
        clip.name = System.IO.Path.GetFileNameWithoutExtension(path);
        foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            if (binding.type == typeof(SpriteRenderer) && binding.propertyName == "m_Sprite")
                AnimationUtility.SetObjectReferenceCurve(clip, binding, null);
        int first = 0, count = pose.seconds.Length;
        bool biteWindup = id == 101 && key.name == "Enemy2_Crouch";
        if (biteWindup) count = 3;
        if (id == 101 && key.name == "Enemy2_Kick") { first = 4; count = 2; }
        if (key.name == "Enemy_Knockdown") { first = pose.seconds.Length - 1; count = 1; }
        float duration = pose.seconds.Skip(first).Take(count).Sum();
        float scale = 1f;
        bool windup = key.name == "Enemy2_Crouch" && id != 103;
        if (windup && !biteWindup && enemy.Kit != null && enemy.Kit.WarningDuration > 0f)
            scale = enemy.Kit.WarningDuration / duration;
        var frames = new ObjectReferenceKeyframe[count];
        float time = 0f;
        for (int i = 0; i < count; i++)
        {
            frames[i] = new ObjectReferenceKeyframe { time = time, value = sprites[pose.SpriteName(first + i)] };
            time += pose.seconds[first + i] * scale;
        }
        duration *= scale;
        if (biteWindup)
        {
            // Open promptly, then hold the last non-looping pose until the attack node strikes.
            for (int i = 0; i < frames.Length; i++) frames[i].time = i * .1f;
            duration = Mathf.Max(.4f, enemy.ResolveWarningDuration(.45f));
        }
        if (id == 111 && pose.name == "Charge")
        {
            // The existing motion-time API separates prepare / moving / braking at .3 and .5.
            for (int i = 0; i < frames.Length; i++)
                frames[i].time = duration * (i < 2 ? i * .15f : i < 6 ? .3f + (i - 2) * .05f : .5f + (i - 6) * .0625f);
        }
        else if (id == 111 && pose.name == "Jump")
        {
            float[] phases = { 0f, .25f, .6f, .7333333f, .8666667f };
            for (int i = 0; i < frames.Length; i++) frames[i].time = phases[i] * duration;
        }
        else if (id == 111 && pose.name == "Cleave")
        {
            float warning = enemy.GetComponent<BossPatternController>().CleaveTiming.telegraphDuration;
            frames[1].time = warning;
            frames[2].time = warning + pose.seconds[1];
            duration = Mathf.Max(warning + pose.seconds[1] + pose.seconds[2],
                warning + enemy.GetComponent<BossPatternController>().CleaveTiming.activeDuration);
        }
        var spriteBinding = new EditorCurveBinding { type = typeof(SpriteRenderer), propertyName = "m_Sprite", path = "" };
        AnimationUtility.SetObjectReferenceCurve(clip, spriteBinding, frames);
        clip.frameRate = 100f;
        var settings = AnimationUtility.GetAnimationClipSettings(source);
        settings.startTime = 0f; settings.stopTime = duration;
        settings.loopTime = pose.name == "Idle" || pose.name == "Walk" || pose.name == "Guard" || pose.name == "ShootStance";
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }
}
