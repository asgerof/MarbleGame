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
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // System will run every frame to rebuild caches
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // ------------------------------------------------------------------
            // 1. Clear caches on the main thread (O(capacity) once per frame)
            // ------------------------------------------------------------------
#if ENABLE_PROFILER
            using (new ProfilerMarker("LookupCache.Clear").Auto())
#endif
            {
                ECSLookups.SplittersByCell.Clear();
                ECSLookups.LiftsByCell.Clear();
                ECSLookups.GoalsByCell.Clear();
                ECSLookups.MarblesByCell.Clear();
            }

            // ------------------------------------------------------------------
            // 2. Schedule population passes — one Entities.ForEach per type
            // ------------------------------------------------------------------
            var splittersWriter = ECSLookups.SplittersByCell.AsParallelWriter();
            state.Dependency = SystemAPI
                .ForEach((Entity e, in CellIndex cell, in SplitterState _) =>
                {
                    splittersWriter.TryAdd(ECSUtils.PackCellKey(cell.xyz), e);
                })
                .ScheduleParallel(state.Dependency);

            var liftsWriter = ECSLookups.LiftsByCell.AsParallelWriter();
            state.Dependency = SystemAPI
                .ForEach((Entity e, in CellIndex cell, in LiftState _) =>
                {
                    liftsWriter.TryAdd(ECSUtils.PackCellKey(cell.xyz), e);
                })
                .ScheduleParallel(state.Dependency);

            var goalsWriter = ECSLookups.GoalsByCell.AsParallelWriter();
            state.Dependency = SystemAPI
                .ForEach((Entity e, in CellIndex cell, in GoalPad _) =>
                {
                    goalsWriter.Add(ECSUtils.PackCellKey(cell.xyz), e);
                })
                .ScheduleParallel(state.Dependency);

            var marblesWriter = ECSLookups.MarblesByCell.AsParallelWriter();
            state.Dependency = SystemAPI
                .ForEach((Entity e, in CellIndex cell, in MarbleTag _) =>
                {
                    marblesWriter.Add(ECSUtils.PackCellKey(cell.xyz), e);
                })
                .ScheduleParallel(state.Dependency);
        }
    }


} 