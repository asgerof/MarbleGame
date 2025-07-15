using Unity.Entities;
using Unity.Collections;
using Unity.Burst;
using UnityEngine;
using System.Collections.Generic;

namespace MarbleMaker.Core.ECS
{
    /// <summary>
    /// Manages the PlaySimWorld lifecycle and TrackCmdBuffer bridge
    /// From ECS docs: "Play Sim World: Pure ECS, fixed–timestep 120 Hz. Created when the player hits Roll; destroyed on Reset"
    /// "TrackCmdBuffer – created by the Editor in Authoring World when the player places/erases a piece"
    /// </summary>
    [CreateAssetMenu(fileName = "PlaySimWorldManager", menuName = "MarbleMaker/Play Sim World Manager")]
    public class PlaySimWorldManager : ScriptableObject
    {
        [Header("World Settings")]
        [SerializeField] [Tooltip("Fixed timestep for simulation (120 Hz from GDD)")]
        private float fixedTimestep = 1f / 120f;
        
        [SerializeField] [Tooltip("Enable debug logging for world lifecycle")]
        private bool enableDebugLogging = false;
        
        [Header("Performance Settings")]
        [SerializeField] [Tooltip("Maximum marbles in simulation")]
        private int maxMarbles = 20000;
        
        [SerializeField] [Tooltip("Initial entity capacity")]
        private int initialEntityCapacity = 50000;
        
        // World references
        private World playSimWorld;
        private World authoringWorld;
        
        // Track command buffer for Editor → ECS bridge
        private NativeList<TrackCommand> trackCmdBuffer;
        private bool hasPendingCommands;
        
        // Simulation state
        private bool isSimulationRunning;
        private uint currentTick;
        private float accumulatedTime;
        
        /// <summary>
        /// Gets whether the simulation is currently running
        /// </summary>
        public bool IsSimulationRunning => isSimulationRunning && playSimWorld != null && playSimWorld.IsCreated;
        
        /// <summary>
        /// Gets the current simulation tick
        /// </summary>
        public uint CurrentTick => currentTick;
        
        /// <summary>
        /// Gets the fixed timestep for simulation
        /// </summary>
        public float FixedTimestep => fixedTimestep;
        
        private void OnEnable()
        {
            // Initialize track command buffer
            if (!trackCmdBuffer.IsCreated) trackCmdBuffer = new NativeList<TrackCommand>(1000, Allocator.Persistent);
            hasPendingCommands = false;
            
            // Get reference to authoring world (default world)
            authoringWorld = World.DefaultGameObjectInjectionWorld;
        }
        
        private void OnDisable()
        {
            // Clean up when disabled
            StopSimulation();
            
            if (trackCmdBuffer.IsCreated)
            {
                trackCmdBuffer.Dispose();
            }
        }
        
        /// <summary>
        /// Starts the simulation (called when player hits Roll)
        /// Creates PlaySimWorld and applies pending track commands
        /// </summary>
        public void StartSimulation()
        {
            if (isSimulationRunning)
            {
                if (enableDebugLogging)
                    Debug.LogWarning("PlaySimWorldManager: Simulation already running");
                return;
            }
            
            try
            {
                CreatePlaySimWorld();
                ApplyTrackCommands();
                
                isSimulationRunning = true;
                currentTick = 0;
                accumulatedTime = 0f;
                
                // Reset deterministic tick counter
                SimulationTick.Reset();
                
                if (enableDebugLogging)
                    Debug.Log("PlaySimWorldManager: Simulation started");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"PlaySimWorldManager: Failed to start simulation: {ex.Message}");
                StopSimulation();
            }
        }
        
        /// <summary>
        /// Stops the simulation (called when player hits Reset)
        /// Destroys PlaySimWorld and clears state
        /// </summary>
        public void StopSimulation()
        {
            if (playSimWorld != null && playSimWorld.IsCreated)
            {
                playSimWorld.Dispose();
                playSimWorld = null;
            }
            
            // Reset shared archetypes
            Archetypes.Reset();
            
            isSimulationRunning = false;
            currentTick = 0;
            accumulatedTime = 0f;
            
            // Clear pending commands
            trackCmdBuffer.FastClear();
            hasPendingCommands = false;
            
            if (enableDebugLogging)
                Debug.Log("PlaySimWorldManager: Simulation stopped");
        }
        
        /// <summary>
        /// Updates the simulation with fixed timestep
        /// Should be called from Update() or FixedUpdate()
        /// </summary>
        public void UpdateSimulation(float deltaTime)
        {
            if (!isSimulationRunning || playSimWorld == null || !playSimWorld.IsCreated)
                return;
            
            accumulatedTime += deltaTime;
            
            // Run fixed timestep updates
            while (accumulatedTime >= fixedTimestep)
            {
                // Update the PlaySimWorld
                playSimWorld.Update();
                
                // Increment deterministic tick counter
                SimulationTick.Increment();
                currentTick++;
                accumulatedTime -= fixedTimestep;
                
                // Apply any new track commands that arrived during this tick
                if (hasPendingCommands)
                {
                    ApplyTrackCommands();
                }
            }
        }
        
        /// <summary>
        /// Adds a track command to the buffer (Editor → ECS bridge)
        /// From ECS docs: "TrackCmdBuffer – created by the Editor in Authoring World"
        /// </summary>
        /// <param name="command">Track command to add</param>
        public void AddTrackCommand(TrackCommand command)
        {
            trackCmdBuffer.Add(command);
            hasPendingCommands = true;
            
            if (enableDebugLogging)
                Debug.Log($"PlaySimWorldManager: Added track command: {command.type} at {command.position}");
        }
        
        /// <summary>
        /// Creates the PlaySimWorld with proper system groups
        /// </summary>
        private void CreatePlaySimWorld()
        {
            // Create the simulation world
            playSimWorld = new World("PlaySimWorld");
            
            // Set fixed timestep
            var fixedStepGroup = playSimWorld.GetOrCreateSystemManaged<FixedStepSimulationSystemGroup>();
            fixedStepGroup.Timestep = fixedTimestep;
            
            // Create custom system groups in the correct order
            var inputActionGroup = playSimWorld.CreateSystem<InputActionGroup>();
            var motionGroup = playSimWorld.CreateSystem<MotionGroup>();
            var moduleLogicGroup = playSimWorld.CreateSystem<ModuleLogicGroup>();
            
            // Add systems to groups
            AddSystemsToGroups();
            
            // Initialize the world
            playSimWorld.GetOrCreateSystemManaged<InitializationSystemGroup>();
            playSimWorld.GetOrCreateSystemManaged<SimulationSystemGroup>();
            playSimWorld.GetOrCreateSystemManaged<PresentationSystemGroup>();
            
            if (enableDebugLogging)
                Debug.Log($"PlaySimWorldManager: Created PlaySimWorld with fixed timestep {fixedTimestep}s");
        }
        
        /// <summary>
        /// Adds all ECS systems to their respective groups
        /// </summary>
        private void AddSystemsToGroups()
        {
            // Get the system groups
            var inputActionGroup = playSimWorld.GetOrCreateSystemManaged<InputActionGroup>();
            var motionGroup = playSimWorld.GetOrCreateSystemManaged<MotionGroup>();
            var moduleLogicGroup = playSimWorld.GetOrCreateSystemManaged<ModuleLogicGroup>();
            
            // InputActionGroup systems (applies queued click/tap events)
            var interactApplySystem = playSimWorld.Unmanaged.GetOrCreateSystem<InteractApplySystem>();
            var seedSpawnerSystem = playSimWorld.Unmanaged.GetOrCreateSystem<SeedSpawnerSystem>();
            inputActionGroup.AddSystemToUpdateList(interactApplySystem);
            inputActionGroup.AddSystemToUpdateList(seedSpawnerSystem);
            
            // MotionGroup systems (pure data jobs - marble integration, collision)
            var marbleIntegrateSystem = playSimWorld.Unmanaged.GetOrCreateSystem<MarbleIntegrateSystem>();
            var collisionDetectSystem = playSimWorld.Unmanaged.GetOrCreateSystem<CollisionDetectSystem>();
            var debrisCompactionSystem = playSimWorld.Unmanaged.GetOrCreateSystem<DebrisCompactionSystem>();
            motionGroup.AddSystemToUpdateList(marbleIntegrateSystem);
            motionGroup.AddSystemToUpdateList(collisionDetectSystem);
            motionGroup.AddSystemToUpdateList(debrisCompactionSystem);
            
            // ModuleLogicGroup systems (state machines for splitters, collectors, cannons, lifts)
            // Goal systems (run first)
            var goalPadInitializationSystem = playSimWorld.Unmanaged.GetOrCreateSystem<GoalPadInitializationSystem>();
            var goalDetectionSystem = playSimWorld.Unmanaged.GetOrCreateSystem<GoalDetectionSystem>();
            var goalPadSystem = playSimWorld.Unmanaged.GetOrCreateSystem<GoalPadSystem>();
            moduleLogicGroup.AddSystemToUpdateList(goalPadInitializationSystem);
            moduleLogicGroup.AddSystemToUpdateList(goalDetectionSystem);
            moduleLogicGroup.AddSystemToUpdateList(goalPadSystem);
            
            // Lift systems
            var liftInitializationSystem = playSimWorld.Unmanaged.GetOrCreateSystem<LiftInitializationSystem>();
            var liftLoadingSystem = playSimWorld.Unmanaged.GetOrCreateSystem<LiftLoadingSystem>();
            var liftStepSystem = playSimWorld.Unmanaged.GetOrCreateSystem<LiftStepSystem>();
            moduleLogicGroup.AddSystemToUpdateList(liftInitializationSystem);
            moduleLogicGroup.AddSystemToUpdateList(liftLoadingSystem);
            moduleLogicGroup.AddSystemToUpdateList(liftStepSystem);
            
            // Splitter systems
            var splitterInputDetectionSystem = playSimWorld.Unmanaged.GetOrCreateSystem<SplitterInputDetectionSystem>();
            var splitterLogicSystem = playSimWorld.Unmanaged.GetOrCreateSystem<SplitterLogicSystem>();
            moduleLogicGroup.AddSystemToUpdateList(splitterInputDetectionSystem);
            moduleLogicGroup.AddSystemToUpdateList(splitterLogicSystem);
            
            // Collector systems
            var collectorEnqueueSystem = playSimWorld.Unmanaged.GetOrCreateSystem<CollectorEnqueueSystem>();
            var collectorDequeueSystem = playSimWorld.Unmanaged.GetOrCreateSystem<CollectorDequeueSystem>();
            moduleLogicGroup.AddSystemToUpdateList(collectorEnqueueSystem);
            moduleLogicGroup.AddSystemToUpdateList(collectorDequeueSystem);
            
            // Runtime spawning system (runs last)
            var runtimeMarbleSpawnerSystem = playSimWorld.Unmanaged.GetOrCreateSystem<RuntimeMarbleSpawnerSystem>();
            moduleLogicGroup.AddSystemToUpdateList(runtimeMarbleSpawnerSystem);
        }
        
        /// <summary>
        /// Applies track commands from the buffer to the PlaySimWorld
        /// From ECS docs: "On Roll, these commands are replayed inside PlaySimWorld, creating Module/Connector entities"
        /// </summary>
        private void ApplyTrackCommands()
        {
            if (!hasPendingCommands || trackCmdBuffer.Length == 0)
                return;
            
            var entityManager = playSimWorld.EntityManager;
            
            for (int i = 0; i < trackCmdBuffer.Length; i++)
            {
                var command = trackCmdBuffer[i];
                ApplyTrackCommand(entityManager, command);
            }
            
            // Clear applied commands
            trackCmdBuffer.FastClear();
            hasPendingCommands = false;
            
            if (enableDebugLogging)
                Debug.Log($"PlaySimWorldManager: Applied {trackCmdBuffer.Length} track commands");
        }
        
        /// <summary>
        /// Applies a single track command to create/modify entities
        /// </summary>
        /// <param name="entityManager">Entity manager for the PlaySimWorld</param>
        /// <param name="command">Command to apply</param>
        private void ApplyTrackCommand(EntityManager entityManager, TrackCommand command)
        {
            switch (command.type)
            {
                case TrackCommand.CommandType.PlaceModule:
                    CreateModuleEntity(entityManager, command);
                    break;
                    
                case TrackCommand.CommandType.PlaceConnector:
                    CreateConnectorEntity(entityManager, command);
                    break;
                    
                case TrackCommand.CommandType.RemovePart:
                    RemovePartEntity(entityManager, command);
                    break;
                    
                case TrackCommand.CommandType.Reset:
                    ResetAllEntities(entityManager);
                    break;
            }
        }
        
        /// <summary>
        /// Creates a module entity from a track command
        /// </summary>
        private void CreateModuleEntity(EntityManager entityManager, TrackCommand command)
        {
            var entity = entityManager.CreateEntity();
            
            // Add core components
            entityManager.AddComponent<CellIndex>(entity);
            entityManager.AddComponent<ModuleRef>(entity);
            
            // Set component data
            entityManager.SetComponentData(entity, new CellIndex(command.position));
            
            // Add specific module state based on part type
            AddModuleStateComponent(entityManager, entity, command.partId, command.upgradeLevel);
            
            if (enableDebugLogging)
                Debug.Log($"PlaySimWorldManager: Created module entity at {command.position}");
        }
        
        /// <summary>
        /// Creates a connector entity from a track command
        /// </summary>
        private void CreateConnectorEntity(EntityManager entityManager, TrackCommand command)
        {
            var entity = entityManager.CreateEntity();
            
            // Add core components
            entityManager.AddComponent<CellIndex>(entity);
            entityManager.AddComponent<ConnectorRef>(entity);
            
            // Set component data
            entityManager.SetComponentData(entity, new CellIndex(command.position));
            
            if (enableDebugLogging)
                Debug.Log($"PlaySimWorldManager: Created connector entity at {command.position}");
        }
        
        /// <summary>
        /// Removes a part entity at the specified position
        /// </summary>
        private void RemovePartEntity(EntityManager entityManager, TrackCommand command)
        {
            // Find entity at the specified position and destroy it
            // In a full implementation, this would query entities by position
            
            if (enableDebugLogging)
                Debug.Log($"PlaySimWorldManager: Removed part entity at {command.position}");
        }
        
        /// <summary>
        /// Resets all simulation entities (marbles, debris, etc.)
        /// </summary>
        private void ResetAllEntities(EntityManager entityManager)
        {
            // Destroy all marble entities
            var marbleQuery = entityManager.CreateEntityQuery(typeof(MarbleTag));
            entityManager.DestroyEntity(marbleQuery);
            
            // Destroy all debris entities
            var debrisQuery = entityManager.CreateEntityQuery(typeof(DebrisTag));
            entityManager.DestroyEntity(debrisQuery);
            
            if (enableDebugLogging)
                Debug.Log("PlaySimWorldManager: Reset all simulation entities");
        }
        
        /// <summary>
        /// Adds the appropriate module state component based on part type
        /// </summary>
        private void AddModuleStateComponent(EntityManager entityManager, Entity entity, int partId, int upgradeLevel)
        {
            // This would lookup the part definition and add the correct state component
            // For example:
            // - Splitter → SplitterState + SplitterTag
            // - Collector → CollectorState + CollectorTag
            // - Lift → LiftState + LiftTag
            
            // Placeholder implementation
            switch (partId)
            {
                case 0: // Splitter
                    entityManager.AddComponent<SplitterState>(entity);
                    entityManager.AddComponent<SplitterTag>(entity);
                    var splitterState = new SplitterState
                    {
                        currentExit = 0,
                        overrideExit = false,
                        overrideValue = 0
                    };
                    entityManager.SetComponentData(entity, splitterState);
                    break;
                    
                case 1: // Collector
                    entityManager.AddComponent<CollectorState>(entity);
                    entityManager.AddComponent<CollectorTag>(entity);
                    var collectorState = new CollectorState
                    {
                        level = (byte)upgradeLevel,
                        head = 0,
                        tail = 0,
                        count = 0,
                        burstSize = upgradeLevel == 2 ? 5u : 1u
                    };
                    entityManager.SetComponentData(entity, collectorState);
                    break;
                    
                case 2: // Lift
                    entityManager.AddComponent<LiftState>(entity);
                    entityManager.AddComponent<LiftTag>(entity);
                    var liftState = new LiftState
                    {
                        isActive = true,
                        currentHeight = 0,
                        targetHeight = 5
                    };
                                entityManager.SetComponentData(entity, liftState);
            break;
        }
    }

}
}