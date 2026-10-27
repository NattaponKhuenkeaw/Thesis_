using System;
using UnityEngine;

namespace Project.Save
{
    /// <summary>
    /// Menu save slots stored in PlayerPrefs.
    /// The active slot selects which meta and boss-lobby keys the rest of the game reads.
    /// </summary>
    public static class SaveSlotStore
    {
        public const int SlotCount = 10;
        public const int PortraitCount = 3;

        private const string ActiveKey = "project.save.active_slot";
        private const string MigratedKey = "project.save.slots.migrated";
        private const string LegacyMetaV2 = "project.player_meta_progression.v2";
        private const string LegacyMetaV1 = "project.player_meta_progression.v1";
        private const string LegacyBossRun = "project.player_run_inventory.boss_lobby.v1";
        private const string LegacyBossMeta = "project.player_meta_progression.boss_lobby.v1";

        private static bool migrating;

        public static int ActiveSlot => Mathf.Clamp(PlayerPrefs.GetInt(ActiveKey, 0), 0, SlotCount - 1);

        public static string ActiveMetaKey => MetaKey(ActiveSlot);
        public static string ActiveBossRunKey => BossRunKey(ActiveSlot);
        public static string ActiveBossMetaKey => BossMetaKey(ActiveSlot);

        public static void SetActive(int slot)
        {
            PlayerPrefs.SetInt(ActiveKey, Mathf.Clamp(slot, 0, SlotCount - 1));
            PlayerPrefs.Save();
        }

        public static bool IsOccupied(int slot)
        {
            if (!IsSlot(slot))
            {
                return false;
            }

            EnsureMigrated();
            return PlayerPrefs.GetInt(OccupiedKey(slot), 0) == 1;
        }

        public static void MarkOccupied(int slot)
        {
            if (!IsSlot(slot))
            {
                return;
            }

            PlayerPrefs.SetInt(OccupiedKey(slot), 1);
            PlayerPrefs.Save();
        }

        public static void Delete(int slot)
        {
            if (!IsSlot(slot))
            {
                return;
            }

            PlayerPrefs.DeleteKey(MetaKey(slot));
            PlayerPrefs.DeleteKey(BossRunKey(slot));
            PlayerPrefs.DeleteKey(BossMetaKey(slot));
            PlayerPrefs.DeleteKey(HeaderKey(slot));
            PlayerPrefs.DeleteKey(OccupiedKey(slot));
            PlayerPrefs.Save();
        }

        public static bool TryReadHeader(int slot, out SaveSlotHeader header)
        {
            header = null;
            if (!IsSlot(slot))
            {
                return false;
            }

            EnsureMigrated();
            string json = PlayerPrefs.GetString(HeaderKey(slot), string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                header = JsonUtility.FromJson<SaveSlotHeader>(json);
                return header != null;
            }
            catch (ArgumentException)
            {
                Debug.LogWarning($"[SaveSlotStore] Save {slot + 1} header is invalid. Original data was preserved.");
                return false;
            }
        }

        public static void WriteActiveHeader(SaveSlotHeader header)
        {
            int slot = ActiveSlot;
            PlayerPrefs.SetString(HeaderKey(slot), JsonUtility.ToJson(header ?? new SaveSlotHeader()));
            PlayerPrefs.SetInt(OccupiedKey(slot), 1);
            PlayerPrefs.Save();
        }

        public static void EnsureMigrated()
        {
            if (migrating || PlayerPrefs.GetInt(MigratedKey, 0) == 1)
            {
                return;
            }

            migrating = true;
            try
            {
                bool meta = CopyString(LegacyMetaV2, MetaKey(0));
                if (!PlayerPrefs.HasKey(MetaKey(0)))
                {
                    meta = CopyString(LegacyMetaV1, MetaKey(0)) || meta;
                }

                bool bossRun = CopyString(LegacyBossRun, BossRunKey(0));
                bool bossMeta = CopyString(LegacyBossMeta, BossMetaKey(0));
                if (meta || bossRun || bossMeta)
                {
                    string bossJson = PlayerPrefs.GetString(BossRunKey(0), string.Empty);
                    if (!Project.ShopSystem.PlayerRunInventory.TryBuildHeaderFromBossJson(bossJson, out SaveSlotHeader header))
                    {
                        header = new SaveSlotHeader();
                    }

                    // Existing slot previews belong to that slot, not to the legacy migration.
                    if (!PlayerPrefs.HasKey(HeaderKey(0)))
                    {
                        PlayerPrefs.SetString(HeaderKey(0), JsonUtility.ToJson(header));
                    }
                    PlayerPrefs.SetInt(OccupiedKey(0), 1);
                }

                PlayerPrefs.SetInt(MigratedKey, 1);
                PlayerPrefs.Save();
            }
            finally
            {
                migrating = false;
            }
        }

        public static string MetaKey(int slot) => $"project.save.slot{slot}.meta.v2";
        public static string BossRunKey(int slot) => $"project.save.slot{slot}.boss_lobby.v1";
        public static string BossMetaKey(int slot) => $"project.save.slot{slot}.boss_meta.v1";

        private static string HeaderKey(int slot) => $"project.save.slot{slot}.header";
        private static string OccupiedKey(int slot) => $"project.save.slot{slot}.occupied";

        private static bool IsSlot(int slot) => slot >= 0 && slot < SlotCount;

        private static bool CopyString(string fromKey, string toKey)
        {
            if (!PlayerPrefs.HasKey(fromKey) || PlayerPrefs.HasKey(toKey))
            {
                return PlayerPrefs.HasKey(toKey);
            }

            PlayerPrefs.SetString(toKey, PlayerPrefs.GetString(fromKey));
            return true;
        }
    }

    [Serializable]
    public class SaveSlotHeader
    {
        public int biomeIndex;
        public int globalLevel = 1;
        public int currency = 1000;
        public string[] characterIds = new string[SaveSlotStore.PortraitCount];
        public bool[] dead = new bool[SaveSlotStore.PortraitCount];

        public string CharacterIdAt(int index)
        {
            if (characterIds == null || index < 0 || index >= characterIds.Length)
            {
                return null;
            }

            return characterIds[index];
        }

        public bool IsDeadAt(int index)
        {
            return dead != null && index >= 0 && index < dead.Length && dead[index];
        }
    }
}
