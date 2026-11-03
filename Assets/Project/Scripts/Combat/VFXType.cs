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
        HitSpark_og_Slash = 19,
        HitSpark_og_Heavy = 20,
        HitSpark_og_Yellow = 21,

        
        // --- Movement ---
        Dash_Dust = 30,
        Dash_Fire = 31,
        Land_Dust = 32,
        Jump_Cloud = 33,
        

        // --- Character Status ---
        Ultimate_Flash = 40,
        Ultimate_FlashEX = 41,
        Buff_Aura = 42,
        Stun_Stars = 43,
        

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
        Kyo_Ultimate_Fire_Loop = 105,
        Kyo_Ultimate_Fire_Punch = 106,
        Kyo_Ultimate_Punch = 107,


        // --- Rugal ---
        Rugal_Skill = 110,
        Rugal_Ultimate_Aura = 111,
        Rugal_Ultimate_Explosion = 112,

        // --- Krizalid ---
        Krizalid_Projectile_Hit = 120,

        // --- Goenitz ---
        Goenitz_Basic = 130,
        Goenitz_Basic2 = 131,
        Goenitz_Basic3 = 132,
        Goenitz_Tornado = 133,
        Goenitz_Light = 134,
        Goenitz_Ultimate_Tornado = 135,
        Goenitz_Ultimate_Tornado2 = 136,
        Goenitz_Ultimate_slash = 137,

        // --- Main ---
        Main_Basic1 = 140,
        Main_Basic2 = 141,
        Main_Basic3 = 142,
        Main_Skill1 = 143,
        Main_Skill2 = 144,
        Main_Skill3 = 145,
        Main_Ultimate = 146,
        Main_Air_Attack1 = 147,
        Main_Plunge_Impact = 148,

        // --- Orochi ---
        Orochi_Basic = 150,
        Orochi_Basic2 = 151,
        Orochi_Skill_EX = 152,
        Orochi_Ult_Start = 153,
        Orochi_Ult_Loop = 154,
        Orochi_Ult_End = 155,

        Main_DimensionSlash_BurstA = 160,
        Main_DimensionSlash_BurstB = 161,
        Main_DimensionSlash_BurstC = 162,
        Main_DimensionSlash_Residual = 163,
        Main_DimensionSlash_Seam = 164,
        Main_DimensionSlash_Final = 165,
        Main_DimensionSlash_Shards = 166,
    }
}
