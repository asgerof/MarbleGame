# MarbleMaker

A marble game project with comprehensive documentation and development planning.

## Project Structure

```
MarbleMaker/
├── docs/                           # Project documentation
│   ├── Feature Backlog - PRD.md   # Product Requirements Document
│   ├── GDD.md                      # Game Design Document
│   ├── System Architecture Diagram.md  # Technical architecture
│   └── TDD.md                      # Technical Design Document
└── README.md                       # This file
```

## Documentation

The project includes comprehensive documentation in the `docs/` folder:

- **[Game Design Document (GDD)](docs/GDD.md)** - Core game mechanics, features, and design vision
- **[Product Requirements Document (PRD)](docs/Feature%20Backlog%20-%20PRD.md)** - Feature backlog and requirements
- **[Technical Design Document (TDD)](docs/TDD.md)** - Technical implementation details
- **[System Architecture](docs/System%20Architecture%20Diagram.md)** - High-level system architecture

## Key Features

### Deterministic ECS Lookups
The simulation engine features a high-performance, deterministic entity lookup system:

- **Unified API**: `ECSLookups` provides consistent access to splitters, lifts, goals, and marbles by cell position
- **Frame-rebuilt caches**: `LookupCacheBuildSystem` rebuilds spatial hash maps every frame for deterministic queries
- **System group ordering**: `MotionGroup → LookupCacheGroup → ModuleLogicGroup` ensures consistent state
- **Deterministic multi-value selection**: When multiple entities occupy the same cell, selection uses `Entity.Index` comparison for replay consistency

### Performance Optimizations
- Aggressive inlining of hot-path lookup methods
- High-water mark capacity management to reduce allocations
- Parallel cache population with proper dependency management
- World isolation prevents cross-world cache pollution

## Getting Started

This project is currently in the planning and documentation phase. Implementation details and setup instructions will be added as development progresses.

## Contributing

Please refer to the documentation in the `docs/` folder for project requirements and technical specifications before contributing.

## License

[License information to be added] 