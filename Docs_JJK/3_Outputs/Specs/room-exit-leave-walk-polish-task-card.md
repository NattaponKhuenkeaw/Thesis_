---
slug: room-exit-leave-walk-polish
status: implemented-pending-playtest
source: manual
gdd_tags:
  - scene-roles
  - core-loop
owner: agent
human_checkpoint: playtest
next_agent: human
blocked_by: []
---

# Task Card: ขัดจังหวะเดินออกห้อง (Exit Wall)

## เป้าหมายฝั่งผู้เล่น

ตอนกดเลือกการ์ดออกจากห้อง ผู้เล่นต้องไม่เห็นตัวละครเดินไปยืนติดกำแพง `exitWall` แล้วค้างอยู่อย่างนั้นระหว่างจอมืด

## ที่มา

- Origin: manual prompt (developer feedback pass ก่อน balancing)
- Requested by: project owner
- อาการที่รายงาน: "ตอนผู้เล่นกดเลือกออกห้องก็ยังเห็นตัวผู้เล่นเดินติดกำแพง exitWall อยู่เลย ทำไมไม่ทำให้มันเปิดหรือ IsEnterCollider มันแล้วเปลี่ยน scene > method สั่งผู้เล่นเดินไป X length แล้วเปลี่ยน"

## GDD Reference

- `@tag:scene-roles` — ห้องมีขอบเขต entrance / exit ที่ชัดเจน
- `@tag:core-loop` — เคลียร์ห้อง แล้วเดินออกไปห้องถัดไป

## ประเภทงาน

- [x] Bug fix (presentation)

## ความสัมพันธ์กับการ์ดอื่น

- `room-wall-camera-lock-task-card.md` (status `implementing`) กำหนดว่าตอนห้องยังไม่เคลียร์ ผู้เล่นต้องยืนชิดขอบจอได้ การ์ดนี้ไม่แตะพฤติกรรมนั้น แก้เฉพาะจังหวะ "หลังเลือกการ์ดแล้วกำลังจะออก"
- Phase B ของการ์ดนี้ผูกกับงานมาตรฐาน entrance / exit ของทุก layout ซึ่งยังไม่มีการ์ดของตัวเอง

## สาเหตุ

พบสามจุดที่ทับกัน

### จุดที่ 1 — เป้าหมายการเดินถูกตั้งให้ "หยุดก่อนกำแพง" โดยตั้งใจ

`SceneTransitionManager.ResolveExitWalkTarget` (บรรทัด ~579) เมื่อเจอ `boundary.ExitWall` จะคำนวณเป้าหมายเป็น `RoomBoundaryMath.ResolveStopBeforeWallX(...)` แล้ว return ทันที ค่า `ExitWalkWallSkin` เดิมคือ `0.08f` แปลว่าเป้าหมายคือชิดกำแพงเหลือช่องว่างแค่ 0.08 หน่วย ซึ่งสายตามองว่าติดกำแพง

branch ที่ตั้งใจให้ "เดินเลยประตูออกไป 2.0 หน่วย" (`ExitWalkPastDistance`) อยู่ถัดลงมา แต่ทำงานเฉพาะตอน `exitWall == null` ซึ่งในห้องที่ setup ถูกต้องจะไม่เกิดเลย จึงเป็น dead branch

### จุดที่ 2 — `PrepareForExit()` ไม่มีคนเรียกแล้ว

`SceneBoundaryController.PrepareForExit` (บรรทัด 207) คือเมธอดที่ออกแบบไว้ขยายขอบกล้องและเลื่อนกำแพงตอนออกห้อง ปัจจุบันไม่มี call site เหลืออยู่เลย doc comment ของมันระบุเหตุผลไว้เอง: "The card-leave walk no longer calls this; it stops before the current ExitWall instead of walking past it"

### จุดที่ 3 — fade เริ่มหลังเดินจบ (ตัวที่ทำให้เห็นชัดที่สุด)

ใน `RunTransitionRoutine` ลำดับเดิมเป็น serial ล้วน คือ `yield return StartCoroutine(PlayExitWalkRoutine())` จนเดินเสร็จ **แล้วค่อย** `FadeOverlayRoutine(0f, 1f)` และ `PlayerExitState.CompleteExit()` จะเข้า `HoldAtTarget()` ที่ตรึง velocity เป็นศูนย์ค้างไว้

ผลรวมคือ เดินไปชนกำแพง → หยุดนิ่งชิดกำแพง → ยืนค้างอยู่อย่างนั้นตลอดช่วง fade

## แนวทางแก้

แบ่งเป็นสองเฟส ตามที่ project owner เลือก (ทาง C)

### Phase A — ทำแล้วในการ์ดนี้

แก้เฉพาะ `Assets/Project/Scripts/Core/ZoneState/SceneTransitionManager.cs`

1. ให้ fade ทำงานคู่ขนานกับการเดิน แทนที่จะรอให้เดินจบก่อน

```csharp
Coroutine exitFade = StartCoroutine(FadeOverlayRoutine(0f, 1f));
yield return StartCoroutine(PlayExitWalkRoutine());
if (WasExitWalkAbortedByDeath())
{
    StopCoroutine(exitFade);
    ...
    yield break;
}

yield return exitFade;
SetFadeAlpha(1f);
```

2. ปรับ `ExitWalkWallSkin` จาก `0.08f` เป็น `0.6f` ให้หยุดห่างกำแพงพอที่ท่าทางจะอ่านว่า "รอออก" ไม่ใช่ "ติด collider"

**เหตุผลที่ปลอดภัย:** กรณีผู้เล่นตายระหว่างเดิน `finally` block เดิม (บรรทัด ~475) มี safety net อยู่แล้ว คือถ้า `!reachedDestination` จะ `FadeOverlayRoutine(GetFadeAlpha(), 0f)` เพื่อ fade กลับจาก alpha ปัจจุบัน ดังนั้นการเริ่ม fade ก่อนจะไม่ทำให้จอค้างดำ

**ข้อจำกัดที่ยอมรับ:** ตัวละครยังเดินชนกำแพงจริง แค่ผู้เล่นไม่ทันเห็นเพราะจอมืดก่อน ถ้าปรับ `fadeDuration` ให้สั้นกว่าระยะเวลาเดินมาก อาการเดิมจะโผล่กลับมา

### Phase B — ยังไม่ทำ รอทำพร้อมงานมาตรฐาน layout

เปิดกำแพงแล้วให้เดินผ่านออกไปจริง ตามที่ project owner เสนอไว้แต่แรก กลไกมีอยู่ครบแล้วไม่ต้องเขียนใหม่

- `SceneBoundaryController.OpenDivider(collider)` (บรรทัด 149) ทำ `enabled = true; isTrigger = true;` อยู่แล้ว ปัจจุบันใช้กับ divider ของ multi-chamber
- `PrepareForExit(target)` ขยาย camera bounds ให้ตามไปด้วย

ลำดับที่ต้องการ: เลือกการ์ด → `OpenDivider(boundary.ExitWall)` → `PrepareForExit(target)` → เป้าหมายการเดิน = ขอบ trigger + `ExitWalkPastDistance` → fade

**สิ่งที่ต้องระวังใน Phase B**

1. `PrepareForExit` เรียก `AlignExitWall` ซึ่งเลื่อนตำแหน่งกำแพง จะพัง multi-chamber layout ต้องใส่ guard `_usesAuthoredStageExitChild` แบบเดียวกับที่ `SetExitAvailable` มีอยู่แล้ว (บรรทัด ~240)
2. ต้องมีพื้นให้เดินเลยกำแพงออกไป ถ้า layout ไหนไม่ได้ author พื้นเผื่อไว้ ผู้เล่นจะตกลงไปตอนออกห้อง ต้องไล่เช็คทุก prefab ใน `Assets/Project/Resources/RoomLayouts/`:
   - `RoomLayout_Combat_01` ถึง `_05`
   - `RoomLayout_Level_01`
   - `RoomLayout_Rest_01`
   - `RoomLayout_Boss_Executioner`

ข้อ 2 เป็นงาน authoring ที่ agent ทำแทนไม่ได้ และตรงกับ feedback อีกข้อของ project owner เรื่อง "แมพยังไม่มีมาตรฐาน กำแพง entrance exit แต่ละด่านเท่ากันไหม" จึงควรทำพร้อมกันทีเดียว

## Acceptance Criteria

### Phase A

- [x] fade เริ่มพร้อมกับการเดินออก ไม่ใช่หลังเดินจบ
- [x] ผู้เล่นตายระหว่างเดินออก แล้วจอไม่ค้างดำ
- [ ] Playtest: กดการ์ดออกจากห้อง combat แล้วไม่เห็นตัวละครยืนชิดกำแพง
- [ ] Playtest: ออกจาก Shop / Level / Rest ได้ปกติ ไม่ hard-cut
- [ ] Playtest: ออกจากห้อง multi-chamber (`CombatStageController`) ได้ปกติ

### Phase B (ยังไม่เริ่ม)

- [ ] กำแพง exit ถูกเปิดเป็น trigger ตอนเริ่มเดินออก
- [ ] camera bounds ขยายตามจนผู้เล่นไม่หลุดขอบจอ
- [ ] ทุก layout prefab มีพื้นรองรับเลยกำแพง exit ออกไปอย่างน้อยเท่ากับ `ExitWalkPastDistance`
- [ ] multi-chamber layout ไม่โดนเลื่อนกำแพงที่ author ไว้

## Human Checkpoints

- [ ] Playtest Phase A (game feel: จังหวะ fade เทียบกับระยะเดิน)
- [ ] ตัดสินใจว่าจะทำ Phase B พร้อมงานมาตรฐาน layout หรือแยก
- [ ] Layout authoring: เพิ่มพื้นเลยกำแพง exit ทุก prefab (มนุษย์เท่านั้น)
- [ ] PR review ก่อน merge

## Router Decision

Workflow ที่แนะนำ: `/fix-bug`
