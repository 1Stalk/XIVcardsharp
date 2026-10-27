# AI Usage Declaration

**Level: Pair**

Hi! o/ Here's a breakdown of how XIVcardsharp was built and where AI was involved.

### My part:
- The whole concept — I wanted a solver for Open Tournaments so I'd stop overthinking every draft pick and card placement while grinding for cards and the umbrella
- I did some reverse engineering: poking around `AddonTripleTriad` node hierarchies with Dalamud tools, figuring out how the board state is laid out, discovering that Chaos-locked cards have their node color dimmed (`MultiplyRed < 100`)
- Working out how to fire card placements via `Callback.Fire(..., 14, ...)` without freezing the game or desyncing the UI
- Had a general idea of what the solver should prioritize (corners, walls, saving rares) but didn't design the actual scoring
- Testing everything in live Open Tournaments, Battlehall, and friendly duels; found and fixed edge cases like Sudden Death restarts and the turn banner blocking input too early

### What AI did (roughly equal collaboration):
- Wrote the C# implementation alongside me — solver loop, game simulation, data models, draft evaluator. I'd describe what I needed and we'd go back and forth on it
- Designed and tuned all the scoring heuristics — positional safety, corner/wall priorities, draft synergy weights, rare card conservation
- Parallelized the Monte Carlo sampling (`Parallel.ForEach` with `ThreadLocal<TriadGameSolver>`) so it finishes in milliseconds without dropping frames
- Generated unit test scaffolding and sample boards for verifying rules (Order, Chaos, draft rarity swaps)

I understand how every part of this code works and can explain any of it. I test and maintain this myself — if something breaks after a patch, ping me or open an issue!
