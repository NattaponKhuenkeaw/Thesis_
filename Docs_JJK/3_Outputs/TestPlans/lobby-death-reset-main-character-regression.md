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

## Setup

1. Lobby จัดทีม 3 ตัว ต้องมี **CharacterObject_main** (ชื่อบนจอ: Main)
2. จำหน้าตา Main ไว้
3. เริ่มรัน

## Cases

### A. ตายตอนคุมตัวหลัก

- [ ] กลับ Lobby เป็นตัวหลัก
- [ ] ทีมยังครบ 3 ตัวตามที่ออกจาก Lobby
- [ ] HP เต็ม, AE ว่าง
- [ ] กดเข้าดันเจี้ยนต่อได้

### B. สลับไปตัว 2 แล้ววipes ทั้งทีม

วิธี: สลับไปตัว 2 → ปล่อยให้ตัว 2 ตาย (สลับไปตัวที่เหลือ) → ปล่อยให้วipes ทั้งทีม

- [ ] กลับ Lobby เป็น **CharacterObject_main** ไม่ใช่ตัวที่ตายคนสุดท้าย
- [ ] ทีมกลับมาครบ 3 ตัว (ตัวที่โดน KO กลางรันต้องกลับมา)
- [ ] HUD / โมเดล / ไฮไลต์ทีม ตรงกับตัวหลัก

### C. สลับไปตัว 3 แล้ววipes

- [ ] เหมือนเคส B แต่เริ่มจากตัว 3

### D. อย่าพังของเดิม

- [ ] กลางรัน ถ้ายังมีเพื่อนร่วมทีม ตายแล้วยังสลับไปตัวที่เหลือได้ (ยังไม่เด้ง Lobby)
- [ ] ที่ Lobby สลับตัวเองได้ตามปกติ ก่อนเริ่มรันใหม่

## Fail if

- เกิดใหม่เป็นตัวที่ตายคนสุดท้าย
- ทีมเหลือไม่ครบ
- โมเดลกับ HUD คนละตัว

Console หลังกลับ Lobby ควรมี: `[TeamLoader] Death → Lobby restored CharacterObject_main: Main`
