using Project.InventorySystem.Items;
using Project.InventorySystem.Artifacts;
using Project.ShopSystem;

namespace Project.Progression
{
    public enum AcquisitionKind
    {
        AddedRunItem,
        AddedRunCharacter,
        AddedRunArtifact,
        ConvertedToFragments,
        Rejected
    }

    public readonly struct AcquisitionResult
    {
        public AcquisitionResult(
            AcquisitionKind kind,
            BaseItem item,
            int fragmentAmount,
            string message,
            string artifactInstanceId = null)
        {
            Kind = kind;
            Item = item;
            FragmentAmount = fragmentAmount;
            Message = message;
            ArtifactInstanceId = artifactInstanceId;
        }

        public AcquisitionKind Kind { get; }
        public BaseItem Item { get; }
        public int FragmentAmount { get; }
        public string Message { get; }
        public string ArtifactInstanceId { get; }
        public bool Succeeded => Kind != AcquisitionKind.Rejected;
    }

    public static class InventoryAcquisitionResolver
    {
        private const int DefaultDuplicateFragmentAmount = 1;

        public static AcquisitionResult Acquire(
            BaseItem item,
            PlayerRunInventory runInventory,
            PlayerMetaProgression metaProgression,
            int amount = 1)
        {
            if (item == null || runInventory == null)
            {
                return new AcquisitionResult(AcquisitionKind.Rejected, item, 0, "Invalid acquisition.");
            }

            if (item is ArtifactDefinition artifactDefinition)
            {
                if (!runInventory.TryAcquireArtifact(artifactDefinition, out ArtifactInstance instance))
                {
                    return new AcquisitionResult(AcquisitionKind.Rejected, item, 0, "Artifact generation failed.");
                }

                return new AcquisitionResult(
                    AcquisitionKind.AddedRunArtifact,
                    item,
                    0,
                    $"Acquired {item.itemName}.",
                    instance.instanceId);
            }

            if (item is CharacterItem)
            {
                return AcquireUniqueOrFragments(item, runInventory, metaProgression);
            }

            int quantity = UnityEngine.Mathf.Max(1, amount);
            if (runInventory.TryAddItem(item, quantity))
            {
                string quantityLabel = quantity > 1 ? $"{quantity}x " : string.Empty;
                return new AcquisitionResult(AcquisitionKind.AddedRunItem, item, 0, $"Acquired {quantityLabel}{item.itemName}.");
            }

            return new AcquisitionResult(AcquisitionKind.Rejected, item, 0, "Inventory is full.");
        }

        private static AcquisitionResult AcquireUniqueOrFragments(
            BaseItem item,
            PlayerRunInventory runInventory,
            PlayerMetaProgression metaProgression)
        {
            if (!runInventory.HasItem(item))
            {
                if (!runInventory.TryAddItem(item))
                {
                    return new AcquisitionResult(AcquisitionKind.Rejected, item, 0, "Inventory is full.");
                }

                return new AcquisitionResult(AcquisitionKind.AddedRunCharacter, item, 0, $"Acquired {item.itemName}.");
            }

            if (metaProgression == null)
            {
                return new AcquisitionResult(AcquisitionKind.Rejected, item, 0, "MetaProgression is missing.");
            }

            metaProgression.AddFragments(item, DefaultDuplicateFragmentAmount);
            return new AcquisitionResult(
                AcquisitionKind.ConvertedToFragments,
                item,
                DefaultDuplicateFragmentAmount,
                $"Converted duplicate {item.itemName} into fragments.");
        }
    }
}
