using Unity.Entities;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using MarbleMaker.Core.Math;
using static Unity.Entities.SystemAPI;

namespace MarbleMaker.Core.ECS
{
    /// <summary>
    /// System for calculating marble acceleration based on module/connector physics
    /// From dev feedback: "Consider splitting that into a ModulatedAccelerationSystem so the integrator stays lean"
    /// This system runs before MarbleIntegrateSystem to update AccelerationFP based on current marble position
    /// </summary>
    [UpdateInGroup(typeof(MotionGroup))]
    [UpdateBefore(typeof(MarbleIntegrateSystem))]
    [BurstCompile]
    public partial struct ModulatedAccelerationSystem : ISystem
    {
        // Fixed-point constants for physics calculations (Q32.32 format)
        private static readonly long _baseGravityAccel = Fixed32.FromFloat(0.1f).Raw;
        private static readonly long _baseFrictionAccel = Fixed32.FromFloat(-0.05f).Raw;
        private static readonly long _rampBoostAccel = Fixed32.FromFloat(0.2f).Raw;
        private static readonly long _liftAccel = Fixed32.FromFloat(0.15f).Raw;
        
        // Cached acceleration map keyed by cell index
        private NativeHashMap<ulong, CachedAcceleration> _accelerationCache;
        
        // Component lookups
        private ComponentLookup<ModuleRef> _moduleRefLookup;
        private ComponentLookup<ConnectorRef> _connectorRefLookup;
        private ComponentLookup<CellIndex> _cellIndexLookup;
        
        // Cache invalidation tracking
        private bool _cacheValid;
        private ulong _lastCacheUpdate;
        
        // ECB system reference
        private EndFixedStepSimulationEntityCommandBufferSystem _endSim;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // System requires marble entities to process
            state.RequireForUpdate<MarbleTag>();
            
            // Initialize component lookups
            _moduleRefLookup = state.GetComponentLookup<ModuleRef>(true);
            _connectorRefLookup = state.GetComponentLookup<ConnectorRef>(true);
            _cellIndexLookup = state.GetComponentLookup<CellIndex>(true);
            
            // Initialize acceleration cache
            _accelerationCache = new NativeHashMap<ulong, CachedAcceleration>(1024, Allocator.Persistent);
            _cacheValid = false;
            _lastCacheUpdate = 0;
            
            // Initialize ECB system reference
            _endSim = state.World.GetOrCreateSystemManaged<EndFixedStepSimulationEntityCommandBufferSystem>();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            // Clean up acceleration cache
            if (_accelerationCache.IsCreated)
                _accelerationCache.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Update component lookups
            _moduleRefLookup.Update(ref state);
            _connectorRefLookup.Update(ref state);
            _cellIndexLookup.Update(ref state);
            
            // Check if cache needs rebuilding
            var currentTick = SimulationTick.Current;
            bool needsCacheRebuild = !_cacheValid || (currentTick - _lastCacheUpdate) > 60; // Rebuild every 60 ticks
            
            JobHandle cacheHandle = state.Dependency;
            
            if (needsCacheRebuild)
            {
                // Rebuild acceleration cache
                cacheHandle = RebuildAccelerationCache(state, cacheHandle);
                _cacheValid = true;
                _lastCacheUpdate = currentTick;
            }
            
            // Set up temporary containers
            var accelerationUpdates = new NativeQueue<AccelerationUpdate>(Allocator.TempJob);
            var faultQueue = new NativeQueue<Fault>(Allocator.TempJob);
            
            // Calculate modulated acceleration using cached data
            var modulatedAccelJob = new ModulatedAccelerationJob
            {
                baseGravityAccel = _baseGravityAccel,
                baseFrictionAccel = _baseFrictionAccel,
                accelerationCache = _accelerationCache,
                accelerationUpdates = accelerationUpdates.AsParallelWriter(),
                faultQueue = faultQueue.AsParallelWriter()
            };
            var accelerationHandle = modulatedAccelJob.ScheduleParallel(cacheHandle);
            
            // Apply acceleration updates
            var applyUpdatesJob = new ApplyAccelerationUpdatesJob
            {
                accelerationUpdates = accelerationUpdates,
                ecb = GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged)
            };
            var applyHandle = applyUpdatesJob.Schedule(accelerationHandle);
            
            // Process faults
            var processFaultsJob = new AccelerationProcessFaultsJob
            {
                faults = faultQueue
            };
            var faultHandle = processFaultsJob.Schedule(applyHandle);
            
            // Set final dependency
            state.Dependency = faultHandle;
        }

        /// <summary>
        /// Rebuilds the acceleration cache for all cells with modules/connectors
        /// </summary>
        private JobHandle RebuildAccelerationCache(SystemState state, JobHandle inputDeps)
        {
            // Clear existing cache
            _accelerationCache.Clear();
            
            // Set up temporary containers
            var moduleAccelerations = new NativeQueue<CachedAcceleration>(Allocator.TempJob);
            var connectorAccelerations = new NativeQueue<CachedAcceleration>(Allocator.TempJob);
            
            // Build module acceleration cache
            var buildModuleCacheJob = new BuildModuleCacheJob
            {
                moduleAccelerations = moduleAccelerations.AsParallelWriter(),
                baseGravityAccel = _baseGravityAccel,
                baseFrictionAccel = _baseFrictionAccel,
                rampBoostAccel = _rampBoostAccel,
                liftAccel = _liftAccel
            };
            var moduleHandle = buildModuleCacheJob.ScheduleParallel(inputDeps);
            
            // Build connector acceleration cache
            var buildConnectorCacheJob = new BuildConnectorCacheJob
            {
                connectorAccelerations = connectorAccelerations.AsParallelWriter(),
                baseGravityAccel = _baseGravityAccel,
                baseFrictionAccel = _baseFrictionAccel,
                rampBoostAccel = _rampBoostAccel
            };
            var connectorHandle = buildConnectorCacheJob.ScheduleParallel(inputDeps);
            
            // Combine results into cache
            var combinedHandle = JobHandle.CombineDependencies(moduleHandle, connectorHandle);
            var populateCacheJob = new PopulateCacheJob
            {
                moduleAccelerations = moduleAccelerations,
                connectorAccelerations = connectorAccelerations,
                accelerationCache = _accelerationCache
            };
            var populateHandle = populateCacheJob.Schedule(combinedHandle);
            
            return populateHandle;
        }
    }

    /// <summary>
    /// Cached acceleration data for a cell
    /// </summary>
    public struct CachedAcceleration
    {
        public ulong cellKey;
        public Fixed32x3 acceleration;
        public AccelerationType type;
    }

    /// <summary>
    /// Types of acceleration that can be applied
    /// </summary>
    public enum AccelerationType : byte
    {
        Base = 0,
        Module = 1,
        Connector = 2,
        Special = 3
    }

    /// <summary>
    /// Represents an acceleration update for a marble
    /// </summary>
    public struct AccelerationUpdate
    {
        public Entity marble;
        public Fixed32x3 acceleration;
    }

    /// <summary>
    /// Job to build module acceleration cache
    /// </summary>
    [BurstCompile]
    public partial struct BuildModuleCacheJob : IJobEntity
    {
        public NativeQueue<CachedAcceleration>.ParallelWriter moduleAccelerations;
        [ReadOnly] public long baseGravityAccel;
        [ReadOnly] public long baseFrictionAccel;
        [ReadOnly] public long rampBoostAccel;
        [ReadOnly] public long liftAccel;

        public void Execute(Entity entity, in CellIndex cellIndex, in ModuleRef moduleRef)
        {
            var cellKey = ECSUtils.PackCellKey(cellIndex.xyz);
            var acceleration = CalculateModuleAcceleration(moduleRef);
            
            moduleAccelerations.Enqueue(new CachedAcceleration
            {
                cellKey = cellKey,
                acceleration = acceleration,
                type = AccelerationType.Module
            });
        }

        [BurstCompile]
        private Fixed32x3 CalculateModuleAcceleration(ModuleRef moduleRef)
        {
            // Start with base physics
            var totalAccel = new Fixed32x3(
                baseFrictionAccel,
                baseGravityAccel,
                baseFrictionAccel
            );
            
            // Apply module-specific acceleration based on type
            // This would read from the module blob data
            // For now, using placeholder logic
            
            // TODO: Read actual module type from blob and apply specific acceleration
            // Example: if (moduleRef.value.Value.moduleType == 2) // lift
            // totalAccel.y += liftAccel;
            
            return totalAccel;
        }
    }

    /// <summary>
    /// Job to build connector acceleration cache
    /// </summary>
    [BurstCompile]
    public partial struct BuildConnectorCacheJob : IJobEntity
    {
        public NativeQueue<CachedAcceleration>.ParallelWriter connectorAccelerations;
        [ReadOnly] public long baseGravityAccel;
        [ReadOnly] public long baseFrictionAccel;
        [ReadOnly] public long rampBoostAccel;

        public void Execute(Entity entity, in CellIndex cellIndex, in ConnectorRef connectorRef)
        {
            var cellKey = ECSUtils.PackCellKey(cellIndex.xyz);
            var acceleration = CalculateConnectorAcceleration(connectorRef);
            
            connectorAccelerations.Enqueue(new CachedAcceleration
            {
                cellKey = cellKey,
                acceleration = acceleration,
                type = AccelerationType.Connector
            });
        }

        [BurstCompile]
        private Fixed32x3 CalculateConnectorAcceleration(ConnectorRef connectorRef)
        {
            // Start with base physics
            var totalAccel = new Fixed32x3(
                baseFrictionAccel,
                baseGravityAccel,
                baseFrictionAccel
            );
            
            // Apply connector-specific acceleration (e.g., ramp effects)
            // This would read from the connector blob data
            // For now, using placeholder logic
            
            // TODO: Read actual ramp angle from blob and apply directional acceleration
            // Example: if (!connectorRef.value.Value.isFlatTrack)
            // totalAccel.x += rampBoostAccel;
            
            return totalAccel;
        }
    }

    /// <summary>
    /// Job to populate the acceleration cache from queued accelerations
    /// </summary>
    [BurstCompile]
    public struct PopulateCacheJob : IJob
    {
        public NativeQueue<CachedAcceleration> moduleAccelerations;
        public NativeQueue<CachedAcceleration> connectorAccelerations;
        public NativeHashMap<ulong, CachedAcceleration> accelerationCache;

        public void Execute()
        {
            // Process module accelerations
            while (moduleAccelerations.TryDequeue(out var moduleAccel))
            {
                accelerationCache.Add(moduleAccel.cellKey, moduleAccel);
            }
            
            // Process connector accelerations
            while (connectorAccelerations.TryDequeue(out var connectorAccel))
            {
                // Connectors can override or combine with module acceleration
                if (accelerationCache.TryGetValue(connectorAccel.cellKey, out var existing))
                {
                    // Combine accelerations
                    var combinedAccel = new CachedAcceleration
                    {
                        cellKey = connectorAccel.cellKey,
                        acceleration = existing.acceleration + connectorAccel.acceleration,
                        type = AccelerationType.Special
                    };
                    accelerationCache[connectorAccel.cellKey] = combinedAccel;
                }
                else
                {
                    accelerationCache.Add(connectorAccel.cellKey, connectorAccel);
                }
            }
            
            // Dispose queues
            moduleAccelerations.Dispose();
            connectorAccelerations.Dispose();
        }
    }

    /// <summary>
    /// Job to calculate modulated acceleration using cached data
    /// </summary>
    [BurstCompile]
    public partial struct ModulatedAccelerationJob : IJobEntity
    {
        [ReadOnly] public long baseGravityAccel;
        [ReadOnly] public long baseFrictionAccel;
        [ReadOnly] public NativeHashMap<ulong, CachedAcceleration> accelerationCache;
        public NativeQueue<AccelerationUpdate>.ParallelWriter accelerationUpdates;
        public NativeQueue<Fault>.ParallelWriter faultQueue;

        public void Execute(Entity entity, in CellIndex cellIndex, in PositionComponent position, in MarbleTag marbleTag)
        {
            var cellKey = ECSUtils.PackCellKey(cellIndex.xyz);
            var acceleration = CalculateAcceleration(cellKey);
            
            // Queue acceleration update
            accelerationUpdates.Enqueue(new AccelerationUpdate
            {
                marble = entity,
                acceleration = acceleration
            });
        }

        /// <summary>
        /// Calculates acceleration based on cached module/connector data
        /// </summary>
        [BurstCompile]
        private Fixed32x3 CalculateAcceleration(ulong cellKey)
        {
            // Check cache first
            if (accelerationCache.TryGetValue(cellKey, out var cached))
            {
                return cached.acceleration;
            }
            
            // Fall back to base physics if no cached data
            return new Fixed32x3(
                baseFrictionAccel,
                baseGravityAccel,
                baseFrictionAccel
            );
        }
    }

    /// <summary>
    /// Job to apply acceleration updates
    /// </summary>
    [BurstCompile]
    public struct ApplyAccelerationUpdatesJob : IJob
    {
        public NativeQueue<AccelerationUpdate> accelerationUpdates;
        public EntityCommandBuffer ecb;

        public void Execute()
        {
            // Process all acceleration updates
            while (accelerationUpdates.TryDequeue(out var update))
            {
                ecb.SetComponent(update.marble, new AccelerationComponent { Value = update.acceleration });
            }
            
            // Dispose the queue
            accelerationUpdates.Dispose();
        }
    }

    /// <summary>
    /// Job to process faults from acceleration calculations
    /// </summary>
    [BurstCompile]
    public struct AccelerationProcessFaultsJob : IJob
    {
        public NativeQueue<Fault> faults;

        public void Execute()
        {
            // Process faults
            while (faults.TryDequeue(out var fault))
            {
                // Log or handle fault
                // UnityEngine.Debug.LogWarning($"Acceleration system fault: {fault.Code}");
            }
            
            // Dispose the fault queue
            faults.Dispose();
        }
    }
} 