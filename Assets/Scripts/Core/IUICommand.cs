using MarbleMaker.Core;

namespace MarbleMaker.Core
{
    /// <summary>
    /// Interface for UI commands as specified in System Architecture Diagram
    /// Commands published by UI Layer and consumed by Editor subsystem
    /// </summary>
    public interface IUICommand
    {
        /// <summary>
        /// Executes the command
        /// </summary>
        /// <returns>True if command was executed successfully</returns>
        bool Execute();
    }
    
    /// <summary>
    /// Command to place a part on the grid
    /// </summary>
    public class PlacePartCommand : IUICommand
    {
        public string partID;
        public GridPosition position;
        public GridRotation rotation;
        public int upgradeLevel;
        
        public PlacePartCommand(string partID, GridPosition position, GridRotation rotation, int upgradeLevel = 0)
        {
            this.partID = partID;
            this.position = position;
            this.rotation = rotation;
            this.upgradeLevel = upgradeLevel;
        }
        
        public bool Execute()
        {
            // Implementation will be handled by BoardEditor
            return true;
        }
    }
    
    /// <summary>
    /// Command to remove a part from the grid
    /// </summary>
    public class RemovePartCommand : IUICommand
    {
        public GridPosition position;
        
        public RemovePartCommand(GridPosition position)
        {
            this.position = position;
        }
        
        public bool Execute()
        {
            // Implementation will be handled by BoardEditor
            return true;
        }
    }
    
    /// <summary>
    /// Command to upgrade a part
    /// </summary>
    public class UpgradePartCommand : IUICommand
    {
        public GridPosition position;
        public int targetLevel;
        
        public UpgradePartCommand(GridPosition position, int targetLevel)
        {
            this.position = position;
            this.targetLevel = targetLevel;
        }
        
        public bool Execute()
        {
            // Implementation will be handled by BoardEditor
            return true;
        }
    }
    
    /// <summary>
    /// Command to interact with a part during simulation (click-action)
    /// </summary>
    public class ClickActionCommand : IUICommand
    {
        public GridPosition position;
        
        public ClickActionCommand(GridPosition position)
        {
            this.position = position;
        }
        
        public bool Execute()
        {
            // Implementation will be handled by BoardEditor (pass-through to simulation)
            return true;
        }
    }
} 