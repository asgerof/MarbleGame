using UnityEngine;
using Unity.Mathematics;
using System;
using MarbleMaker.Core.ECS;

namespace MarbleMaker.Core.UI
{
    /// <summary>
    /// Simulation Controller - ECS→UI shim
    /// From UI docs: "SimulationCtl (ECS→UI shim)" and "PlaySimWorld exposes a SnapshotBlob each rendered frame"
    /// "SimulationCtl ViewModel converts marble count, tick time, etc., into simple structs consumed by UITK labels"
    /// </summary>
    [CreateAssetMenu(fileName = "SimulationController", menuName = "MarbleMaker/UI/Simulation Controller")]
    public class SimulationController : ScriptableObject
    {
        [Header("ECS Integration")]
        [SerializeField] private PlaySimWorldManager playSimWorldManager;
        [SerializeField] private EconomyViewModel economyViewModel;
        
        [Header("Performance Monitoring")]
        [SerializeField] private bool enablePerformanceLogging = false;
        [SerializeField] private float snapshotUpdateInterval = 0.016f; // ~60 FPS
        
        [Header("Debug Settings")]
        [SerializeField] private bool enableDebugLogging = false;
        
        // Simulation state
        private bool isInitialized = false;
        private float lastSnapshotTime = 0f;
        private uint lastTickCount = 0;
        private float tickTimeAccumulator = 0f;
        private int frameCount = 0;
        
        // Selection state
        private bool hasSelection = false;
        private int3 selectedPosition;
        private string selectedPartId;
        
        // Events
        public event Action<UIBus.SimulationSnapshot> OnSimulationSnapshotUpdated;
        
        /// <summary>
        /// Gets whether the simulation is running
        /// </summary>
        public bool IsSimulationRunning => playSimWorldManager != null && playSimWorldManager.IsSimulationRunning;
        
        /// <summary>
        /// Gets the current tick
        /// </summary>
        public uint CurrentTick => playSimWorldManager != null ? playSimWorldManager.CurrentTick : 0;
        
        private void OnEnable()
        {
            // Subscribe to UIBus commands
            UIBus.OnSimCommand += HandleSimCommand;
            UIBus.OnPlacePartCommand += HandlePlacePartCommand;
            UIBus.OnUpgradeCommand += HandleUpgradeCommand;
            UIBus.OnClickActionCommand += HandleClickActionCommand;
            UIBus.OnRemovePartCommand += HandleRemovePartCommand;
            UIBus.OnModuleSelected += HandleModuleSelected;
            UIBus.OnSelectionCleared += HandleSelectionCleared;
            
            Initialize();
        }
        
        private void OnDisable()
        {
            // Unsubscribe from UIBus events
            UIBus.OnSimCommand -= HandleSimCommand;
            UIBus.OnPlacePartCommand -= HandlePlacePartCommand;
            UIBus.OnUpgradeCommand -= HandleUpgradeCommand;
            UIBus.OnClickActionCommand -= HandleClickActionCommand;
            UIBus.OnRemovePartCommand -= HandleRemovePartCommand;
            UIBus.OnModuleSelected -= HandleModuleSelected;
            UIBus.OnSelectionCleared -= HandleSelectionCleared;
        }
        
        /// <summary>
        /// Initializes the simulation controller
        /// </summary>
        private void Initialize()
        {
            if (isInitialized)
                return;
            
            if (playSimWorldManager == null)
            {
                Debug.LogError("SimulationController: PlaySimWorldManager reference is missing");
                return;
            }
            
            isInitialized = true;
            
            if (enableDebugLogging)
                Debug.Log("SimulationController: Initialized");
        }
        
        /// <summary>
        /// Updates the simulation controller (should be called each frame)
        /// From UI docs: "exposes a SnapshotBlob each rendered frame"
        /// </summary>
        public void UpdateController()
        {
            if (!isInitialized)
                return;
            
            frameCount++;
            
            // Update simulation world
            if (playSimWorldManager != null)
            {
                playSimWorldManager.UpdateSimulation(Time.deltaTime);
            }
            
            // Generate and publish snapshots at regular intervals
            if (Time.time - lastSnapshotTime >= snapshotUpdateInterval)
            {
                GenerateSnapshots();
                lastSnapshotTime = Time.time;
            }
        }
        
        /// <summary>
        /// Generates and publishes simulation snapshots
        /// From UI docs: "converts marble count, tick time, etc., into simple structs consumed by UITK labels"
        /// </summary>
        private void GenerateSnapshots()
        {
            // Generate simulation snapshot
            var simulationSnapshot = GenerateSimulationSnapshot();
            UIBus.PublishSimulationSnapshot(simulationSnapshot);
            OnSimulationSnapshotUpdated?.Invoke(simulationSnapshot);
            
            // Generate economy snapshot
            var economySnapshot = GenerateEconomySnapshot();
            UIBus.PublishEconomySnapshot(economySnapshot);
            
            // Generate selection snapshot
            var selectionSnapshot = GenerateSelectionSnapshot();
            UIBus.PublishSelectionSnapshot(selectionSnapshot);
        }
        
        /// <summary>
        /// Generates simulation state snapshot
        /// </summary>
        /// <returns>Simulation snapshot</returns>
        private UIBus.SimulationSnapshot GenerateSimulationSnapshot()
        {
            var isRunning = IsSimulationRunning;
            var currentTick = CurrentTick;
            
            // Calculate tick time (time between ticks)
            float tickTime = 0f;
            if (currentTick > lastTickCount && isRunning)
            {
                tickTime = Time.deltaTime / (currentTick - lastTickCount);
                tickTimeAccumulator += tickTime;
            }
            lastTickCount = currentTick;
            
            // Calculate FPS
            float fps = frameCount > 0 ? frameCount / Time.time : 0f;
            
            return new UIBus.SimulationSnapshot
            {
                isRunning = isRunning,
                isPaused = false, // Would be determined from simulation state
                currentTick = currentTick,
                tickTime = tickTime,
                activeMarbles = GetActiveMarbleCount(),
                totalCollisions = GetTotalCollisionCount(),
                fps = fps
            };
        }
        
        /// <summary>
        /// Generates economy state snapshot
        /// </summary>
        /// <returns>Economy snapshot</returns>
        private UIBus.EconomySnapshot GenerateEconomySnapshot()
        {
            if (economyViewModel == null)
            {
                return new UIBus.EconomySnapshot
                {
                    coins = 100,
                    partTokens = 0,
                    idleIncomeRate = 10
                };
            }
            
            return new UIBus.EconomySnapshot
            {
                coins = economyViewModel.Coins,
                partTokens = economyViewModel.PartTokens,
                idleIncomeRate = economyViewModel.IdleIncomeRate
            };
        }
        
        /// <summary>
        /// Generates selection state snapshot
        /// </summary>
        /// <returns>Selection snapshot</returns>
        private UIBus.SelectionSnapshot GenerateSelectionSnapshot()
        {
            if (!hasSelection)
            {
                return new UIBus.SelectionSnapshot
                {
                    hasSelection = false
                };
            }
            
            // In a full implementation, this would query the ECS world
            // for the actual module state at the selected position
            var partDef = GetPartDefinitionAt(selectedPosition);
            
            return new UIBus.SelectionSnapshot
            {
                hasSelection = true,
                selectedPosition = selectedPosition,
                selectedPartId = selectedPartId,
                currentUpgradeLevel = GetCurrentUpgradeLevel(selectedPosition),
                maxUpgradeLevel = partDef?.MaxUpgradeLevel ?? 0,
                upgradeCost = CalculateUpgradeCost(selectedPartId, GetCurrentUpgradeLevel(selectedPosition)),
                canUpgrade = CanUpgradeModule(selectedPosition)
            };
        }
        
        /// <summary>
        /// Handles simulation commands from UI
        /// </summary>
        /// <param name="command">Simulation command</param>
        private void HandleSimCommand(UIBus.SimCommand command)
        {
            if (!isInitialized || playSimWorldManager == null)
                return;
            
            switch (command)
            {
                case UIBus.SimCommand.Play:
                    playSimWorldManager.StartSimulation();
                    break;
                    
                case UIBus.SimCommand.Pause:
                    // Would pause the simulation
                    break;
                    
                case UIBus.SimCommand.Reset:
                    playSimWorldManager.StopSimulation();
                    break;
                    
                case UIBus.SimCommand.Undo:
                    // Would undo the last action
                    break;
            }
            
            if (enableDebugLogging)
                Debug.Log($"SimulationController: Handled sim command {command}");
        }
        
        /// <summary>
        /// Handles place part commands from UI
        /// </summary>
        /// <param name="command">Place part command</param>
        private void HandlePlacePartCommand(UIBus.PlacePartCommand command)
        {
            if (!isInitialized || playSimWorldManager == null)
                return;
            
            var trackCommand = new TrackCommand
            {
                type = TrackCommand.CommandType.PlaceModule, // or PlaceConnector based on part type
                position = command.position,
                partId = GetPartIdAsInt(command.partId),
                upgradeLevel = command.upgradeLevel,
                rotation = command.rotation
            };
            
            playSimWorldManager.AddTrackCommand(trackCommand);
            
            if (enableDebugLogging)
                Debug.Log($"SimulationController: Placed part {command.partId} at {command.position}");
        }
        
        /// <summary>
        /// Handles upgrade commands from UI
        /// </summary>
        /// <param name="command">Upgrade command</param>
        private void HandleUpgradeCommand(UIBus.UpgradeCommand command)
        {
            // Check if player can afford the upgrade
            if (economyViewModel != null && !economyViewModel.TrySpendCoins(command.cost))
            {
                if (enableDebugLogging)
                    Debug.LogWarning($"SimulationController: Cannot afford upgrade (cost: {command.cost})");
                return;
            }
            
            // Apply upgrade to ECS world
            // In a full implementation, this would modify the component data
            
            if (enableDebugLogging)
                Debug.Log($"SimulationController: Upgraded module at {command.position} to level {command.newUpgradeLevel}");
        }
        
        /// <summary>
        /// Handles click action commands from UI
        /// From UI docs: "Click-to-control actions post an InteractCmd to an EntityCommandBuffer via UIBus"
        /// </summary>
        /// <param name="command">Click action command</param>
        private void HandleClickActionCommand(UIBus.ClickActionCommand command)
        {
            // In a full implementation, this would add the click event to the ECS world
            // The ECS InteractApplySystem would then process it on the next 120-Hz step
            
            if (enableDebugLogging)
                Debug.Log($"SimulationController: Click action {command.actionId} at {command.targetPosition}");
        }
        
        /// <summary>
        /// Handles remove part commands from UI
        /// </summary>
        /// <param name="command">Remove part command</param>
        private void HandleRemovePartCommand(UIBus.RemovePartCommand command)
        {
            var trackCommand = new TrackCommand
            {
                type = TrackCommand.CommandType.RemovePart,
                position = command.position
            };
            
            playSimWorldManager.AddTrackCommand(trackCommand);
            
            if (enableDebugLogging)
                Debug.Log($"SimulationController: Removed part at {command.position}");
        }
        
        /// <summary>
        /// Handles module selection from UI
        /// </summary>
        /// <param name="position">Selected position</param>
        private void HandleModuleSelected(int3 position)
        {
            hasSelection = true;
            selectedPosition = position;
            selectedPartId = GetPartIdAt(position);
            
            if (enableDebugLogging)
                Debug.Log($"SimulationController: Selected module at {position}");
        }
        
        /// <summary>
        /// Handles selection cleared from UI
        /// </summary>
        private void HandleSelectionCleared()
        {
            hasSelection = false;
            selectedPosition = int3.zero;
            selectedPartId = null;
            
            if (enableDebugLogging)
                Debug.Log("SimulationController: Selection cleared");
        }
        
        // Helper methods for ECS world queries (placeholder implementations)
        
        private int GetActiveMarbleCount()
        {
            // Would query ECS world for entities with MarbleTag
            return 0;
        }
        
        private int GetTotalCollisionCount()
        {
            // Would track collision statistics
            return 0;
        }
        
        private PartDef GetPartDefinitionAt(int3 position)
        {
            // Would query ECS world for part at position
            return null;
        }
        
        private int GetCurrentUpgradeLevel(int3 position)
        {
            // Would query ECS world for module state
            return 0;
        }
        
        private int CalculateUpgradeCost(string partId, int currentLevel)
        {
            // Would calculate upgrade cost based on part definition
            return (currentLevel + 1) * 100;
        }
        
        private bool CanUpgradeModule(int3 position)
        {
            // Would check if module can be upgraded
            var partDef = GetPartDefinitionAt(position);
            var currentLevel = GetCurrentUpgradeLevel(position);
            return partDef != null && currentLevel < partDef.MaxUpgradeLevel;
        }
        
        private string GetPartIdAt(int3 position)
        {
            // Would query ECS world for part ID at position
            return "unknown_part";
        }
        
        private int GetPartIdAsInt(string partId)
        {
            // Would convert string ID to int for ECS system
            return partId.GetHashCode();
        }
    }
}