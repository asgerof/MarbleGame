using UnityEngine;

namespace MarbleMaker.Core
{
    /// <summary>
    /// Implements the exact physics integration formulas from GDD Section 4.2
    /// Timing & Motion Model specifications
    /// </summary>
    [CreateAssetMenu(fileName = "PhysicsIntegrator", menuName = "MarbleMaker/Physics Integrator")]
    public class PhysicsIntegrator : ScriptableObject
    {
        [Header("Physics Constants (from GDD Section 4.2)")]
        [SerializeField] [Tooltip("Gravity component: +0.10 cells/s² × sin θ")]
        private float gravityAcceleration = GameConstants.GRAVITY_ACCELERATION;
        
        [SerializeField] [Tooltip("Friction (flat track): –0.05 cells/s²")]
        private float frictionAcceleration = -GameConstants.FRICTION_FLAT;
        
        [SerializeField] [Tooltip("Terminal speed cap: 5 cells/s")]
        private float terminalSpeedCap = GameConstants.TERMINAL_SPEED_DEFAULT;
        
        [SerializeField] [Tooltip("Tick rate: 120 ticks/s")]
        private float tickRate = GameConstants.TICK_RATE;
        
        [Header("Runtime Calculated Values")]
        [SerializeField] [Tooltip("Calculated tick duration (1/tickRate)")]
        private float tickDuration = GameConstants.TICK_DURATION;
        
        /// <summary>
        /// Gravity acceleration constant from GDD
        /// </summary>
        public float GravityAcceleration => gravityAcceleration;
        
        /// <summary>
        /// Friction acceleration constant from GDD
        /// </summary>
        public float FrictionAcceleration => frictionAcceleration;
        
        /// <summary>
        /// Terminal speed cap from GDD
        /// </summary>
        public float TerminalSpeedCap => terminalSpeedCap;
        
        /// <summary>
        /// Tick rate from GDD (120 ticks/s)
        /// </summary>
        public float TickRate => tickRate;
        
        /// <summary>
        /// Duration of one tick in seconds
        /// </summary>
        public float TickDuration => tickDuration;
        
        /// <summary>
        /// Fixed-point versions for deterministic simulation
        /// </summary>
        public FixedPoint GravityAccelerationFixed => FixedPoint.FromFloat(gravityAcceleration);
        public FixedPoint FrictionAccelerationFixed => FixedPoint.FromFloat(frictionAcceleration);
        public FixedPoint TerminalSpeedCapFixed => FixedPoint.FromFloat(terminalSpeedCap);
        public FixedPoint TickDurationFixed => FixedPoint.FromFloat(tickDuration);
        
        private void OnValidate()
        {
            // Recalculate tick duration when tick rate changes
            if (tickRate > 0)
            {
                tickDuration = 1.0f / tickRate;
            }
        }
        
        /// <summary>
        /// Calculates acceleration for a marble based on ramp angle and surface type
        /// Implements: Gravity component: +0.10 cells/s² × sin θ (θ = ramp angle)
        /// Plus: Friction (flat track): –0.05 cells/s²
        /// </summary>
        /// <param name="rampAngleRadians">Ramp angle in radians</param>
        /// <param name="isFlatTrack">True if this is flat track (applies friction)</param>
        /// <returns>Total acceleration in cells/s²</returns>
        public FixedPoint CalculateAcceleration(FixedPoint rampAngleRadians, bool isFlatTrack)
        {
            // Gravity component: +0.10 cells/s² × sin θ
            FixedPoint gravityComponent = GravityAccelerationFixed * FixedPoint.Sin(rampAngleRadians);
            
            // Friction (flat track): –0.05 cells/s²
            FixedPoint frictionComponent = isFlatTrack ? FrictionAccelerationFixed : FixedPoint.Zero;
            
            return gravityComponent + frictionComponent;
        }
        
        /// <summary>
        /// Integrates velocity over one tick with acceleration
        /// Clamps to terminal speed cap as specified in GDD
        /// </summary>
        /// <param name="currentVelocity">Current velocity in cells/s</param>
        /// <param name="acceleration">Acceleration in cells/s²</param>
        /// <returns>New velocity clamped to terminal speed</returns>
        public FixedPoint IntegrateVelocity(FixedPoint currentVelocity, FixedPoint acceleration)
        {
            // v = v₀ + a × Δt
            FixedPoint newVelocity = currentVelocity + acceleration * TickDurationFixed;
            
            // Clamp to terminal speed cap: 5 cells/s
            if (newVelocity > TerminalSpeedCapFixed)
            {
                newVelocity = TerminalSpeedCapFixed;
            }
            else if (newVelocity < -TerminalSpeedCapFixed)
            {
                newVelocity = -TerminalSpeedCapFixed;
            }
            
            return newVelocity;
        }
        
        /// <summary>
        /// Integrates position over one tick with velocity
        /// </summary>
        /// <param name="currentPosition">Current position in cells</param>
        /// <param name="velocity">Velocity in cells/s</param>
        /// <returns>New position</returns>
        public FixedPoint IntegratePosition(FixedPoint currentPosition, FixedPoint velocity)
        {
            // x = x₀ + v × Δt
            return currentPosition + velocity * TickDurationFixed;
        }
        
        /// <summary>
        /// Complete physics integration step for one marble
        /// </summary>
        /// <param name="currentPosition">Current position in cells</param>
        /// <param name="currentVelocity">Current velocity in cells/s</param>
        /// <param name="rampAngleRadians">Ramp angle in radians</param>
        /// <param name="isFlatTrack">True if this is flat track</param>
        /// <param name="newPosition">Output: new position</param>
        /// <param name="newVelocity">Output: new velocity</param>
        public void IntegrateMarble(
            FixedPoint currentPosition, 
            FixedPoint currentVelocity,
            FixedPoint rampAngleRadians,
            bool isFlatTrack,
            out FixedPoint newPosition,
            out FixedPoint newVelocity)
        {
            // Calculate acceleration based on ramp angle and surface type
            FixedPoint acceleration = CalculateAcceleration(rampAngleRadians, isFlatTrack);
            
            // Integrate velocity
            newVelocity = IntegrateVelocity(currentVelocity, acceleration);
            
            // Integrate position
            newPosition = IntegratePosition(currentPosition, newVelocity);
        }
    }
}