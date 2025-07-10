using System;
using Unity.Mathematics;

namespace MarbleMaker.Core.UI
{
    /// <summary>
    /// UIBus - thin C# event hub for UI communication
    /// From UI docs: "UIBus is a thin C# event hub (PlacePartCmd, UpgradeCmd, ClickActionCmd, etc.). 
    /// Editors send commands; Simulation posts snapshots."
    /// </summary>
    public static class UIBus
    {
        // Simulation Control Commands
        public static event Action<SimCommand> OnSimCommand;
        public static event Action<PlacePartCommand> OnPlacePartCommand;
        public static event Action<UpgradeCommand> OnUpgradeCommand;
        public static event Action<ClickActionCommand> OnClickActionCommand;
        public static event Action<RemovePartCommand> OnRemovePartCommand;
        
        // Simulation State Events
        public static event Action<SimulationSnapshot> OnSimulationSnapshot;
        public static event Action<EconomySnapshot> OnEconomySnapshot;
        public static event Action<SelectionSnapshot> OnSelectionSnapshot;
        
        // UI State Events
        public static event Action<string> OnPartSelected;
        public static event Action<int3> OnModuleSelected;
        public static event Action OnSelectionCleared;
        public static event Action<string> OnTooltipShow;
        public static event Action OnTooltipHide;

        /// <summary>
        /// Simulation control commands
        /// </summary>
        public enum SimCommand
        {
            Play,      // ▶ Roll button
            Pause,     // ⏸ Pause button  
            Reset,     // ⭮ Reset button
            Undo       // ⟲ Undo button
        }

        /// <summary>
        /// Command to place a part on the grid
        /// </summary>
        public struct PlacePartCommand
        {
            public string partId;
            public int3 position;
            public int rotation;
            public int upgradeLevel;
        }

        /// <summary>
        /// Command to upgrade an existing part
        /// </summary>
        public struct UpgradeCommand
        {
            public int3 position;
            public int newUpgradeLevel;
            public int cost;
        }

        /// <summary>
        /// Command for click-to-control actions (splitter toggle, lift pause, etc.)
        /// </summary>
        public struct ClickActionCommand
        {
            public int3 targetPosition;
            public int actionId;        // 0=toggle splitter, 1=pause lift, etc.
            public long absoluteTick;   // tick when action should be applied
        }

        /// <summary>
        /// Command to remove a part from the grid
        /// </summary>
        public struct RemovePartCommand
        {
            public int3 position;
        }

        /// <summary>
        /// Simulation state snapshot for UI updates
        /// </summary>
        public struct SimulationSnapshot
        {
            public bool isRunning;
            public bool isPaused;
            public uint currentTick;
            public float tickTime;
            public int activeMarbles;
            public int totalCollisions;
            public float fps;
        }

        /// <summary>
        /// Economy state snapshot
        /// </summary>
        public struct EconomySnapshot
        {
            public int coins;
            public int partTokens;
            public int idleIncomeRate;
        }

        /// <summary>
        /// Selection state snapshot
        /// </summary>
        public struct SelectionSnapshot
        {
            public bool hasSelection;
            public int3 selectedPosition;
            public string selectedPartId;
            public int currentUpgradeLevel;
            public int maxUpgradeLevel;
            public int upgradeCost;
            public bool canUpgrade;
        }

        // Command publishing methods
        public static void PublishSimCommand(SimCommand command)
        {
            OnSimCommand?.Invoke(command);
        }

        public static void PublishPlacePartCommand(PlacePartCommand command)
        {
            OnPlacePartCommand?.Invoke(command);
        }

        public static void PublishUpgradeCommand(UpgradeCommand command)
        {
            OnUpgradeCommand?.Invoke(command);
        }

        public static void PublishClickActionCommand(ClickActionCommand command)
        {
            OnClickActionCommand?.Invoke(command);
        }

        public static void PublishRemovePartCommand(RemovePartCommand command)
        {
            OnRemovePartCommand?.Invoke(command);
        }

        // State publishing methods
        public static void PublishSimulationSnapshot(SimulationSnapshot snapshot)
        {
            OnSimulationSnapshot?.Invoke(snapshot);
        }

        public static void PublishEconomySnapshot(EconomySnapshot snapshot)
        {
            OnEconomySnapshot?.Invoke(snapshot);
        }

        public static void PublishSelectionSnapshot(SelectionSnapshot snapshot)
        {
            OnSelectionSnapshot?.Invoke(snapshot);
        }

        // UI state publishing methods
        public static void PublishPartSelected(string partId)
        {
            OnPartSelected?.Invoke(partId);
        }

        public static void PublishModuleSelected(int3 position)
        {
            OnModuleSelected?.Invoke(position);
        }

        public static void PublishSelectionCleared()
        {
            OnSelectionCleared?.Invoke();
        }

        public static void PublishTooltipShow(string text)
        {
            OnTooltipShow?.Invoke(text);
        }

        public static void PublishTooltipHide()
        {
            OnTooltipHide?.Invoke();
        }
    }
}