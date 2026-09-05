---
slug: death-run-teardown-inventory-integrity
status: draft
source: manual
gdd_tags:
  - core-loop
  - characters
owner: agent
human_checkpoint: architecture-approval
next_agent: human
blocked_by: []
---

# Task Card: ล้าง Run ตอนตาย / ความถูกต้องของ Inventory

## เป้าหมายฝั่งผู้เล่น

เมื่อ run จบเพราะทีมตายยกทีม การตายต้องมีราคาจริง ตัวละครที่ตายต้องไม่กลับมาด้วยเลือดเต็ม และของที่เก็บระหว่าง run ต้องไม่ติดไปกับ run ถัดไป

## ที่มา

- Origin: manual prompt (developer feedback pass ก่อน balancing)
- Requested by: project owner
- อาการที่รายงาน: "character main ตาย playerOwnInventory ถูกลบแต่ถูกสร้างใหม่ทันที ทำให้ไอ้ตัวนี้ตายก็กลับมาเล่นใหม่เลือดเต็ม"
- อาการที่เกี่ยวข้อง: "on RunEnd item ใน playerOwnInventory ไม่ถูกลบ คือรีรันใหม่แต่ตัวละคร / artifact ที่เคยมีอยู่ครบหมด"

## GDD Reference

ตรวจกับ `Docs_JJK/1_Inputs_Templates/GDD_Neo-JJK.md` (อัปเดตล่าสุด 2026-08-18) แล้ว

- `@tag:core-loop` — run ล้มเหลว แล้วเสีย progress ที่เป็น run-scoped ก่อนกลับ hub
- `@tag:characters` — HP และ team slot ระหว่าง run เป็น state ของ run ไม่ใช่ state ถาวร

หัวข้อ **Rogue-Lite Progression → Utility Resource** ระบุกติกาไว้ครบ

| Entity | Rogue-lite Reset |
| --- | --- |
| Currency | Lose |
| Character | Lose but upgraded are retain |
| Artifact | Lose |
| Run Global Level | Lose; reset to Lv.1 |
| Fragment | Retain |
| Items | Lose |

สอดคล้องกับหัวข้อ Run Global Level ("Losing or resetting the run resets Run Global Level to Lv.1") และ roadmap Sprint 8 ("Soul Fragments (retained on death) + in-run currency (lost on death)")

**หมายเหตุสำคัญ:** GDD หัวข้อ Current Implementation Replacements and Gaps บันทึกไว้เองว่า "Finding Artifacts during Cursed Realm runs and losing them on run failure — Not implemented by this branch" แปลว่างานนี้คือการ implement ตาม design ที่เขียนไว้แล้ว ไม่ใช่การเปลี่ยน design

## ประเภทงาน

- [x] Bug fix
- [x] Refactor (ownership + state model)

## ความสัมพันธ์กับการ์ดอื่น

ต่อยอดจาก `lobby-death-reset-main-character-task-card.md` (status `implemented-pending-playtest`) การ์ดนั้นแก้ว่า **ตัวไหน** โผล่ที่ Lobby หลังทีมตายยกทีม การ์ดนี้แก้ว่าตัวนั้นและ inventory **กลับมาในสภาพไหน** โดย Fix C ต้องไม่ทำการ์ดเดิมพัง

## สาเหตุ

ไม่ใช่บั๊กจุดเดียว แต่เป็นกลไก 4 ชั้นที่ต่างคนต่างตีความ "ไม่มีข้อมูล" ว่าเท่ากับ "สุขภาพดี" แล้วทับกัน

### ชั้นที่ 1 — `runCharacters.Clear()` อ่านกลับมาเป็นเลือดเต็ม

`PlayerRunInventory.ResetRunProgressForDeath` ล้าง `runCharacters` ทิ้งทั้งลิสต์ แล้ว `TeamLoader` ตีความว่า "ไม่มี `RunCharacterState`" เท่ากับเลือดเต็มและยังมีชีวิต

- `TeamLoader.GetCharacterCurrentHP` (~บรรทัด 1033): `state == null` → `return GetCharacterMaxHP(character)`
- `TeamLoader.IsCharacterAliveFromInventory` (~บรรทัด 1568): `state == null || state.currentHP < 0f` → ยังมีชีวิต

`RunCharacterState.currentHP` มีค่า default เป็น `-1f` ในฐานะ sentinel ที่แปลว่า "ยังไม่ init" (`PlayerRunInventory.cs:30`) ระบบจึงแยกไม่ออกระหว่าง **"ยังไม่เคยลงสนาม"** กับ **"ตายไปแล้วใน run นี้"** ทั้งสองอ่านออกมาเป็นเลือดเต็มเหมือนกัน

### ชั้นที่ 2 — inventory ซ่อมตัวเองทุกครั้งที่ถูกแตะ

ทุก mutation จบด้วย `PostInventoryMutation()` → `EnsurePlayableState()` (~บรรทัด 3144) ซึ่งเรียก `EnsureDefaultCharacterOwnership()` → `EnsureMainCharacterOwned()` ที่โหลด `Resources/ItemSO/char_0_Main` แล้วใส่กลับ ซึ่งภายในจะเรียก `GetOrCreateRunCharacterState()` ต่อ และถ้าทีมว่าง `FillTeamSlotsFromOwnedCharacters()` จะเอา Main กลับขึ้น slot ให้อีก

ผลคือ `ClearInventory()` ไม่สามารถทำให้ inventory ว่างได้แม้แต่เฟรมเดียว เพราะ Main ถูกใส่กลับภายในเมธอดเดียวกัน นี่คือ "ถูกลบแต่ถูกสร้างใหม่ทันที" ตรงตัว

### ชั้นที่ 3 — ตอนกลับ Lobby เซ็ตเลือดเต็มตรงๆ อีกรอบ

`TeamLoader.RestoreLobbyStateAfterDeath` (~บรรทัด 728) เขียน `SetCharacterCurrentHP(character, maxHp)` ให้ทุกตัวในทีม อันนี้ตั้งใจสำหรับการเข้า Lobby แต่ซ้ำซ้อนกับชั้น 1-2 จนไม่มีทางแยกได้ว่าเลือดเต็มเพราะ "ตั้งใจเตรียม run ใหม่" หรือเพราะ "state หายไปเงียบๆ"

### ชั้นที่ 4 — TeamLoader ปั้น inventory ปลอมขึ้นมาเองได้

`TeamLoader.ResolveDependencies` (~บรรทัด 1232)

```csharp
if (runInventory == null)
{
    runInventory = ScriptableObject.CreateInstance<PlayerRunInventory>();
    runInventory.hideFlags = HideFlags.DontSave;
}
```

ถ้า `Resources.Load` คืน null แม้แค่เฟรมเดียว `TeamLoader` จะผูกกับ inventory เปล่าใหม่เอี่ยมแบบเงียบๆ แล้ว

- ทุกตัวอ่านได้เลือดเต็ม ของหายหมด
- `ShouldUsePersistentStorage()` เทียบด้วย `persistentAsset == this` ตัวปลอมจึงได้ `false` แปลว่าไม่ save ไม่ load อะไรเลย HP ที่เขียนลงไปหายทั้งหมด
- `GameRunManager`, `RunEndController` และ `TeamLoader` ต่างคนต่างเรียก `Resources.Load` เอง จึงถือคนละ instance ได้

### สิ่งที่พบเพิ่มระหว่างไล่โค้ด

- **RunResultUI เป็น dead wiring ไม่ใช่ถูกลบ** component ยังอยู่บน `Assets/Project/Prefab/Core/Manager.prefab` (MonoBehaviour block `&3419202181910011112`) และยัง assign เข้า `RunEndController.runResultUI` (prefab บรรทัด 130) แต่ `RunResultUI.Show()` ไม่มี call site ที่ไหนเลย `RunEndController.DeathFlowRoutine` สร้าง `RunResultSnapshot` ขึ้นมาเพื่อคำนวณ fragment delta อย่างเดียวแล้วทิ้ง — project owner ยืนยันว่าหน้า run result อยู่นอก design แล้ว ให้ลบทิ้ง ไม่ใช่เปิดกลับมา
- **artifact ยังล้างไม่หมด** `StripNonCharacterRunLoot()` ลบ Artifact ออกจาก `ownedItems` และ `DeployTeam(..., artifactsBySlot: null)` ล้าง ref ที่สวมอยู่ทุก slot (ยืนยันที่ `PlayerRunInventory.cs:941-955`) แต่ pool `artifactInstances` ที่เก็บค่า roll จริงไม่เคยถูกแตะ มันถูกล้างเฉพาะใน `ClearInventory()` ซึ่ง `ResetRunProgressForDeath` ไม่ได้เรียก
- **ตัวละครถูกเก็บไว้โดยตั้งใจ ซึ่งขัด GDD** `StripNonCharacterRunLoot()` ข้าม `CharacterItem` ทุกตัว แล้ว `AddUnlockedRosterItems()` เติม roster ที่ปลดล็อกกลับทันที ขณะที่ GDD ระบุ Character = Lose นอกจากนี้ `AddUnlockedRosterItems` พึ่ง `metaProgression.IsCharacterUnlocked()` ซึ่งเป็น concept "ปลดล็อกตัวละครถาวร" ที่ **GDD ไม่มีเลย** ต้องการการตัดสินใจจากมนุษย์

## ขอบเขต

### ระบบที่กระทบ

- ownership และ lifecycle ของ run inventory
- การ resolve HP / สถานะเป็นตายของตัวละครใน run
- flow ตาย → Lobby
- การแสดงผล run result (ลบทิ้ง)

### ไฟล์ที่ต้องดูก่อน

- `Assets/Project/Scripts/Shop/PlayerRunInventory.cs`
- `Assets/Project/Scripts/Systems/TeamLoader.cs`
- `Assets/Project/Scripts/Core/RunEndController.cs`
- `Assets/Project/Scripts/Core/GameRunManager.cs`
- `Assets/Project/Prefab/Core/Manager.prefab`
- `Assets/Project/Scripts/Editor/Tests/DeathResetOwnershipTests.cs`

### นอกขอบเขต

- balancing บทลงโทษการตาย (เสีย currency เท่าไร เสีย artifact กี่ชิ้น)
- การเปลี่ยนระบบ meta progression / fragment economy
- การสลับตัวตอนเพื่อนร่วมทีมตายกลาง run (ทำงานถูกอยู่แล้ว)
- การเลือกตัวละครที่ spawn ใน Lobby (อยู่ในการ์ด `lobby-death-reset-main-character`)
- UI แจ้งการตายตัวใหม่มาแทน RunResult — project owner ปฏิเสธชัดเจนแล้ว

## แนวทางแก้

### Fix A — ให้ `PlayerRunInventory` มีเจ้าของคนเดียว (P0)

**A1.** ลบ fallback `ScriptableObject.CreateInstance<PlayerRunInventory>()` ออกจาก `TeamLoader.ResolveDependencies` (~บรรทัด 1232) ถ้าหาไม่เจอใน play mode ให้ `Debug.LogError` แล้ว return ออกโดยไม่ผูกอะไร การพังแบบเห็นชัดดีกว่าการเล่นต่อได้ด้วยข้อมูลผิด

**A2.** ให้ `GameRunManager` เป็นเจ้าของ มันเหมาะที่สุดเพราะมี `[DefaultExecutionOrder(-8500)]` และ `DontDestroyOnLoad` อยู่แล้ว (`GameRunManager.cs:70-96`) `Awake` ของมันจึงจบก่อน `TeamLoader.Awake` ทุก scene load และ `RunInventory` เป็น public property อยู่แล้ว (`GameRunManager.cs:67`)

ลำดับ resolve ใหม่ของทั้ง `TeamLoader` และ `RunEndController`

```csharp
runInventory = GameRunManager.Instance != null ? GameRunManager.Instance.RunInventory : null;
if (runInventory == null && Application.isPlaying)
    runInventory = Resources.Load<PlayerRunInventory>(RunInventoryResourcePath);
// ห้ามมี CreateInstance
```

**สิ่งที่ต้องระวัง**

- scene ทดสอบที่ไม่มี `Manager.prefab` (เช่นเปิด `Test_Playground` เดี่ยวๆ) จะไม่มี `GameRunManager.Instance` ต้องคง fallback `Resources.Load` ไว้
- ก่อนลบ ต้องเช็คว่ามี edit-mode test ตัวไหนสร้าง `TeamLoader` ลอยๆ แล้วพึ่ง fallback `CreateInstance` อยู่หรือไม่ ถ้ามีให้ย้ายไปหลัง `#if UNITY_INCLUDE_TESTS` แทนการลบ
- `DeathResetOwnershipTests` สร้าง inventory เองตรงๆ ไม่ผ่าน `TeamLoader` จึงไม่กระทบ

### Fix B — แยก "ตายแล้ว" ออกจาก "ยังไม่ init" (P0)

**B1.** เพิ่ม `public bool isDefeated;` ใน `RunCharacterState` (`PlayerRunInventory.cs:25-31`) และเพิ่มใน `SavedRunCharacterRecord` (บรรทัด 186-192) ให้สอดคล้องกัน แม้ตอนนี้ `PersistRunInventoryAcrossSessions` จะเป็น `false` ก็ตาม

**B2.** ติดธงที่ประตูเดียวที่ HP วิ่งผ่าน คือ `PlayerRunInventory.SetRunCharacterCurrentHP` (บรรทัด 1608)

```csharp
state.currentHP = Mathf.Max(0f, hp);
state.isDefeated = state.currentHP <= 0f;
```

เขียนแบบนี้ได้ทั้งติดธงและปลดธงในบรรทัดเดียว path ที่ฟื้นตัวละคร (`RestoreLobbyStateAfterDeath`, `HealTeamByMaxHpPercent`) วิ่งผ่าน `TeamLoader.SetCharacterCurrentHP` เข้ามาที่เมธอดนี้อยู่แล้ว จึงปลดธงให้อัตโนมัติ ไม่ต้องไล่แก้ทีละจุด

**B3.** อ่านธงก่อน fallback max HP ทุกที่ สามจุด

| ตำแหน่ง | บรรทัด | แก้เป็น |
| --- | --- | --- |
| `TeamLoader.GetCharacterCurrentHP` | ~1033 | `state.isDefeated` → `return 0f` |
| `TeamLoader.IsCharacterAliveFromInventory` | ~1568 | `state.isDefeated` → `return false` |
| `PlayerRunInventory.IsRunCharacterAlive` | ใช้ที่ `TryRemoveDefeatedTeamCharacter:1289` | ให้ตีความธงตรงกัน |

**สิ่งที่ต้องระวัง — มีสอง path ที่เขียน `currentHP` ตรงๆ ข้าม setter**

- `TeamLoader.ResetCharacterHP` (บรรทัด 1142-1151) เขียน `state.currentHP = -1f` ตรงๆ ต้องเซ็ต `isDefeated = false` ด้วย ไม่งั้นจะฟื้นไม่ขึ้นตลอดกาล
- `TeamLoader.GetCharacterCurrentHP` (~บรรทัด 1039-1048) มี side effect เขียน `state.currentHP = maxHp` เมื่อเจอค่าติดลบ ตัวที่ตายถือค่า `0` ไม่ใช่ค่าลบ จึงยังปลอดภัย แต่ควรใส่ guard `!state.isDefeated` กันเหนียว

### Fix C — ปิด auto-bootstrap ระหว่าง teardown (P1)

**C1.** เพิ่ม `[NonSerialized] private bool suppressAutoBootstrap;` แล้วครอบสองบรรทัดใน `EnsurePlayableState()` ด้วย `if (!suppressAutoBootstrap)`

- `EnsureDefaultCharacterOwnership();`
- `if (CountTeamCharacters() == 0) FillTeamSlotsFromOwnedCharacters();`

ปล่อยให้ `NormalizeRunCharacterStates()`, `NormalizeTeamSlots()` และ `EnsureTeamSlotCapacity()` ทำงานปกติ เพราะเป็นการทำความสะอาดข้อมูลที่มีอยู่ ไม่ใช่การสร้างของใหม่

**C2.** แยก `ResetRunProgressForDeath` (บรรทัด 818-840) เป็นสองเฟสชัดเจน

```
เฟส 1 — teardown (suppressAutoBootstrap = true ครอบด้วย try/finally)
    runCharacters.Clear()
    currency = DefaultCurrency
    _globalLevel = 1
    StripNonCharacterRunLoot()
    ลบ CharacterItem ทุกตัวยกเว้น Main            // ตาม GDD, รอ checkpoint
    artifactInstances.Clear(); artifactInstancesById = null;    // Fix D1
    -> ถึงตรงนี้ inventory ต้องว่างจริง (ตรวจได้ด้วย log / breakpoint)

เฟส 2 — rebuild lobby roster (ปลด suppression)
    EnsureMainCharacterOwned()
    DeployTeam(BuildOwnedDeathResetTeam(lobbyTeam), 0, null)
    SetActiveTeamSlotIndex(GetMainCharacterSlotIndex())
```

**สิ่งที่ต้องระวัง:** ขอบเขต suppression ต้องอยู่ใน `try/finally` และเป็นโค้ด synchronous ทั้งหมด ห้ามมี `yield` หรือ event callback คั่นกลาง ไม่งั้นระบบอื่นอาจเห็น inventory ในสภาพครึ่งๆ กลางๆ

### Fix D — ล้าง artifact pool และลบ RunResult (P1)

**D1 — ล้าง artifact pool** เพิ่มสองบรรทัดในเฟส 1 ตามที่ `ClearInventory()` ทำอยู่แล้วที่บรรทัด 794-795

```csharp
artifactInstances.Clear();
artifactInstancesById = null;
```

ไม่ต้องรีเซ็ต `nextArtifactAcquisitionOrder` เอง เพราะ `EnsureArtifactIndices()` คำนวณใหม่ให้ที่บรรทัด 3782

**D2 — ลบ RunResult** ตามที่ project owner ตัดสินใจ คือลบทิ้งเลย ไม่มี UI มาแทน

ลบไฟล์ (พร้อม `.meta`)

- `Assets/Project/Scripts/UI/RunResultUI.cs`
- `Assets/Project/Scripts/Core/RunResultSnapshot.cs`

ตรวจแล้วไม่มีไฟล์อื่นอ้างถึง

ตัดออกจาก `RunEndController.cs`

| สมาชิก | บรรทัด |
| --- | --- |
| field `runResultUI` | 24 |
| dict `fragmentBaseline` | 36 |
| `BuildSnapshot()` | 283-342 |
| `AppendFragmentRewardDeltas()` | 344-375 |
| `CaptureFragmentBaseline()` | 377-397 |
| `AddLostArtifactOnce()` / `GetItemId()` / `GetItemDisplayName()` / `BuildFragmentKey()` | 483-525 |
| บล็อก resolve `runResultUI` | 411-414 |
| การเรียก `CaptureFragmentBaseline();` ใน `ResetRunTracking()` | 97 |

`DeathFlowRoutine` จะเหลือเท่านี้

```csharp
ResolveDependencies();
metaProgression?.SavePersistentState();
runInventory?.ResetRunProgressForDeath(metaProgression);
TeamLoader teamLoader = TeamLoader.Instance ?? FindFirstObjectByType<TeamLoader>();
teamLoader?.ForceLobbyMainCharacter();
ReturnToLobby();
```

แล้วเก็บกวาด `using` ที่ไม่ได้ใช้ต่อ: `Project.UI` ตัดได้แน่นอน ส่วน `Project.InventorySystem.Items` น่าจะตัดได้ด้วยเพราะ `BaseItem` / `Artifact` / `Item` ถูกใช้แค่ใน `BuildSnapshot`

แก้ prefab `Assets/Project/Prefab/Core/Manager.prefab`

- ถอด component `RunResultUI` ออก (MonoBehaviour block `&3419202181910011112` บรรทัด 104-115)
- field `runResultUI:` บรรทัด 130 จะหายไปเอง

**ทำใน Unity Editor ด้วย Remove Component เท่านั้น ห้ามแก้ YAML ของ prefab ด้วยมือ** เพราะ YAML ที่ผิดรูปทำให้ prefab พังทั้งไฟล์

**ข้อจำกัดด้านลำดับ:** ต้องถอด component ออกจาก prefab **ก่อน** ลบไฟล์ `RunResultUI.cs` ไม่งั้น Unity จะเหลือ Missing Script ค้างบน `Manager.prefab` ให้ตามเก็บทีหลัง

## ลำดับการทำ

| ลำดับ | งาน | วิธีตรวจ |
| --- | --- | --- |
| 1 | Fix A | log `runInventory.GetInstanceID()` จากทั้งสามคลาส ต้องตรงกันตลอด ทั้งตอนเริ่ม run เปลี่ยนห้อง และกลับ lobby หลังตาย |
| 2 | Fix D2 (ลบ RunResult) | โปรเจกต์คอมไพล์ผ่าน ไม่มี Missing Script บน `Manager.prefab` |
| 3 | Fix B | ตัวละครที่ HP 0 อ่านค่าเป็นตาย ไม่ใช่เลือดเต็ม |
| 4 | Fix C + D1 | breakpoint ท้ายเฟส 1 เห็น inventory ว่างจริง |

ทำ D2 เป็นลำดับสองเพราะมันลดโค้ดใน `RunEndController` ลงเยอะ ทำให้ตอนดีบัก `DeathFlowRoutine` ในลำดับ 3-4 อ่านง่ายขึ้นมาก

## Acceptance Criteria

- [ ] `TeamLoader` ไม่สร้าง `PlayerRunInventory` instance ใน play mode อีก resolve ไม่สำเร็จให้ log error แทน
- [ ] `GameRunManager`, `TeamLoader` และ `RunEndController` อ้างถึง `PlayerRunInventory` instance เดียวกันตอน runtime (พิสูจน์ด้วย instance id log)
- [ ] ตัวละครที่ HP ถึง 0 อ่านจาก inventory ได้ว่าตาย จนกว่าจะมีการฟื้นอย่างชัดเจน ไม่ใช่เลือดเต็ม
- [ ] ระหว่าง teardown มองเห็นได้ว่า inventory ว่างจริงก่อนที่ lobby roster จะถูกสร้างใหม่
- [ ] artifact ที่ได้มาระหว่าง run หายไปใน run ถัดไป ทั้งใน `ownedItems` และ pool `artifactInstances`
- [ ] currency และ Run Global Level รีเซ็ตเป็นค่าเริ่มต้นใน run ถัดไป
- [ ] `RunResultUI` และ `RunResultSnapshot` ถูกลบหมด ไม่มี Missing Script บน `Manager.prefab` และไม่มี using ค้างใน `RunEndController`
- [ ] ตายกลาง run ตอนยังมีเพื่อนร่วมทีมรอด ยังสลับตัวได้และ run ไม่จบ (ไม่ regress การ์ด `lobby-death-reset-main-character`)

## เทสต์ที่ต้องเพิ่ม

เพิ่มใน `Assets/Project/Scripts/Editor/Tests/DeathResetOwnershipTests.cs`

- ตัวละครที่ถูกเซ็ต HP เป็น 0 ต้องไม่อ่านกลับมาเป็นยังมีชีวิต
- `artifactInstances` ต้องว่างหลัง `ResetRunProgressForDeath`
- Main ต้องยังอยู่ในทีมหลังเฟส 2 (กัน Fix C ไปทำ `lobby-death-reset-main-character` พัง)

Regression ที่ต้องเล่นจริง

- ตายกลาง run ตอนเพื่อนยังไม่ตาย ต้องสลับไปตัวที่รอดได้ปกติและ run ดำเนินต่อ เส้นนี้วิ่งผ่าน `TryBeginActiveDefeatRecovery` → `TryRemoveDefeatedTeamCharacter` → `IsRunCharacterAlive` ซึ่ง Fix B3 ไปแก้พอดี

## Human Checkpoints

- [ ] อนุมัติสถาปัตยกรรม: single-owner inventory + state model `isDefeated` (Fix A + B) ก่อนลงมือ
- [ ] อนุมัติการแก้ prefab: ถอด component `RunResultUI` ออกจาก `Manager.prefab`
- [ ] ตัดสินใจเรื่อง `AddUnlockedRosterItems` — GDD ระบุ Character = Lose แต่โค้ดเติม roster ที่ปลดล็อกกลับทุกครั้ง และ concept "ปลดล็อกตัวละครถาวร" ไม่มีใน GDD เลย ตัวเลือกคือ ย้ายไปหลัง debug flag / ลบทิ้ง / เก็บไว้แล้วไปอัป GDD ทีหลัง
- [ ] Playtest หลัง implement
- [ ] PR review ก่อน merge

## Router Decision

Workflow ที่แนะนำ: `/fix-bug`

ลำดับที่แนะนำ: A → B → C → D โดย A กับ B รวมกันแก้อาการที่รายงานได้ ส่วน C ทำให้ regression ในอนาคตวินิจฉัยได้ และ D ทับกับงาน RunEnd loot ใน Phase 1.2 จึง merge เป็น PR เดียวกันได้
