using NexShift.Core.Entities;

namespace NexShift.Core.Interfaces
{
    public interface INugetService
    {
        Task<NugetPackage?> GetPackageInfoAsync(string packageName);
        Task<bool> CheckAndUpdatePackageAsync(string packageName);
    }
}
