using System;

namespace PotopopiCamSync.Models
{
    public enum AIAnalysisMode
    {
        None,
        Standard,   // Blur + pHash (OpenCV)
        Balanced,   // CPU AI (ONNX)
        Extreme     // GPU AI (ONNX + CUDA/DirectML)
    }
}
