# towergame - Implementation Plan

## Scene Structure

| Scene | Purpose | Systems |
|-------|---------|---------|
| **Boot** | App initialization | Version check, asset preloading |
| **MainMenu** | Class selection, map selection, unlocks | Save data, persistent gold display |
| **Game** | Core gameplay | All gameplay systems |
| **GameOver** | Run summary | Score display, persistent gold earned |

---

## Game Scene Architecture

### Data Layer (ScriptableObjects)
```
Assets/
├── Configs/
│   ├── TowerConfig/        # Stats: cost, damage, range, energy, hp
│   ├── WeaponConfig/       # Stats: damage, fire rate, energy cost/duration
│   ├── EnemyConfig/        # Stats: hp, speed, damage, reward
│   ├── WaveConfig/         # Scaling curves, boss intervals
│   ├── MapConfig/          # Grid size, energy nodes, spawn points
│   └── ClassConfig/        # Class-specific visuals, starting bonuses
```

### Core Managers (Scripts)
```
Scripts/
├── Managers/
│   ├── GameManager.cs           # State machine (Build/Combat phases)
│   ├── GridManager.cs           # Tile data, world<->grid conversion
│   ├── EnergyNetwork.cs         # Power flow, tree traversal
│   ├── WaveManager.cs           # Spawning, infinite scaling
│   ├── EconomyManager.cs        # Gold, upgrades, repairs
│   ├── TowerManager.cs          # Tower configs, placement, state
│   ├── EnemyManager.cs          # Enemy configs, spawning
│   └── PlayerManager.cs         # Stats, weapons, energy consumption
```

### Visual Systems
```
Scripts/
├── Visuals/
│   ├── GridVisuals.cs           # Tile rendering, selection
│   ├── TowerObject.cs            # Instance behavior
│   ├── EnemyObject.cs            # Instance behavior, NavMeshAgent
│   ├── Projectile.cs             # Weapon shots
│   └── Effects/                  # Particles, FX
```

### UI
```
Scripts/
├── UI/
│   ├── GameHUD.cs                # Wave counter, gold, base HP
│   ├── BuildMenu.cs              # Tower selection
│   ├── TowerInfo.cs              # Upgrade/repair/sell panel
│   └── Menus/                    # MainMenu, GameOver screens
```

---

## Implementation Order

### Step 1: Grid Foundation
- [ ] GridManager - tile data structure
- [ ] GridVisuals - render tiles, show occupied/available
- [ ] MapConfig - define grid size per map
- [ ] World<->grid conversion helpers

### Step 2: Tower Placement System
- [ ] TowerConfig - ScriptableObject for each type
- [ ] TowerManager - registry of placed towers
- [ ] Placement validation (can build here?)
- [ ] Simple tower prefab (visual only, no shooting yet)

### Step 3: Energy Network (Core Mechanic)
- [ ] EnergyNode - map-placed power sources
- [ ] Pylon placement - connects to nodes
- [ ] EnergyNetwork - tree traversal for power flow
- [ ] Tower power state - powered/unpowered based on connection
- [ ] Visual feedback - power lines, unpowered tint

### Step 4: Wave System
- [ ] WaveManager - track current wave
- [ ] EnemyConfig - enemy types
- [ ] Spawn points from MapConfig
- [ ] Simple NavMesh setup per map
- [ ] Basic enemy movement to Base

### Step 5: Combat - Enemies
- [ ] EnemyObject - health, damage
- [ ] Pathfinding - Unity NavMesh
- [ ] Enemy types: Grunt (base), Saboteur (pylons), Heavy (blockers)
- [ ] Boss spawning every X waves
- [ ] Flying enemies (ignore NavMesh)

### Step 6: Combat - Player
- [ ] Player weapons - weapon configs
- [ ] Shooting mechanics - projectiles
- [ ] Energy consumption on fire
- [ ] Tower flicker when over capacity
- [ ] Basic attack (no energy)

### Step 7: Economy
- [ ] EconomyManager - gold tracking
- [ ] Tower upgrades (fire rate, efficiency, damage)
- [ ] Repair system
- [ ] Sell system (50% refund)

### Step 8: Game Loop
- [ ] Build<->Combat phase transitions
- [ ] "Start Wave" button
- [ ] Wave complete detection
- [ ] Base HP tracking
- [ ] Game Over trigger

### Step 9: UI
- [ ] GameHUD - gold, wave, base HP
- [ ] BuildMenu - tower selection
- [ ] TowerInfo panel - upgrade/repair/sell

### Step 10: Main Menu & Persistence
- [ ] MainMenu scene
- [ ] Class selection
- [ ] Map selection
- [ ] Weapon selection (from unlocked weapons)
- [ ] Persistent gold save (PlayerPrefs or JSON)
- [ ] Unlock tracking (towers, weapons, maps, classes)

### Step 11: Game Over & Meta
- [ ] GameOver scene
- [ ] Score calculation
- [ ] Persistent gold addition
- [ ] Retry button

---

## Technical Decisions

### NavMesh
- Use Unity's built-in **NavMeshSystem**
- Each map needs a NavMesh bake
- Obstacles (towers) marked as NavMeshObstacles
- Flying enemies use different movement (direct pathing)

### State Machine (GameManager)
```
States:
- Menu          (in MainMenu scene)
- Build         (in Game scene)
- Combat        (in Game scene)
- GameOver      (in GameOver scene)
```

### Energy Flow Algorithm
```
1. Find all energy sources (connected to matching class node)
2. Build power tree (BFS/DFS from each source)
3. For each tower:
   - Calculate path length (hops) from source
   - Sum energy demand
4. If demand > capacity:
   - Sort towers by hops (furthest first)
   - Disable towers until demand <= capacity
```

### Scaling Formula
```
Wave N difficulty:
- Enemy HP = base * (1 + growth_rate)^N
- Enemy count = base + (N * density)
- Boss every X waves
```

---

## Unity 6 Features Used

| Feature | Usage |
|---------|-------|
| **New Input System** | WASD movement, mouse aim, click actions |
| **ScriptableObjects** | All configs (towers, enemies, waves, maps) |
| **Unity Physics** | Projectile collisions |
| **NavMesh** | Enemy pathfinding |
| **Unity Physics** | Tower/enemy collision detection |

---

## File Structure Summary

```
Assets/
├── Boot/                    # Boot scene
├── MainMenu/                # MainMenu scene
├── Game/                    # Game scene
├── GameOver/                # GameOver scene
├── Scripts/
│   ├── Managers/
│   ├── Visuals/
│   └── UI/
├── Configs/
│   ├── TowerConfig/
│   ├── EnemyConfig/
│   ├── WaveConfig/
│   ├── MapConfig/
│   └── ClassConfig/
├── Prefabs/
│   ├── Towers/
│   ├── Enemies/
│   ├── Projectiles/
│   └── UI/
└── Materials/              # Shaders, materials
```
