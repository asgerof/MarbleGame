using Unity.Collections;
using Unity.Entities;

namespace MarbleMaker.Core.ECS
{
    /// <summary>
    /// Represents a fault that occurred during job execution
    /// </summary>
    public struct Fault
    {
        public int SystemId;     // e.g. math.hash(nameof(CollisionDetectSystem))
        public int Code;         // system-specific enum/int
    }

    /// <summary>
    /// Buffer component for collecting faults from jobs
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct FaultBuffer : IBufferElementData
    {
        public Fault Value;
    }
} 