public static class ElementalReactionFactory
{
    public static IElementalReaction Create(ElementType type)
    {
        return type switch
        {
            ElementType.Memory => new MemoryReaction(),
            ElementType.Echo => new EchoReaction(),
            ElementType.Ash => new AshReaction(),
            ElementType.Void => new VoidReaction(),
            _ => null
        };
    }
}
