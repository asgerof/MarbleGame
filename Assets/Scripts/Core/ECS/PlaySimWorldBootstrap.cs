using Unity.Entities;

[DisableAutoCreation]
public partial class EndSimulationEcbSystem : EntityCommandBufferSystem { }

[WorldSystemFilter(WorldSystemFilterFlags.Simulation)]
public class PlaySimWorldBootstrap : ICustomBootstrap
{
    public bool Initialize(string defaultWorldName)
    {
        var world = new World(defaultWorldName);
        World.DefaultGameObjectInjectionWorld = world;

        world.GetOrCreateSystemManaged<EndSimulationEcbSystem>();

        ScriptBehaviourUpdateOrder.AddWorldToCurrentPlayerLoop(world);
        return true;
    }
} 