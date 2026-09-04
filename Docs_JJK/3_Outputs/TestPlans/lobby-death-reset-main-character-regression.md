---
slug: lobby-death-reset-main-character
status: draft
gdd_tags:
  - core-loop
  - characters
owner: อาย
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Regression: Lobby Death Reset To Main Character

อายเทสชิ้นนี้. งานแก้โค้ดเสร็จแล้ว ไม่ต้องแก้ต่อ.

หลังวipes คนที่เดินต้องเป็น **CharacterObject_main** เสมอ ทีมที่เหลือได้เฉพาะตัวที่มี CharacterItem จริง ห้ามมีตัวผีที่อยู่ในช่องทีมแต่ไม่มี Own Item

## Setup

1. Lobby จัดทีมที่มี **CharacterObject_main** (ชื่อบนจอ: Main) เป็นตัวนำ
2. ตัวอื่นในทีมต้องมี CharacterItem ใน `PlayerRunInventory.ownedItems`
3. จำหน้าตา Main ไว้
4. เริ่มรัน

## Cases

### A. ตายตอนคุมตัวหลัก

- [ ] กลับ Lobby เป็นตัวหลัก
- [ ] ทีมที่ยังมี CharacterItem ตอนออกจาก Lobby กลับมาได้ โดย Main อยู่ช่องนำ
- [ ] ตัวที่ไม่มี CharacterItem ต้องไม่โผล่ในทีม
- [ ] HP เต็ม, AE ว่าง
- [ ] กดเข้าดันเจี้ยนต่อได้

### B. สลับไปตัว 2 แล้ววipes ทั้งทีม

วิธี: สลับไปตัว 2 → ปล่อยให้ตัว 2 ตาย (สลับไปตัวที่เหลือ) → ปล่อยให้วipes ทั้งทีม

- [ ] กลับ Lobby เป็น **CharacterObject_main** ไม่ใช่ตัวที่ตายคนสุดท้าย และไม่ใช่แค่ช่อง 0
- [ ] ตัวที่โดน KO กลางรันกลับมาได้ถ้ายังมี CharacterItem (ไม่ใช่ CharacterObject ลอยๆ)
- [ ] Inspector / PlayerRunInventory: ทุกตัวในทีมมี CharacterItem คู่กัน
- [ ] HUD / โมเดล / ไฮไลต์ทีม ตรงกับตัวหลัก

### C. สลับไปตัว 3 แล้ววipes

- [ ] เหมือนเคส B แต่เริ่มจากตัว 3

### D. อย่าพังของเดิม

- [ ] กลางรัน ถ้ายังมีเพื่อนร่วมทีม ตายแล้วยังสลับไปตัวที่เหลือได้ (ยังไม่เด้ง Lobby)
- [ ] ที่ Lobby สลับตัวเองได้ตามปกติ ก่อนเริ่มรันใหม่

### E. ตัวผี

- [ ] ถ้าใส่ CharacterObject ลงทีมโดยไม่มี CharacterItem แล้วเริ่มรันจนวipes ตัวนั้นต้องหายจากทีม
- [ ] ตัวที่ได้ระหว่างรัน (ไม่ได้อยู่ในทีมตอนออกจาก Lobby) ต้องไม่ติดไปรันถัดไป

## Fail if

- เกิดใหม่เป็นตัวที่ตายคนสุดท้าย หรือเป็นช่อง 0 ที่ไม่ใช่ Main
- ทีมมี CharacterObject แต่ ownedItems ไม่มี CharacterItem คู่กัน
- โมเดลกับ HUD คนละตัว

Console หลังกลับ Lobby ควรมี: `[TeamLoader] Death → Lobby restored CharacterObject_main: Main`
และต้องไม่มี error `Refusing to load slot 0 as a fallback`
