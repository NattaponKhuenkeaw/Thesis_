using System;
using System.Linq;
using UnityEngine;
using Plan = LevelDesignLayoutBuilder.Plan;
using RoomPlan = LevelDesignSpawnBuilder.RoomPlan;

public static class Biome2LevelPlans
{
    public static Plan GetPlan(int level)
    {
        switch (level)
        {
            case 6: return new Plan {
                level=6, baseline=848, start=new Vector2(155,848), exit=new Vector2(1812,715),
                solids=new[] { R(108,848,1812,972),R(790,715,1812,848) },
                platforms=Array.Empty<Vector3>(), gates=new[] { G(428),G(791),G(1188),G(1526) }
            };
            case 7: return new Plan {
                level=7, baseline=848, start=new Vector2(155,848), exit=new Vector2(1812,540),
                solids=new[] { R(108,848,1812,972),R(432,706,1812,848),R(1191,540,1812,706) },
                platforms=new[] { P(1164,627,80),P(1164,548,80) },
                gates=new[] { G(428),G(791),G(1188),G(1526) }
            };
            case 8: return new Plan {
                level=8, baseline=848, start=new Vector2(155,707), exit=new Vector2(1812,508),
                solids=new[] { R(108,848,1812,972),R(108,707,428,848),R(794,675,995,848),
                    R(1190,676,1812,848),R(1526,508,1812,676) },
                platforms=new[] { P(767,786,78),P(767,707,78),P(1499,597,78),P(1499,518,78) },
                gates=new[] { G(428),G(794),G(1188),G(1526) }
            };
            case 9: return new Plan {
                level=9, baseline=917, start=new Vector2(155,857), exit=new Vector2(1812,917),
                solids=new[] { R(108,917,1812,972),R(108,857,1141,917),R(407,739,715,857),
                    R(1049,484,1141,857),R(1141,484,1595,550),R(1384,364,1595,484),
                    R(1301,641,1432,816),R(1432,762,1812,816) },
                platforms=new[] { P(1024,778,80),P(1024,699,80),P(1024,620,80),P(1024,541,80) },
                gates=new[] { G(407),G(715),G(1049),G(1384),G(1556,917,816) }
            };
            case 10: return new Plan {
                level=10, baseline=865.4f, start=new Vector2(155,865.4f), exit=new Vector2(1812,543.3f),
                solids=new[] { R(108,865.4f,1812,961.4f),R(390.7f,662.9f,660.8f,865.4f),
                    R(660.8f,539.1f,1034.7f,865.4f),R(1456.7f,543.3f,1812,865.4f) },
                platforms=new[] { P(369,848.2571f,90),P(369,758.2571f,90),P(369,668.2571f,90),
                    P(1418.2f,793.7f,70),P(1342.9f,657.2f,70) },
                gates=new[] { G(388),G(664),G(1034),G(1455) }
            };
            default: throw new ArgumentOutOfRangeException(nameof(level));
        }
    }

    public static Vector2[] ExpansionSpans(int level)
    {
        switch (level)
        {
            case 6: return Spans(180,340,490,650,890,1050,1270,1410,1600,1750);
            case 7: return Spans(180,340,500,650,850,1050,1280,1410,1600,1750);
            case 8: return Spans(180,330,500,650,840,950,1250,1400,1600,1750);
            case 9: return Spans(180,330,480,630,790,950,1170,1300,1420,1530,1640,1770);
            case 10: return Spans(180,300,470,580,730,880,1130,1290,1560,1750);
            default: throw new ArgumentOutOfRangeException(nameof(level));
        }
    }

    public static RoomPlan[] Rooms(int level)
    {
        switch (level)
        {
            case 6: return new[] {
                Row(166,848,848,210,240,270,300,330),Row(470,848,848,490,530,570,610,650),
                Row(850,715,715,890,930,970,1010,1050),Row(1250,715,715,1270,1310,1350,1390,1430),
                Row(1590,715,715,1600,1640,1680,1720,1760) };
            case 7: return new[] {
                Row(166,848,848,210,240,270,300,330),Row(490,706,706,500,535,570,605,640),
                Row(850,706,706,860,900,940,980,1020),Row(1260,540,540,1280,1320,1360,1400,1440),
                Row(1590,540,540,1600,1640,1680,1720,1760) };
            case 8: return new[] {
                Row(166,707,707,210,240,270,300,330),Row(490,848,848,500,540,580,620,660),
                Row(850,675,675,850,875,900,925,950),Row(1250,676,676,1260,1300,1340,1380,1420),
                Row(1600,508,508,1600,1640,1680,1720,1760) };
            case 9: return new[] {
                Row(166,857,857,210,240,270,300,328),Row(480,739,739,490,525,560,595,630),
                Row(790,857,857,800,840,880,920,960),Row(1180,484,484,1180,1210,1240,1270,1300),
                Row(1430,364,364,1435,1455,1475,1495,1515),Row(1640,917,917,1640,1670,1700,1730,1760) };
            case 10: return new[] {
                Row(166,865.4f,865.4f,210,235,260,285,306),Row(440,662.9f,662.9f,470,495,520,545,580),
                Row(710,539.1f,539.1f,740,775,810,845,880),Row(1090,865.4f,865.4f,1140,1180,1220,1260,1300),
                Row(1510,543.3f,543.3f,1560,1610,1660,1710,1760)
            };
            default: throw new ArgumentOutOfRangeException(nameof(level));
        }
    }

    private static Rect R(float l,float t,float r,float b) => new Rect(l,t,r-l,b-t);
    private static Vector3 P(float x,float y,float w) => new Vector3(x,y,w);
    private static Vector3 G(float x,float bottom=-1,float top=-1) => new Vector3(x,bottom,top);
    private static Vector2[] Spans(params float[] values) => Enumerable.Range(0,values.Length/2)
        .Select(i=>new Vector2(values[i*2],values[i*2+1])).ToArray();
    private static RoomPlan Row(float x,float y,float floor,params float[] xs) => new RoomPlan {
        entry=new Vector2(x,y),slots=xs.Select(n=>new Vector2(n,floor)).ToArray() };
}
