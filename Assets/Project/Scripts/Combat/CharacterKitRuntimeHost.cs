using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public sealed class CharacterKitRuntimeHost : IDisposable
{
    private sealed class Entry
    {
        public CharacterObject owner;
        public CharacterKitRuntimeDefinition definition;
        public ICharacterKitRuntime runtime;
    }

    private readonly TeamLoader _team;
    private readonly List<Entry> _entries = new List<Entry>();
    private CharacterObject[] _characters = Array.Empty<CharacterObject>();
    public CharacterObject[] Characters => _characters;

    public CharacterKitRuntimeHost(TeamLoader team)
    {
        _team = team;
        CombatEventHub.Shared.DamageResolved += OnDamageResolved;
        SceneManager.activeSceneChanged += OnSceneChanged;
    }

    public void Reconcile(CharacterObject[] characters)
    {
        characters ??= Array.Empty<CharacterObject>();
        bool changed = _characters.Length != characters.Length;
        for (int i = 0; !changed && i < characters.Length; i++) changed = _characters[i] != characters[i];
        if (changed) _characters = (CharacterObject[])characters.Clone();
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            Entry entry = _entries[i];
            if (Array.IndexOf(_characters, entry.owner) >= 0
                && entry.owner.kitDefinition?.runtimeDefinition == entry.definition) continue;
            entry.runtime.Dispose();
            _entries.RemoveAt(i);
        }
        for (int i = 0; i < _characters.Length; i++)
        {
            CharacterObject owner = _characters[i];
            CharacterKitRuntimeDefinition definition = owner?.kitDefinition?.runtimeDefinition;
            if (definition == null || Find(owner) != null) continue;
            _entries.Add(new Entry { owner = owner, definition = definition, runtime = definition.CreateRuntime(owner, _team) });
        }
        for (int i = 0; i < _entries.Count; i++) _entries[i].runtime.Reconcile();
    }

    public ICharacterKitRuntime Find(CharacterObject owner)
    {
        for (int i = 0; i < _entries.Count; i++)
            if (_entries[i].owner == owner) return _entries[i].runtime;
        return null;
    }

    public void Tick(float delta)
    {
        if (delta <= 0f) return;
        for (int i = 0; i < _entries.Count; i++) _entries[i].runtime.Tick(delta);
    }

    public void OnPositivePrimaryHit(in DamageRequest request, in DamageResult result, float damageDealt)
    {
        for (int i = 0; i < _entries.Count; i++)
            _entries[i].runtime.OnPositivePrimaryHit(request, result, damageDealt);
    }

    private void OnDamageResolved(DamageResolvedEvent result)
    {
        for (int i = 0; i < _entries.Count; i++) _entries[i].runtime.OnDamageResolved(result);
    }

    public bool TryGetMode(CharacterObject character, out float remaining)
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            ICharacterKitRuntime runtime = _entries[i].runtime;
            if (!runtime.Affects(character)) continue;
            remaining = runtime.RemainingSeconds;
            return true;
        }
        remaining = 0f;
        return false;
    }

    private void OnSceneChanged(Scene previous, Scene current)
    {
        // The run-owned timed/permanent recipes survive; world mode bindings do not.
        for (int i = 0; i < _entries.Count; i++) _entries[i].runtime.Dispose();
        _entries.Clear();
    }

    public void Dispose()
    {
        CombatEventHub.Shared.DamageResolved -= OnDamageResolved;
        SceneManager.activeSceneChanged -= OnSceneChanged;
        for (int i = 0; i < _entries.Count; i++) _entries[i].runtime.Dispose();
        _entries.Clear();
    }
}
