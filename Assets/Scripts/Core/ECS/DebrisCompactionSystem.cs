using Unity.Entities;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;

namespace MarbleMaker.Core.ECS
{
    /// <summary>
    /// Compacts marble arrays and frees ID pool after collisions
    /// From ECS docs: "DebrisCompactionSystem (ScheduleParallel) • Remove dead marbles; free id pool"
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(MotionGroup))]
    [UpdateAfter(typeof(CollisionDetectSystem))]
    [BurstCompile]
    public partial struct DebrisCompactionSystem : ISystem
    {
        private NativeList<Entity> entitiesToRemove;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // Initialize collections for compaction
            int initialCapacity = 1024;
            int expectedPeak = 10000; // from design doc
            
            if (!entitiesToRemove.IsCreated) 
            {
                entitiesToRemove = new NativeList<Entity>(initialCapacity, Allocator.Persistent);
                entitiesToRemove.Capacity = math.max(entitiesToRemove.Capacity, expectedPeak);
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            // Clean up native collections
            if (entitiesToRemove.IsCreated) 
                entitiesToRemove.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Clear previous frame data
            entitiesToRemove.Clear();

            // Collect entities marked for destruction
            var collectDeadEntitiesJob = new CollectDeadEntitiesJob
            {
                entitiesToRemove = entitiesToRemove
            };

            // This job would typically look for entities with a "DestroyedTag" or similar
            // Since CollisionDetectSystem already destroys entities via ECB,
            // this system mainly handles any remaining cleanup tasks

            // Schedule the compaction job
            state.Dependency = collectDeadEntitiesJob.Schedule(state.Dependency);
            state.Dependency.Complete();

            // Clean up any remaining references or perform additional compaction
            if (entitiesToRemove.Length > 0)
            {
                PerformCompaction();
            }
        }

        [BurstCompile]
        private void PerformCompaction()
        {
            // Free ID pool entries
            // In a full implementation, this would return marble IDs to a pool
            // for reuse to avoid memory fragmentation
            
            // For now, we just clear the collection
            entitiesToRemove.Clear();
        }

        /// <summary>
        /// Job to collect entities that need to be removed during compaction
        /// This would typically run after collision detection has marked entities for destruction
        /// </summary>
        [BurstCompile]
        private struct CollectDeadEntitiesJob : IJob
        {
            [WriteOnly] public NativeList<Entity> entitiesToRemove;

            [BurstCompile]
            public void Execute()
            {
                // In a full implementation, this would:
                // 1. Scan for entities with DestroyedTag or IsDestroyed flag
                // 2. Collect their IDs for pool recycling
                // 3. Update any marble count statistics
                // 
                // Since our CollisionDetectSystem already handles entity destruction
                // via EntityCommandBuffer, this is primarily for cleanup and statistics
            }
        }
    }

    /// <summary>
    /// Simple marble ID pool for recycling destroyed marble IDs
    /// Helps prevent memory fragmentation with frequent marble creation/destruction
    /// </summary>
    [BurstCompile]
    public struct MarbleIdPool
    {
        private NativeQueue<int> availableIds;
        private int nextNewId;

        public MarbleIdPool(Allocator allocator)
        {
            availableIds = new NativeQueue<int>(allocator);
            nextNewId = 0;
        }

        public void Dispose()
        {
            if (availableIds.IsCreated)
                availableIds.Dispose();
        }

        /// <summary>
        /// Gets an available marble ID (recycled or new)
        /// </summary>
        /// <returns>Available marble ID</returns>
        public int GetId()
        {
            if (availableIds.TryDequeue(out int recycledId))
            {
                return recycledId;
            }
            
            return nextNewId++;
        }

        /// <summary>
        /// Returns a marble ID to the pool for reuse
        /// </summary>
        /// <param name="id">ID to return to pool</param>
        public void ReturnId(int id)
        {
            availableIds.Enqueue(id);
        }

        /// <summary>
        /// Gets the total number of IDs that have been allocated
        /// </summary>
        public int TotalAllocated => nextNewId;

        /// <summary>
        /// Gets the number of IDs available for reuse
        /// </summary>
        public int AvailableCount => availableIds.Count;
    }
}