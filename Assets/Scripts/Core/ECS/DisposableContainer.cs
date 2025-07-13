using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace MarbleMaker.Core.ECS
{
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
    /// Simple helper to queue NativeContainers for automatic disposal
    /// at the end of this system's OnUpdate.
    /// </summary>
    public struct IDisposableContainer
    {
        private NativeList<JobHandle> _handles;

        public IDisposableContainer(ref SystemState state)
            => _handles = new NativeList<JobHandle>(Allocator.Temp);

        public void Add<T>(NativeList<T> container, JobHandle dependsOn) where T : unmanaged
            => _handles.Add(NativeList<T>.Dispose(dependsOn));

        public JobHandle Flush(JobHandle inputDeps)
        {
            var job = new DisposeJob { Handles = _handles };
            var handle = job.Schedule(inputDeps);
            _handles = default;
            return handle;
        }
    }
} 