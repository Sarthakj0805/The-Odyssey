# The Odyssey: Project Notes (code walkthrough + theory)

These are study notes for explaining the project in an interview. They cover what the game does,
how every part of the code works, the theory behind each design choice, the bugs hit along the
way, and likely interview questions with short answers.

---

## 1. The pitch

**30 seconds.**
The Odyssey is a 2D side-scrolling action game in Unity 6 and C#, set on Homer's Island of the
Cyclops. The level is assembled at runtime from hand-drawn chunks. Between segments, an **AI
Director** looks at how the player just did and chooses the next segment's difficulty. The Director
is a small **PPO reinforcement-learning policy** trained with **Unity ML-Agents (PyTorch)** against
simulated players, exported to ONNX, and run inside the game. It is compared against a fixed
difficulty and a hand-written rule.

**One line (AI part).**
A PPO agent picks easy/medium/hard from 4 telemetry features; reward is +1 when the player loses
15–50% HP without dying, −1 per death, −0.5 if trivial; trained on simulated players at 3 skill
levels.

**Resume line → code.**

| Claim | Where it lives |
|---|---|
| 2D side-scroller inspired by the Odyssey | `RunManager`, `Level01` scene, opening/ending dialogue |
| State-machine combat | `FSM/StateMachine`, `Player/States/*`, `Enemies/States/EnemyStates` |
| Rogue-lite modifier system | `Modifiers/BoonDefinition`, `RunModifierRegistry`, `BoonOffer`, shrine UI |
| Generates levels | `LevelGen/*`: ASCII chunks assembled at runtime. Precisely: *assembles* levels from handcrafted chunks |
| RL-based AI Director tuning difficulty in real time | `Director/*`: decides at each segment boundary while you play |
| ML-Agents, PyTorch | `DirectorAgent` (ML-Agents C#), `ml/configs/director_ppo.yaml` trained by `mlagents-learn` (PyTorch) |

**Size:** about 58 C# files and about 4,000 lines (runtime, editor tool, tests), plus a PPO config,
a training script and a Python map validator.

---

## 2. How a run plays out

```
Main menu (choose Director: RL / Heuristic / Fixed)
  └─ Level01
       intro chunk + opening dialogue (game paused, typewriter text)
       ┌─ Director decides difficulty for segment N (from last segment's telemetry)
       │   LevelAssembler appends a chunk of that difficulty
       │   Player fights through it  →  TelemetryTracker records HP lost, time, deaths
       │   Shrine gate at the end:
       │       telemetry closed → shrine heals 15% → pick 1 of 3 boons
       └─ repeat for 3 segments
       ship chunk → touch the ship → ending dialogue
  └─ End screen (victory/defeat, time, kills, deaths, boons, which Director)
```

- **Death:** respawn at the start of the current segment with full HP. Telemetry counts the
  death. 3 deaths end the run (defeat).
- **First decision:** nothing has been played yet, so the Director gets a *neutral* observation
  (medium difficulty, 30% HP lost, on-par time, no death).
- **F1** shows the live Director readout (current difficulty, live HP lost, last segment, recent
  decisions). Every decision is also printed to the Console and appended to `telemetry.csv`.

---

## 3. Code map

```
Assets/Scripts/Runtime/
  FSM/         IState, StateBase, StateMachine
  Combat/      DamageInfo + IDamageable + Team, HealthModel (pure C#), Health (component), Hitbox, Projectile
  Player/      PlayerConfig (data), PlayerInputReader, PlayerController, States/ (7 state classes)
  Enemies/     EnemyConfig (data), EnemyController, States/EnemyStates (patrol, chase, melee/ranged attack, hurt, dead)
  Modifiers/   BoonDefinition (data), RunModifierRegistry + BoonOffer (pure C#)
  LevelGen/    ChunkDefinition + ChunkLibrary (data), ChunkLayout (ASCII parser) + ChunkSelector, LevelAssembler
  Level/       SegmentGate (shrine), Hazard (spikes), LevelExit (ship)
  Telemetry/   SegmentTelemetry (struct), TelemetryTracker (+ CSV log)
  Director/    IDirector, Difficulty, ChallengeBand (reward), DirectorObservation, FixedDirector, HeuristicDirector
    Sim/       RunSimulator + SkillProfile (simulated player), DirectorEvaluation
    ML/        DirectorAgent (ML-Agents Agent), RLDirector, DirectorTrainingArena, DirectorEvaluator
  Core/        RunManager (game loop), GameDatabase, GameSession, CameraFollow, ParallaxFollow, Layers, Placeholder
  UI/          UIFactory, Hud, BoonChoicePanel, PauseMenu, MainMenuController, EndScreenController
  Dialogue/    DialogueSequence (data), DialogueRunner (typewriter text box)
Assets/Scripts/Editor/   OdysseyBuilder (one click: layers, data assets, 5 scenes), DefaultChunks (generated maps)
Assets/Scripts/Tests/    5 NUnit edit-mode suites
ml/                      director_ppo.yaml, train_director.bat, export_tensorboard.py
tools/chunk_maps.py      chunk maps + reachability validator, regenerates DefaultChunks.cs
```

**Assemblies (asmdef).** The code compiles into three assemblies:
- `Odyssey.Runtime` references Input System, ML-Agents, Inference Engine and uGUI.
- `Odyssey.Editor` is editor-only.
- `Odyssey.Tests.EditMode` holds the NUnit tests.

Splitting the code into assemblies speeds up recompiles and stops runtime code from depending on
editor APIs.

---

## 4. Module by module

### 4.1 Finite state machine (`FSM/`)
- **`IState`:** `Enter`, `Tick(dt)` (from `Update`), `FixedTick(dt)` (from `FixedUpdate`, for
  physics) and `Exit`. `StateBase` adds `TimeInState`.
- **`StateMachine`:** stores transitions as *(from, to, condition)* entries, plus *any-state*
  transitions that can fire from any state.
- Each `Tick`, it checks the any-state transitions first, then the current state's transitions.
  The first condition that is true wins. It then calls `Exit` on the old state and `Enter` on the
  new one, and ticks the new state.
- **Why this design:** states are plain C# classes, and every transition is declared in one place
  (`BuildStateMachine()` in the player and enemy controllers). States never switch themselves, so
  behaviour is easy to read, debug and unit-test. Any-state transitions model interrupts: getting
  hit or dying can happen in any state, and they take priority.

**Theory: FSM vs alternatives.**
- **FSM:** a finite set of states, one active at a time, with transitions on conditions. Simple
  and predictable, but the number of transitions grows quickly as states are added.
- **Hierarchical FSM:** groups states (for example, "Grounded" containing Idle and Run) to cut
  down duplicated transitions.
- **Behaviour trees:** scale better for complex AI decision making.

For a small action game, a flat FSM with any-state transitions is the right size.

### 4.2 Player (`Player/`)
- **`PlayerInputReader`** polls the new Input System (keyboard, mouse, gamepad) and turns it into
  *intent*: a move axis, whether jump is held, and timestamps for jump and attack presses (input
  buffering). States read intent and never touch devices.
- **`PlayerController`** owns the `Rigidbody2D`, `BoxCollider2D`, `Health` and spear `Hitbox`, and
  builds the state machine. The 7 states:

  | State | What it does | Leaves when |
  |---|---|---|
  | Idle | Slows to a stop | Move input → Run; jump → Jump; attack → Attack; walked off a ledge → Fall |
  | Run | Accelerates toward `MoveSpeed × input` | No input → Idle; jump / attack / ledge |
  | Jump | Sets upward velocity on enter; releasing jump early halves it (variable jump height) | Vertical speed ≤ 0 → Fall; attack |
  | Fall | Air control at 80%; extra gravity while falling | Grounded → Idle; coyote jump → Jump |
  | Attack | windup 0.08 s → active 0.12 s (hitbox open) → recovery 0.18 s, all divided by attack speed | Finished → Idle or Fall |
  | Hurt | Knockback velocity, 0.3 s stun | Stun over → Idle |
  | Dead | Stops; the RunManager respawns you or ends the run | Respawn calls `SetState(idle)` |

- **Game-feel techniques:**
  - *Coyote time* (0.1 s): you can still jump just after walking off a ledge.
  - *Jump buffer* (0.12 s): a jump pressed just before landing still fires.
  - *Variable jump height:* releasing the button early cuts the jump.
  - *Fall gravity multiplier* (1.6×): the jump arc feels snappier.
  - *Invulnerability frames* (0.8 s) after a hit.
- **Ground check:** a thin `Physics2D.BoxCast` down from the feet against the Ground layer, and
  only while not moving upward.
- **Stats come from boons:** `MoveSpeedStat`, `DamageStat` and the rest are computed as
  `Modifiers.Evaluate(stat, baseValue)` whenever they're read, so a new boon applies immediately.

### 4.3 Combat (`Combat/`)
- **`IDamageable`** is the only thing a hitbox talks to: `Team`, `IsAlive` and
  `TakeDamage(DamageInfo)`. `DamageInfo` holds the amount, the knockback vector, the source team and
  the attacker. Teams stop friendly fire.
- **`HealthModel`** is pure C#: current/max HP, invulnerability timer, `ApplyDamage`, `Heal`,
  `SetMax` (keeps the amount of missing HP), and events (`Changed`, `Damaged`, `Died`, fired once).
  `Health` is the component wrapper.
- **`Hitbox`** uses frame-data timing. The attack state opens it on the first active frame and
  closes it after. While open, it runs `Physics2D.OverlapBox` every physics tick, and a `HashSet`
  makes sure each target is hit at most once per swing. Using an overlap query instead of trigger
  callbacks keeps timing deterministic and independent of collider enter/exit events.
- **`Projectile`** is the rock-thrower's rock: a ballistic `Rigidbody2D` with a trigger collider
  that damages the first opposing target or breaks on terrain. Its aim solves for a fixed flight
  time *t*: `vx = dx/t`, `vy = dy/t + ½·g·t`, so it lands where the player was.

### 4.4 Enemies (`Enemies/`)
- **Same FSM as the player:** Patrol → Chase → Attack → (Hurt) → Dead.
  - *Patrol* walks and turns at ledges and walls (two short raycasts).
  - *Chase* faces the player and closes in without walking off ledges.
  - *Attack* goes windup → active → recovery. The eye turns red during the attack (a telegraph).
- **Data-driven types:** `EnemyConfig` is a ScriptableObject holding size, colour, HP, damage,
  speeds, ranges, cooldown and attack frame data. *Melee vs ranged is data*: `Kind = Ranged`
  swaps the attack state for one that throws a rock. Two assets ship:

  | Enemy | HP | Damage | Cooldown | Behaviour |
  |---|---|---|---|---|
  | Cyclops Brute (melee) | 30 | 12 | 1.2 s | Patrols, chases, lunges |
  | Rock Hurler (ranged) | 20 | 10 | 2.2 s | Holds position, throws arcing rocks |

- **Director scaling:** difficulty sets a *tier* multiplier on HP and damage (×0.8, ×1.0, ×1.3).

### 4.5 Rogue-lite modifiers (`Modifiers/`)
- **`BoonDefinition`** (ScriptableObject): id, name, god, description, `MaxStacks`, and a list of
  `StatModifier { Stat, Op, Value }`. Six boons ship, one per god:

  | God | Boon | Effect |
  |---|---|---|
  | Athena | Athena's Insight | +25% damage |
  | Hermes | Winged Sandals | +15% move speed |
  | Apollo | Apollo's Tempo | +20% attack speed |
  | Hera | Hera's Resolve | +25 max HP |
  | Ares | Ares' Bloodlust | heal 5 HP per kill |
  | Poseidon | Undertow | +60% knockback |

- **`RunModifierRegistry`** (pure C#) records picked boons and stack counts for the current run.
  It applies them in one formula:

  `final = (base + Σ flat) × (1 + Σ percent)`

  Flat bonuses add together, percent bonuses add together, and the percent total is applied once.
  This keeps stacking predictable: two +20% boons give +40%, not +44%. Each boon stops at
  `MaxStacks`. The registry is cleared when a run ends.
- **`BoonOffer.Draw`** picks up to 3 random boons for the shrine, at most one per god and skipping
  maxed-out boons, using a seeded `System.Random`.
- **Why "rogue-lite":** runs are short and self-contained, and you build up random power-ups
  during a run that reset afterwards, as in Hades or Dead Cells.

### 4.6 Level generation (`LevelGen/`, `Level/`)
- **Chunks are ASCII maps**, 12 rows tall with row 0 at the top. Legend: `#` block, `^` spikes,
  `m` melee slot, `r` ranged slot, `P` player start, `X` ship, `.` empty. There are 8 chunks:
  intro, 2 easy, 2 medium, 2 hard, and the ship.
- **`ChunkLayout.Parse`** turns a map into data:
  - solid cells merged into as few rectangles as possible (horizontal runs first, then identical
    runs stacked vertically), so a 40×12 chunk needs a handful of colliders instead of hundreds
  - lists of spikes and enemy slots
  - player start and exit positions
  - `StandHeight(col)`, used to place checkpoints
- **`ChunkSelector`** picks a chunk of the requested difficulty without repeating the previous one,
  and chooses which enemy slots to fill (40/70/100% for easy/medium/hard, always at least one).
  Both use a seeded RNG, so they're deterministic and testable.
- **`LevelAssembler`** places chunks left to right at an advancing `CursorX`. For each chunk it
  creates block GameObjects (`BoxCollider2D` + sprite on the Ground layer), spikes (`Hazard`
  trigger) and enemies. It adds a **shrine gate** (`SegmentGate`) at the right edge of combat
  chunks: a trigger, plus a wall that opens after the boon pick. It also places the **ship**
  (`LevelExit`).
- **Design-time validation:** `tools/chunk_maps.py` runs a breadth-first search over cells the
  player can stand on, using the player's limits (jump ≤ 2 tiles up, reach ≤ 4 tiles, 2 tiles of
  headroom). It confirms every chunk can be crossed left to right and that no enemy floats. It
  caught a real bug: after one-way platforms were removed, the cave chunk had become uncrossable.

**Theory: procedural generation styles.**
- **Fully procedural** (noise, grammars, wave-function collapse): endless variety but hard to
  guarantee good design.
- **Chunk-based assembly** (Spelunky, Dead Cells): designers hand-craft chunks and the game
  recombines them, so every piece is designed while each run differs.

This project uses chunk-based assembly, with the Director choosing the difficulty pool.

### 4.7 Telemetry (`Telemetry/`)
- **`SegmentTelemetry`** fields: segment index, difficulty played, HP lost, max HP, seconds, par
  seconds (45), and deaths. It derives `HpLostFrac` and `TimeRatio`.
- **`TelemetryTracker`** opens a segment when the player starts it, gathers damage taken and deaths
  from player events, closes the segment at the shrine, and appends a row to `telemetry.csv`.
- The simulator produces the **same struct**, so the Director sees identical features in training
  and in the game.

### 4.8 The AI Director (`Director/`)
All three directors implement **`IDirector.Decide(lastTelemetry) → difficulty (0, 1, 2)`**. The
game loop doesn't know which one it's talking to; this is the strategy pattern.

| Director | Logic |
|---|---|
| **Fixed** | Always medium. Baseline 1 |
| **Heuristic** | Start medium. Died or lost >50% HP → one step easier; lost <15% → one step harder; otherwise keep. Baseline 2 |
| **RL** | PPO policy: 4 features → probabilities for easy/medium/hard |

**Observation (`DirectorObservation`, 4 floats in [0, 1]):**
1. HP lost fraction
2. time ratio ÷ 3
3. died (0/1)
4. difficulty played ÷ 2

**Action:** one discrete branch with 3 values.

**Reward (`ChallengeBand.Reward`):**
- −1 per death (frustration)
- +1 if HP lost is between 15% and 50% (challenged but surviving)
- −0.5 if HP lost is under 5% (trivial)
- 0 otherwise (too hard, but survived)

**Simulated player (`Sim/RunSimulator`).** Real players are too slow to train on, so training uses
a one-formula model of a run:

```
challenge = difficultyFactor[d] / ((skill / 0.6) × power)      difficultyFactor = 0.6 / 1.0 / 1.5
HP lost   = 0.35 × challenge^1.5 + Gaussian noise (σ = 0.08)
time      = 45 s × (0.7 + 0.3 × challenge + noise)
```

- Skill profiles: Novice 0.35, Average 0.60, Expert 0.85, with ±0.05 random jitter during training.
- It follows the game's rules:
  - HP carries over between segments.
  - From the second segment on, the shrine heals 15% and adds +5% "power" (the boon).
  - If HP lost ≥ current HP, it counts a death, respawns at full HP and retries the segment.
  - 3 deaths end the run. A run is 3 segments.
- The constants are hand-picked so that each skill level has a different best difficulty:
  - Novice does best on easy (≈96% of segments in band if always easy).
  - Average does best on medium (≈96%).
  - Expert does best on hard (≈94%).
- So the policy has to **infer the player's skill from telemetry** and adapt.

**Baselines on the simulator** (300 runs per skill level, same seeds):

| Director | Novice | Average | Expert |
|---|---|---|---|
| Fixed (medium), in-band segments | 0.2% | 96.3% | 70.4% |
| Heuristic, in-band segments | 36.8% | 94.9% | 76.4% |

These are baselines only. The RL result comes from `results/director_eval.md` after training.

- **Why the Heuristic struggles with novices:** it always starts on medium. A novice loses about
  80% HP there, carries low HP into the next segment, and often dies even on easy.
- **What RL can learn that the rule doesn't:** a better opening move, and how to weigh the risk
  across the whole run, because it maximises the *sum* of rewards rather than reacting one step
  at a time.

**ML-Agents wiring (`ML/`).**
- **`DirectorAgent : Agent`:**
  - `CollectObservations` writes the 4 features.
  - `OnActionReceived` reads `DiscreteActions[0]`.
  - In **training mode** it steps its own `RunSimulator`, adds the reward, logs stats and calls
    `EndEpisode()` when the simulated run ends. It picks a random skill level each episode.
  - `Heuristic()` falls back to the Heuristic director when no model or trainer is available.
- **`BehaviorParameters`** (set by the builder): behaviour name `Director`, observation size 4,
  `ActionSpec.MakeDiscrete(3)`, Behaviour Type Default. Default means: use the Python trainer if
  connected, else the ONNX model if assigned, else `Heuristic()`.
- **Training scene:** 16 agents, each with a `DecisionRequester` (one decision per Academy step),
  so they collect experience in parallel.
- **In-game decisions (`RLDirector.Decide` → `DecideNow`):** it sets the observation, calls
  `RequestDecision()`, then `Academy.Instance.EnvironmentStep()` manually. That runs observation →
  policy → `OnActionReceived` *right away*, so the decision is synchronous at the shrine. Automatic
  stepping is turned off in the game, because decisions are event-driven, not per-frame.

### 4.9 Evaluation (`Director/Sim/DirectorEvaluation`, `ML/DirectorEvaluator`)
- **Method:** every director plays the same simulated runs (seeds 1000–1299) for each skill level.
  Fixed seeds mean the dice rolls are identical, so differences come from decisions, not luck.
- **Metrics:**
  - **In-band segments** (the main one)
  - **Runs completed**
  - **Deaths per run**
- **Output:** `results/director_eval.md` and `.csv`. The RL row uses the trained ONNX model with
  deterministic inference.

### 4.10 Game loop and UI (`Core/`, `UI/`, `Dialogue/`)
- **`RunManager`** is the conductor:
  - creates the HUD, dialogue, boon panel and pause menu
  - picks the Director from the main-menu setting
  - builds the intro and spawns the player
  - runs the decide → append → play → shrine loop
  - handles death and respawn, victory and defeat
  - writes the end-screen result to `GameSession`
- **`GameDatabase`** is a ScriptableObject loaded from `Resources/`. It references every data
  asset (player config, enemies, boons, chunks, dialogue) plus run rules: 3 segments, 3 lives, 45 s
  par, 15% shrine heal.
- **UI is built from code** (`UIFactory`: canvas, panels, text, buttons with Unity's built-in font),
  so scenes stay trivial.
- **`DialogueRunner`** pauses the game (`Time.timeScale = 0`) and runs the text box on unscaled
  time. It types text out character by character: Space finishes the line or advances, Esc skips.
- **`Placeholder`** generates square, circle and triangle sprites in code, so the repo needs no art
  assets.

### 4.11 Editor tooling and tests
- **`OdysseyBuilder`** (menu **Odyssey → Build Project Content**):
  - adds physics layers
  - creates or loads every data asset (existing assets are kept, so Inspector tweaks survive)
  - regenerates the 5 scenes (MainMenu, Level01, EndScreen, DirectorTraining, DirectorEval)
  - sets the build scene list
  - assigns the ONNX model to agents if `Assets/ML/Models/Director.onnx` exists

  Other menu items: *Assign Director Model To Scenes*, *Run Director Evaluation*, *Open Training
  Scene*.
- **Edit-mode tests (NUnit)** cover the state machine, health, boon maths and offers, chunk
  parsing and selection, and the Director (observation, reward, heuristic, simulator determinism,
  seeded evaluation). They work because the core logic is plain C#.

---

## 5. Theory you should be able to explain

### 5.1 Reinforcement learning basics
- **MDP framing:** state *s* (the 4 features), action *a* (difficulty), reward *r* (band reward),
  next state *s′* (the next segment's telemetry), episode (one run of 3 segments).
- The **policy** π(a|s) is a probability distribution over actions; the goal is to maximise the
  expected **return** G = Σ γᵗ rₜ.
- **γ (gamma) = 0.9:** episodes are only 3 steps, so near-term reward matters most, but the agent
  still weighs the next segments (dying later costs too).
- **Why not supervised learning:** there are no labelled "correct difficulty" answers. Only the
  outcome (reward) is known, and actions affect future states (HP carries over). That is exactly
  RL's setting.
- **Why not a contextual bandit:** a bandit ignores how today's action changes tomorrow's state.
  Here, a too-hard segment drains HP and causes a death later.

### 5.2 PPO (Proximal Policy Optimization)
- **Actor–critic:** an *actor* network outputs action probabilities. A *critic* network estimates
  the state value V(s), used to compute the advantage A = how much better an action was than
  expected.
- **GAE (λ = 0.95):** Generalised Advantage Estimation blends short- and long-range return
  estimates, trading bias against variance.
- **Clipped objective:** with ratio rₜ = π_new(a|s) / π_old(a|s), PPO maximises
  `E[min(rₜ·Aₜ, clip(rₜ, 1−ε, 1+ε)·Aₜ)]` with ε = 0.2. Clipping stops one update from changing
  the policy too much, which makes PPO stable and forgiving of hyperparameters. That's why it is
  ML-Agents' default.
- **Entropy bonus (β = 0.005)** keeps exploring early, so the policy doesn't collapse onto one
  difficulty too soon.
- **Training loop:**
  1. Collect `buffer_size` = 2048 steps from all 16 agents.
  2. Run `num_epoch` = 3 passes of minibatch (`batch_size` = 128) gradient descent.
  3. Learning rate is 3e-4, decaying linearly.
  4. Repeat until `max_steps` = 100k.
- **`time_horizon` = 8:** how many steps are collected before the critic's value estimate is used
  to fill in the rest of the return. An episode is only 3 steps, so whole episodes fit.
- **Network:** a small MLP with 2 hidden layers of 32 units. Inputs are already in [0, 1], so
  observation normalisation is off.

### 5.3 ML-Agents architecture
- **Unity side (C#):** `Academy` (singleton that steps the environment), `Agent` (observations,
  actions, rewards), `BehaviorParameters` (spaces and model), sensors, actuators.
- **Python side:** `mlagents-learn` runs the PyTorch trainer and talks to Unity over **gRPC** on
  port 5004. That's why the Console says *"Couldn't connect to trainer on port 5004 … will perform
  inference instead"* when it isn't running.
- **Deployment:** after training, the policy is exported to **ONNX** and run in Unity by the
  **Inference Engine** (`com.unity.ai.inference`, formerly Sentis). No Python is needed at runtime.
- **Those import warnings:** ML-Agents' exported ONNX files carry extra constant outputs
  (`version_number`, `memory_size`, `discrete_action_output_shape` …) as metadata. Inference Engine
  drops outputs that aren't connected to a tensor, hence the *"not connected … will be skipped"*
  warnings on import. They're harmless.
- **Versions:** ML-Agents Release 23 = Unity package 4.0.0 + Python `mlagents` 1.1.0. The `odyssey_ai`
  conda env has Python 3.10.12 and torch 2.2.2 (CPU). Inference Engine resolved to 2.5.0.

### 5.4 Dynamic difficulty adjustment (DDA)
- **Flow theory (Csikszentmihalyi):** engagement peaks when challenge matches skill. Too easy is
  boring, too hard is frustrating. The 15–50% HP band is this project's measurable stand-in for
  "flow", and deaths stand in for frustration.
- **Known examples:** Left 4 Dead's AI Director (pacing by player stress), Resident Evil 4's hidden
  difficulty rank, rubber-banding in racing games.
- **Rule-based vs learned DDA:** rules are transparent but tuned by hand and short-sighted. A
  learned policy optimises a stated objective over the whole run, but depends on the quality of
  the reward and of the training environment.

### 5.5 Sim-to-real and evaluation
- **Sim-to-real gap:** the policy is only as good as the simulator. Here the simulator is a
  hand-tuned model, not fitted to real players, so in-game behaviour may differ.
- **Mitigations:** randomising skill and noise (domain randomisation), logging real telemetry
  (`telemetry.csv`) to calibrate the simulator later, keeping a safe fallback (Heuristic), and
  comparing against baselines.
- **Fair evaluation:** same seeds for every director (paired comparison), several skill levels,
  and more than one metric (in-band rate, completion, deaths) so one number can't hide a failure.

### 5.6 Unity concepts used
- **MonoBehaviour lifecycle:**
  - `Awake` runs on creation, `Start` before the first frame.
  - `Update` runs every frame (input, state logic).
  - `FixedUpdate` runs on the fixed physics step (0.02 s), so movement goes there.
  - `LateUpdate` runs after `Update` (camera follow).
- **Physics 2D:**
  - `Rigidbody2D` velocity-based movement (`linearVelocity` in Unity 6).
  - Colliders vs triggers.
  - Layers plus `IgnoreLayerCollision`: the player and enemies pass through each other, and
    rocks ignore enemies.
  - Overlap and cast queries.
- **ScriptableObjects:** data assets that live outside scenes. Designers edit them in the
  Inspector, and many objects share one asset.
- **Time:** `Time.timeScale = 0` pauses gameplay. UI and dialogue use unscaled time.
- **Assembly definitions:** separate compiled modules with explicit dependencies.
- **Unity rule learned the hard way:** a MonoBehaviour or ScriptableObject must live in a file of
  the same name, or assets and scenes can't link to it (see section 6).

### 5.7 Software design patterns in the code
- **State:** player and enemy behaviour.
- **Strategy:** `IDirector` with Fixed, Heuristic and RL.
- **Observer/events:** `Health.Died`, `HealthModel.Changed`, `SegmentGate.Reached`,
  `RunModifierRegistry.Changed`.
- **Data-driven design:** ScriptableObject configs.
- **Dependency inversion:** hitboxes depend on `IDamageable`, not on concrete enemies.
- **Pure core with a thin engine shell:** testable logic in plain C#, with MonoBehaviours as
  adapters.

---

## 6. Problems hit and how they were fixed (good interview stories)

1. **Invisible level.**
   - Symptom: the HUD showed, but no blocks or player.
   - Cause: the project's quality settings referenced a URP render-pipeline asset that was never
     committed, so Unity silently fell back to the built-in pipeline. The sprites used a URP-only
     shader, which draws nothing there.
   - Fix: use `Sprites/Default`, which renders under both pipelines.
2. **Missing-script error / empty level.**
   - `ChunkLibrary` (a ScriptableObject) was declared inside `ChunkDefinition.cs`. Unity links
     assets to scripts *by file*, so the asset lost its script and loaded as null.
   - Fix: one Unity class per file (7 classes moved into their own files), plus a check that
     every MonoBehaviour or ScriptableObject class name matches its file name.
3. **Uncrossable chunk.** After one-way platforms were replaced with solid blocks, a platform
   sealed a dip in the cave chunk. The BFS validator caught it before play-testing.
4. **Over-scoped AI.** The first Director chose 18 action combinations from 10 features with a
   5-term reward. It was simplified to 3 actions, 4 features and a 3-term reward: easier to train,
   evaluate and explain, with the same resume claim.

---

## 7. Likely interview questions (short answers)

**AI / ML** (1–2 lines each)
- *What does the Director learn?* A policy mapping last-segment telemetry (HP lost, time, death,
  difficulty) to next difficulty, maximising time in a 15–50% HP-loss band.
- *Why PPO?* Stable clipped policy-gradient updates, works with discrete actions, ML-Agents'
  default; DQN would also work for 3 actions but PPO needs less tuning.
- *Why train on simulated players?* Human data is slow and scarce; a stochastic simulator with
  randomised skill gives thousands of runs in minutes. The cost is a sim-to-real gap.
- *How do you know it works?* Paired evaluation on fixed seeds against Fixed and Heuristic
  baselines at 3 skill levels; metrics are in-band rate, completion rate and deaths per run.
- *What would you change with real data?* Fit the simulator's constants to `telemetry.csv`, or
  fine-tune offline from logged play.
- *Reward hacking risk?* The policy could keep players on easy to avoid deaths; the −0.5 trivial
  penalty and the in-band +1 counter that.
- *Why is the observation normalised to [0, 1]?* Stable gradients and comparable feature scales;
  ML-Agents' normaliser is off because scaling is done by hand.
- *Discount factor 0.9 with 3-step episodes?* Short horizon; future deaths still matter, but
  immediate band-keeping dominates.

**Game / Unity / C#**
- *How is combat structured?* One generic FSM; attacks use windup/active/recovery frame data; the
  hitbox is open only on active frames, hits each target once per swing, and damages through
  `IDamageable`.
- *How do boons stack?* `(base + Σflat) × (1 + Σ%)`, capped by `MaxStacks`; the registry is pure
  C# and unit-tested.
- *How are levels generated?* Handcrafted ASCII chunks are parsed into merged colliders and enemy
  slots; a seeded selector picks chunks by Director difficulty; a BFS script validates every chunk.
- *Update vs FixedUpdate?* Input and state logic in `Update`; physics and velocity in
  `FixedUpdate`, on the fixed timestep.
- *How did you make it testable?* Core logic (FSM, health, modifiers, chunk parsing, simulator,
  reward) has no MonoBehaviours, so NUnit edit-mode tests run without a scene.
- *Why generate scenes from an editor script?* Hand-edited scene files are fragile in git; the
  builder makes setup reproducible in one click.

---

## 8. Limitations and next steps

**Limitations**
- The Director is trained on a hand-tuned simulator, not humans.
- The beta is one short level with block art and two enemy types.
- Difficulty changes only between segments.

**Next steps**
1. Train the model and publish the evaluation results in the README.
2. Calibrate the simulator from real `telemetry.csv` data.
3. Add art (CC0) and audio.
4. Add more chunks and enemy types.
5. Let the Director also choose the enemy mix or the boon pool.
6. Add CI with GitHub Actions running the edit-mode tests.
