using System.Security.Cryptography;

/// <summary>
/// Scans one or more directories and loads all files as <see cref="Asset"/> instances.
/// Each asset is fingerprinted with an MD5 hash for server-side deduplication and caching.
/// </summary>
public class AssetManager : IAssetManager
{
    public List<Asset> Assets => _assets;
    private readonly List<Asset> _assets = [];

    public async Task LoadAssetsAsync(IProgress<float>? progress = null, CancellationToken ct = default)
    {
        if (_assets.Count > 0)
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

            _assets.Add(new Asset
            {
                data = data,
                dataLength = data.Length,
                name = Path.GetFileName(fileList[i]).ToLowerInvariant(),
                md5 = ComputeMd5(data),
            });

            progress?.Report((float)(i + 1) / fileList.Count);
        }
    }

    private static async Task<IEnumerable<string>> ReadManifestAsync(CancellationToken ct)
    {
        await using Stream s = await FileSystem.OpenAppPackageFileAsync("assets_manifest.txt");
        using var sr = new StreamReader(s);
        string content = await sr.ReadToEndAsync(ct);

        return content
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Replace('\\', '/'));
    }

    private static async Task<byte[]> LoadFileAsync(string filename, CancellationToken ct)
    {
        await using Stream stream = await FileSystem.OpenAppPackageFileAsync(filename);
        using MemoryStream ms = new();
        await stream.CopyToAsync(ms, ct);
        return ms.ToArray();
    }

    private static string ComputeMd5(byte[] data)
        => Convert.ToHexString(MD5.HashData(data)).ToLowerInvariant();
}