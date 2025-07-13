using MarbleMaker.Core;

namespace MarbleMaker.Core.ECS
{
    /// <summary>
    /// Deterministic integer tick service to replace floating-point tick computation
    /// Eliminates float drift and maintains bit-perfect simulation
    /// </summary>
    public static class SimulationTick
    {
        public const int Rate = GameConstants.TICK_RATE;   // 120
        public static ulong Current;                       // starts at 0

        /// <summary>
        /// Increments the tick counter. Should be called once per frame.
        /// </summary>
        public static void Increment()
        {
            Current++;
        }

        /// <summary>
        /// Resets the tick counter to zero. Used for starting new simulations.
        /// </summary>
        public static void Reset()
        {
            Current = 0;
        }
    }
} 