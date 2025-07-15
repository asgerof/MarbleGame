using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace MarbleMaker.Core.ECS
{
    /// <summary>
    /// Burst-compatible job for disposing containers
    /// </summary>
    [BurstCompile]
    public struct DisposeJob : IJob
    {
        public NativeList<JobHandle> Handles;
        
        public void Execute() 
        { 
            for (int i = 0; i < Handles.Length; i++) 
                Handles[i].Complete(); 
        }
    }

    /// <summary>
    /// Queues NativeContainer disposals without main-thread Complete().
    /// Simple helper to queue NativeContainers for automatic disposal
    /// at the end of this system's OnUpdate.
    /// </summary>
    public struct DisposableContainer
    {
        private NativeList<JobHandle> _handles;

        public DisposableContainer(Allocator alloc) 
            => _handles = new NativeList<JobHandle>(alloc);

        public void Add<T>(NativeList<T> list, JobHandle dependsOn) where T : unmanaged
            => _handles.Add(list.Dispose(dependsOn));

        public JobHandle Flush(JobHandle inputDeps)
        {
            var job = new DisposeJob { Handles = _handles };
            return job.Schedule(inputDeps);
        }
    }
} 