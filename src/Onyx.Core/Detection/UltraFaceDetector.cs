using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace Onyx.Core.Detection;

/// <summary>
/// Face detector using the UltraFace (version-RFB-320) ONNX model via ONNX
/// Runtime. Uses the DirectML execution provider (GPU) when available, falling
/// back to CPU. Input is 320x240 RGB normalized as (px - 127) / 128.
/// </summary>
public sealed class UltraFaceDetector : IFaceDetector
{
    private const int InW = 320;
    private const int InH = 240;

    private readonly InferenceSession _session;
    private readonly string _inputName;

    public float ScoreThreshold { get; set; } = 0.7f;
    public float NmsIouThreshold { get; set; } = 0.3f;

    /// <summary>True if the DirectML (GPU) provider was successfully enabled.</summary>
    public bool UsingGpu { get; }

    public UltraFaceDetector(string modelPath, bool useGpu = true)
    {
        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException(
                $"Face model not found at '{modelPath}'. Run .\\get-model.ps1 to download it.",
                modelPath);
        }

        var options = new SessionOptions { GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL };
        if (useGpu)
        {
            try
            {
                options.AppendExecutionProvider_DML(0);
                UsingGpu = true;
            }
            catch
            {
                UsingGpu = false; // fall back to CPU provider (default)
            }
        }

        _session = new InferenceSession(modelPath, options);
        _inputName = _session.InputMetadata.Keys.First();
    }

    public IReadOnlyList<Rect> Detect(Mat frame)
    {
        if (frame.Empty()) { return Array.Empty<Rect>(); }

        var input = Preprocess(frame);
        var inputs = new[] { NamedOnnxValue.CreateFromTensor(_inputName, input) };

        using var results = _session.Run(inputs);

        // UltraFace has two outputs: scores [1,N,2] and boxes [1,N,4].
        Tensor<float>? scores = null, boxes = null;
        foreach (var r in results)
        {
            var t = r.AsTensor<float>();
            if (t.Dimensions.Length == 3 && t.Dimensions[2] == 2) { scores = t; }
            else if (t.Dimensions.Length == 3 && t.Dimensions[2] == 4) { boxes = t; }
        }
        if (scores is null || boxes is null) { return Array.Empty<Rect>(); }

        return Postprocess(scores, boxes, frame.Width, frame.Height);
    }

    private static DenseTensor<float> Preprocess(Mat frame)
    {
        using var resized = new Mat();
        Cv2.Resize(frame, resized, new Size(InW, InH));
        Cv2.CvtColor(resized, resized, ColorConversionCodes.BGR2RGB);
        resized.GetArray(out Vec3b[] px);

        var t = new DenseTensor<float>(new[] { 1, 3, InH, InW });
        for (int y = 0; y < InH; y++)
        {
            for (int x = 0; x < InW; x++)
            {
                Vec3b p = px[y * InW + x];
                t[0, 0, y, x] = (p.Item0 - 127f) / 128f; // R
                t[0, 1, y, x] = (p.Item1 - 127f) / 128f; // G
                t[0, 2, y, x] = (p.Item2 - 127f) / 128f; // B
            }
        }
        return t;
    }

    private IReadOnlyList<Rect> Postprocess(Tensor<float> scores, Tensor<float> boxes, int fw, int fh)
    {
        int n = scores.Dimensions[1];
        var candidates = new List<(Rect box, float score)>();

        for (int i = 0; i < n; i++)
        {
            float score = scores[0, i, 1]; // index 1 = "face" probability
            if (score < ScoreThreshold) { continue; }

            float x1 = boxes[0, i, 0] * fw;
            float y1 = boxes[0, i, 1] * fh;
            float x2 = boxes[0, i, 2] * fw;
            float y2 = boxes[0, i, 3] * fh;

            var rect = new Rect(
                (int)x1, (int)y1,
                (int)Math.Max(0, x2 - x1), (int)Math.Max(0, y2 - y1));
            candidates.Add((rect, score));
        }

        return NonMaxSuppression(candidates);
    }

    private List<Rect> NonMaxSuppression(List<(Rect box, float score)> items)
    {
        var kept = new List<Rect>();
        foreach (var (box, _) in items.OrderByDescending(c => c.score))
        {
            bool overlaps = kept.Any(k => IoU(k, box) > NmsIouThreshold);
            if (!overlaps) { kept.Add(box); }
        }
        return kept;
    }

    private static float IoU(Rect a, Rect b)
    {
        var inter = a.Intersect(b);
        float interArea = inter.Width * (float)inter.Height;
        if (interArea <= 0) { return 0; }
        float union = a.Width * (float)a.Height + b.Width * (float)b.Height - interArea;
        return union <= 0 ? 0 : interArea / union;
    }

    public void Dispose() => _session.Dispose();
}
