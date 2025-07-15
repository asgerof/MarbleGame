using Unity.Entities;
using Unity.Collections;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MarbleMaker.Core.ECS
{
    [DisableAutoCreation]
    public partial class EndSimulationEcbSystem : EntityCommandBufferSystem { }

    #if UNITY_EDITOR
    [InitializeOnLoad]
    #endif
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

            // NOTE: AddWorldToCurrentPlayerLoop is deprecated in Unity ECS 1.3.14
            // The world will be automatically added to the player loop by Unity
            // ScriptBehaviourUpdateOrder.AddWorldToCurrentPlayerLoop(_playWorld);
            
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
        }
    }
} 