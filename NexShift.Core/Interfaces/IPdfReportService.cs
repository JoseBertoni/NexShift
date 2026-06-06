namespace NexShift.Core.Interfaces
{
    public interface IPdfReportService
    {
        Task<byte[]> GenerateDebtReport(ProjectAnalysisResult analysis, string repoUrl);
    }
}
