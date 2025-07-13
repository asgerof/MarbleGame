using Unity.Entities;
using Unity.Mathematics;
using Unity.Burst;
using MarbleGame.Core.Math;

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

    /// <summary>
    /// Grid cell index component
    /// From ECS docs: "CellIndex : IComponentData { public int3 xyz; // grid coords }"
    /// </summary>
    public struct CellIndex : IComponentData
    {
        public int3 xyz;                   // grid coords
        
        public CellIndex(int x, int y, int z)
        {
            xyz = new int3(x, y, z);
        }
        
        public CellIndex(int3 position)
        {
            xyz = position;
        }
        
        public static CellIndex Zero => new(0, 0, 0);
    }

    /// <summary>
    /// Tag component for marble entities
    /// From ECS docs: "Marble archetype contains MarbleTag"
    /// </summary>
    public struct MarbleTag : IComponentData
    {
        // Tag component - no data needed
    }

    /// <summary>
    /// Tag component for debris entities
    /// From ECS docs: "BlockDebris: CellIndex, DebrisTag"
    /// </summary>
    public struct DebrisTag : IComponentData
    {
        // Tag component - no data needed
    }

    /// <summary>
    /// Reference to module blob asset
    /// From ECS docs: "ModuleRef (blob), ModuleState<T>"
    /// </summary>
    public struct ModuleRef : IComponentData
    {
        public BlobAssetReference<ModuleBlobData> value;
    }

    /// <summary>
    /// Reference to connector blob asset
    /// From ECS docs: "ConnectorRef (blob)"
    /// </summary>
    public struct ConnectorRef : IComponentData
    {
        public BlobAssetReference<ConnectorBlobData> value;
    }

    /// <summary>
    /// Concrete module state component for splitters
    /// From ECS docs: "ModuleState<T>" - replaced with concrete components to avoid archetype explosion
    /// </summary>
    public struct SplitterState : IComponentData
    {
        public byte NextLaneIndex;   // round-robin pointer
        public bool OverrideEnabled; // set by click
    }

    /// <summary>
    /// Concrete module state component for collectors
    /// From collector docs: "CollectorState with circular buffer indices"
    /// </summary>
    public struct CollectorState : IComponentData
    {
        public uint Head;   // dequeue ptr
        public uint Tail;   // enqueue ptr
        public uint CapacityMask; // (capacity-1) – MUST be power of two
    }

    /// <summary>
    /// Concrete module state component for lifts
    /// </summary>
    public struct LiftState : IComponentData
    {
        public bool isActive;       // true if lift is moving
        public int currentHeight;   // current height position
        public int targetHeight;    // target height position
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
    /// Used in NativeMultiHashMap<ulong, MarbleHandle> - stores full entity for safe access
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
    /// Goal pad component for marble destruction and scoring
    /// </summary>
    public struct GoalPad : IComponentData
    {
        public int3 goalPosition;       // Grid position of goal
        public int coinReward;          // Coins awarded per marble
        public int marblesCollected;    // Total marbles collected by this goal
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
}