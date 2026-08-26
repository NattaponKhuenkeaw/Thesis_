# Enemy Kit

## Goal

Split an enemy into two authoring layers so adding a new enemy type does not mean touching the Behavior Graph or duplicating tuning across prefabs.

- **Prefab (`EnemyController`)** — body: HP, colliders, sprites, muzzle Transform, hit reactions.
- **Kit (`EnemyKit` asset)** — how it fights: combat role, attack range, ranged shot values.

A prefab with no kit keeps working exactly as before; its own fields are used.

## Create a kit

1. `Assets → Create → Project → Enemies → Enemy Kit`
2. Name it after the behaviour, e.g. `EnemyKit_MeleeInterruptible`, `EnemyKit_MeleeCommit`, `EnemyKit_RangedStraight`
3. Fill in:

| Field | Meaning |
| --- | --- |
| Combat Role | `InterruptibleMelee` / `CommitMelee` / `RangedHold` |
| Attack Range | `> 0` overrides the Behavior Graph range. Ranged also uses it as laser length and max shot reach |
| Shot Speed | Bullet travel speed |
| Shot Lifetime | Seconds the bullet lives. Travel time is auto-extended when range is longer |
| Shot Sprite / Tint | Optional bullet art. Empty = default square |
| Shot Radius | Bullet thickness and beam tolerance for the line-of-fire test |
| Muzzle Offset | Fallback origin when the prefab has no `RangedMuzzle` child. X flips with facing |
| Line Of Fire Mask | Layers that block a straight shot |

4. On the enemy prefab, drop the asset into `EnemyController → Combat Kit → Kit`

## Inspector behaviour

`EnemyControllerEditor` hides fields the current setup cannot use:

- Kit assigned → `Combat Role`, `Attack Range Override`, and all ranged shot values are hidden (the kit owns them). `Ranged Muzzle` stays because it is a prefab Transform.
- No kit, role is melee → the whole `Ranged Shot` block is hidden.
- No kit, role is `RangedHold` → the `Ranged Shot` block is shown.

## Adding a new enemy type

1. Duplicate the closest prefab for the body (sprite, colliders, HP).
2. Create a new kit asset for the fighting values.
3. Add it to the room `Encounter` enemy pool.

No new Behavior Graph, no new script.

## Notes

- The kit only overrides values at read time; nothing is copied onto the prefab, so clearing the kit restores the prefab's own numbers.
- `RangedMuzzle` is still authored per prefab. If it is empty, the controller looks for a child named `RangedMuzzle`, then falls back to `Muzzle Offset`.
- Covered by `Assets/Project/Scripts/Editor/Tests/EnemyKitTests.cs`.
