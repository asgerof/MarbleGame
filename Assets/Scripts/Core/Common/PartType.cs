namespace MarbleMaker.Core
{
    /// <summary>
    /// Defines the two types of parts in the Module-Connector system
    /// as specified in the updated GDD Section 5
    /// </summary>
    public enum PartType : byte
    {
        /// <summary>
        /// Modules are functional parts like Straight Path, Splitter, Lift, Collector, Cannon, etc.
        /// Must be connected via Connectors - no Module-to-Module adjacency allowed
        /// </summary>
        Module = 0,
        
        /// <summary>
        /// Connectors are transitional parts like Curve, Ramp, Spiral, Junction, etc.
        /// Must connect Modules together - no Connector-to-Connector adjacency allowed
        /// </summary>
        Connector = 1
    }
} 