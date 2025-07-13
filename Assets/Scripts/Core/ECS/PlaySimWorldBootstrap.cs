using Unity.Entities;
using UnityEngine;

namespace MarbleMaker.Core.ECS
{
    [DisableAutoCreation]
    public partial class EndSimulationEcbSystem : EntityCommandBufferSystem { }

    #if UNITY_EDITOR
    [UnityEditor.InitializeOnLoad]
    #endif
    [WorldSystemFilter(WorldSystemFilterFlags.Simulation)]
    public class PlaySimWorldBootstrap : ICustomBootstrap
    {
        static World _playWorld;

        public bool Initialize(string defaultWorldName)
        {
            // Create custom play simulation world
            _playWorld = new World("PlaySim");
            World.DefaultGameObjectInjectionWorld = _playWorld;

            // Add ECB system for end simulation
            _playWorld.GetOrCreateSystemManaged<EndSimulationEcbSystem>();
            
            // Initialize shared archetypes
            var entityManager = _playWorld.EntityManager;
            Archetypes.Initialize(entityManager);
            
            // Create simulation system group
            _playWorld.GetOrCreateSystemManaged<SimulationSystemGroup>();

            // Add world to player loop
            ScriptBehaviourUpdateOrder.AddWorldToCurrentPlayerLoop(_playWorld);
            
            // Return false to prevent default world creation
            return false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void OnUnload()
        {
            // Clean up world on unload
            if (_playWorld != null && _playWorld.IsCreated)
            {
                _playWorld.Dispose();
                _playWorld = null;
            }
            
            ECSLookups.Dispose();      // Clean up static lookup caches
            
            // Reset archetypes
            Archetypes.Reset();
        }

        #if UNITY_EDITOR
        [UnityEditor.InitializeOnPlayModeStateChanged]
        static void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
            {
                // Clean up world when exiting play mode
                if (_playWorld != null && _playWorld.IsCreated)
                {
                    _playWorld.Dispose();
                    _playWorld = null;
                }
                
                // Reset archetypes
                Archetypes.Reset();
            }
        }
        #endif
    }
} 