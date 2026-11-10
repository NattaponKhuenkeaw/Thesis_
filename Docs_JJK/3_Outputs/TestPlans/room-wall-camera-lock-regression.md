---
slug: room-wall-camera-lock
status: draft
gdd_tags:
  - scene-roles
owner: อาย
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Regression: Room Wall + Camera Lock

อายเทสชิ้นนี้ หลังมีกำแพงในเลย์เอาต์ (เจ้าของห้องวางใน Editor)

## Cases

### A. ยังไม่เคลียร์

- [ ] เดินไปทางออกแล้ว **ติดกำแพง** ออกไม่ได้
- [ ] กล้องไม่ไหลไปข้างนอกห้อง
- [ ] ยืนชิดกำแพงแล้วผู้เล่นอยู่ **ขอบจอ** ไม่ใช่กลางจอพร้อมฉากว่างข้างนอก
- [ ] Enter Combat01 from the lobby at 16:9 and 21:9. The player stays visible as the fade reveals the intro walk, before attacking and while the first gate is closed.

### B. เคลียร์แล้ว

- [ ] กำแพงขวาเปิด / เดินเข้าโซนทางออกได้
- [ ] Choice UI ยังขึ้นเมื่อยืนที่ trigger
- [ ] Engage the first enemy, then clear the chamber. Combat framing takes over on engagement and Explore returns to its authored zoom once the expanded bounds can fit it.

### C. ห้องถัดไป

- [ ] เข้าห้องใหม่แล้วขอบกล้องตามห้องใหม่ ไม่ค้างความกว้างห้องเก่า
