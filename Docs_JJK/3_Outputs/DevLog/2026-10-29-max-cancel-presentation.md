# MAX CANCEL motion and VFX continuity

Implemented the user's five-part pixel text sequence with GNF TTF, ease-out left entrance, a growing white aura, slow right drift and ease-out right exit. Captured 28 native camera frames and corrected the aura to scale around the text rather than empty layout space.

Changed spawned visual cleanup into a lifetime handoff through MAX CANCEL, ordinary interrupts, advanced steps and swaps/QTE. Procedural fields restore the actor immediately and finish their own frames; pooled visuals keep their existing coroutine and captured trajectory space, with generation checks for reused objects. QTE impact tails continue independently, while an aborted fall fades existing visuals without a false impact. Camera, movement, hitbox and gameplay ownership still follow the action lifecycle.

Native focused tests: **155/155 passed** at 23:17 Bangkok. Thirteen visual lifecycle/animation cases and two state-exit/MAX CANCEL integration cases added. Fixed one fragile exact float assertion in the prior QTE reward test. The broader suite's previously recorded seven failures remain unresolved. Final visual feel and overlap-density checks remain with the user.
