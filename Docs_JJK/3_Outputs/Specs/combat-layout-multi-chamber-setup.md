# หลายห้องใน 1 เลย์เอาต์ combat — วางใน Editor

โค้ดรองรับแล้ว: **1 `RoomLayout_Combat_*` = 1 ด่าน** มีหลายห้องต่อเนื่อง  
เคลียร์ห้อง 1 เดินไป 2 ไป 3 ตามที่คุณใส่ในลิสต์  
**เลือกห้อง (RouteOffer 3 ใบ) ขึ้นหลังห้องสุดท้ายเท่านั้น**

เลย์เอาต์ที่ยังไม่มี `CombatStageController` ทำงานแบบเดิม (1 เลย์เอาต์ = 1 ห้อง)

## มองเห็นตอนจูน

1. เปิดปุ่ม **Gizmos** มุมขวาบน Scene View
2. คลิก object ที่มี `CombatStageController`
3. ใน Scene จะขึ้นกล่องสีต่อห้อง, ป้าย **ห้อง 1 / ซ้าย / ขวา**, จุดเหลือง = Spawn, เส้นเขียว = Enter Trigger
4. ลากก้อนสี่เหลี่ยมที่ขอบซ้าย/ขวา เพื่อขยับกำแพง
5. ใน Inspector มีสถานะแต่ละห้อง และปุ่ม **Focus**

ถ้า Chambers ยัง Size 0 จะยังไม่มีกล่องใน Scene

## วางบนเลย์เอาต์

ใส่ `CombatStageController` ที่เดียวกับ `Scene Boundary Controller` (`EnvironmentDependency`)

`Chambers` Size = จำนวนห้องในด่านนี้ เช่น `3`

แต่ละช่อง:

1. **Entrance Wall** — กำแพงซ้ายของห้องนั้น
2. **Exit Wall** — กำแพงขวา (ห้องติดกันใช้กำแพงเดียวกันได้: ขวาของห้อง 1 = ซ้ายของห้อง 2)
3. **Enter Trigger** — ห้อง 2 เป็นต้นไป แนะนำให้มี BoxCollider2D เป็น Trigger **ในห้อง** (เลยกำแพงกั้นมาแล้ว ไม่ซ้อนกับกำแพง)
4. **Spawn Root** — พาเรนต์ของ `Spawn_0`, `Spawn_1`, `Spawn_2` **ของห้องนั้นเท่านั้น**
5. **Encounter** — คลังชนิดตัวของห้องนั้น ลาก `EncounterSO` (Enemy Types + Weight + Type Pick บน asset)
6. **Enemy Count** — จำนวนศัตรูในห้องนั้น เช่น `3` ได้ 3 ตัว, `0` = ไม่มีไฟต์ เปิดทางต่อ

ชนิดตัวอ่านจาก Encounter ของ**ห้องนั้น** แล้วสุ่มตาม Weight / Type Pick บน asset นั้น
ถ้า Encounter ยังไม่ใส่ Enemy Types จะย้อนไปอ่าน Waves ใน asset นั้น
ถ้าห้องไม่ลาก Encounter เลย จะใช้พูลจาก RouteOffer ของด่าน (fallback)

ห้องแรกใช้ Trigger ของ `RoomWaveManager` เริ่มไฟต์ได้ ไม่บังคับ Enter Trigger

`Scene Boundary Controller` ยังเก็บ **Exit Trigger** ของท้ายด่าน (Choice UI) ไว้ที่เดิม

## ตัวอย่าง 3 ห้องซ้าย → ขวา

```
[StartWall] ห้อง1 [Divider1] ห้อง2 [Divider2] ห้อง3 [ExitWall] [Exit]
```

| Chamber | Entrance | Exit | Spawn Root |
| --- | --- | --- | --- |
| 0 Room 1 | StartWall | Divider1 | Combat/Room1/Spawns |
| 1 Room 2 | Divider1 | Divider2 | Combat/Room2/Spawns |
| 2 Room 3 | Divider2 | ExitWall | Combat/Room3/Spawns |

แต่ละห้องมีจุดเกิดของตัวเอง อย่าให้ทุกห้องชี้ `Spawn_0` ชุดเดียวกัน

## พฤติกรรมตอนเล่น

- เข้าด่าน → ล็อกกำแพงห้อง 1 แล้วสปอว์น Pressure ตามโควต้าห้อง 1
- เคลียร์ห้อง 1 → กำแพงกั้นเปิด กล้องขยายครอบคลุมห้องถัดไปด้วย **ยังไม่ขึ้น Choice** เดินกลับห้องที่เคลียร์แล้วได้
- เดินเข้าห้อง 2 → เริ่มไฟต์ หน้าจอไม่ตัด/ไม่คิวกล้อง กำแพงหน้ายังปิด เดินกลับห้อง 1 ได้
- เคลียร์แล้วเปิดทางหลังด้วย เดินกลับห้อง 1 ได้
- เคลียร์ห้องสุดท้าย → ปลด Exit + เตรียมการ์ด 3 ใบ ยังเดินย้อนห้องที่ผ่านมาได้

## ไม่ต้องแตะ

- `RoomChoiceDirector` / จำนวนการ์ด
- Boss layout (`Encounter_Boss_*` ยัง Burst ห้องเดียว)
