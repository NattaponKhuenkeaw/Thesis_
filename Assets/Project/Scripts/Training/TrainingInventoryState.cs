using System;
using System.Collections.Generic;
using Project.InventorySystem.Items;
using Project.ShopSystem;
using UnityEngine;

namespace Project.Training
{
    // A fresh, nonpersistent practice inventory. It never reads a saved inventory.
    public sealed class TrainingInventoryState : CharacterInventoryState
    {
        public void PopulateCatalog(IEnumerable<BaseItem> catalog)
        {
            if (catalog == null) return;
            var characters = new HashSet<string>(StringComparer.Ordinal);
            var artifacts = new HashSet<string>(StringComparer.Ordinal);
            foreach (var owned in ownedItems)
                if (owned is CharacterItem character && character.characterObjectData != null)
                    characters.Add(character.characterObjectData.characterId);
            foreach (var artifact in ArtifactInstances)
                if (artifact != null) artifacts.Add(artifact.definitionId);
            var entries = new List<BaseItem>(catalog);
            foreach (var item in entries)
            {
                if (item is CharacterItem character && character.characterObjectData != null
                    && !string.IsNullOrWhiteSpace(character.characterObjectData.characterId)
                    && characters.Add(character.characterObjectData.characterId))
                {
                    ownedItems.Add(character);
                    GetOrCreateRunCharacterState(character.characterObjectData);
                }
                else if (item is ArtifactDefinition definition && !string.IsNullOrWhiteSpace(definition.id)
                    && artifacts.Add(definition.id))
                    TryAcquireArtifact(definition, out _, biomeIndexOverride: 0);
            }
            BuildItemIndices();
            foreach (var item in entries)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.id)) continue;
                itemIndexById[item.id] = item;
                if (item is CharacterItem character && character.characterObjectData != null)
                    characterItemsByCharacterId[character.characterObjectData.characterId] = character;
                if (item is ArtifactDefinition definition) artifactDefinitionsById[definition.id] = definition;
            }
            NotifyArtifactsChanged();
        }

        public void SetLevel(int level, bool notify = true)
        {
            int before = GlobalLevel;
            _globalLevel = Mathf.Clamp(level, 1, GlobalMaxLevel);
            if (notify) NotifyLevelChanged(before);
        }

        public void NotifyLevelChanged(int before) => NotifyGlobalLevelChanged(before);
    }
}
