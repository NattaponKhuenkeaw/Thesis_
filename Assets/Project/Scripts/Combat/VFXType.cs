namespace Project.Combat
{
    /// <summary>
    /// Centralized registry for all VFX types in the game.
    /// To add a new VFX, simply add a new entry here and register it in the VFXLibrary asset.
    /// </summary>
    public enum VFXType
    {
        None = 0,
        
        // --- Hitsparks ---
        HitSpark_Light = 10,
        HitSpark_Medium = 11,
        HitSpark_Heavy = 12,
        HitSpark_Ultimate = 13,
        
        
        // --- Movement ---
        Dust_Dash = 30,
        Dust_Land = 31,
        Jump_Cloud = 32,
        
        // --- Character Status ---
        Ultimate_Flash = 40,
        Buff_Aura = 41,
        Stun_Stars = 42,
        
        // --- Environmental / Common ---
        Blood_Splash = 50,
        Explosion_Small = 60,
        Explosion_Large = 61,

        // --- Custom / Designer Slots (100+) ---
        // Use these for quick prototype or character-specific VFX
        Custom_01 = 100,
        Custom_02 = 101,
        Custom_03 = 102,
        Custom_04 = 103,
        Custom_05 = 104,
        Custom_06 = 105,
        Custom_07 = 106,
        Custom_08 = 107,
        Custom_09 = 108,
        Custom_10 = 109
    }
}
