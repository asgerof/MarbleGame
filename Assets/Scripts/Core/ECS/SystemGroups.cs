using Unity.Entities;

namespace MarbleMaker.Core.ECS
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class InputActionGroup : ComponentSystemGroup { }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(InputActionGroup))]
    public partial class MotionGroup : ComponentSystemGroup { }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(MotionGroup))]          // motion has finished
    [UpdateBefore(typeof(ModuleLogicGroup))]    // logic systems rely on the caches
    public partial class LookupCacheGroup : ComponentSystemGroup { }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(LookupCacheGroup))]
    public partial class ModuleLogicGroup : ComponentSystemGroup { }
}