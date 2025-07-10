using Unity.Entities;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;

namespace MarbleMaker.Core.ECS
{
    /// <summary>
    /// Detects collisions between marbles and creates debris
    /// From ECS docs: "CollisionDetectSystem (ScheduleParallel) • NativeMultiHashMap<CellIndex, MarbleHandle> • If >1 marble OR cell has DebrisTag ➜ destroy marbles, add BlockDebris entity"
    /// From GDD: "When two marbles try to occupy the same cell on any tick, both destruct"
    /// </summary>
    [UpdateInGroup(typeof(MotionGroup))]
    [UpdateAfter(typeof(MarbleIntegrateSystem))]
    [BurstCompile]
    public partial struct CollisionDetectSystem : ISystem
    {
        private NativeMultiHashMap<int3, MarbleHandle> cellMarbleMap;
        private NativeHashSet<int3> debrisCells;
        private NativeList<Entity> marblestoDestroy;
        private NativeList<int3> newDebrisPositions;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // System requires marble entities to process
            state.RequireForUpdate<MarbleTag>();
            
            // Initialize collections
            cellMarbleMap = new NativeMultiHashMap<int3, MarbleHandle>(10000, Allocator.Persistent);
            debrisCells = new NativeHashSet<int3>(1000, Allocator.Persistent);
            marblestoDestroy = new NativeList<Entity>(1000, Allocator.Persistent);
            newDebrisPositions = new NativeList<int3>(1000, Allocator.Persistent);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            // Clean up native collections
            if (cellMarbleMap.IsCreated) cellMarbleMap.Dispose();
            if (debrisCells.IsCreated) debrisCells.Dispose();
            if (marblestoDestroy.IsCreated) marblestoDestroy.Dispose();
            if (newDebrisPositions.IsCreated) newDebrisPositions.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Clear previous frame data
            cellMarbleMap.Clear();
            debrisCells.Clear();
            marblestoDestroy.Clear();
            newDebrisPositions.Clear();

            // Build debris cell set
            var buildDebrisJob = new BuildDebrisCellsJob
            {
                debrisCells = debrisCells.AsParallelWriter()
            };
            state.Dependency = buildDebrisJob.ScheduleParallel(state.Dependency);

            // Build marble position map
            var buildMarbleMapJob = new BuildMarbleMapJob
            {
                cellMarbleMap = cellMarbleMap.AsParallelWriter()
            };
            state.Dependency = buildMarbleMapJob.ScheduleParallel(state.Dependency);

            // Wait for jobs to complete before collision detection
            state.Dependency.Complete();

            // Detect collisions
            var detectCollisionsJob = new DetectCollisionsJob
            {
                cellMarbleMap = cellMarbleMap,
                debrisCells = debrisCells,
                marblesToDestroy = marblestoDestroy,
                newDebrisPositions = newDebrisPositions
            };
            state.Dependency = detectCollisionsJob.Schedule(state.Dependency);
            state.Dependency.Complete();

            // Apply collision results
            if (marblestoDestroy.Length > 0 || newDebrisPositions.Length > 0)
            {
                var ecbSingleton = SystemAPI.GetSingleton<BeginFixedStepSimulationEntityCommandBufferSystem.Singleton>();
                var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

                ApplyCollisionResults(ecb);
            }
        }

        [BurstCompile]
        private void ApplyCollisionResults(EntityCommandBuffer ecb)
        {
            // Destroy collided marbles
            for (int i = 0; i < marblestoDestroy.Length; i++)
            {
                ecb.DestroyEntity(marblestoDestroy[i]);
            }

            // Create debris entities
            for (int i = 0; i < newDebrisPositions.Length; i++)
            {
                var debrisEntity = ecb.CreateEntity();
                ecb.AddComponent(debrisEntity, new CellIndex(newDebrisPositions[i]));
                ecb.AddComponent(debrisEntity, new DebrisTag());
            }
        }

        /// <summary>
        /// Job to build the debris cells hash set
        /// </summary>
        [BurstCompile]
        private partial struct BuildDebrisCellsJob : IJobEntity
        {
            [WriteOnly] public NativeHashSet<int3>.ParallelWriter debrisCells;

            [BurstCompile]
            public void Execute(in CellIndex cellIndex, in DebrisTag debrisTag)
            {
                debrisCells.Add(cellIndex.xyz);
            }
        }

        /// <summary>
        /// Job to build the marble position map
        /// NativeMultiHashMap<CellIndex, MarbleHandle> as specified in ECS docs
        /// </summary>
        [BurstCompile]
        private partial struct BuildMarbleMapJob : IJobEntity
        {
            [WriteOnly] public NativeMultiHashMap<int3, MarbleHandle>.ParallelWriter cellMarbleMap;

            [BurstCompile]
            public void Execute(Entity entity, in CellIndex cellIndex, in MarbleTag marbleTag)
            {
                var marbleHandle = new MarbleHandle(entity, entity.Index);
                cellMarbleMap.Add(cellIndex.xyz, marbleHandle);
            }
        }

        /// <summary>
        /// Job to detect collisions and mark entities for destruction
        /// Implements: "If >1 marble OR cell has DebrisTag ➜ destroy marbles, add BlockDebris entity"
        /// </summary>
        [BurstCompile]
        private struct DetectCollisionsJob : IJob
        {
            [ReadOnly] public NativeMultiHashMap<int3, MarbleHandle> cellMarbleMap;
            [ReadOnly] public NativeHashSet<int3> debrisCells;
            [WriteOnly] public NativeList<Entity> marblesToDestroy;
            [WriteOnly] public NativeList<int3> newDebrisPositions;

            [BurstCompile]
            public void Execute()
            {
                // Get all unique cell positions
                var allCells = cellMarbleMap.GetKeyArray(Allocator.Temp);

                for (int i = 0; i < allCells.Length; i++)
                {
                    var cellPos = allCells[i];
                    var marblesInCell = new NativeList<MarbleHandle>(Allocator.Temp);

                    // Collect all marbles in this cell
                    if (cellMarbleMap.TryGetFirstValue(cellPos, out var marbleHandle, out var iterator))
                    {
                        do
                        {
                            marblesInCell.Add(marbleHandle);
                        }
                        while (cellMarbleMap.TryGetNextValue(out marbleHandle, ref iterator));
                    }

                    // Check collision conditions
                    bool hasCollision = false;

                    // Condition 1: More than 1 marble in cell
                    if (marblesInCell.Length > 1)
                    {
                        hasCollision = true;
                    }
                    // Condition 2: Cell has debris and marbles
                    else if (marblesInCell.Length > 0 && debrisCells.Contains(cellPos))
                    {
                        hasCollision = true;
                    }

                    // Apply collision results
                    if (hasCollision)
                    {
                        // Destroy all marbles in this cell
                        for (int j = 0; j < marblesInCell.Length; j++)
                        {
                            marblesToDestroy.Add(marblesInCell[j].entity);
                        }

                        // Create debris if this cell doesn't already have debris
                        if (!debrisCells.Contains(cellPos))
                        {
                            newDebrisPositions.Add(cellPos);
                        }
                    }

                    marblesInCell.Dispose();
                }

                allCells.Dispose();
            }
        }
    }
}