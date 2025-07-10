using Unity.Entities;
using Unity.Mathematics;

namespace MarbleMaker.Core.ECS
{
    /// <summary>
    /// Fixed-point position component (Q32.32 fixed-point)
    /// From ECS docs: "TranslationFP, VelocityFP, AccelerationFP, CellIndex, MarbleTag (32 B aligned)"
    /// </summary>
    public struct TranslationFP : IComponentData
    {
        public long value;                 // Q32.32 fixed-point
        
        public static TranslationFP FromFloat(float f) => new() { value = (long)(f * 4294967296f) };
        public float ToFloat() => (float)value / 4294967296f;
        
        public static TranslationFP Zero => new() { value = 0 };
    }

    /// <summary>
    /// Fixed-point velocity component (Q32.32 fixed-point)
    /// </summary>
    public struct VelocityFP : IComponentData
    {
        public long value;                 // Q32.32 fixed-point
        
        public static VelocityFP FromFloat(float f) => new() { value = (long)(f * 4294967296f) };
        public float ToFloat() => (float)value / 4294967296f;
        
        public static VelocityFP Zero => new() { value = 0 };
    }

    /// <summary>
    /// Fixed-point acceleration component (Q32.32 fixed-point)
    /// </summary>
    public struct AccelerationFP : IComponentData
    {
        public long value;                 // Q32.32 fixed-point
        
        public static AccelerationFP FromFloat(float f) => new() { value = (long)(f * 4294967296f) };
        public float ToFloat() => (float)value / 4294967296f;
        
        public static AccelerationFP Zero => new() { value = 0 };
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
    /// Generic module state component
    /// From ECS docs: "ModuleState<T>"
    /// </summary>
    public struct ModuleState<T> : IComponentData where T : unmanaged
    {
        public T state;
    }

    /// <summary>
    /// Splitter module state
    /// </summary>
    public struct SplitterState
    {
        public int currentExit;     // 0 or 1 for two-way splitter
        public bool overrideExit;   // true if player clicked to override
        public int overrideValue;   // the overridden exit value
    }

    /// <summary>
    /// Collector module state
    /// </summary>
    public struct CollectorState
    {
        public int queuedMarbles;   // number of marbles in queue
        public int upgradeLevel;    // 0=basic, 1=FIFO, 2=burst control
        public int burstSize;       // for level 2 upgrade
    }

    /// <summary>
    /// Lift module state
    /// </summary>
    public struct LiftState
    {
        public bool isActive;       // true if lift is moving
        public int currentHeight;   // current height position
        public int targetHeight;    // target height position
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
    /// Used in NativeMultiHashMap<CellIndex, MarbleHandle>
    /// </summary>
    public struct MarbleHandle
    {
        public Entity entity;
        public int marbleId;
        
        public MarbleHandle(Entity e, int id)
        {
            entity = e;
            marbleId = id;
        }
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
}