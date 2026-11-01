# Parkour game — portfolio prototype

A story-driven parkour game made in Unity. The playable journey moves through dark memory fragments, a volcano escape, water and an island, and a return toward a hospital awakening. Movement, lighting, and color carry much of the story.

## Play the current review build

Watch the [71.70-second silent v580 gameplay excerpt](https://github.com/RayzerCat76/parkour-game-portfolio/releases/download/portfolio-2026-11-01/parkour-v580-manual-gameplay-silent.mp4). It follows the dark opening, rooftop jumps, volcano route, water and island, recovered streets, and hospital ending. The footage is edited from Ray's complete 1 November manual recording; the cuts skip travel between those moments.

Download the macOS build from [Releases](https://github.com/RayzerCat76/parkour-game-portfolio/releases). Open `Return.app`, then press **Enter** to begin. Use **A/D** to move, **Space** to jump, **E** to interact, and **Tab** to inspect memories. The source project is under `UnityProject/` and targets Unity 6000.4.11f1.

The available build is **v580, a review candidate**. Ray confirms a complete start-to-ending manual recording of this build on 1 November 2026; the silent edited excerpt is linked above and attached to the release. In a separate automated check on 1 November 2026, the build reached the Fragments chapter but failed to verify movement during the fourth voice route; the process then ended with a native double fault. Later chapters were not assessed in that automated run. The later v591 walking revision built but failed a street-turn review, so v580 remains the usable build.

The source snapshot in this repository was copied from the latest local development worktree after that v591 experiment. It contains newer experimental code than the v580 downloadable build. It should be read as development source, not as an exact source match for the v580 binary or a final accepted version.

See [PORTFOLIO.md](PORTFOLIO.md) for the initial design, changes, and current scope. See [CREDITS.md](CREDITS.md) for collaborators, GPT assistance, macOS synthetic voices, and third-party material.

## Where to look

- [Concept sheets with a reading guide](docs/initial-design/README.md) → [development account](PORTFOLIO.md) → [v580 release](https://github.com/RayzerCat76/parkour-game-portfolio/releases/tag/portfolio-2026-11-01).
- [Build provenance and source limitation](docs/BUILD_PROVENANCE.md): the v580 binary and the newer public source snapshot are different revisions.
- [Unity scene](UnityProject/Assets/ReturnRedesign/Return.unity), [game flow](UnityProject/Assets/ReturnRedesign/Runtime/ReturnGame.cs), [movement](UnityProject/Assets/ReturnRedesign/Runtime/ReturnMotor.cs), [3D journey](UnityProject/Assets/ReturnRedesign/Runtime/ReturnJourney3D.cs), [ending](UnityProject/Assets/ReturnRedesign/Runtime/ReturnDoorReunion.cs), and [review checks](UnityProject/Assets/ReturnRedesign/Runtime/ReturnAcceptance.cs).
