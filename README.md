# Life Simulator — GPU-Accelerated Cellular Evolution

A real-time artificial life simulator powered by GPU compute shaders. Watch as digital organisms evolve strategies for survival through energy collection, reproduction, and mutation in a dynamic environment.

## Project Overview

This simulator implements a complete ecosystem on the GPU where cellular organisms compete for resources (organics and energy) distributed across a soil substrate. Organisms are guided by genetic programs encoded in 32-gene genomes, enabling complex behaviors like growth, migration, energy trading, and reproduction.

The entire simulation runs on the GPU via compute shaders, with only minimal CPU orchestration for UI, parameter adjustment, and async readback of statistics.

## World Structure

### Environment & Resources

The world is a continuous 2D grid containing two primary resources:

- **Soil Organics** — Available through roots, decays through diffusion, represents plant matter and nutrients
- **Soil Energy** — Available through antenna structures, produced by leaf photosynthesis, gradually diffuses across the landscape
- **Sunlight** — Global parameter that boosts leaf production efficiency

### Simulation Cycle

Each frame executes 12 sequential substeps:

1. **Energy Diffusion** — Soil energy disperses across the grid (9-point averaging kernel)
2. **Leaf Production** — Leaf cells convert available organics into cellular energy (photosynthesis-like process)
3. **Root Absorption** — Root cells extract organics from soil and convert to energy; convert to wood if starving
4. **Antenna Absorption** — Antenna cells extract energy from soil; convert to wood if source depleted
5. **Energy Rerouting** — Cells establish energy transport channels with neighbors based on availability
6. **Energy Transport** — Energy flows between connected cells along established channels
7. **Life Maintenance** — All cells consume energy to stay alive
8. **Death Check** — Cells die if energy depleted or local conditions become unfavorable
9. **Cell Death Processing** — Dead cells spill resources back into soil; cleanup connections
10. **Seed Movement** — Seed cells migrate if unattached; convert to sprouts upon maturation
11. **Decision Making** — Sprout cells read their genome and select an action
12. **Action Execution** — Execute the selected behavior (grow, move, eat, separate, etc.)

## Cell Types & Behaviors

### Cell Type Hierarchy

Each cell has a fundamental type that determines its role in the organism:

- **Leaf** — Primary energy producer; absorbs light energy from abundant organics
- **Root** — Resource harvester; extracts organics from soil substrate
- **Antenna** — Energy collector; extracts soil energy for distribution
- **Wood** — Structural support and energy conduit; inert but efficient at transport
- **Sprout** — Active growth node; interprets genome and executes behaviors
- **Seed** — Mobile reproduction unit; migrates before developing into sprout
- **Empty** — No cell present; available for growth

## Genetic System

### Genome Structure

Each organism carries a 32-gene genome. Each gene encodes:

- **Growth directions** — Which cell types to spawn in each relative direction
- **Conditions** — Two environmental checks (resource abundance, obstacles, neighbor comparison, randomness)
- **Parameters** — Thresholds and comparison values for conditions
- **Commands** — Action to execute if conditions succeed (with fallback if they fail)
- **Gene transitions** — Which gene becomes active next based on success/failure

### Available Gene Conditions

1. **Organic vs. Energy** — Check if soil has more organics than energy
2. **Obstacle detection** — Test for neighbors blocking growth/move
3. **Resource comparison** — Compare resource levels in neighboring cells
4. **Abundance checks** — Local or regional resource availability
5. **Randomness** — Probabilistic branching

### Available Commands

Commands allow organisms to:

- **Grow** — Spawn new cells in specified directions
- **Move** — Relocate in the cell direction (single cells)
- **Rotate** — Reorient the cell direction (single cells)
- **Extract** — Harvest organics or energy from soil
- **Trade** — Exchange resources with neighboring cells
- **Reproduce** — Create seeds for dispersal
- **Consume** — Eat nearby cells for quick energy
- **Separate** — Break connections
- **Adapt** — Transform into a seed to wait for better environment conditions
- **Attach** — Attach to nearby organism to receive energy from it

## Evolution & Mutation

### Reproduction Strategy

When an organism creates an offspring (seed or sprout):

1. **Inherit** the parent's genome or create a mutated copy (25% chance)
2. **Placement** of offspring depends on available space and genetic program
3. **Initialization** with modest energy; must survive on its own

### Mutation Mechanism

When mutations occur, a random gene is selected and one of its properties is randomly modified:

- Growth direction types shift
- Condition thresholds adjust
- Command sequences change
- Gene transitions rewrite
- Behavioral parameters drift

### Evolutionary Dynamics

Over generations, populations naturally select for:

- **Efficient collectors** — Organisms with good root/leaf placement
- **Robust structures** — Shapes resistant to starvation
- **Smart reproduction** — Genomes that spawn offspring in favorable conditions
- **Adaptive behaviors** — Dynamic response to local environment changes
- **Cooperative strategies** — Resource sharing between specialized cells (emerging behavior)

The combination of finite energy, spatial constraints, and random mutations drives continuous adaptation. Successful strategies persist; failed experiments die and are recycled into the soil.

## Architecture & Implementation

### GPU-Accelerated Design

The entire simulation runs on the GPU for maximum performance:

- **Compute Shaders** execute parallel cell updates across the grid
- **Structured Buffers** store cell data, genomes, and resource values
- **Ping-pong Textures** enable simultaneous reads and writes without conflicts
- **Atomic Operations** safely manage shared state (genome refcounts, resource mutations)
- **Tree-reduce Aggregation** efficiently computes global statistics without CPU blocking

### CPU Role

CPU orchestration is minimal:

- **Frame dispatch** — Trigger GPU compute passes in correct sequence
- **Parameter updates** — Adjust simulation constants (sunlight, diffusion rate, life costs)
- **UI rendering** — Display world heatmaps, cell graphics, and energy flow overlays
- **Player tools** — Apply brushes to add/remove resources or kill cells
- **Async readback** — Non-blocking queries of individual cell data for inspection

### Rendering System

The simulator supports multiple visual modes:

- **Organism view** — Renders individual cells with directional graphics and energy flow arrows
- **Cell energy heatmap** — Visualizes energy levels across all cells
- **Soil organics heatmap** — Shows nutrient distribution in the environment
- **Soil energy heatmap** — Displays available energy in the substrate

### Interactive Controls

- **Play/Pause** — Control simulation flow
- **Speed adjustment** — Run faster or single-step for debugging
- **Brush tools** — Add/remove resources or kill cells in an area
- **Cell inspector** — Query individual cell data, genome content, and history in real-time
- **Statistics** — Track population counts by cell type, resource totals, and diversity metrics

## Getting Started

### Requirements

- Unity 6.4 LTS or later
- GPU supporting Compute Shaders (most modern GPUs)
- HLSL/Compute Shader support in your graphics backend

### Running the Simulation

1. Open the scene `Assets/Scenes/Main.unity`
2. Press **Play** in the Unity editor
3. Use **Space** to pause/resume
4. Use **[1-4]** to switch visualization modes
5. Use **[R]** to restart with a fresh world
6. Use brush tools to interact with the environment

### Customization

- Adjust **sunlight** intensity to change energy availability
- Modify **cell constants** (growth cost, maintenance, diffusion rate) to tune difficulty
- Adjust **brush properties** to control interaction magnitude

## Project Structure

```
Assets/
├── Code/
│   ├── WorldSimulation.cs          # Main orchestrator
│   ├── Definitions.cs              # Data structures
│   ├── WorldRenderer.cs            # Visualization
│   ├── GenomeStorage.cs            # Genome library
│   └── UI/
│       ├── SimulationUI.cs         # Controls & inspector
│       └── UIPanZoom.cs            # Camera control
├── Shaders/
│   ├── Simulation.compute          # Main simulation passes
│   ├── Init.compute                # Initialization kernels
│   ├── Mutator.compute             # External modification kernels
│   ├── CellBehavior.compute        # Genome interpretation
│   ├── *.hlsl                      # Shared libraries & utilities
│   └── *.shader                    # Rendering shaders
├── Data/
│   └── [Default genome collections]
└── Scenes/
    └── Main.unity                  # Main simulation scene
```

## Performance Characteristics

- **Typical framerates**: 60+ FPS at 1024x1024 grid on modern GPUs
- **Scalability**: Performance scales with GPU compute units
- **Memory**: ~2GB for base simulation + textures + buffers

## Future Possibilities

- **Long-term evolution** — Record lineage histories and diversity metrics
- **Environmental variation** — Moving sunlight patterns, resource gradients

## References

This project implements concepts described in video series about artificial life research published on channel https://www.youtube.com/@foo52ru.

## License

This project is distributed under the **MIT** license. The full text of the license and usage conditions are available in the [LICENSE](./LICENSE) file.

## Author

Created as an experimental platform for studying digital evolution and emergent complexity.

@FortyFourTech
