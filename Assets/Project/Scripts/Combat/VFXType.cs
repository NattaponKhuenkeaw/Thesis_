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
        Kyo_Ultimate_loop = 105,
        Kyo_Ultimate_flash = 106,
        Kyo_Ultimate_flash2 = 107,


        // --- Rugal ---
        Rugal_Skill = 108,
        Rugal_Ultimate_Aura = 109,
        Rugal_Ultimate_Explosion = 110,

        // --- Krizalid ---
        Krizalid_Projectile_Hit = 111,

        // --- Goenitz ---
        Goenitz_Basic = 112,
        Goenitz_Basic2 = 113,
        Goenitz_Basic3 = 114,
        Goenitz_Tornado = 115,
        Goenitz_Light = 116,
        Goenitz_Ultimate_Tornado = 117,
        Goenitz_Ultimate_Tornado2 = 118,
        Goenitz_Ultimate_slash = 119,

        // --- Main ---
        Main_Basic1 = 120,
        Main_Basic2 = 121,
        Main_Basic3 = 122,
        Main_Skill1 = 123,
        Main_Skill2 = 124,
        Main_Skill3 = 125,
        Main_Ultimate = 126,
        Main_Air_Attack1 = 127,
        Main_Plunge_Impact = 128,
    }
}
