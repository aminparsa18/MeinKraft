public interface IDisplayService
{
    IReadOnlyList<DisplayResolution> GetDisplayResolutions();
    DisplayResolution GetDisplayResolutionDefault();
}