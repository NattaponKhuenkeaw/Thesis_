using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ParticleBehaviorPool : MonoBehaviour
{
    private readonly List<ParticleBehaviorRuntime> _active = new List<ParticleBehaviorRuntime>();
    private readonly Stack<ParticleBehaviorRuntime> _inactive = new Stack<ParticleBehaviorRuntime>();
    private TeamLoader _teamLoader;

    public void Initialize(TeamLoader teamLoader)
    {
        _teamLoader = teamLoader;
    }

    public void Spawn(ArtifactPassiveRuntime source, ParticleBehaviorDefinition definition,
        DamageResolvedEvent? triggerEvent)
    {
        if (source == null || definition == null || _teamLoader == null) return;
        ParticleBehaviorRuntime runtime = _inactive.Count > 0
            ? _inactive.Pop()
            : new ParticleBehaviorRuntime();
        runtime.Initialize(source, definition, triggerEvent, _teamLoader);
        _active.Add(runtime);
    }

    private void Update()
    {
        float now = Time.time;
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            ParticleBehaviorRuntime runtime = _active[i];
            if (runtime.Tick(now)) continue;
            runtime.Reset();
            _active.RemoveAt(i);
            _inactive.Push(runtime);
        }
    }

    private void OnDestroy()
    {
        for (int i = 0; i < _active.Count; i++)
        {
            _active[i].Reset();
            _active[i].CleanupVisualizer();
        }
        while (_inactive.Count > 0)
        {
            _inactive.Pop().CleanupVisualizer();
        }
        _active.Clear();
        _inactive.Clear();
    }
}

