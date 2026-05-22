using SDL2;

public sealed class DisplayService : IDisplayService
{
    public IReadOnlyList<DisplayResolution> GetDisplayResolutions()
    {
        List<DisplayResolution> resolutions = [];

        if (SDL.SDL_Init(SDL.SDL_INIT_VIDEO) != 0)
        {
            throw new Exception(SDL.SDL_GetError());
        }

        int displayCount = SDL.SDL_GetNumVideoDisplays();

        for (int displayIndex = 0; displayIndex < displayCount; displayIndex++)
        {
            int modeCount = SDL.SDL_GetNumDisplayModes(displayIndex);

            for (int modeIndex = 0; modeIndex < modeCount; modeIndex++)
            {
                SDL.SDL_GetDisplayMode(displayIndex, modeIndex, out SDL.SDL_DisplayMode mode);

                DisplayResolution resolution = new()
                {
                    Width = mode.w,
                    Height = mode.h,
                    BitsPerPixel = SDL.SDL_BITSPERPIXEL(mode.format),
                    RefreshRate = mode.refresh_rate
                };

                if (resolution.Width < 800 ||
                    resolution.Height < 600 ||
                    resolution.BitsPerPixel < 16)
                {
                    continue;
                }

                resolutions.Add(resolution);
            }
        }

        return [.. resolutions
            .DistinctBy(r => (r.Width, r.Height, r.RefreshRate, r.BitsPerPixel))
            .OrderBy(r => r.Width)
            .ThenBy(r => r.Height)
            .ThenBy(r => r.RefreshRate)];
    }

    public DisplayResolution GetDisplayResolutionDefault()
    {
        // Primary display is usually display index 0
        int displayIndex = 0;

        if (SDL.SDL_GetDesktopDisplayMode(displayIndex, out SDL.SDL_DisplayMode mode) != 0)
        {
            // fallback
            return new DisplayResolution
            {
                Width = 1920,
                Height = 1080,
                BitsPerPixel = 32,
                RefreshRate = 60
            };
        }

        return new DisplayResolution
        {
            Width = mode.w,
            Height = mode.h,
            BitsPerPixel = SDL.SDL_BITSPERPIXEL(mode.format),
            RefreshRate = mode.refresh_rate
        };
    }
}