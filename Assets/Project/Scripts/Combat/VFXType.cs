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
        HitSpark_Grab = 14,
        HitSpark_Grab_Slash = 15,
        HitSpark_Burn = 16,
        HitSpark_Explosion = 17,
        HitSpark_Slam = 18,

        
        // --- Movement ---
        Dash_Dust = 30,
        Dash_Fire = 31,
        Land_Dust = 32,
        Jump_Cloud = 33,
        

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
        

        // --- Kyo ---
        Kyo_Skill1 = 100,
        Kyo_Skill1_Slash = 101,
        Kyo_Skill2 = 102,
        Kyo_Skill2_Fire = 103,
        Kyo_Skill3 = 104,


        // --- Rugal ---
        Rugal_Skill = 105,
        Rugal_Ultimate_Aura = 106,
        Rugal_Ultimate_Explosion = 107,

        // --- Krizalid ---
        Krizalid_Projectile_Hit = 108,

        // --- Goenitz ---
        Goenitz_Basic = 109,
        Goenitz_Basic2 = 110,
        Goenitz_Basic3 = 111,
        Goenitz_Tornado = 112,
        Goenitz_Light = 113,
        Goenitz_Ultimate_Tornado = 114,

        // --- Main ---
        Main_Basic1 = 115,
        Main_Basic2 = 116,
        Main_Basic3 = 117,
        Main_Skill1 = 118,
        Main_Skill2 = 119,
        Main_Skill3 = 120,
        Main_Ultimate = 121,
        Main_Air_Attack1 = 122,
        Main_Plunge_Impact = 123,
    }
}
