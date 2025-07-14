using UnityEngine;

namespace MarbleMaker.Core
{
    /// <summary>
    /// Core game constants as defined in the GDD and TDD
    /// </summary>
    public static class GameConstants
    {
        // Timing & Simulation (from GDD Section 4.2)
        public const int TICK_RATE = 120; // ticks per second
        public const float TICK_DURATION = 1f / TICK_RATE; // seconds per tick
        
        // Grid System (from GDD Section 4.1)
        public const float CELL_SIZE = 1f; // 1 × 1 × 1 units
        public const int ROTATION_STEP = 90; // degrees
        
        // Physics Constants (from GDD Section 4.2)
        public const float GRAVITY_ACCELERATION = 0.10f; // cells/s² × sin θ
        public const float FRICTION_FLAT = 0.05f; // cells/s² on flat track
        public const float TERMINAL_SPEED_DEFAULT = 5f; // cells/s
        
        // Performance Budgets (from TDD Section 6)
        public const int MAX_MARBLES_PC = 20000;
        public const int MAX_MARBLES_STEAM_DECK = 10000;
        public const int MAX_MARBLES_SWITCH = 5000;
        
        // Grid Bounds (from TDD Section 7)
        public const int MAX_GRID_SIZE = 16384; // ±16,384 cells
        public const int MIN_GRID_SIZE = -16384;
        
        // Save System (from TDD Section 5)
        public const int MAX_SAVE_SIZE_KB = 1024; // 1 MB max save size
        public const int TYPICAL_SAVE_SIZE_KB = 200; // ~5,000 parts compressed
    }
} 