using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;
using MarbleMaker.Core.Math;

namespace MarbleMaker.Core.ECS
{
    /// <summary>
    /// Fixed-point position component (Q32.32 fixed-point)
    /// From ECS docs: "PositionComponent, VelocityComponent, AccelerationComponent, CellIndex, MarbleTag (32 B aligned)"
    /// </summary>
    public struct PositionComponent : IComponentData
    {
        public Fixed32x3 Value;
    }

    /// <summary>
    /// Fixed-point velocity component (Q32.32 fixed-point)
    /// </summary>
    public struct VelocityComponent : IComponentData
    {
        public Fixed32x3 Value;
    }

    /// <summary>
    /// Fixed-point acceleration component (Q32.32 fixed-point)
    /// </summary>
    public struct AccelerationComponent : IComponentData
    {
        public Fixed32x3 Value;
    }

    // Legacy component names for backwards compatibility with test files
    /// <summary>
    /// Legacy fixed-point translation component (alias for position)
    /// </summary>
    public struct TranslationFP : IComponentData
    {
        public Fixed32x3 value;
        
        public TranslationFP(Fixed32x3 position)
        {
            value = position;
        }
    }
    
    /// <summary>
    /// Legacy fixed-point velocity component
    /// </summary>
    public struct VelocityFP : IComponentData
    {
        public Fixed32x3 value;
        
        public VelocityFP(Fixed32x3 velocity)
        {
            value = velocity;
        }
    }
    
    /// <summary>
    /// Legacy fixed-point acceleration component
    /// </summary>
    public struct AccelerationFP : IComponentData
    {
        public Fixed32x3 value;
        
        public AccelerationFP(Fixed32x3 acceleration)
        {
            value = acceleration;
        }
    }

    /// <summary>
    /// A marble's position information 
    /// Uses Fixed32 for determinism as per TDD Section 2
    /// </summary>
    [BurstCompile]
    public struct CellIndex : IComponentData
    {
        public int3 Value;

        public CellIndex(int3 xyz)
        {
            Value = xyz;
        }

        public int3 xyz
        {
            readonly get => Value;
            set => Value = value;
        }
    }

    /// <summary>
    /// A marble's velocity information
    /// Uses Fixed32 for determinism as per TDD Section 2
    /// </summary>
    [BurstCompile]
    public struct MarbleVelocity : IComponentData
    {
        public Fixed32 X;
        public Fixed32 Y;
        public Fixed32 Z;
    }
    
    /// <summary>
    /// A marble's position information 
    /// Uses Fixed32 for determinism as per TDD Section 2
    /// </summary>
    [BurstCompile]
    public struct MarblePosition : IComponentData
    {
        public Fixed32 X;
        public Fixed32 Y;
        public Fixed32 Z;
    }

    /// <summary>
    /// Tag component for marble entities
    /// </summary>
    [BurstCompile]
    public struct MarbleTag : IComponentData
    {
        // Tag component - no data needed
    }

    /// <summary>
    /// Component for marble collision detection
    /// </summary>
    [BurstCompile]
    public struct MarbleCollision : IComponentData
    {
        public bool hasCollided;
        public Entity collidedWith;
    }

    /// <summary>
    /// Component for marble destruction
    /// </summary>
    [BurstCompile]
    public struct MarbleDestruction : IComponentData
    {
        public bool shouldDestroy;
    }

    /// <summary>
    /// Health component for game entities
    /// </summary>
    [BurstCompile]
    public struct Health : IComponentData
    {
        public int currentHealth;
        public int maxHealth;
    }

    /// <summary>
    /// Component for entities that can cause damage
    /// </summary>
    [BurstCompile]
    public struct DamageSource : IComponentData
    {
        public int damageAmount;
        public bool isActive;
    }

    /// <summary>
    /// Component for tracking entity lifetime
    /// </summary>
    [BurstCompile]
    public struct TimeToLive : IComponentData
    {
        public float remainingTime;
    }

    /// <summary>
    /// Component for entities that can be interacted with
    /// </summary>
    [BurstCompile]
    public struct Interactable : IComponentData
    {
        public bool isActive;
        public int interactionType;
    }

    /// <summary>
    /// Component for tracking marble collisions
    /// </summary>
    [BurstCompile]
    public struct CollisionTracker : IComponentData
    {
        public int collisionCount;
        public float lastCollisionTime;
    }

    /// <summary>
    /// Component for block debris spawned by collisions
    /// </summary>
    [BurstCompile]
    public struct BlockDebris : IComponentData
    {
        public bool isActive;
        public float spawnTime;
    }

    /// <summary>
    /// Component for goal pad entities
    /// </summary>
    [BurstCompile]
    public struct GoalPad : IComponentData
    {
        public bool isActive;
        public int targetScore;
        public int3 goalPosition;
        public int coinReward;
        public int marblesCollected;
    }

    /// <summary>
    /// Tag component for splitter modules
    /// </summary>
    public struct SplitterTag : IComponentData
    {
        // Tag component - no data needed
    }

    /// <summary>
    /// Tag component for collector modules
    /// </summary>
    public struct CollectorTag : IComponentData
    {
        // Tag component - no data needed
    }

    /// <summary>
    /// Tag component for lift modules
    /// </summary>
    public struct LiftTag : IComponentData
    {
        // Tag component - no data needed
    }

    /// <summary>
    /// Tag component for marbles in splitter trigger volume
    /// </summary>
    public struct InSplitterTrigger : IComponentData
    {
        // Tag component - no data needed
    }

    /// <summary>
    /// Click action event for interactive modules
    /// From ECS docs: "ClickActionEvent (entity): TargetEntity, ActionId, AbsTick"
    /// </summary>
    public struct ClickActionEvent : IBufferElementData
    {
        public Entity targetEntity;
        public int actionId;        // 0=toggle splitter, 1=pause lift, etc.
        public long absoluteTick;   // tick when action should be applied
    }

    /// <summary>
    /// Blob data for module definitions
    /// From ECS docs: "Blob-assets hold immutable data: footprint sockets, mesh id, base constants"
    /// </summary>
    public struct ModuleBlobData
    {
        public BlobArray<int3> footprintSockets;
        public int meshId;
        public float baseAcceleration;
        public float baseFriction;
        public int moduleType;      // 0=splitter, 1=collector, 2=lift, etc.
    }

    /// <summary>
    /// Blob data for connector definitions
    /// </summary>
    public struct ConnectorBlobData
    {
        public BlobArray<int3> footprintSockets;
        public int meshId;
        public float rampAngle;     // in radians
        public bool isFlatTrack;    // true if friction applies
    }

    /// <summary>
    /// Marble handle for collision detection
    /// Used in NativeParallelMultiHashMap<ulong, MarbleHandle> - stores full entity for safe access
    /// </summary>
    public struct MarbleHandle
    {
        public Entity MarbleEntity;     // Full entity for safe lookup
        public long absoluteTick;       // Tick when marble was created for deterministic ordering
        
        public MarbleHandle(Entity entity, long tick)
        {
            MarbleEntity = entity;
            absoluteTick = tick;
        }
    }

    /// <summary>
    /// Seed spawner component for initial marble spawning
    /// From marble lifecycle docs: "Editor instantiates Seed Spawner entities for every start pad defined in board JSON"
    /// </summary>
    public struct SeedSpawner : IComponentData
    {
        public int3 spawnPosition;      // Grid position to spawn marbles
        public int maxMarbles;          // Maximum marbles this spawner can create (-1 = unlimited)
        public int spawnedCount;        // Number of marbles spawned so far
        public bool isActive;           // Whether spawner is currently active
    }

    /// <summary>
    /// Collector queue element for marble queuing
    /// From collector docs: "DynamicBuffer<CollectorQueueElem> + CollectorState indices"
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct CollectorQueueElem : IBufferElementData
    {
        public Entity marble;
        public long enqueueTick;        // Tick when marble was enqueued for deterministic order
    }

    /// <summary>
    /// Track command buffer data for Editor → ECS bridge
    /// From ECS docs: "TrackCmdBuffer – created by the Editor in Authoring World"
    /// </summary>
    public struct TrackCommand
    {
        public enum CommandType
        {
            PlaceModule,
            PlaceConnector,
            RemovePart,
            Reset
        }
        
        public CommandType type;
        public int3 position;
        public int partId;
        public int upgradeLevel;
        public int rotation;
    }

    /// <summary>
    /// Utility functions for cell hash and marble operations
    /// </summary>
    [BurstCompile]
    public static class ECSUtils
    {
        /// <summary>
        /// Packs a 3D cell position into a 63-bit key for collision detection
        /// From collision docs: "21 bits per axis -> 63-bit key, supports ±1,048,575-world cells"
        /// </summary>
        [BurstCompile]
        public static ulong PackCellKey(int3 cellPos)
        {
            const ulong MASK = 0x1FFFFFUL;  // 21 bits
            return ((ulong)((uint)cellPos.x & MASK) << 42) |
                   ((ulong)((uint)cellPos.y & MASK) << 21) |
                   ((ulong)((uint)cellPos.z & MASK));
        }

        /// <summary>
        /// Unpacks a 63-bit key back to 3D cell position
        /// </summary>
        [BurstCompile]
        public static int3 UnpackCellKey(ulong packedKey)
        {
            const ulong MASK = 0x1FFFFFUL;
            int x = (int)((packedKey >> 42) & MASK);
            int y = (int)((packedKey >> 21) & MASK);
            int z = (int)(packedKey & MASK);
            
            // Handle negative values (sign extension)
            if (x >= 0x100000) x -= 0x200000;
            if (y >= 0x100000) y -= 0x200000;
            if (z >= 0x100000) z -= 0x200000;
            
            return new int3(x, y, z);
        }

        /// <summary>
        /// Converts fixed-point position to cell index
        /// </summary>
        [BurstCompile]
        public static int3 PositionToCellIndex(in Fixed32x3 pos, long cellSizeFP)
        {
            return new int3(
                (int)(pos.x / cellSizeFP),
                (int)(pos.y / cellSizeFP),
                (int)(pos.z / cellSizeFP));
        }

        /// <summary>
        /// Converts cell index to fixed-point center position
        /// </summary>
        [BurstCompile]
        public static Fixed32x3 CellIndexToPosition(int3 cellIndex)
        {
            // Convert grid cell to world position (center of cell)
            return new Fixed32x3(
                Fixed32.FromFloat(cellIndex.x).Raw + Fixed32.HALF.Raw,
                Fixed32.FromFloat(cellIndex.y).Raw + Fixed32.HALF.Raw,
                Fixed32.FromFloat(cellIndex.z).Raw + Fixed32.HALF.Raw
            );
        }
    }

    /// <summary>
    /// Concrete module state component for splitters
    /// From splitter docs: "SplitterState with round-robin counter + click override"
    /// </summary>
    [BurstCompile]
    public struct SplitterState : IComponentData
    {
        public byte NextLaneIndex;   // round-robin pointer
        public bool OverrideEnabled; // legacy flag
        public bool overrideExit;    // player override active
        public byte currentExit;     // current exit index
        public byte overrideValue;   // override exit value
    }

    /// <summary>
    /// Concrete module state component for collectors
    /// From collector docs: "CollectorState with circular buffer indices"
    /// </summary>
    [BurstCompile]
    public struct CollectorState : IComponentData
    {
        public uint Head;   // dequeue ptr
        public uint Tail;   // enqueue ptr
        public uint CapacityMask; // (capacity-1) – MUST be power of two
        public byte level;  // upgrade level (0=basic, 1=FIFO, 2=burst)
        public uint burstSize; // burst size for level 2

        // Legacy names used by tests
        public uint head;
        public uint tail;
        public uint count;
    }

    /// <summary>
    /// Concrete module state component for lifts
    /// </summary>
    [BurstCompile]
    public struct LiftState : IComponentData
    {
        public bool isActive;       // true if lift is moving
        public int currentHeight;   // current height position
        public int targetHeight;    // target height position
        public byte level;          // upgrade level
    }

    /// <summary>
    /// Tag component for debris entities (broken/destroyed marble pieces)
    /// </summary>
    [BurstCompile]
    public struct DebrisTag : IComponentData
    {
        // Empty tag component - no data needed
    }

    /// <summary>
    /// Reference component for module entities (splitters, lifts, etc.)
    /// Contains reference to the module definition and configuration
    /// </summary>
    [BurstCompile]
    public struct ModuleRef : IComponentData
    {
        public Entity ModuleEntity;  // Reference to the module entity
        public PartType Type;        // Type of module (from PartType enum)
        public byte Level;           // Module level/tier
    }

    /// <summary>
    /// Reference component for connector entities (tracks, tubes, etc.)
    /// Contains reference to the connector definition and configuration
    /// </summary>
    [BurstCompile]
    public struct ConnectorRef : IComponentData
    {
        public Entity ConnectorEntity;  // Reference to the connector entity
        public PartType Type;          // Type of connector (from PartType enum)
        public bool IsFlatTrack;       // Whether this is a flat track connector
    }

    // ------------------------------------------------------------------
    // Singleton components for global state
    // ------------------------------------------------------------------
}