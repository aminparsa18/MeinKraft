public interface IAssetManager
{
    List<Asset> Assets { get; }
    Task LoadAssetsAsync(IProgress<float>? progress = null, CancellationToken ct = default);
}