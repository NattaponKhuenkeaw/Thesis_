# Artifact VFX Polish — 2026-10-15

Owner requested particle/procedural VFX, a rounded 3D-looking Scales shield matching the supplied reference, improved Mark/blood attack visuals, and more Y reach. Owner clarified the actual damage area must grow and Urn must remain completely hidden. Continued the approved shared presentation/authority structure.

Scales now uses a procedural shaded sphere rim and projected hex facets, two animated depth arcs, up to 12 loop motes and 32 world-space attack shards. Chrono Mark has a rotating halo and local ember particles; its explosion combines a short core flash, soft expanding wave and up to 48 blood streaks. Exit allows bounded particle tails; disable/death/teardown clears them. Views use cached renderers and shared textures/materials; gameplay does not wait for VFX.

Added a vertical radius multiplier to the existing particle/explosion volume definitions. Starting value 2 gives X=3/Y=6 for Scales and Chrono; inspector range 1–5 allows tuning. Other content keeps multiplier 1 and its existing circle queries. Shared ellipse geometry projects candidate colliders against the curved area after the broad-phase box query; no new gameplay colliders. Damage/heal ratios, intervals, target filters/caps, Mark 10s and Claw 7s are retained.

Unity 6000.3.4f1 compilation succeeded. Final focused EditMode run: 84 passed / 0 failed / 0 skipped. Added geometry tests for tall targets, ellipse corners/outside and rotated bodies; actual Scales and Chrono damage sequences verify the extra reach. Verified X/Y VFX radii under scaled/flipped parents, bounded particles, orbit animation, reuse and cleanup. Inspected actual Unity phase renders. Updated advanced preview supports playback/scrubbing and zooms out for the tall attack area.

Final combat-zoom readability, airborne reach tuning and profiling remain human review. Starting value test: identify onset/Mark/attack in 8/10 demonstrations while silhouettes remain readable; lower opacity/count if intrusive, strengthen rims/shape if unclear. No PlayMode/build/profile result is claimed. Work is uncommitted; existing user changes and scenes are preserved.
