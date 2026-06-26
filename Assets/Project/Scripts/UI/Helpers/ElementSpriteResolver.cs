using UnityEngine;
using UnityEngine.UI;

namespace Project.UI
{
    [System.Obsolete("Use ElementDatabase instead to resolve element icons and properties.")]
    [CreateAssetMenu(fileName = "ElementSpriteResolver", menuName = "UI/Element Sprite Resolver")]
    public class ElementSpriteResolver : ScriptableObject
    {
        [Tooltip("Legacy: Assign sprites in order: None, Earth, Water, Fire, Wind (matching ElementType enum order)")]
        [SerializeField] private Sprite[] elementSprites = new Sprite[5];

        public Sprite GetSprite(ElementType elementType)
        {
            var db = ElementDatabase.Load();
            if (db != null)
            {
                var icon = db.GetIcon(elementType);
                if (icon != null) return icon;
            }

            int index = (int)elementType;
            if (index >= 0 && index < elementSprites.Length)
            {
                return elementSprites[index];
            }
            return null;
        }

        public void ResolveImage(Image targetImage, ElementType elementType)
        {
            if (targetImage == null) return;
            Sprite sprite = GetSprite(elementType);
            targetImage.enabled = sprite != null;
            if (sprite != null)
            {
                targetImage.sprite = sprite;
            }
        }
    }
}
