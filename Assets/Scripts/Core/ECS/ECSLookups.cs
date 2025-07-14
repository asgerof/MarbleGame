using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;
using System.Runtime.CompilerServices;
using System.Collections.Generic;

namespace MarbleMaker.Core.ECS
{
    /// <summary>
    /// Unified lookup API for ECS entity queries with deterministic behavior
    /// </summary>
    public static class ECSLookups
    {
        // -----------------------------------------------------------------------------
        // Burst-friendly comparer (no interface call)
        // -----------------------------------------------------------------------------
        internal struct EntityIndexComparer
        {
            // NativeArray.Sort(...) expects a static Compare(ref, ref) method
            public static int Compare(ref Entity a, ref Entity b)
            {
                return a.Index.CompareTo(b.Index);
            }
        }
        // Static caches for fast lookups
        [NativeDisableUnsafePtrRestriction] static NativeParallelHashMap<ulong, Entity> _splittersByCell;
        [NativeDisableUnsafePtrRestriction] static NativeParallelHashMap<ulong, Entity> _liftsByCell;
        [NativeDisableUnsafePtrRestriction] static NativeParallelMultiHashMap<ulong, Entity> _goalsByCell;
        [NativeDisableUnsafePtrRestriction] static NativeParallelMultiHashMap<ulong, Entity> _marblesByCell;

        /// <summary>
        /// Module initializer - runs once per domain reload
        /// </summary>
        [ModuleInitializer]
        public static void Init()
        {
            _splittersByCell = new NativeParallelHashMap<ulong, Entity>(1024, Allocator.Persistent);
            _liftsByCell = new NativeParallelHashMap<ulong, Entity>(1024, Allocator.Persistent);
            _goalsByCell = new NativeParallelMultiHashMap<ulong, Entity>(1024, Allocator.Persistent);
            _marblesByCell = new NativeParallelMultiHashMap<ulong, Entity>(4096, Allocator.Persistent);
        }

        /// <summary>
        /// Dispose all static caches
        /// </summary>
        public static void Dispose()
        {
            if (_splittersByCell.IsCreated) _splittersByCell.Dispose();
            if (_liftsByCell.IsCreated) _liftsByCell.Dispose();
            if (_goalsByCell.IsCreated) _goalsByCell.Dispose();
            if (_marblesByCell.IsCreated) _marblesByCell.Dispose();
        }

        // Public accessors for cache maps (needed by LookupCacheBuildSystem)
        public static NativeParallelHashMap<ulong, Entity> SplittersByCell => _splittersByCell;
        public static NativeParallelHashMap<ulong, Entity> LiftsByCell => _liftsByCell;
        public static NativeParallelMultiHashMap<ulong, Entity> GoalsByCell => _goalsByCell;
        public static NativeParallelMultiHashMap<ulong, Entity> MarblesByCell => _marblesByCell;

        // Lookup API methods

        /// <summary>
        /// Try to get a splitter at the specified cell
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetSplitterAtCell(in int3 cell, out Entity splitter)
        {
            ulong key = ECSUtils.PackCellKey(cell);
            return _splittersByCell.TryGetValue(key, out splitter);
        }

        /// <summary>
        /// Try to get a lift at the specified cell
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetLiftAtCell(in int3 cell, out Entity lift)
        {
            ulong key = ECSUtils.PackCellKey(cell);
            return _liftsByCell.TryGetValue(key, out lift);
        }

        /// <summary>
        /// Try to get a goal at the specified cell
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetGoalAtCell(in int3 cell, out Entity goal)
        {
            goal = default;
            ulong key = ECSUtils.PackCellKey(cell);

            if (_goalsByCell.TryGetFirstValue(key, out var candidate, out var it))
            {
                Entity best = candidate;

                while (_goalsByCell.TryGetNextValue(out candidate, ref it))
                    if (candidate.Index < best.Index)
                        best = candidate;

                goal = best;
                return true;
            }
            return false;
        }



        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetMarbleAtCell(in int3 cell, out Entity marble)
        {
            marble = default;
            ulong key = ECSUtils.PackCellKey(cell);

            if (_marblesByCell.TryGetFirstValue(key, out var candidate, out var it))
            {
                Entity best = candidate;

                while (_marblesByCell.TryGetNextValue(out candidate, ref it))
                    if (candidate.Index < best.Index)
                        best = candidate;

                marble = best;
                return true;
            }
            return false;
        }

        public static bool TryGetMarblesAtCell(in int3 cell, NativeList<Entity> results)
        {
            results.Clear();
            ulong key = ECSUtils.PackCellKey(cell);

            if (_marblesByCell.TryGetFirstValue(key, out var e, out var it))
            {
                do { results.Add(e); }
                while (_marblesByCell.TryGetNextValue(out e, ref it));

                results.AsNativeArray().Sort<Entity, EntityIndexComparer>();          // Deterministic order
                return true;
            }
            return false;
        }

        /// <summary>
        /// Convenience wrapper so caller doesn't have to unpack CellIndex.
        /// `cellLookup` *must* be read-only.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetMarbleAtSplitter(
            in Entity splitter,
            in ComponentLookup<CellIndex> cellLookup,
            out Entity marble)
        {
            if (!cellLookup.HasComponent(splitter))
            {
                marble = Entity.Null;
                return false;
            }
            return TryGetMarbleAtCell(cellLookup[splitter].xyz, out marble);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetMarbleAtLift(
            in Entity lift,
            in ComponentLookup<CellIndex> cellLookup,
            out Entity marble)
        {
            if (!cellLookup.HasComponent(lift))
            {
                marble = Entity.Null;
                return false;
            }
            return TryGetMarbleAtCell(cellLookup[lift].xyz, out marble);
        }


    }
} 