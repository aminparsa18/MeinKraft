using MeinKraft;

public partial class Game
{
    internal Packet_ServerRedirect redirectTo;

    // -------------------------------------------------------------------------
    // Server identification
    // -------------------------------------------------------------------------

    public void ProcessServerIdentification(Packet_Server packet)
    {
        _publisher.Publish(new SetupProgressEventArgs() { Title = "Identified By Server..." });
        LocalPlayerId = packet.Identification.AssignedClientId;
        ServerInfo.ConnectData = ConnectData;
        ServerInfo.ServerName = packet.Identification.ServerName;
        ServerInfo.ServerMotd = packet.Identification.ServerMotd;
        TerrainChunkTesselator.ENABLE_TEXTURE_TILING = packet.Identification.RenderHint_ == (int)RenderHint.Fast;

        var requiredMd5 = packet.Identification.RequiredBlobMd5;
        var requiredName = packet.Identification.RequiredBlobName;

        ChatLog("[GAME] Processed server identification");

        int getCount = 0;
        if (requiredMd5 != null)
        {
            ChatLog(string.Format("[GAME] Server has {0} assets", requiredMd5.Length));
            for (int i = 0; i < requiredMd5.Length; i++)
            {
                string md5 = requiredMd5[i];
                if (gameService.IsCached(md5))
                {
                    Asset cachedAsset = gameService.LoadAssetFromCache(md5);
                    string name = requiredName != null ? requiredName[i] : cachedAsset.name;
                    SetFile(name, cachedAsset.md5, cachedAsset.data, cachedAsset.dataLength);
                }
                else
                {
                    if (requiredName != null)
                    {
                        if (!HasAsset(md5, requiredName[i]))
                        {
                            getAsset[getCount++] = md5;
                        }
                    }
                    else
                    {
                        getAsset[getCount++] = md5;
                    }
                }
            }

            ChatLog(string.Format("[GAME] Will download {0} missing assets", getCount));
        }

        SendGameResolution();
        ChatLog("[GAME] Sent window resolution to server");
        sendResize = true;

        SendRequestBlob(getAsset, getCount);
        ChatLog("[GAME] Sent BLOB request");

        if (packet.Identification.MapSizeX != _voxelMap.MapSizeX
            || packet.Identification.MapSizeY != _voxelMap.MapSizeY
            || packet.Identification.MapSizeZ != _voxelMap.MapSizeZ)
        {
            _voxelMap.Reset(packet.Identification.MapSizeX,
                packet.Identification.MapSizeY,
                packet.Identification.MapSizeZ);
           _voxelMap.Heightmap.Restart(packet.Identification.MapSizeX,
                packet.Identification.MapSizeY);
        }

        _lightManager.ShadowsSimple = packet.Identification.DisableShadows == 1;
        maxdrawdistance = 256;
        ChatLog("[GAME] Map initialized");
    }

    internal void InvalidVersionAllow()
    {
        if (InvalidVersionDrawMessage != null)
        {
            InvalidVersionDrawMessage = null;
            ProcessServerIdentification(InvalidVersionPacketIdentification);
            InvalidVersionPacketIdentification = null;
        }
    }

    // -------------------------------------------------------------------------
    // Server redirect / exit
    // -------------------------------------------------------------------------

    public void ExitAndSwitchServer(Packet_ServerRedirect newServer)
    {
        redirectTo = newServer;
        IsExitingToMainMenu = true;
    }

    public void ExitToMainMenu()
    {
        redirectTo = null;
        IsExitingToMainMenu = true;
    }

    public Packet_ServerRedirect Redirect => redirectTo;

    // -------------------------------------------------------------------------
    // Chat log
    // -------------------------------------------------------------------------

    public void ChatLog(string p)
    {
        if (!gameService.ChatLog(ServerInfo.ServerName, p))
        {
            Console.WriteLine(string.Format(Language.CannotWriteChatLog(), ServerInfo.ServerName));
        }
    }
}