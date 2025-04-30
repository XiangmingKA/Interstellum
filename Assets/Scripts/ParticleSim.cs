using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Main simulation controller for galaxy particle simulation using compute shaders,
/// spatial hashing, bitonic sort, and neighbor-based gravitational interaction.
/// </summary>
public class ParticleSim : MonoBehaviour
{
    private Vector2 cursorPos;

    struct Particle
    {
        public Vector3 position;
        public Vector3 velocity;
        public float life;
        public float temp;
        public float mass;
    }

    const int SIZE_PARTICLE = 9 * sizeof(float);

    [Header("Simulation Parameters")]
    public int particleCount = 1024 * 1024;
    public float neighbourDistance = 1.0f;
    public float gridCellSize = 1.0f;
    public Vector2 boundsSize = new Vector2(100, 100);

    [Header("External References")]
    public Material material;
    public ComputeShader particleCompute;
    public ComputeShader particleHashCompute;
    public ComputeShader offsetShader;
    public ComputeShader sortShader;
    public Transform galaxy1;
    public Transform galaxy2;

    private int kernelID;
    private int kernelHash;
    private int kernelClearOffsets;
    private int kernelBuildOffsets;
    private int groupSizeX;
    private int maxHash;

    private ComputeBuffer particleBuffer;
    private ComputeBuffer hashBuffer;
    private ComputeBuffer cellOffsetBuffer;

    private RenderParams rp;
    private BitonicSorter sorter;

    void Start()
    {
        // Initialize compute buffers and shader parameters
        sorter = new BitonicSorter(sortShader);
        hashBuffer = new ComputeBuffer(particleCount, sizeof(uint) * 2);
        maxHash = Mathf.CeilToInt(boundsSize.x / gridCellSize) * Mathf.CeilToInt(boundsSize.y / gridCellSize);
        cellOffsetBuffer = new ComputeBuffer(maxHash + 1, sizeof(uint));
        InitParticles();
    }

    void InitParticles()
    {
        Particle[] particleArray = new Particle[particleCount];

        for (int i = 0; i < particleCount; i++)
        {
            Vector3 xyz = Random.onUnitSphere * (Random.value * 5);
            particleArray[i].position = xyz;
            particleArray[i].velocity = Vector3.zero;
            particleArray[i].life = 1.0f;
            particleArray[i].temp = 0.0f;
            particleArray[i].mass = 1.0f;
        }

        particleBuffer = new ComputeBuffer(particleCount, SIZE_PARTICLE);
        particleBuffer.SetData(particleArray);

        kernelID = particleCompute.FindKernel("CSParticle");
        kernelHash = particleHashCompute.FindKernel("HashParticles");
        kernelClearOffsets = offsetShader.FindKernel("ClearOffsets");
        kernelBuildOffsets = offsetShader.FindKernel("BuildOffsets");

        uint threadsX;
        particleCompute.GetKernelThreadGroupSizes(kernelID, out threadsX, out _, out _);
        groupSizeX = Mathf.CeilToInt((float)particleCount / threadsX);

        // Set static shader parameters
        particleHashCompute.SetFloat("cellSize", gridCellSize);
        particleHashCompute.SetInts("bounds", (int)boundsSize.x, (int)boundsSize.y);

        particleCompute.SetFloat("cellSize", gridCellSize);
        particleCompute.SetInts("bounds", (int)boundsSize.x, (int)boundsSize.y);

        particleCompute.SetBuffer(kernelID, "particleBuffer", particleBuffer);
        material.SetBuffer("particleBuffer", particleBuffer);

        rp = new RenderParams(material)
        {
            worldBounds = new Bounds(Vector3.zero, 10000 * Vector3.one)
        };
    }

    void OnDestroy()
    {
        particleBuffer?.Release();
        hashBuffer?.Release();
        cellOffsetBuffer?.Release();
    }

    void Update()
    {
        // Step 1: Spatial hashing
        particleHashCompute.SetBuffer(kernelHash, "particles", particleBuffer);
        particleHashCompute.SetBuffer(kernelHash, "hashes", hashBuffer);
        particleHashCompute.Dispatch(kernelHash, groupSizeX, 1, 1);

        // Step 2: Sort particles by hash
        sorter.Sort(hashBuffer, particleCount);

        // Step 3: Clear and build cell offset table
        offsetShader.SetInt("NumParticles", particleCount);
        offsetShader.SetInt("MaxHash", maxHash);

        offsetShader.SetBuffer(kernelClearOffsets, "cellOffsets", cellOffsetBuffer);
        offsetShader.SetBuffer(kernelClearOffsets, "sortedHashes", hashBuffer);
        offsetShader.Dispatch(kernelClearOffsets, Mathf.CeilToInt((maxHash + 1) / 256f), 1, 1);

        offsetShader.SetBuffer(kernelBuildOffsets, "sortedHashes", hashBuffer);
        offsetShader.SetBuffer(kernelBuildOffsets, "cellOffsets", cellOffsetBuffer);
        offsetShader.Dispatch(kernelBuildOffsets, Mathf.CeilToInt((float)particleCount / 256f), 1, 1);

        // Step 4: Compute particle interactions
        particleCompute.SetBuffer(kernelID, "particles", particleBuffer);
        particleCompute.SetBuffer(kernelID, "hashes", hashBuffer);
        particleCompute.SetBuffer(kernelID, "cellOffsets", cellOffsetBuffer);

        particleCompute.SetFloat("deltaTime", Time.deltaTime);
        particleCompute.SetFloat("time", Time.time);
        particleCompute.SetFloat("neighbourDistance", neighbourDistance);
        particleCompute.SetFloats("galaxyCenter1", galaxy1.position.x, galaxy1.position.y, galaxy1.position.z);
        particleCompute.SetFloats("galaxyCenter2", galaxy2.position.x, galaxy2.position.y, galaxy2.position.z);
        particleCompute.SetInt("particlesCount", particleCount);
        particleCompute.SetInt("maxHash", maxHash);

        particleCompute.Dispatch(kernelID, groupSizeX, 1, 1);

        // Step 5: Render
        Graphics.RenderPrimitives(rp, MeshTopology.Points, 1, particleCount);
    }

    void OnGUI()
    {
        // Convert mouse position to world coordinates
        Camera c = Camera.main;
        Event e = Event.current;
        Vector2 mousePos = new Vector2(e.mousePosition.x, c.pixelHeight - e.mousePosition.y);
        Vector3 worldPos = c.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, c.nearClipPlane));

        cursorPos = new Vector2(worldPos.x, worldPos.y);
    }
}
