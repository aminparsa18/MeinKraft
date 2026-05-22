public partial class Game
{
    // -------------------------------------------------------------------------
    // Asset lookup
    // -------------------------------------------------------------------------

    private bool HasAsset(string md5, string name)
    {
        for (int i = 0; i < _assetManager.Assets.Count; i++)
        {
            Asset a = _assetManager.Assets[i];
            // Check both MD5 and name as there might be files with same content.
            if (a.md5 == md5 && a.name == name)
            {
                return true;
            }
        }

        return false;
    }

    public byte[] GetAssetFile(string p)
    {
        string pLower = p.ToLowerInvariant();
        for (int i = 0; i < _assetManager.Assets.Count; i++)
        {
            if (_assetManager.Assets[i].name == pLower)
            {
                return _assetManager.Assets[i].data;
            }
        }

        return null;
    }

    public int GetAssetFileLength(string p)
    {
        string pLower = p.ToLowerInvariant();
        for (int i = 0; i < _assetManager.Assets.Count; i++)
        {
            if (_assetManager.Assets[i].name == pLower)
            {
                return _assetManager.Assets[i].dataLength;
            }
        }

        return 0;
    }

    // -------------------------------------------------------------------------
    // Asset cache
    // -------------------------------------------------------------------------

    private void CacheAsset(Asset asset)
    {
        // Prevent crash on old servers that don't send a checksum.
        if (asset.md5 == null)
        {
            return;
        }

        if (!EncodingHelper.IsChecksum(asset.md5))
        {
            return;
        }

        if (!gameService.IsCached(asset.md5))
        {
            gameService.SaveAssetToCache(asset);
        }
    }

    public void SetFile(string name, string md5, byte[] downloaded, int downloadedLength)
    {
        string nameLower = name.ToLowerInvariant();

        // Update mouse cursor if the cursor asset changed.
        if (nameLower == "mousecursor.png")
        {
            gameService.SetWindowCursor(0, 0, 32, 32, downloaded, downloadedLength);
        }

        Asset newAsset = new()
        {
            data = downloaded,
            dataLength = downloadedLength,
            name = nameLower,
            md5 = md5
        };

        for (int i = 0; i < _assetManager.Assets.Count; i++)
        {
            if (_assetManager.Assets[i] == null)
            {
                continue;
            }

            if (_assetManager.Assets[i].name == nameLower)
            {
                if (options.UseServerTextures)
                {
                    _assetManager.Assets[i] = newAsset;
                }

                CacheAsset(newAsset);
                return;
            }
        }

        _assetManager.Assets.Add(newAsset);
        CacheAsset(newAsset);
    }
}