using System.Security.Cryptography;

/// <summary>
/// Scans one or more directories and loads all files as <see cref="Asset"/> instances.
/// Each asset is fingerprinted with an MD5 hash for server-side deduplication and caching.
/// </summary>
public class ServerAssetManager : IAssetManager
{
    private readonly string _assetsRoot;

    public List<Asset> Assets { get; } = [];

    public ServerAssetManager(IWebHostEnvironment env)
    {
        _assetsRoot = Path.Combine(env.ContentRootPath, "wwwroot");
    }

    public async Task LoadAssetsAsync(IProgress<float>? progress = null, CancellationToken ct = default)
    {
        if (Assets.Count > 0)
        {
            progress?.Report(1f);
            return;
        }

        var files = await ReadManifestAsync(ct);
        var fileList = files.ToList();

        for (int i = 0; i < fileList.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            byte[] data = await LoadFileAsync(fileList[i], ct);

            Assets.Add(new Asset
            {
                data = data,
                dataLength = data.Length,
                name = Path.GetFileName(fileList[i]).ToLowerInvariant(),
                md5 = ComputeMd5(data),
            });

            progress?.Report((float)(i + 1) / fileList.Count);
        }
    }

    private async Task<IEnumerable<string>> ReadManifestAsync(CancellationToken ct)
    {
        string manifestPath = Path.Combine(_assetsRoot, "assets_manifest.txt");
        string content = await File.ReadAllTextAsync(manifestPath, ct);

        return content
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Replace('\\', '/'));
    }

    private async Task<byte[]> LoadFileAsync(string filename, CancellationToken ct)
    {
        string fullPath = Path.Combine(_assetsRoot, filename);
        return await File.ReadAllBytesAsync(fullPath, ct);
    }

    private static string ComputeMd5(byte[] data)
        => Convert.ToHexString(MD5.HashData(data)).ToLowerInvariant();
}