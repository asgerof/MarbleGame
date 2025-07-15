using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;

namespace MarbleMaker.Core.ECS
{
    /// <summary>
    /// System that rebuilds lookup caches every frame for deterministic entity queries
    /// </summary>
    [UpdateInGroup(typeof(LookupCacheGroup))]
    [BurstCompile]
    public partial struct LookupCacheBuildSystem : ISystem
    {
#if ENABLE_PROFILER
        static readonly ProfilerMarker _clearMarker = new ProfilerMarker("LookupCache.Clear");
        static readonly ProfilerMarker _populateSplittersMarker = new ProfilerMarker("LookupCache.PopulateSplitters");
        static readonly ProfilerMarker _populateLiftsMarker = new ProfilerMarker("LookupCache.PopulateLifts");
        static readonly ProfilerMarker _populateGoalsMarker = new ProfilerMarker("LookupCache.PopulateGoals");
        static readonly ProfilerMarker _populateMarblesMarker = new ProfilerMarker("LookupCache.PopulateMarbles");
#endif
        
        // High-water mark optimization to reduce O(N) resize operations
        private static int _splitterHighWaterMark = 1024;
        private static int _liftHighWaterMark = 1024;  
        private static int _goalHighWaterMark = 1024;
        private static int _marbleHighWaterMark = 4096;
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // System will run every frame to rebuild caches
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Run only inside the primary Game/Simulation world
            if ((state.WorldUnmanaged.Flags & WorldFlags.Game) == 0)
                return;

            void RecordDuplicate(in ulong key, byte type)   // 0 = Splitter, 1 = Lift
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                Unity.Assertions.Assert.IsTrue(false, 
                    $"Duplicate {(type == 0 ? "splitter" : "lift")} at cellKey {key}");
#endif
                // Future: push to a FaultQueue if we wire one in
            }

            // ------------------------------------------------------------------
            // 1. Clear caches on the main thread (O(capacity) once per frame)
            // ------------------------------------------------------------------
#if ENABLE_PROFILER
            using (_clearMarker.Auto())
#endif
            {
                ECSLookups.SplittersByCell.Clear();
                ECSLookups.LiftsByCell.Clear();
                ECSLookups.GoalsByCell.Clear();
                ECSLookups.MarblesByCell.Clear();
            }

            // ------------------------------------------------------------------
            // 1b. Ensure capacities match this frame's entity counts
            // ------------------------------------------------------------------
            // Fixed: Use SystemAPI.QueryBuilder().Build().CalculateEntityCount() instead of deprecated API
            int splitterCount = SystemAPI.QueryBuilder().WithAll<CellIndex, SplitterState>().Build().CalculateEntityCount();
            int liftCount     = SystemAPI.QueryBuilder().WithAll<CellIndex, LiftState>().Build().CalculateEntityCount();
            int goalCount     = SystemAPI.QueryBuilder().WithAll<CellIndex, GoalPad>().Build().CalculateEntityCount();
            int marbleCount   = SystemAPI.QueryBuilder().WithAll<CellIndex, MarbleTag>().Build().CalculateEntityCount();

            // Use high-water marks to avoid frequent resizing
            // Fixed: Use Capacity property instead of deprecated EnsureCapacity method
            if (splitterCount > _splitterHighWaterMark)
            {
                _splitterHighWaterMark = splitterCount * 2;
                ECSLookups.SplittersByCell.Capacity = math.max(ECSLookups.SplittersByCell.Capacity, _splitterHighWaterMark);
            }
            if (liftCount > _liftHighWaterMark)
            {
                _liftHighWaterMark = liftCount * 2;
                ECSLookups.LiftsByCell.Capacity = math.max(ECSLookups.LiftsByCell.Capacity, _liftHighWaterMark);
            }
            if (goalCount > _goalHighWaterMark)
            {
                _goalHighWaterMark = goalCount * 2;
                ECSLookups.GoalsByCell.Capacity = math.max(ECSLookups.GoalsByCell.Capacity, _goalHighWaterMark);
            }
            if (marbleCount > _marbleHighWaterMark)
            {
                _marbleHighWaterMark = marbleCount * 2;
                ECSLookups.MarblesByCell.Capacity = math.max(ECSLookups.MarblesByCell.Capacity, _marbleHighWaterMark);
            }

            // ------------------------------------------------------------------
            // 2. Schedule population passes (run in parallel)
            // ------------------------------------------------------------------
            JobHandle splitterHandle, liftHandle, goalHandle, marbleHandle;
            
#if ENABLE_PROFILER
            using (_populateSplittersMarker.Auto())
#endif
            {
                var splittersWriter = ECSLookups.SplittersByCell.AsParallelWriter();
                var populateSplittersJob = new PopulateSplittersJob
                {
                    splittersWriter = splittersWriter
                };
                splitterHandle = populateSplittersJob.ScheduleParallel(state.Dependency);
            }

#if ENABLE_PROFILER
            using (_populateLiftsMarker.Auto())
#endif
            {
                var liftsWriter = ECSLookups.LiftsByCell.AsParallelWriter();
                var populateLiftsJob = new PopulateLiftsJob
                {
                    liftsWriter = liftsWriter
                };
                liftHandle = populateLiftsJob.ScheduleParallel(state.Dependency);
            }

#if ENABLE_PROFILER
            using (_populateGoalsMarker.Auto())
#endif
            {
                var goalsWriter = ECSLookups.GoalsByCell.AsParallelWriter();
                var populateGoalsJob = new PopulateGoalsJob
                {
                    goalsWriter = goalsWriter
                };
                goalHandle = populateGoalsJob.ScheduleParallel(state.Dependency);
            }

#if ENABLE_PROFILER
            using (_populateMarblesMarker.Auto())
#endif
            {
                var marblesWriter = ECSLookups.MarblesByCell.AsParallelWriter();
                var populateMarblesJob = new PopulateMarblesJob
                {
                    marblesWriter = marblesWriter
                };
                marbleHandle = populateMarblesJob.ScheduleParallel(state.Dependency);
            }

            // Combine all four handles so later systems see a fully built cache
            // Fixed: Use NativeArray overload for 4 dependencies instead of individual parameters
            var handles = new NativeArray<JobHandle>(4, Allocator.Temp);
            handles[0] = splitterHandle;
            handles[1] = liftHandle;
            handles[2] = goalHandle;
            handles[3] = marbleHandle;
            state.Dependency = JobHandle.CombineDependencies(handles);
            handles.Dispose();
        }
    }

    /// <summary>
    /// Job to populate splitters by cell lookup
    /// </summary>
    [BurstCompile]
    public partial struct PopulateSplittersJob : IJobEntity
    {
        public NativeParallelHashMap<ulong, Entity>.ParallelWriter splittersWriter;

        public void Execute(Entity entity, in CellIndex cellIndex, in SplitterState splitterState)
        {
            var key = ECSUtils.PackCellKey(cellIndex.xyz);
            splittersWriter.TryAdd(key, entity);
        }
    }

    /// <summary>
    /// Job to populate lifts by cell lookup
    /// </summary>
    [BurstCompile]
    public partial struct PopulateLiftsJob : IJobEntity
    {
        public NativeParallelHashMap<ulong, Entity>.ParallelWriter liftsWriter;

        public void Execute(Entity entity, in CellIndex cellIndex, in LiftState liftState)
        {
            var key = ECSUtils.PackCellKey(cellIndex.xyz);
            liftsWriter.TryAdd(key, entity);
        }
    }

    /// <summary>
    /// Job to populate goals by cell lookup
    /// </summary>
    [BurstCompile]
    public partial struct PopulateGoalsJob : IJobEntity
    {
        public NativeParallelMultiHashMap<ulong, Entity>.ParallelWriter goalsWriter;

        public void Execute(Entity entity, in CellIndex cellIndex, in GoalPad goalPad)
        {
            var key = ECSUtils.PackCellKey(cellIndex.xyz);
            goalsWriter.Add(key, entity);
        }
    }

    /// <summary>
    /// Job to populate marbles by cell lookup
    /// </summary>
    [BurstCompile]
    public partial struct PopulateMarblesJob : IJobEntity
    {
        public NativeParallelMultiHashMap<ulong, Entity>.ParallelWriter marblesWriter;

        public void Execute(Entity entity, in CellIndex cellIndex, in MarbleTag marbleTag)
        {
            var key = ECSUtils.PackCellKey(cellIndex.xyz);
            marblesWriter.Add(key, entity);
        }
    }
} 