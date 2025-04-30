using UnityEngine;

public class BitonicSorter
{
    ComputeShader sortShader;
    int kernelBitonic;

    public BitonicSorter(ComputeShader shader)
    {
        sortShader = shader;
        kernelBitonic = sortShader.FindKernel("BitonicSort");
    }

    public void Sort(ComputeBuffer buffer, int elementCount)
    {
        sortShader.SetInt("NumElements", elementCount);
        sortShader.SetBuffer(kernelBitonic, "Data", buffer);

        int numStages = (int)Mathf.Log(elementCount, 2);
        for (int stage = 1; stage <= numStages; stage++)
        {
            for (int passOfStage = stage; passOfStage > 0; passOfStage--)
            {
                sortShader.SetInt("Stage", stage);
                sortShader.SetInt("PassOfStage", passOfStage);
                sortShader.Dispatch(kernelBitonic, Mathf.Max(1, elementCount / 512), 1, 1);
            }
        }
    }
}