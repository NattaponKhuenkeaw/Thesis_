---
slug: kit-information
status: approved
source: manual
gdd_tags: [ui, characters, progression]
owner: implementation-agent
human_checkpoint: required
next_agent: implementation-agent
blocked_by: []
---

# Kit Information Design

The user approved the ownership and nested layout proposal with "lgtm" on 2026-11-03.

Following the user's 2026-11-03 layout revisions, show sections in this order: Skill, Ultimate, Passive, Special, QTE. Ordinary Normal Attack entries are excluded, including explicitly authored ones. QTE reuse of a Normal action and Normal-input standalone actions remain eligible. A section with one visible action is flat; sections with multiple visible actions use nested cards. Each action card uses the existing inspector artwork and typography. Sections are initially expanded; existing scrolling handles long kits. No new animation or combat interaction is introduced.

An action card explains its name and authored effect copy. Authored activation copy may appear when supplied; the builder generates no activation or conditional-upgrade sentence. Conditional modifiers never inflate unconditional values. The builder also injects no damage-scaling sentence or standalone action-scaling label. Cooldown and AE cost appear in header tags when applicable. Tier zero remains a usable base kit. QTE and Special do not gain fictional Potential tracks. Hide actions until their permanent Potential requirements are satisfied, and hide permanently replaced actions. A learned action stays visible while waiting for transient combat conditions. Direct PotentialLevelActionCondition requirements are read from gameplay data; effect-granted actions can author an additional presentation-only minimum level/track.

The primary Skill and Ultimate retain their existing authored copy through targeted migration. Secondary actions never inherit the primary's whole-track description. A QTE can reference an existing ordinary action through a separate entry.

Numbers exposed as bindings read execution data and shared modifier evaluation. Do not infer total damage from the first hit. Existing effect-specific narrative values require normal content authoring; they are not automatically inferred.

GDD alignment: `@tag: ui` calls for Basic/Skill/Ultimate/Passive information with Potential-dependent values. QTE/Special and multiple cards extend that approved reference view. The main clarity goal is to distinguish how each action is activated and which upgrade changes it.

## Root Levels and Header Tags

Categories with one visible entry render a flat action card without an enclosing category box or child indentation. Categories with multiple visible entries retain the enclosing box and nested cards. This count follows Potential filtering, so learning another action can expand a flat category into a nested group. Category titles and root Potential levels remain visible in both layouts.

Skill, Ultimate, and Passive root headers display the current Lv.0 through Lv.4 beside the section title. Children do not repeat the track level. A cross-track unlock never changes the root's own level: Main's Judgment Cut appears at Passive 4 even if Skill is Lv.0. QTE/Special have no fabricated level.

Each action header places compact context and cooldown badges below its title, with code-drawn symbols and text. Ground, Airborne, and Both are explicit. Ultimate AE cost uses the same badge row. Generated context/cooldown text is removed from the body/metadata; authored activation/follow-up explanations remain in the body.

The supplied reference image informs the compact title/tag hierarchy; the existing project art and fonts remain in use.
