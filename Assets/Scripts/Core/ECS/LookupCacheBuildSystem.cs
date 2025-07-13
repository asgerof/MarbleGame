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
#endif
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // System will run every frame to rebuild caches
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
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
            // 2. Schedule population passes (run in parallel)
            // ------------------------------------------------------------------
            var splittersWriter = ECSLookups.SplittersByCell.AsParallelWriter();
            var splitterHandle = SystemAPI
                .ForEach((Entity e, in CellIndex c, in SplitterState _) =>
                {
                    var key = ECSUtils.PackCellKey(c.xyz);
                    if (!splittersWriter.TryAdd(key, e))
                        RecordDuplicate(key, 0);
                })
                .ScheduleParallel(state.Dependency);

            var liftsWriter = ECSLookups.LiftsByCell.AsParallelWriter();
            var liftHandle = SystemAPI
                .ForEach((Entity e, in CellIndex c, in LiftState _) =>
                {
                    var key = ECSUtils.PackCellKey(c.xyz);
                    if (!liftsWriter.TryAdd(key, e))
                        RecordDuplicate(key, 1);
                })
                .ScheduleParallel(state.Dependency);

            var goalsWriter = ECSLookups.GoalsByCell.AsParallelWriter();
            var goalHandle = SystemAPI
                .ForEach((Entity e, in CellIndex c, in GoalPad _) =>
                    goalsWriter.Add(ECSUtils.PackCellKey(c.xyz), e))
                .ScheduleParallel(state.Dependency);

            var marblesWriter = ECSLookups.MarblesByCell.AsParallelWriter();
            var marbleHandle = SystemAPI
                .ForEach((Entity e, in CellIndex c, in MarbleTag _) =>
                    marblesWriter.Add(ECSUtils.PackCellKey(c.xyz), e))
                .ScheduleParallel(state.Dependency);

            // Combine all four handles so later systems see a fully built cache
            state.Dependency = JobHandle.CombineDependencies(
                splitterHandle, liftHandle, goalHandle, marbleHandle);
        }
    }


} 