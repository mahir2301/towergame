# towergame

A **roguelite tower defense** game with action-strategy gameplay. Build a serial-linked power grid to support automated towers while fighting alongside them in real-time.

---

## Overview

**Genre**: Roguelite Tower Defense + Action  
**Platform**: PC  
**Perspective**: Isometric (2.5D) with orthographic camera  
**Visual Style**: Low-poly 3D models with optional pixelated post-processing  
**Game Modes**: Single-player, Network Co-op (2-4 players, shared Base)

**Core Loop**: Each run is different. Build your power grid, defend against waves, unlock upgrades for future runs. One loss = restart.

---

## The Run (Roguelite Core)

Every game is a fresh attempt. What changes between runs?

### Permadeath
- **One life per run**: Lose everything on defeat
- **No save scumming**: Run ends when Base HP hits 0
- **High stakes**: Every wave matters
- **Infinite scaling**: Difficulty never stops increasing—the run only ends when your Base dies

### Run-Unique Elements
- **Map selection**: Choose from available maps (unlock more through meta-progression)
- **Energy node placement**: Different layouts per map and seed
- **Enemy waves**: Procedurally scaled based on seed
- **Class selection**: Choose your class each run

### Between Runs (Meta-Progression)

After each run (win or lose), earn persistent progress:

| Currency | Earned From | Used For |
|----------|-------------|----------|
| **Persistent Gold** | Enemies killed across runs | Unlock new towers, classes, maps |
| **Score** | Waves survived, enemies killed | Leaderboards |

This is your **long-term progression**. Even if you lose, the gold you earned stays with you.

### Unlockables
Work toward unlocking new content between runs:
- New **maps** with different layouts and challenges
- New **tower types** (class-specific) for more strategic options
- New **classes** for variety
- New **weapons** (class-specific) with different stats and playstyles
- **Difficulty modifiers** (optional challenges for extra rewards)

---

## Gameplay Loop

### Build Phase
- Strategic, pause-like phase with no time limit
- Expand power grid using pylons connected to energy nodes
- Place and position towers on a grid system
- Purchase towers with Gold
- Ends when player clicks "Start Wave"

### Combat Phase
- Real-time action
- **No building allowed** - focus entirely on fighting
- Enemies spawn in waves and attack the Base or power grid
- Player fights alongside towers using weapons
- Strategic weapon use—overloading energy causes towers to flicker
- Phase ends when all enemies are defeated

---

## The Player

**Invincibility**: Player cannot die. Loss occurs only when Base HP reaches 0.

### Controls
| Input | Action |
|-------|--------|
| WASD | Move |
| Mouse Cursor | Aim direction |
| Left Click | Place tower |
| Scroll Wheel | Zoom camera |

### Weapons

**Basic Attack**: No energy cost, low damage. Always available as fallback.

**Heavy Weapons**: High damage, consume energy spikes. Risk vs reward—firing powerful weapons may disable your towers temporarily.

| Weapon | Energy Cost | Fire Rate | Damage |
|--------|-------------|-----------|--------|
| Rifle | Low | Fast | Low |
| Minigun | High | Fast | High |
| Sniper | High | Slow | Very High |
| Shotgun | Low | Slow | Medium-High |

*Note: Weapons are placeholders and subject to change.*

---

## Classes

Players choose one class at game start (in MainMenu) and are locked to it for the run.

Each class has:
- Unique tower types (can only build towers for your class)
- Unique weapons bound to that class
- Visually distinct pylons (they are a type of tower)

**Towers & Weapons**: Both are class-specific. Must be unlocked through meta-progression before they can be used in runs.

**Requirement**: Towers only function when connected via pylons to an energy node matching the player's class.

| Class | Specialty |
|-------|-----------|
| Tech | Range and fire-rate buffs |
| Arcane | Shield-piercing and chain attacks |
| Water | Slow/Freeze effects |
| Medieval | High-durability blocker towers |

*Note: Classes are placeholders and subject to change.*

---

## Energy & Power Grid

Energy is a **capacity** system, not a consumable. Each energy source provides a fixed capacity, and towers consume a portion of that capacity to function.

### How It Works

1. **Energy Nodes** (pre-placed on map, cannot be destroyed)
   - Each node provides **X capacity**
   - Example: A node with 10 capacity can power 10 towers (if each tower needs 1)

2. **Towers** (including pylons)
   - Each tower requires **X capacity** to function
   - Must be connected to an energy node (directly or via other towers)
   - Connection path must have sufficient throughput

3. **Pylons** (a type of tower)
   - Act as extenders/connectors in the network
   - Have **X throughput capacity** - limits how much energy can flow through
   - Example: A pylon with 5 throughput can only pass 5 capacity worth of towers

4. **Connection Rule**
   - A tower is powered if:
     - It's connected to an energy source (directly or via chain)
     - AND that energy source has **spare capacity** remaining

### Power Loss
- If a pylon is destroyed, all towers "downstream" lose power immediately
- If an energy source's capacity is filled, no more towers can connect to it

### Pylon Throttling
Each pylon has throughput limits. Even if an energy source has capacity, a low-throughput pylon in the chain restricts how many towers can be powered beyond it.

### Energy Spikes (Weapon Firing)
When player fires a weapon:
- The energy source's capacity **temporarily decreases** for the weapon's duration
- This may cause some towers to lose power (flicker/disable)
- Towers lose power based on distance from source (furthest first)

---

## Towers

### Types
- **Turret**: Basic ranged attack
- **AoE**: Damages enemies in an area
- **Slow**: Reduces enemy movement speed
- **Block**: High-HP wall; enemies must destroy it to pass

*More tower types unlock through meta-progression.*
*Note: Types are placeholders and subject to change.*

### Upgrades
Towers can be upgraded using Gold. Players choose which stat to improve:
- Fire rate
- Efficiency (energy consumption)
- Damage

### Health & Repair
- Towers have HP and can be destroyed by enemies
- Can be repaired with Gold
- Different enemies deal different damage (e.g., Saboteurs excel at destroying towers)

### Grid Placement
- 1m × 1m grid system
- Towers snap to grid
- Towers can occupy multiple grid cells

---

## Economy

### During a Run
| Resource | Source | Use |
|----------|--------|-----|
| Gold | Enemy drops | Build/upgrade/repair towers. 50% refund on sell. |
| Energy | Map nodes | Capacity for towers + weapons |
| Base HP | Fixed pool | Win/loss condition |

### Persistent (Between Runs)
| Currency | Source | Use |
|----------|--------|-----|
| Persistent Gold | Earned from runs | Unlock content |
| Score | Performance | Leaderboards |

### Gold
- Carries between waves within a run
- Spent on towers, upgrades, repairs

### Energy
- Provided by energy nodes on the map (pre-placed)
- Each node has fixed capacity (e.g., 10 capacity = power 10 towers)
- Capacity is fixed per run (doesn't change unless weapons are fired)

### Base HP
- Does NOT heal between waves
- Starting amount depends on difficulty

---

## Enemies

### Basic Types
| Type | Density | Target | Behavior |
|------|---------|--------|----------|
| Grunt | High | Base | Basic fodder |
| Saboteur | Low | Pylons | Targets power grid |
| Heavy | Medium | Blockers | Brute-force path |

*Note: Types are placeholders and subject to change.*

### Additional Types
- **Boss**: Appears every X waves
- **Flying**: Flies over towers (immune to Block towers)

### Pathing
- Most enemies find the quickest path to Base, routing around towers when possible
- Flying enemies ignore terrain
- Brute-force enemies destroy anything in their direct path

### Waves
- Infinite scaling: difficulty keeps increasing with each wave
- No "final wave" - waves continue until the Base is destroyed
- Boss appears every X waves

---

## Difficulty

### Challenges (Optional Modifiers)
Enable modifiers for increased difficulty and extra rewards:
- **Ironman**: No selling towers
- **Hardcore**: Less Base HP
- **Speedrun**: Time attack
- **Chaos**: Random enemy buffs

### Difficulty Levels
Affects:
- Number of waves
- Enemy density and scaling
- Starting Base HP
- Number of energy nodes

---

## Victory & Defeat

### In a Run
- **No "winning"**: There is no final wave. Waves continue infinitely.
- **Defeat**: The run ends when Base HP reaches 0
- Your score reflects how far you made it

### Meta-Game
- **Score recorded**: Compete on leaderboards
- **Gold earned**: Added to persistent bank
- **Unlocks progress**: Work toward unlocking new content
- **Retry**: Start a new run with fresh chances

---

## To Be Determined

- Number of pylon tiers and throughput values
- Number of tower upgrade tiers
- Exact gold income rates
- Starting gold amount
- Base HP per difficulty
- Wave count and density curves per difficulty
- Specific unlockables and progression curves
- Challenge modifier details
- Final game title

---

## Technical Notes

- **Save System**: Local (cloud sync optional)
- **Audio/Music**: TBD
- **UI/UX**: TBD
- **Mod Support**: Not planned initially
- **DLC**: Not planned initially
