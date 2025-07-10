using Unity.Entities;
using Unity.NetCode;

namespace MarbleMaker.Core.ECS
{
    /// <summary>
    /// System groups for PlaySimWorld as specified in ECS documentation
    /// "PlaySimWorld contains three custom SystemGroups that run inside Unity's built-in FixedStepSimulationSystemGroup"
    /// Order: InputActionGroup → MotionGroup → ModuleLogicGroup
    /// </summary>

    /// <summary>
    /// Input action group - applies queued click/tap events
    /// From ECS docs: "InputActionGroup — applies queued click/tap events"
    /// </summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial class InputActionGroup : ComponentSystemGroup
    {
        // First system group to run
    }

    /// <summary>
    /// Motion group - pure data jobs (marble integration, collision)
    /// From ECS docs: "MotionGroup — pure data jobs (marble integration, collision)"
    /// </summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(InputActionGroup))]
    public partial class MotionGroup : ComponentSystemGroup
    {
        // Second system group to run
    }

    /// <summary>
    /// Module logic group - state machines for splitters, collectors, cannons, lifts
    /// From ECS docs: "ModuleLogicGroup — state machines for splitters, collectors, cannons, lifts"
    /// </summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(MotionGroup))]
    public partial class ModuleLogicGroup : ComponentSystemGroup
    {
        // Third system group to run
    }
}