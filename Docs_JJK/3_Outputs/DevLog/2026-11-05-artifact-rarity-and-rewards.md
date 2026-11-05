# Artifact rarity and room rewards

Implemented the approved rarity, shop pacing, Hard reward patterns and TeamBuilder copy actions. The ten R definitions add one fixed stat bonus and use the lower roll profile. Eight SR definitions implement the attached brief, while the nine existing definitions retain their identity as SSR. Shop selection reads authored phase weights.

Hard routes now carry total Soul and pick count together. The final encounter owns the reward queue, and each picker commits the displayed instance once before the room becomes accessible. The existing serialized zone provider hosts the clearing phase during migration, while camera and music can still read room presentation data.

Unity MCP authored and saved the reward picker, copy menu, discard confirmation and updated inventory tile. Runtime code binds the saved hierarchy and drives motion. Existing pixel frames and badges are reused. The R definitions currently reuse available Artifact art.

The SR duration and scope defaults selected during implementation are a 6-second Scales field with a 20-second cooldown, a 10-second Chrono mark with a 35% stored-damage explosion plus the existing Bleed ratio, and a 5-second single-target Crest field. SR Shroud uses the values in the brief, which match the current SSR passive. These are starting balance values.

The final focused suite passed 44 of 44 checks. The saved UI was rendered and inspected at 16∶9 and 4∶3. Visual verification found and corrected the missing rarity defaults in the inventory's fallback roll table. The broader run passed 77 of 87 checks. See [the validation plan](../TestPlans/artifact-rarity-and-rewards-test-plan.md) for the remaining diagnostic and VFX failures and the playtest script.

Save inspection confirmed JSON text in PlayerPrefs for boss-lobby checkpoints and disabled ordinary cross-session run inventory writes. The second-device observation remains unverified.
