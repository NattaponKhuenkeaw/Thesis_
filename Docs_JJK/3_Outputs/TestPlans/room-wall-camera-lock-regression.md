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
- [ ] Enter Combat08 in Biome 2 at 16:9 and 21:9. Check the first visible frame, then let an enemy engage. The floor and player stay in frame during the Explore-to-Combat blend.
- [ ] Explore stays at its authored 8.5 lens through entry. Combat retains its authored 8.3–9.5 group range. Narrow rooms use independent axis confinement and can show beyond the gate. A character swap preserves CamLookAhead as Explore's follow target and the manager's player reference.
- [ ] Enter Boss_02 from Shop in Biome 2. The boss stays unspawned and the arena gate stays open through loading and the entrance walk. Walk past the arena gate to start the boss reveal, then verify the gate closes behind the player.

### B. เคลียร์แล้ว

- [ ] กำแพงขวาเปิด / เดินเข้าโซนทางออกได้
- [ ] Choice UI ยังขึ้นเมื่อยืนที่ trigger
- [ ] Engage the first enemy, then clear the chamber. Combat framing takes over on engagement and Explore returns to its authored zoom once the expanded bounds can fit it.

### C. ห้องถัดไป

- [ ] เข้าห้องใหม่แล้วขอบกล้องตามห้องใหม่ ไม่ค้างความกว้างห้องเก่า
