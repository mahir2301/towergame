# TowerGame - Architecture Documentation

## Overview

This document describes the architecture for a roguelite tower defense game with:
- **Single-player & multiplayer** support (Netcode for GameObjects)
- **Players** who can move, place towers, and use weapons
- **Energy nodes** that provide power to towers and weapons
- **Class system** that determines energy compatibility (NOT player restrictions)
- **Achievement-based unlocks** - towers/weapons unlocked permanently via achievements
- **Per-game gold** - earned during run, used to build/upgrade/repair towers, resets each run
- **Fixed loadout** - players select towers/weapons before game starts (no in-run purchases)

---

## Progression System Summary

```
┌─────────────────────────────────────────────────────────────┐
│                    TWO-LAYER SYSTEM                          │
├─────────────────────────────────────────────────────────────┤
│ META-PROGRESSION (Permanent)                                 │
│ ├── Unlock towers/weapons through achievements ONLY          │
│ ├── Unlocks persist forever                                  │
│ ├── Saved to disk (PlayerPrefs/JSON/cloud)                  │
│ ├── NO gold cost for unlocking                               │
│ └── Managed by: PersistentUnlockManager                      │
├─────────────────────────────────────────────────────────────┤
│ RUN-ECONOMY (Per-Game)                                        │
│ ├── Earn gold by killing enemies                            │
│ ├── Spend gold to: PLACE towers, UPGRADE towers, REPAIR     │
│ ├── Gold resets when game ends                              │
│ └── Managed by: RunEconomyManager                            │
├─────────────────────────────────────────────────────────────┤
│ LOADOUT (Per-Game Selection)                                │
│ ├── Select towers/weapons BEFORE game starts                 │
│ ├── Fixed number of slots (e.g., 4 towers, 3 weapons)       │
│ ├── Selection is FREE (no gold cost)                        │
│ └── Selected items are available for the entire run         │
└─────────────────────────────────────────────────────────────┘

Example Flow:
1. MAIN MENU: View unlocked items, achievements
2. PRE-GAME: Select loadout from unlocked items (4 towers, 3 weapons)
3. RUN START: Gold = starting amount (e.g., 100)
4. DURING RUN: Earn gold from kills → Spend on building/upgrading
5. RUN END: All gold lost, achievements checked
6. NEW RUN: Same unlocked items, fresh gold, select new loadout
```

---

## Core Concepts

### Player-Entity Relationship

```
┌─────────────────────────────────────────────────────────────────┐
│                    CORE ENTITIES                                │
└─────────────────────────────────────────────────────────────────┘

PLAYER (One per client)
├── Moves around the world
├── Selects loadout BEFORE game starts (fixed slots)
├── Can connect to ONE EnergyRuntime at a time (for weapon power)
├── Can place multiple Towers (each Tower connects to its own node)
├── Earns gold during run (for building/upgrading)
└── NO Class restriction (can use any unlocked weapon/tower)

TOWER (Multiple in world)
├── Has a Class (determines compatible EnergyRuntime types)
├── Has prefab, stats, grid size, energy cost
├── Has buildCost (gold to place), upgradeCost (gold to upgrade)
├── Must connect to compatible EnergyRuntime to function
└── Each Tower connects independently

WEAPON (Attached to player, NOT in world)
├── Has a Class (determines compatible EnergyRuntime types)
├── Has stats (damage, fire rate, range, energy cost)
├── Must connect to compatible EnergyRuntime to fire
└── Player carries multiple weapons, switches between them

ENERGY NODE (Spawned at game start)
├── Has a Type (Tech, Magic, Water, Medieval, etc.)
├── Provides energy capacity
├── Can power multiple Towers AND the connected Player
└── Fixed position on map

CLASS (Data-only)
├── Defines which EnergyRuntime types can power this
├── Example: "Tech" class → can connect to Tech and Electric nodes
└── Attached to TowerType and WeaponType, NOT to Player
```

### Key Insight: Class is NOT a Player Property

```
WRONG: Player → HAS Class → limits available weapons/towers
CORRECT: Player → selects from unlocked weapons/towers → Tower/Weapon → HAS Class → determines compatible EnergyRuntimes
```

A player can use ANY unlocked weapon or tower, but each weapon/tower can only draw power from compatible EnergyRuntime types.

---

## Architecture Layers

### Layer 1: Configuration (ScriptableObjects)

**Read-only at runtime. Shared across network.**

```
┌──────────────────────────────────────────────────────────────┐
│                SCRIPTABLEOBJECTS                              │
└──────────────────────────────────────────────────────────────┘

EnergyType (SO)
├── id: string
├── displayName: string
├── nodeColor: Color
└── icon: Sprite

Class (SO)
├── id: string
├── displayName: string
├── compatibleNodeTypes: List<EnergyType>
├── classColor: Color
└── classIcon: Sprite

TowerType (SO)
├── id: string
├── displayName: string
├── towerClass: Class
├── prefab: GameObject (has TowerRuntime + TowerBehavior)
├── baseStats: TowerStats { maxHealth, damage, range, fireRate }
├── gridSize: Vector2Int
├── energyCost: int
├── buildCost: int (gold to place)
├── upgradeCost: int (gold to upgrade, or array for levels)
├── repairCost: int (gold to repair per HP)
├── startsUnlocked: bool
└── unlockCondition: UnlockConditionSO (optional - achievement)

WeaponType (SO)
├── id: string
├── displayName: string
├── weaponClass: Class
├── baseStats: WeaponStats { damage, fireRate, range }
├── energyCostPerShot: int
├── energySpikeDuration: float (0 = no spike)
├── startsUnlocked: bool
└── unlockCondition: UnlockConditionSO (optional)

GameRegistry (SO)
├── nodeTypes: List<EnergyType>
├── classes: List<Class>
├── towerTypes: List<TowerType>
├── weaponTypes: List<WeaponType>
├── startingGold: int (beginning of each run)
├── maxTowersInLoadout: int (e.g., 4)
└── maxWeaponsInLoadout: int (e.g., 3)
```

---

### Layer 2: Runtime State (NetworkBehaviours)

**Server-authoritative. Synced to clients.**

```
┌──────────────────────────────────────────────────────────────┐
│                RUNTIME COMPONENTS                             │
└──────────────────────────────────────────────────────────────┘

PlayerRuntime (NetworkBehaviour) ─ ONE PER CLIENT
├── State (synced):
│   ├── gold: int [SyncVar]
│   ├── selectedTowerIds: List<string> [SyncList] (loadout)
│   ├── selectedWeaponIds: List<string> [SyncList] (loadout)
│   ├── ownedWeapons: List<WeaponInstance> [SyncList] (with cooldowns)
│   ├── currentWeaponIndex: int [SyncVar]
│   ├── connectedNodeId: ulong [SyncVar] (0 = not connected)
│   └── serverPosition: Vector3 [SyncVar] (for correction)
├── References:
│   ├── PlayerController (MonoBehaviour)
│   ├── PlayerWeaponController (MonoBehaviour)
│   └── connectedEnergyRuntime: EnergyRuntime (runtime)
├── Actions:
│   ├── SelectLoadoutServerRpc(towerIds, weaponIds)
│   ├── SwitchWeaponServerRpc(index)
│   ├── FireWeaponServerRpc(targetPosition)
│   ├── ConnectToEnergyRuntimeServerRpc(nodeId)
│   ├── PlaceTowerServerRpc(towerId, gridPosition)
│   ├── UpgradeTowerServerRpc(towerId)
│   └── RepairTowerServerRpc(towerId)
└── Validation:
    ├── Weapon Class must match EnergyRuntime type
    ├── Tower Class must match nearby EnergyRuntime type
    ├── Must have enough gold to place/upgrade/repair
    └── Tower must be in selectedTowerIds (loadout)

TowerRuntime (NetworkBehaviour) ─ MANY PER GAME
├── State (synced):
│   ├── configId: string [SyncVar]
│   ├── ownerClientId: ulong [SyncVar]
│   ├── gridPosition: Vector2Int [SyncVar]
│   ├── currentHealth: float [SyncVar]
│   ├── maxHealth: float [SyncVar]
│   ├── isPowered: bool [SyncVar]
│   └── upgradeLevel: int [SyncVar]
├── References:
│   ├── config: TowerType (from registry)
│   └── connectedEnergyRuntime: EnergyRuntime
├── Components:
│   └── TowerBehavior (MonoBehaviour) - logic module
└── Actions:
    ├── TakeDamageServerRpc(amount)
    ├── RepairServerRpc(amount)
    └── UpgradeServerRpc()

EnergyRuntime (NetworkBehaviour) ─ SPAWNED AT GAME START
├── State (synced):
│   ├── nodeType: EnergyType
│   ├── gridPosition: Vector2Int
│   ├── currentCapacity: int [SyncVar]
│   └── maxCapacity: int
├── Connections:
│   ├── connectedTowers: List<TowerRuntime>
│   └── connectedPlayer: PlayerRuntime (only ONE player)
├── Actions:
│   ├── HasCapacity(amount): bool
│   ├── ConsumeCapacity(amount)
│   ├── ReleaseCapacity(amount)
│   └── ApplyEnergySpike(amount, duration)
└── Power State:
    ├── Re-evaluate power state when capacity changes
    └── Furthest towers disconnect first if over capacity
```

---

### Layer 3: Client-Side Input (MonoBehaviours)

**Local-only. Delegates to NetworkBehaviours.**

```
┌──────────────────────────────────────────────────────────────┐
│                CLIENT-SIDE CONTROLLERS                        │
└──────────────────────────────────────────────────────────────┘

PlayerController (MonoBehaviour)
├── Handles: Movement (WASD), Rotation (mouse), Zoom (scroll)
├── References: PlayerRuntime, PlayerWeaponController
├── Input handlers:
│   ├── OnMove(context) → moves Rigidbody
│   ├── OnFireWeapon(context) → playerRuntime.FireWeaponServerRpc()
│   ├── OnSwitchWeapon(context) → playerRuntime.SwitchWeaponServerRpc()
│   └── OnPlaceTower(context) → towerPlacementController.OnPlaceTower()
└── Movement: Client-side with server correction (for cheating prevention)

PlayerWeaponController (MonoBehaviour)
├── Handles: Weapon swap input, aiming visuals, cooldown UI
├── References: PlayerRuntime
├── Client-side prediction: weapon cooldown display
└── Input: Left click to fire, 1-5 for weapon slots

TowerPlacementController (MonoBehaviour)
├── Handles: Ghost preview, placement validation
├── References: GridManager, PlayerRuntime, GameRegistry
├── State:
│   ├── selectedTowerIndex: int (which tower in loadout)
│   ├── ghostInstance: GameObject
│   └── isValidPlacement: bool
└── Actions:
    ├── UpdateGhostState() → shows/hides ghost, validates position
    ├── TryPlaceTower() → calls PlayerRuntime.PlaceTowerServerRpc()
    ├── TryUpgradeTower() → calls PlayerRuntime.UpgradeTowerServerRpc()
    └── TryRepairTower() → calls PlayerRuntime.RepairTowerServerRpc()
```

---

### Layer 4: Behavior Modules (MonoBehaviours)

**Logic modules, attached to prefabs.**

```
┌──────────────────────────────────────────────────────────────┐
│                BEHAVIOR MODULES                               │
└──────────────────────────────────────────────────────────────┘

TowerBehavior (MonoBehaviour) - Base class
├── Methods:
│   ├── Initialize(TowerRuntime runtime)
│   ├── OnTick() - called every frame when powered
│   ├── OnPlaced() - called once when placed
│   ├── OnPowerStateChanged(bool isPowered)
│   ├── OnUpgraded(int newLevel)
│   └── FindTarget(): Transform
└── Subclasses:
    ├── BasicTurretBehavior (simple targeting, firing)
    ├── WallBehavior (no OnTick, just blocks)
    ├── AoETowerBehavior (area damage)
    └── ... (custom behaviors per tower)
```

---

## Data Structures

### WeaponInstance (Serializable, NOT MonoBehaviour)

```csharp
[Serializable]
public class WeaponInstance
{
    public string configId;
    public float currentCooldown;
    
    public WeaponInstance(WeaponType config) 
    { 
        configId = config.id; 
        currentCooldown = 0f; 
    }
    
    public WeaponType GetConfig() => GameRegistry.Instance.GetWeaponType(configId);
}
```

**Note:** No `TowerInstance` class needed. Towers in loadout are tracked via `selectedTowerIds: List<string>`. Once placed, `TowerRuntime` handles per-instance state.

---

## File Structure

```
Assets/
├── Scripts/
│   ├── Data/
│   │   ├── EnergyType.cs           # SO: Node type definition
│   │   ├── Class.cs                     # SO: Class definition
│   │   ├── TowerType.cs                 # SO: Tower config
│   │   ├── WeaponType.cs                # SO: Weapon config
│   │   ├── TowerStats.cs                # Stats struct
│   │   ├── WeaponStats.cs               # Stats struct
│   │   ├── UnlockConditionSO.cs         # SO: Achievement conditions
│   │   └── GameRegistry.cs              # Central type registry
│   ├── Runtime/
│   │   ├── PlayerRuntime.cs             # NB: Player state
│   │   ├── WeaponInstance.cs            # Weapon instance data
│   │   ├── TowerRuntime.cs              # NB: Per-tower state
│   │   └── EnergyRuntime.cs                # NB: Energy node state
│   ├── Managers/
│   │   ├── PersistentUnlockManager.cs   # Achievement-based unlocks
│   │   ├── RunEconomyManager.cs         # Per-game gold (build/upgrade/repair)
│   │   ├── LoadoutManager.cs            # Loadout selection UI
│   │   ├── GridManager.cs               # Grid logic (existing)
│   │   ├── EnergyNetworkManager.cs      # Connection graph
│   │   └── PlayerSpawnManager.cs        # Spawns PlayerRuntime
│   ├── Controllers/
│   │   ├── PlayerController.cs          # Input + movement
│   │   ├── PlayerWeaponController.cs    # Weapon input
│   │   └── TowerPlacementController.cs  # Placement preview
│   ├── Behaviors/
│   │   ├── TowerBehavior.cs             # Base class
│   │   └── Towers/
│   │       ├── BasicTurretBehavior.cs
│   │       ├── WallBehavior.cs
│   │       └── ...
│   ├── Network/
│   │   └── TowerSpawnSystem.cs          # Server-authoritative spawning
│   └── Visuals/
│       ├── GridVisuals.cs               # Grid line rendering (existing)
│       └── EnergyNetworkVisualization.cs # Power lines
├── Prefabs/
│   ├── Players/
│   │   └── PlayerRuntime.prefab        # Has PlayerController, PlayerRuntime
│   ├── Towers/
│   │   ├── BasicTurret.prefab           # Has TowerRuntime + TowerBehavior
│   │   ├── Wall.prefab
│   │   └── ...
│   └── EnergyRuntimes/
│       ├── TechNode.prefab
│       ├── MagicNode.prefab
│       └── ...
└── Configs/
    ├── GameRegistry.asset              # Central registry
    ├── Classes/
    │   ├── Tech.asset
    │   ├── Magic.asset
    │   └── ...
    ├── Towers/
    │   ├── BasicTurret.asset           # TowerType SO
    │   └── ...
    ├── Weapons/
    │   ├── Rifle.asset                 # WeaponType SO
    │   └── ...
    └── EnergyTypes/
        ├── Tech.asset                  # EnergyType SO
        └── ...
```

---

## Multiplayer Flow

### Game Start Sequence

```
1. MAIN MENU
   ├── Load PersistentUnlockManager (from disk)
   └── Display unlocked towers/weapons, achievements

2. PRE-GAME: LOADOUT SELECTION
   ├── Show available towers/weapons (from PersistentUnlockManager)
   ├── Player selects up to maxTowersInLoadout (e.g., 4)
   ├── Player selects up to maxWeaponsInLoadout (e.g., 3)
   └── Selection is FREE - no gold cost

3. SERVER: SPAWN WORLD
   ├── Spawn EnergyRuntimes from map config
   └── Each has EnergyRuntime component (NetworkBehaviour)

4. SERVER: SPAWN PLAYERS
   ├── Spawn PlayerRuntime for each client
   └── Send loadout selection to server: SelectLoadoutServerRpc(towerIds, weaponIds)

5. SERVER: VALIDATE & INITIALIZE
   ├── Check unlock status (PersistentUnlockManager)
   ├── Initialize selectedTowerIds, selectedWeaponIds
   ├── Initialize WeaponInstances from selectedWeaponIds
   ├── Set starting gold (from GameRegistry.startingGold)
   └── Sync [SyncVar] [SyncList] to all clients

6. GAME START
   ├── Players can move (client-side, server correction)
   ├── Players can place towers (costs gold, must be in loadout)
   ├── Players can upgrade towers (costs gold)
   ├── Players can repair towers (costs gold)
   ├── Players can use weapons (must be in loadout)
   └── Players can connect to EnergyRuntimes (ServerRpc validation)
```

### Tower Placement Flow

```
CLIENT (Input)                    SERVER (Authoritative)
    │                                    │
    │ Click to place tower              │
    │ ▼                                  │
    │ TowerPlacementController          │
    │   - Validate grid position        │
    │   - Check tower is in loadout     │
    │   - Show ghost (client-only)      │
    │   - PlayerRuntime.PlaceTowerRpc()┤
    │                                    │
    │                          Validate:│
    │                          - Tower in selectedTowerIds?
    │                          - Position valid?
    │                          - Near compatible node?
    │                          - Enough gold? (buildCost)
    │                                    │
    │                          Process: │
    │                          - Deduct gold
    │                          - Spawn TowerNetworkObject
    │                          - Set ownerClientId
    │                          - Initialize state
    │                          - Connect to node
    │                                    │
    │                          Sync:    │
    │                          - [SyncVar] gold, gridPosition, etc.
    │                                    │
    └────────────────────────────────────┘
```

### Tower Upgrade Flow

```
CLIENT (Input)                    SERVER (Authoritative)
    │                                    │
    │ Click on placed tower             │
    │ ▼                                  │
    │ Show upgrade UI (cost, new stats) │
    │ ▼                                  │
    │ PlayerRuntime.UpgradeTowerRpc()   ─┤
    │                                    │
    │                          Validate:│
    │                          - Tower exists?
    │                          - Enough gold? (upgradeCost)
    │                          - Not max level?
    │                                    │
    │                          Process: │
    │                          - Deduct upgradeCost
    │                          - Increase upgradeLevel
    │                          - Update maxHealth, damage, etc.
    │                                    │
    │                          Sync:    │
    │                          - [SyncVar] upgradeLevel, currentHealth
    │                                    │
    └────────────────────────────────────┘
```

### Tower Repair Flow

```
CLIENT (Input)                    SERVER (Authoritative)
    │                                    │
    │ Click on damaged tower             │
    │ ▼                                  │
    │ Show repair UI (cost per HP)      │
    │ ▼                                  │
    │ PlayerRuntime.RepairTowerRpc()    ─┤
    │                                    │
    │                          Validate:│
    │                          - Tower exists?
    │                          - Tower damaged? (currentHealth < maxHealth)
    │                          - Enough gold?
    │                                    │
    │                          Process: │
    │                          - Deduct repairCost * (maxHealth - currentHealth)
    │                          - Set currentHealth = maxHealth
    │                                    │
    │                          Sync:    │
    │                          - [SyncVar] currentHealth
    │                                    │
    └────────────────────────────────────┘
```

### Weapon Firing Flow

```
CLIENT (Input)                    SERVER (Authoritative)
    │                                    │
    │ Left click                         │
    │ ▼                                  │
    │ PlayerController.OnFireWeapon()    │
    │   - Get target position            │
    │   - client prediction (visual)     │
    │   - FireWeaponServerRpc(pos) ──────┤
    │                                    │
    │                          Validate:│
    │                          - Weapon in selectedWeaponIds?
    │                          - Not on cooldown?
    │                          - Connected to node?
    │                          - Node has capacity?
    │                          - Weapon Class compatible?
    │                                    │
    │                          Process: │
    │                          - Consume energy
    │                          - Deal damage
    │                          - Set cooldown
    │                          - Energy spike (if applicable)
    │                                    │
    │                          Sync:    │
    │                          - [SyncVar] currentCooldown
    │                          - EnergyRuntime capacity
    │                                    │
    │                          Broadcast:│
    │                          - Visual effects
    │                          - Sound   │
    │                                    │
    └────────────────────────────────────┘
```

---

## Economy System

### Gold Sources

```
┌─────────────────────────────────────────────────────────────┐
│                    GOLD SOURCES                              │
└─────────────────────────────────────────────────────────────┘

1. STARTING GOLD
   └── GameRegistry.startingGold (e.g., 100)

2. ENEMY KILLS
   └── Each enemy drops gold (defined in EnemyType SO)

3. WAVE BONUSES
   └── Bonus gold for completing waves (optional)

4. INTEREST (optional)
   └── % of current gold every N waves
```

### Gold Sinks

```
┌─────────────────────────────────────────────────────────────┐
│                    GOLD SINKS                                │
└─────────────────────────────────────────────────────────────┘

1. BUILD TOWER
   └── TowerType.buildCost (gold to place)

2. UPGRADE TOWER
   └── TowerType.upgradeCost (gold per upgrade level)

3. REPAIR TOWER
   └── TowerType.repairCost per HP restored

NO gold costs for:
- Selecting loadout (free)
- Switching weapons (free)
- Connecting to nodes (free)
```

### RunEconomyManager

```csharp
public class RunEconomyManager : MonoBehaviour
{
    public static RunEconomyManager Instance { get; private set; }
    
    // State (resets each game)
    [SyncVar] private int currentGold;
    
    // Actions
    public bool CanAfford(int cost) => currentGold >= cost;
    
    public void AddGold(int amount)
    {
        currentGold += amount;
        // Optional: Track for achievements
        // PersistentUnlockManager.Instance.AddTotalGoldEarned(amount);
    }
    
    public bool SpendGold(int cost)
    {
        if (!CanAfford(cost)) return false;
        currentGold -= cost;
        return true;
    }
    
    public int GetCurrentGold() => currentGold;
    
    // Reset for new run
    public void ResetForNewRun(int startingGold)
    {
        currentGold = startingGold;
    }
}
```

---

## Persistence System

### PersistentUnlockManager (Achievement-Based)

```csharp
public class PersistentUnlockManager : MonoBehaviour
{
    public static PersistentUnlockManager Instance { get; private set; }
    
    // State (saved to disk)
    private HashSet<string> unlockedTowerIds = new();
    private HashSet<string> unlockedWeaponIds = new();
    
    // Achievement tracking (for unlock conditions)
    private int totalEnemiesKilled;
    private int maxWavesSurvived;
    private int totalTowersPlaced;
    private int bossesDefeated;
    
    // Properties for achievements
    public int TotalEnemiesKilled => totalEnemiesKilled;
    public int MaxWavesSurvived => maxWavesSurvived;
    public int TotalTowersPlaced => totalTowersPlaced;
    public int BossesDefeated => bossesDefeated;
    
    // Queries
    public bool IsTowerUnlocked(TowerType tower) 
        => unlockedTowerIds.Contains(tower.id) || tower.startsUnlocked;
    
    public bool IsWeaponUnlocked(WeaponType weapon) 
        => unlockedWeaponIds.Contains(weapon.id) || weapon.startsUnlocked;
    
    // Unlocks (called when achievement is met)
    public void UnlockTower(string towerId)
    {
        if (unlockedTowerIds.Add(towerId))
            SaveUnlocks();
    }
    
    public void UnlockWeapon(string weaponId)
    {
        if (unlockedWeaponIds.Add(weaponId))
            SaveUnlocks();
    }
    
    // Called during gameplay
    public void OnEnemyKilled() => totalEnemiesKilled++;
    public void OnWaveCompleted(int waveNumber) => maxWavesSurvived = Mathf.Max(maxWavesSurvived, waveNumber);
    public void OnTowerPlaced() => totalTowersPlaced++;
    public void OnBossDefeated() => bossesDefeated++;
    
    // Check all unlock conditions
    public void CheckUnlocks()
    {
        foreach (var tower in GameRegistry.Instance.towerTypes)
        {
            if (IsTowerUnlocked(tower)) continue;
            if (tower.unlockCondition != null && tower.unlockCondition.IsMet())
            {
                UnlockTower(tower.id);
                Debug.Log($"Unlocked tower: {tower.displayName}");
            }
        }
        
        foreach (var weapon in GameRegistry.Instance.weaponTypes)
        {
            if (IsWeaponUnlocked(weapon)) continue;
            if (weapon.unlockCondition != null && weapon.unlockCondition.IsMet())
            {
                UnlockWeapon(weapon.id);
                Debug.Log($"Unlocked weapon: {weapon.displayName}");
            }
        }
    }
    
    // Persistence (implementation omitted for brevity)
    private void SaveUnlocks() { /* Save to PlayerPrefs/JSON/cloud */ }
    private void LoadUnlocks() { /* Load from PlayerPrefs/JSON/cloud */ }
    
    // Get all unlocked items (for UI)
    public List<TowerType> GetUnlockedTowers() 
        => GameRegistry.Instance.towerTypes.Where(IsTowerUnlocked).ToList();
    
    public List<WeaponType> GetUnlockedWeapons() 
        => GameRegistry.Instance.weaponTypes.Where(IsWeaponUnlocked).ToList();
}

// Unlock condition checking
public class UnlockConditionSO : ScriptableObject
{
    public UnlockType unlockType;
    public int requiredValue;
    
    public bool IsMet()
    {
        return unlockType switch
        {
            UnlockType.SurviveWaves => PersistentUnlockManager.Instance.MaxWavesSurvived >= requiredValue,
            UnlockType.KillEnemies => PersistentUnlockManager.Instance.TotalEnemiesKilled >= requiredValue,
            UnlockType.PlaceTowers => PersistentUnlockManager.Instance.TotalTowersPlaced >= requiredValue,
            UnlockType.DefeatBoss => PersistentUnlockManager.Instance.BossesDefeated >= requiredValue,
            _ => false
        };
    }
}

public enum UnlockType
{
    SurviveWaves,
    KillEnemies,
    PlaceTowers,
    DefeatBoss
}
```

---

## Loadout System

### Loadout Selection Flow

```
┌─────────────────────────────────────────────────────────────┐
│                  LOADOUT SELECTION                           │
└─────────────────────────────────────────────────────────────┘

1. Player sees all unlocked towers (PersistentUnlockManager)
2. Player selects up to maxTowersInLoadout (e.g., 4)
3. Player sees all unlocked weapons
4. Player selects up to maxWeaponsInLoadout (e.g., 3)
5. Selection is FREE - no gold cost
6. Selection is SENT TO SERVER on game start
7. Server validates: All items must be unlocked
8. During run: Can ONLY use selected items

Example UI:
┌─────────────────────────────────────┐
│  SELECT LOADOUT                     │
│                                     │
│  TOWERS (4 slots)                   │
│  [Basic Turret] [Wall] [AoE]  [  ] │
│                                     │
│  WEAPONS (3 slots)                  │
│  [Rifle] [Shotgun] [    ]          │
│                                     │
│  [START GAME]                       │
└─────────────────────────────────────┘
```

### PlayerRuntime Loadout Handling

```csharp
public class PlayerRuntime : NetworkBehaviour
{
    // Loadout (selected before game starts)
    private List<string> selectedTowerIds = new();  // Up to 4
    private List<string> selectedWeaponIds = new(); // Up to 3
    
    // Runtime state
    private List<WeaponInstance> ownedWeapons = new();
    private int currentWeaponIndex = 0;
    
    [SyncVar] private int gold;
    
    // Loadout selection (called at game start)
    [ServerRpc]
    public void SelectLoadoutServerRpc(string[] towerIds, string[] weaponIds)
    {
        // Validate: check count limits
        if (towerIds.Length > GameRegistry.Instance.maxTowersInLoadout)
            return;
        if (weaponIds.Length > GameRegistry.Instance.maxWeaponsInLoadout)
            return;
        
        // Validate: check all items are unlocked
        foreach (var id in towerIds)
        {
            var tower = GameRegistry.Instance.GetTowerType(id);
            if (!PersistentUnlockManager.Instance.IsTowerUnlocked(tower))
                return;
        }
        foreach (var id in weaponIds)
        {
            var weapon = GameRegistry.Instance.GetWeaponType(id);
            if (!PersistentUnlockManager.Instance.IsWeaponUnlocked(weapon))
                return;
        }
        
        // Assign loadout
        selectedTowerIds = towerIds.ToList();
        selectedWeaponIds = weaponIds.ToList();
        
        // Initialize weapon instances
        ownedWeapons = selectedWeaponIds
            .Select(id => new WeaponInstance(GameRegistry.Instance.GetWeaponType(id)))
            .ToList();
        
        // Set starting gold
        gold = GameRegistry.Instance.startingGold;
    }
    
    // Check if tower is in loadout
    public bool CanPlaceTower(string towerId)
        => selectedTowerIds.Contains(towerId);
    
    // Check if weapon is in loadout
    public bool CanUseWeapon(int index)
        => index >= 0 && index < ownedWeapons.Count;
    
    // Get available towers (for UI)
    public List<TowerType> GetAvailableTowers()
        => selectedTowerIds
            .Select(id => GameRegistry.Instance.GetTowerType(id))
            .Where(t => t != null)
            .ToList();
}
```

---

## Energy Connection System

### Player → EnergyRuntime Connection

```
1. Player walks near EnergyRuntime (or presses "interact" key)
2. PlayerRuntime.ConnectToEnergyRuntimeServerRpc(nodeId)
3. SERVER validates:
   ├─ Current weapon exists?
   ├─ Weapon.Class compatible with Node.Type?
   └─ Node has capacity?
4. If valid:
   ├─ Set connectedEnergyRuntime
   ├─ Draw power line (ClientRpc)
   └─ Can now fire weapon (draws energy from node)
5. If invalid:
   └─ Deny connection, show error message
```

### Tower → EnergyRuntime Connection

```
1. Player places tower near EnergyRuntime
2. TowerSpawnSystem tries to find nearest compatible node
3. Validates:
   ├─ Tower.Class compatible with Node.Type?
   └─ Node has capacity for tower's energyCost?
4. If valid:
   ├─ Tower connects to node
   ├─ Power line drawn (visual)
   └─ Tower.isPowered = true
5. If invalid:
   ├─ Tower still spawns, but isPowered = false
   └─ Shows "unpowered" visual (grayed out, no power line)
```

### Energy Spike (Weapon Firing)

```
When player fires weapon:
1. EnergyRuntime.ConsumeCapacity(energyCostPerShot)
2. If weapon has energySpikeDuration > 0:
   └─ EnergyRuntime.ApplyEnergySpike(amount, duration)
      ├─ Temporarily reduce capacity
      ├─ Re-evaluate powered state (towers may lose power)
      └─ Restore after duration

This creates risk/reward: Powerful weapons can temporarily disable towers!
```

---

## Class Compatibility Examples

```
Class: "Tech"
├── compatibleNodeTypes: [Tech, Electric]
└── Towers/Weapons with this Class:
    ├─ Can connect to Tech EnergyRuntimes ✓
    ├─ Can connect to Electric EnergyRuntimes ✓
    └─ Cannot connect to Magic EnergyRuntimes ✗

Class: "Magic"
├── compatibleNodeTypes: [Magic, Arcane]
└── Towers/Weapons with this Class:
    ├─ Can connect to Magic EnergyRuntimes ✓
    ├─ Can connect to Arcane EnergyRuntimes ✓
    └─ Cannot connect to Tech EnergyRuntimes ✗

Example:
Player has Tech Rifle (Class: Tech)
├─ Player connects to Tech EnergyRuntime → Can fire ✓
├─ Player connects to Magic EnergyRuntime → Cannot fire ✗
└─ Player switches to Magic Staff (Class: Magic)
    ├─ Player connects to Magic EnergyRuntime → Can fire ✓
    └─ Player connects to Tech EnergyRuntime → Cannot fire ✗
```

---

## Implementation Checklist

### Phase 1: Core Types (ScriptableObjects)
- [ ] Create `EnergyType.cs`
- [ ] Create `Class.cs`
- [ ] Create `TowerType.cs` (include buildCost, upgradeCost, repairCost)
- [ ] Create `WeaponType.cs`
- [ ] Create `GameRegistry.cs` (include startingGold, maxLoadoutSlots)
- [ ] Create `UnlockConditionSO.cs`
- [ ] Create Editor menu items (`CreateAssetMenu`)
- [ ] Create test assets (Tech class, Basic tower, Rifle weapon)

### Phase 2: Energy System
- [ ] Create `EnergyRuntime.cs` (NetworkBehaviour)
- [ ] Implement capacity tracking
- [ ] Implement connection management
- [ ] Create `EnergyNetworkVisualization.cs` (power lines)

### Phase 3: Tower System
- [ ] Create `TowerRuntime.cs` (NetworkBehaviour)
- [ ] Create `TowerBehavior.cs` (base class)
- [ ] Create `BasicTurretBehavior.cs` (simple behavior)
- [ ] Update `TowerType` to reference prefab with TowerRuntime
- [ ] Create `TowerSpawnSystem.cs` (server-authoritative spawning)
- [ ] Implement upgrade system
- [ ] Implement repair system

### Phase 4: Weapon System
- [ ] Create `WeaponInstance.cs` (serializable data)
- [ ] Update `PlayerRuntime.cs` to handle weapon instances
- [ ] Implement `FireWeaponServerRpc`
- [ ] Implement `SwitchWeaponServerRpc`

### Phase 5: Player System
- [ ] Create `PlayerRuntime.cs` (NetworkBehaviour)
- [ ] Create `PlayerWeaponController.cs` (input handling)
- [ ] Update `PlayerController.cs` to reference PlayerRuntime
- [ ] Implement `ConnectToEnergyRuntimeServerRpc`
- [ ] Implement loadout selection (`SelectLoadoutServerRpc`)

### Phase 6: Economy System
- [ ] Create `RunEconomyManager.cs` (per-game gold)
- [ ] Implement gold earning (enemy kills)
- [ ] Implement gold spending (build, upgrade, repair)
- [ ] Integrate with TowerSpawnSystem

### Phase 7: Persistence
- [ ] Create `PersistentUnlockManager.cs` (achievement-based unlocks)
- [ ] Implement save/load system (PlayerPrefs or JSON)
- [ ] Create unlock condition checking
- [ ] Test persistence across game restarts

### Phase 8: Multiplayer Integration
- [ ] Add `NetworkManager` setup
- [ ] Create `PlayerSpawnManager.cs`
- [ ] Test weapon firing across network
- [ ] Test tower placement across network
- [ ] Test gold synchronization
- [ ] Test energy spike synchronization

---

## Key Decisions Made

1. **Players don't have a Class** - Class is a property of Towers/Weapons, not players. Players can use any unlocked weapon/tower.

2. **Weapons are NOT spawned objects** - They're data attached to PlayerRuntime, not Instantiated prefabs.

3. **Class determines energy compatibility** - A Tower/Weapon's Class defines which EnergyRuntime types can power it.

4. **Energy connections are independent** - A player connects to one node for weapons, each tower connects to its own node.

5. **Server-authoritative state** - All game state (hp, capacity, cooldowns) lives on server, synced via [SyncVar].

6. **Client-side prediction** - Movement and aiming are client-side for responsiveness, server corrects if needed.

7. **Achievement-based unlocks** - Towers/weapons unlocked via achievements ONLY. No gold cost for unlocking.

8. **Per-game gold** - Gold earned during run, used for BUILD/UPGRADE/REPAIR. Resets each game.

9. **Fixed loadout** - Players select towers/weapons BEFORE game starts. Fixed number of slots.

10. **No in-run purchases** - Can't buy towers/weapons mid-game. Only build/upgrade/repair with gold.

---

## Open Questions

1. **Energy connection range:** How close must player be to connect? (Distance, grid adjacency, explicit interact?)

2. **Weapon hotbar:** Fixed 1-5 keys, or scroll wheel/selection wheel?

3. **Tower upgrade system:** How many upgrade levels? Linear stat increases or branching paths?

4. **Multiple players per node:** How many players can connect to one EnergyRuntime? Separate capacity pools?

5. **Energy spike visuals:** How to show that towers lost power? (Flickering, fading, status icons?)

6. **Starting gold amount:** How much gold at game start?

7. **Save system:** PlayerPrefs (simple), JSON file (better), or cloud save (multiplayer)?

8. **Tower repair mechanic:** Repair per HP or full repair? Repair cooldown?

---

## References

- **Concept.md** - Game concept and mechanics
- **Plan.md** - Implementation roadmap
- **PlayerController.cs** - Existing movement implementation
- **GridManager.cs** - Existing grid system