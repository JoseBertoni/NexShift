namespace NexShift.Core.Interfaces;

public class CodeTransformRequest
{
    public string FilePath { get; set; } = string.Empty;
    public string OriginalCode { get; set; } = string.Empty;
    public string SourceFramework { get; set; } = "net45";
    public string TargetFramework { get; set; } = "net8.0";
    public List<string> DeprecatedPackagesFound { get; set; } = new();
}

public class CodeTransformResult
{
    public string TransformedCode { get; set; } = string.Empty;
    public List<string> ChangesApplied { get; set; } = new();
    public List<string> ManualReviewRequired { get; set; } = new();
    public bool WasModified { get; set; }
    public bool WasAiTransformed { get; set; }  // true si Claude fue invocado
}

public interface ICodeTransformer
{
    Task WarmUpAsync(CancellationToken cancellationToken = default);
    Task<CodeTransformResult> TransformAsync(CodeTransformRequest request, CancellationToken cancellationToken = default);
}