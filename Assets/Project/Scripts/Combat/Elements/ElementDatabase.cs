using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ElementDatabase", menuName = "Game/Combat/Element Database")]
public class ElementDatabase : ScriptableObject
{
    [SerializeField] private Color physicalDamageColor = Color.white;
    [SerializeField] private List<ElementDefinition> definitions = new List<ElementDefinition>();

    private Dictionary<ElementType, ElementDefinition> definitionMap;
    private static ElementDatabase cachedDatabase;

    public static ElementDatabase Load()
    {
        if (cachedDatabase != null)
        {
            return cachedDatabase;
        }

        cachedDatabase = Resources.Load<ElementDatabase>("ElementSO/ElementDatabase");
        if (cachedDatabase == null)
        {
            cachedDatabase = Resources.Load<ElementDatabase>("Combat/ElementDatabase");
        }
        if (cachedDatabase == null)
        {
            Debug.LogWarning("ElementDatabase not found at 'Resources/ElementSO/ElementDatabase' or 'Resources/Combat/ElementDatabase'. Creating a temporary fallback instance.");
            cachedDatabase = CreateInstance<ElementDatabase>();
        }
        return cachedDatabase;
    }

    public void InitializeLookup()
    {
        definitionMap = new Dictionary<ElementType, ElementDefinition>();
        if (definitions != null)
        {
            foreach (var def in definitions)
            {
                if (def != null && !definitionMap.ContainsKey(def.elementType))
                {
                    definitionMap.Add(def.elementType, def);
                }
            }
        }

        // Auto-discover any ElementDefinition assets in Resources/ElementSO/
        ElementDefinition[] loaded = Resources.LoadAll<ElementDefinition>("ElementSO");
        if (loaded != null)
        {
            foreach (var def in loaded)
            {
                if (def != null && !definitionMap.ContainsKey(def.elementType))
                {
                    definitionMap.Add(def.elementType, def);
                }
            }
        }
    }

    private void OnEnable()
    {
        InitializeLookup();
    }

    public ElementDefinition GetDefinition(ElementType type)
    {
        if (definitionMap == null || definitionMap.Count == 0)
        {
            InitializeLookup();
        }

        if (type == ElementType.None)
        {
            return null;
        }

        if (definitionMap.TryGetValue(type, out var definition) && definition != null)
        {
            return definition;
        }

        // Direct lookup attempt in Resources
        ElementDefinition direct = Resources.Load<ElementDefinition>($"ElementSO/Element_{type}");
        if (direct != null)
        {
            definitionMap[type] = direct;
            return direct;
        }

        // Programmatic fallback if SO is missing in Resources
        ElementDefinition fallback = CreateInstance<ElementDefinition>();
        fallback.elementType = type;
        fallback.displayName = type.ToString();
        fallback.dotDuration = 5f;
        fallback.dotDamageRatio = 0.3f;
        fallback.dotTickInterval = 1f;

        switch (type)
        {
            case ElementType.Lightning:
                fallback.damageTextColor = new Color(1f, 0.92f, 0.016f, 1f);
                break;
            case ElementType.Bleed:
                fallback.damageTextColor = new Color(0.85f, 0.1f, 0.1f, 1f);
                break;
            case ElementType.Darkness:
                fallback.damageTextColor = new Color(0.5f, 0.1f, 0.7f, 1f);
                break;
            case ElementType.Particle:
                fallback.damageTextColor = new Color(0.78f, 0.72f, 1f, 1f);
                break;
        }

        if (definitionMap != null)
        {
            definitionMap[type] = fallback;
        }

        return fallback;
    }

    public Color GetDamageColor(ElementType type)
    {
        if (type == ElementType.None)
        {
            return physicalDamageColor;
        }

        var def = GetDefinition(type);
        return def != null ? def.damageTextColor : Color.white;
    }

    public Sprite GetIcon(ElementType type)
    {
        var def = GetDefinition(type);
        return def != null ? def.icon : null;
    }

    public bool HasDotDefinition(ElementType type)
    {
        return GetDefinition(type) != null;
    }
}
