using Unity.Entities;

namespace MarbleMaker.Core.ECS
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class InputActionGroup : ComponentSystemGroup { }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(InputActionGroup))]
    public partial class MotionGroup : ComponentSystemGroup { }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(MotionGroup))]
    public partial class ModuleLogicGroup : ComponentSystemGroup { }
}