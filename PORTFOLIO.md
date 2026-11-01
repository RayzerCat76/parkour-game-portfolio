# Parkour game — design and development

## How it started

Ray Liu and Weiguo Tang discussed the original design and lore together. Four early handwritten sheets mapped a journey through darkness, a pressured volcanic escape, a slower water-and-island recovery of color, and a hospital ending. [View the archived concept sheets](docs/initial-design/README.md). The raw design conversation is kept privately and is not needed to play or inspect the code.

## From first design to prototype

The initial presentation was largely side-scrolling, with high-contrast black and white and collectible light/color. Iteration added four distinct parkour circuits, readable landing and recovery routes, changing lighting, and a more spatial 3D water/island and ending. The product goal is a playable emotional journey in which movement and color changes help a new player understand the story without an explanation beside the screen.

## Current stage

The downloadable v580 macOS review build starts and accepts movement input; the development record contains earlier complete-journey checks. A later v591 source/build experiment failed its street-turn movement review and was held. This portfolio copy therefore presents a working prototype and its development process, not a finished or externally accepted game. Movement feel, visual clarity, pacing, and a full fresh human playthrough still need review.

`UnityProject/` contains the latest local source snapshot as of 1 November 2026. Its newer experimental code is not identical to the v580 binary. The source includes Unity assets, packages, project settings, and the game-specific rebuild/build tools; generated caches and the private design audio are excluded.

## What I contributed

Ray directed implementation, prompts/specifications, iteration, playtesting, and decisions about which versions to retain. GPT/Codex generated substantial portions of code, visuals, audio, tooling, tests, and documentation under Ray's direction; Ray reviewed the outputs and supplied corrections. Weiguo Tang contributed to the original design and lore discussions and gave feedback. [Detailed credits](CREDITS.md) distinguish these roles and third-party source material.
