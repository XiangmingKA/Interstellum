# Interstellum: Galactic Collision Simulator

**Interstellum** is a high-performance real-time simulation that visualizes the collision and eventual merging of two galaxies. It uses Unity 6 and GPU-based compute shaders to simulate and render up to **4 million particles**, each representing a star or a stellar object. The system computes gravitational interactions between particles, optimized by **Bitonic Sort** and **Grid Hashing** to support large-scale particle systems at interactive frame rates.

![Interstellum](Image/interstellum.gif)

## Features

* Realistic gravitational simulation of two colliding galaxies
* GPU-accelerated particle simulation using compute shaders
* Optimized performance using spatial hashing and Bitonic sorting
* Particle color encoding based on density and distance
* Black hole accretion center simulation
* Fully implemented in Unity 6

## Core Concepts

### Particle Structure
Each particle contains position, velocity, life, and distance-to-center:

```hlsl
struct Particle {
    float3 position;
    float3 velocity;
    float life;
    float distanceToCenter;
};
```

### Gravitational Physics
Each particle is affected by gravity from two galactic centers. The gravitational force is calculated as:

```hlsl
float3 dir = center - particle.position;
float dist = max(length(dir), 1.0);
float3 gravityForce = normalize(dir) * G / (dist * dist);
```

This force is applied to the particle’s velocity. When two galaxy centers are used, the resulting force is the sum of the two:

```hlsl
particle.velocity += (forceFromGalaxy1 + forceFromGalaxy2) * deltaTime;
```

### Neighborhood Interaction and Optimization
Why Sorting? 
Computing all particle-pair gravitational interactions results in O(N²) time complexity, which is infeasible for millions of particles. However, gravitational interactions mostly occur between nearby particles, so we limit calculations to spatial neighbors.

To enable this, we use Grid Hashing:
1. Divide 2D space into a uniform grid.
2. For each particle, compute its cell ID based on position.
3. Store a key-value pair (cell hash, particle index).

```hlsl
uint2 gridCoord = floor(particle.position.xy / cellSize);
uint hash = gridCoord.y * gridResolution + gridCoord.x;
```

### Bitonic Sort
After hashing, particles must be sorted by cell ID to group neighboring particles together in memory. We use **Bitonic Sort**, a GPU-parallel sorting algorithm that works efficiently on structured buffers in compute shaders.

How Bitonic Sort Works (Simplified):
* Bitonic sort works by building **bitonic sequences** (where data first increases then decreases), then merging them to form sorted sequences.
* The algorithm is ideal for GPU execution because of its predictable data access pattern and regular structure.
* It has **O(log² N)** time complexity and performs well on the GPU even for millions of items.

Integration with Particle System:
* Each frame, the compute shader:
    * Computes cell hashes.
    * Sorts particles by cell hash using Bitonic sort.
    * In a subsequent kernel, iterates over each particle and checks only the particles in the same or adjacent cells.

This spatial locality allows us to reduce neighbor search complexity to **O(N)** (plus constant work per particle).

## Code Snippets
### Assigning Cell Hash in Compute Shader

```hlsl
int2 gridPos = floor(particle.position.xy / cellSize);
uint hash = gridPos.y * gridResolution + gridPos.x;
hashBuffer[i] = hash;
```

### Neighborhood Interaction (Simplified)

```hlsl
for (int j = i - neighborRange; j <= i + neighborRange; j++) {
    if (j < 0 || j >= particleCount) continue;
    if (hashBuffer[j] != hashBuffer[i]) continue;

    float3 dir = particles[j].position - particles[i].position;
    float dist = length(dir);
    if (dist < neighborDistance) {
        float3 force = normalize(dir) * gravityStrength / (dist * dist + epsilon);
        particles[i].velocity += force * deltaTime;
    }
}
```

## How to Run

* Open the project in Unity 6.
* Load the main scene (Particle Galaxy.unity).
* Press Play to begin simulation.

## Performance Summary

* Particle Count: Up to 4,000,000
* 60 FPS on RTX 3060
* Sorting Algorithm: GPU Bitonic Sort
* Interaction Space: Grid Hashed Neighborhood
* Compute Kernels: Position Update, Grid Hashing, Bitonic Sort, Force Accumulation

## File Structure

```
Assets/
├── Shaders/
│   ├── ParticleSim.compute          # Main compute shader
│   ├── BitonicSort.compute          # GPU-based sorting
├── Scripts/
│   ├── ParticleController.cs        # C# script handling simulation loop
│   ├── BitonicSorter.cs             # Manages sorting dispatch
├── Materials/
├── Scenes/
│   └── Particle Galaxy.unity
```
