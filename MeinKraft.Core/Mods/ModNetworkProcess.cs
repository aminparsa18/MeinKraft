//This class is the network packet processor — it sits between the raw network socket and the rest of the game.
//Every frame on a background thread it reads incoming messages from the server,
//deserializes them, and splits work into two phases:
//Background thread(ProcessInBackground) handles data-heavy operations that don't touch game state:
//assembling chunk data from multiple compressed parts, decompressing them, and filling the voxel map.
//Also handles heightmap chunks the same way.
//Main thread (ProcessPacket) handles everything that mutates visible game state:
//player stats, block type definitions, chat messages, disconnects, sound, entities,
//seasons, inventory — essentially every game event the server can send.
//The split exists because chunk decompression is expensive and safe to do off the main thread,
//while UI/game-state mutations must happen on the main thread

using MessagePipe;
using System.Buffers;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace MeinKraft.Mods;

public class ModNetworkProcess : ModBase
{
    private readonly IGameWindowService _platform;
    private readonly IVoxelMap voxelMap;
    private readonly IBlockRegistry blockTypeRegistry;
    private readonly IGameLogger _gameLogger;
    private readonly ITerrainChunkTesselator _terrainChunkTesselator;
    private readonly ILightManager _lightManager;
    private readonly IPublisher<SetupProgressEventArgs> _publisher;

    private readonly int[] _receivedchunk;
    private readonly byte[] _decompressedchunk;

    // Was static — safe today because ProcessPacket runs on the main thread,
    // but fragile if a second instance ever ran concurrently. Instance field
    // makes the threading contract explicit.
    private readonly string[] _textureIdScratch = new string[7];

    private byte[] _currentChunk;
    private int _currentChunkCount;

    public ModNetworkProcess(IGameWindowService gamePlatform, IVoxelMap voxelMap, IBlockRegistry blockTypeRegistry,
        IGameLogger gameLogger, ITerrainChunkTesselator terrainChunkTesselator, ILightManager lightManager,
        IGame game, IPublisher<SetupProgressEventArgs> publisher) : base(game)
    {
        _platform = gamePlatform;
        this.voxelMap = voxelMap;
        this.blockTypeRegistry = blockTypeRegistry;
        _gameLogger = gameLogger;
        _terrainChunkTesselator = terrainChunkTesselator;
        _lightManager = lightManager;
        _publisher = publisher;
        _currentChunk = new byte[1024 * 64];
        _currentChunkCount = 0;
        _receivedchunk = new int[32 * 32 * 32];
        _decompressedchunk = new byte[32 * 32 * 32 * 2];
    }

    public override void OnFrame(float dt)
        => NetworkProcess();

    public void NetworkProcess()
    {
        Game.CurrentTimeMilliseconds = _platform.TimeMillisecondsFromStart;

        NetIncomingMessage msg;
        while (Game.InvalidVersionPacketIdentification == null
            && (msg = Game.NetClient.ReadMessage()) != null)
        {
            // Connect/disconnect lifecycle messages carry no payload — skip them.
            if (msg.Type != NetworkMessageType.Data || msg.Payload.Length == 0)
            {
                continue;
            }

            int payloadLength = msg.Payload.Length;
            byte[] rentedPayload = ArrayPool<byte>.Shared.Rent(payloadLength);
            try
            {
                msg.Payload.CopyTo(rentedPayload);
                TryReadPacket(rentedPayload, payloadLength);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rentedPayload);
            }
        }
    }

    private int _setBlockCount;
    private int _blockTypeCount;

    public void TryReadPacket(byte[] data, int dataLength)
    {
        Packet_Server packet = MemoryPackSerializer.Deserialize<Packet_Server>(
            data.AsSpan(0, dataLength));

        LogPacket(packet.Id);
        ProcessInBackground(packet);
        ProcessPacket(packet);
        Game.LastReceivedMilliseconds = Game.CurrentTimeMilliseconds;
    }

    private void LogPacket(Packet_ServerIdEnum id)
    {
        if (id == Packet_ServerIdEnum.SetBlock)
        {
            FlushBatchLog(ref _blockTypeCount, Packet_ServerIdEnum.BlockType);
            _setBlockCount++;
            return;
        }

        if (id == Packet_ServerIdEnum.BlockType)
        {
            FlushBatchLog(ref _setBlockCount, Packet_ServerIdEnum.SetBlock);
            _blockTypeCount++;
            return;
        }

        FlushSetBlockLog();
        FlushBlockTypeLog();
        _gameLogger.Client.Debug("Received Packet Type: {PacketId}", id);
    }

    private void FlushSetBlockLog() => FlushBatchLog(ref _setBlockCount, Packet_ServerIdEnum.SetBlock);
    private void FlushBlockTypeLog() => FlushBatchLog(ref _blockTypeCount, Packet_ServerIdEnum.BlockType);

    private void FlushBatchLog(ref int count, Packet_ServerIdEnum id)
    {
        if (count == 0)
        {
            return;
        }

        _gameLogger.Client.Debug("Received Packet Type: {PacketId} [{Count} times]", id, count);
        count = 0;
    }

    private void ProcessInBackground(Packet_Server packet)
    {
        switch (packet.Id)
        {
            case Packet_ServerIdEnum.ChunkPart:
                {
                    byte[] arr = packet.ChunkPart.CompressedChunkPart;
                    if (_currentChunkCount + arr.Length > _currentChunk.Length)
                    {
                        _currentChunkCount = 0;
                        break;
                    }

                    Buffer.BlockCopy(arr, 0, _currentChunk, _currentChunkCount, arr.Length);
                    _currentChunkCount += arr.Length;
                    break;
                }

            case Packet_ServerIdEnum.Chunk_:
                {
                    Packet_ServerChunk p = packet.Chunk_;

                    int compressedLength = _currentChunkCount;
                    _currentChunkCount = 0;

                    if (compressedLength != 0)
                    {
                        GzipDecompress(_currentChunk, compressedLength, _decompressedchunk);

                        int i = 0;
                        for (int zz = 0; zz < p.SizeZ; zz++)
                        {
                            for (int yy = 0; yy < p.SizeY; yy++)
                            {
                                for (int xx = 0; xx < p.SizeX; xx++)
                                {
                                    int block = (_decompressedchunk[i + 1] << 8) + _decompressedchunk[i];
                                    if (block < GameConstants.MAX_BLOCKTYPES)
                                    {
                                        _receivedchunk[VectorIndexUtil.Index3d(xx, yy, zz, p.SizeX, p.SizeY)] = block;
                                    }

                                    i += 2;
                                }
                            }
                        }
                    }
                    else
                    {
                        Array.Clear(_receivedchunk, 0, p.SizeX * p.SizeY * p.SizeZ);
                    }

                    voxelMap.SetMapPortion(p.X, p.Y, p.Z, _receivedchunk, p.SizeX, p.SizeY, p.SizeZ);
                    Game.ReceivedMapLength += compressedLength;
                    break;
                }

            case Packet_ServerIdEnum.HeightmapChunk:
                {
                    Packet_ServerHeightmapChunk p = packet.HeightmapChunk;
                    GzipDecompress(
                        p.CompressedHeightmap, p.CompressedHeightmap.Length, _decompressedchunk);
                    ReadOnlySpan<ushort> heights = MemoryMarshal.Cast<byte, ushort>(
                        _decompressedchunk.AsSpan(0, p.SizeX * p.SizeY * 2));
                    for (int xx = 0; xx < p.SizeX; xx++)
                    {
                        for (int yy = 0; yy < p.SizeY; yy++)
                        {
                            voxelMap.Heightmap.SetBlock(
                                p.X + xx, p.Y + yy,
                                heights[VectorIndexUtil.Index2d(xx, yy, p.SizeX)]);
                        }
                    }

                    break;
                }
        }
    }

    // a private Handle*() method to make this switch a clean dispatch table
    // and enable per-handler unit testing. Deferred to keep this diff focused.
    private void ProcessPacket(Packet_Server packet)
    {
        Game.PacketHandlers[(int)packet.Id]?.Handle(packet);
        switch (packet.Id)
        {
            case Packet_ServerIdEnum.ServerIdentification:
                {
                    ProcessIdentificationPacket(packet);
                    break;
                }

            case Packet_ServerIdEnum.FillAreaLimit:
                Game.FillAreaLimit = Math.Min(packet.FillAreaLimit.Limit, 100000);
                break;

            case Packet_ServerIdEnum.Freemove:
                Game.AllowFreeMove = packet.Freemove.IsEnabled != 0;
                if (!Game.AllowFreeMove)
                {
                    Game.Controls.FreeMove = false;
                    Game.Controls.NoClip = false;
                    Game.MoveSpeed = Game.Basemovespeed;
                    Game.AddChatLine(Game.Language.MoveNormal());
                }

                break;

            case Packet_ServerIdEnum.FiniteInventory:
                if (packet.Inventory.Inventory != null)
                {
                    Game.UseInventory(packet.Inventory.Inventory);
                }

                break;

            case Packet_ServerIdEnum.FillArea:
                {
                    int startx = Math.Min(packet.FillArea.X1, packet.FillArea.X2);
                    int endx = Math.Max(packet.FillArea.X1, packet.FillArea.X2);
                    int starty = Math.Min(packet.FillArea.Y1, packet.FillArea.Y2);
                    int endy = Math.Max(packet.FillArea.Y1, packet.FillArea.Y2);
                    int startz = Math.Min(packet.FillArea.Z1, packet.FillArea.Z2);
                    int endz = Math.Max(packet.FillArea.Z1, packet.FillArea.Z2);
                    int blockCount = packet.FillArea.BlockCount;

                    for (int x = startx; x <= endx && blockCount > 0; x++)
                    {
                        for (int y = starty; y <= endy && blockCount > 0; y++)
                        {
                            for (int z = startz; z <= endz && blockCount > 0; z++)
                            {
                                Game.PlaceBlockAndRedraw(x, y, z, packet.FillArea.BlockType);
                                blockCount--;
                            }
                        }
                    }

                    break;
                }

            case Packet_ServerIdEnum.PlayerStats:
                Game.PlayerStats = packet.PlayerStats;
                break;

            case Packet_ServerIdEnum.Message:
                Game.AddChatLine(packet.Message.Message);
                _gameLogger.Client.Debug(packet.Message.Message);
                break;

            case Packet_ServerIdEnum.LevelInitialize:
                _publisher.Publish(new SetupProgressEventArgs { Title = "Level Initialized" });
                _gameLogger.Client.Debug("[GAME] Initialized map loading");
                Game.ReceivedMapLength = 0;
                Game.InvokeMapLoadingProgress(0, 0, Game.Language.Connecting());
                break;

            case Packet_ServerIdEnum.LevelDataChunk:
                Game.InvokeMapLoadingProgress(
                    packet.LevelDataChunk.PercentComplete,
                    Game.ReceivedMapLength,
                    packet.LevelDataChunk.Status);
                break;

            case Packet_ServerIdEnum.BlockType:
                blockTypeRegistry.NewBlockTypes[packet.BlockType.Id] = packet.BlockType.Blocktype;
                break;

            case Packet_ServerIdEnum.Translation:
                Game.Language.Override(
                    packet.Translation.Lang,
                    packet.Translation.Id,
                    packet.Translation.Translation);
                break;

            case Packet_ServerIdEnum.SunLevels:
                _lightManager.NightLevels = packet.SunLevels.Sunlevels;
                break;

            case Packet_ServerIdEnum.LightLevels:
                for (int i = 0; i < packet.LightLevels.Lightlevels.Length; i++)
                {
                    _lightManager.LightLevels[i] = EncodingHelper.DecodeFixedPoint(packet.LightLevels.Lightlevels[i]);
                }

                break;

            case Packet_ServerIdEnum.LevelFinalize:
                _gameLogger.Client.Debug("[GAME] Finished map loading");
                break;

            case Packet_ServerIdEnum.Season:
                {
                    ProcessSeasonPacket(packet);

                    break;
                }

            case Packet_ServerIdEnum.Ping:
                Game.SendPingReply();
                Game.ServerInfo.ServerPing.Send(_platform.TimeMillisecondsFromStart);
                break;

            case Packet_ServerIdEnum.PlayerPing:
                Game.ServerInfo.ServerPing.Receive(_platform.TimeMillisecondsFromStart);
                break;

            case Packet_ServerIdEnum.SetBlock:
                Game.PlaceBlockAndRedraw(
                    packet.SetBlock.X, packet.SetBlock.Y, packet.SetBlock.Z,
                    packet.SetBlock.BlockType);
                break;

            case Packet_ServerIdEnum.PlayerSpawnPosition:
                {
                    int x = packet.PlayerSpawnPosition.X;
                    int y = packet.PlayerSpawnPosition.Y;
                    int z = packet.PlayerSpawnPosition.Z;
                    Game.PlayerPositionSpawnX = x;
                    Game.PlayerPositionSpawnY = z;
                    Game.PlayerPositionSpawnZ = y;
                    Game.AddChatLine(string.Format(
                        Game.Language.SpawnPositionSetTo(), $"{x},{y},{z}"));
                    break;
                }

            case Packet_ServerIdEnum.DisconnectPlayer:
                _gameLogger.Client.Debug($"[GAME] Disconnected by the server ({packet.DisconnectPlayer.DisconnectReason})");
                if (_platform.IsMousePointerLocked())
                {
                    _platform.ExitMousePointerLock();
                }

                _publisher.Publish(new SetupProgressEventArgs { Title = "Disconnected by server, trying to reconnect...", Progress = -1 });
                Game.ExitToMainMenu();
                break;

            case Packet_ServerIdEnum.BlobInitialize:
                Game.BlobDownload = new MemoryStream();
                Game.BlobDownloadName = packet.BlobInitialize.Name;
                Game.BlobDownloadMd5 = packet.BlobInitialize.Md5;
                break;

            case Packet_ServerIdEnum.BlobPart:
                {
                    int length = packet.BlobPart.Data.Length;
                    Game.BlobDownload.Write(packet.BlobPart.Data, 0, length);
                    Game.ReceivedMapLength += length;
                    break;
                }

            case Packet_ServerIdEnum.BlobFinalize:
                {
                    byte[] downloaded = Game.BlobDownload.ToArray();
                    if (Game.BlobDownloadName != null)
                    {
                        Game.SetFile(Game.BlobDownloadName, Game.BlobDownloadMd5,
                            downloaded, (int)Game.BlobDownload.Length);
                    }

                    Game.BlobDownload = null;
                    break;
                }

            case Packet_ServerIdEnum.Sound:
                Game.PlayAudio(
                    packet.Sound.Name,
                    packet.Sound.X, packet.Sound.Y, packet.Sound.Z);
                break;

            case Packet_ServerIdEnum.RemoveMonsters:
                for (int i = GameConstants.entityMonsterIdStart;
                     i < GameConstants.entityMonsterIdStart + GameConstants.entityMonsterIdCount; i++)
                {
                    Game.Entities[i] = null;
                }

                break;

            case Packet_ServerIdEnum.Follow:
                Game.Follow = packet.Follow.Client;
                if (packet.Follow.Tpp != 0)
                {
                    Game.SetCamera(CameraType.Overhead);
                    Game.LocalOrientationX = MathF.PI;
                    Game.GuiStateBackToGame();
                }
                else
                {
                    Game.SetCamera(CameraType.Fpp);
                }

                break;

            case Packet_ServerIdEnum.Bullet:
                Game.EntityAddLocal(Entity.CreateBullet(
                    EncodingHelper.DecodeFixedPoint(packet.Bullet.FromXFloat),
                    EncodingHelper.DecodeFixedPoint(packet.Bullet.FromYFloat),
                    EncodingHelper.DecodeFixedPoint(packet.Bullet.FromZFloat),
                    EncodingHelper.DecodeFixedPoint(packet.Bullet.ToXFloat),
                    EncodingHelper.DecodeFixedPoint(packet.Bullet.ToYFloat),
                    EncodingHelper.DecodeFixedPoint(packet.Bullet.ToZFloat),
                    EncodingHelper.DecodeFixedPoint(packet.Bullet.SpeedFloat)));
                break;

            case Packet_ServerIdEnum.Ammo:
                {
                    if (!Game.AmmoStarted)
                    {
                        Game.AmmoStarted = true;
                        for (int i = 0; i < packet.Ammo.TotalAmmo.Length; i++)
                        {
                            Packet_IntInt k = packet.Ammo.TotalAmmo[i];
                            Game.LoadedAmmo[k.Key_] = Math.Min(
                                k.Value_, blockTypeRegistry.BlockTypes[k.Key_].AmmoMagazine);
                        }
                    }

                    if (Game.TotalAmmo == null)
                    {
                        Game.TotalAmmo = new int[GameConstants.MAX_BLOCKTYPES];
                    }
                    else
                    {
                        Array.Clear(Game.TotalAmmo, 0, GameConstants.MAX_BLOCKTYPES);
                    }

                    for (int i = 0; i < packet.Ammo.TotalAmmo.Length; i++)
                    {
                        Game.TotalAmmo[packet.Ammo.TotalAmmo[i].Key_] = packet.Ammo.TotalAmmo[i].Value_;
                    }

                    break;
                }

            case Packet_ServerIdEnum.Explosion:
                Game.EntityAddLocal(new Entity
                {
                    Expires = new Expiry
                    {
                        TimeLeft = EncodingHelper.DecodeFixedPoint(packet.Explosion.TimeFloat)
                    },
                    Push = packet.Explosion,
                });
                break;

            case Packet_ServerIdEnum.Projectile:
                Game.EntityAddLocal(new Entity
                {
                    Sprite = new Sprite
                    {
                        Image = "ChemicalGreen.png",
                        Size = 14,
                        AnimationCount = 0,
                        PositionX = EncodingHelper.DecodeFixedPoint(packet.Projectile.FromXFloat),
                        PositionY = EncodingHelper.DecodeFixedPoint(packet.Projectile.FromYFloat),
                        PositionZ = EncodingHelper.DecodeFixedPoint(packet.Projectile.FromZFloat),
                    },
                    Grenade = new Grenade
                    {
                        VelocityX = EncodingHelper.DecodeFixedPoint(packet.Projectile.VelocityXFloat),
                        VelocityY = EncodingHelper.DecodeFixedPoint(packet.Projectile.VelocityYFloat),
                        VelocityZ = EncodingHelper.DecodeFixedPoint(packet.Projectile.VelocityZFloat),
                        Block = packet.Projectile.BlockId,
                        SourcePlayer = packet.Projectile.SourcePlayerID,
                    },
                    Expires = Expiry.Create(EncodingHelper.DecodeFixedPoint(packet.Projectile.ExplodesAfterFloat)),
                });
                break;

            case Packet_ServerIdEnum.BlockTypes:
                {
                    blockTypeRegistry.BlockTypes = blockTypeRegistry.NewBlockTypes;
                    blockTypeRegistry.NewBlockTypes = [];

                    // Old code: Contains() + IndexOf() scanned a 1024-entry array
                    // for every texture of every block type (up to 7168 scans).
                    // Old code also passed textureInAtlasIdsCount=1024 even though
                    // only lastTextureId entries were populated — scanning nulls.
                    // HashSet gives O(1) membership checks; a parallel List
                    // preserves insertion order for IndexOf lookups.
                    HashSet<string> textureSet = new(StringComparer.Ordinal);
                    List<string> textureList = new();

                    string[] scratch = _textureIdScratch;

                    foreach ((int _, BlockType? blockType) in blockTypeRegistry.BlockTypes)
                    {
                        scratch[0] = blockType.TextureIdLeft;
                        scratch[1] = blockType.TextureIdRight;
                        scratch[2] = blockType.TextureIdFront;
                        scratch[3] = blockType.TextureIdBack;
                        scratch[4] = blockType.TextureIdTop;
                        scratch[5] = blockType.TextureIdBottom;
                        scratch[6] = blockType.TextureIdForInventory;

                        for (int k = 0; k < 7; k++)
                        {
                            if (textureSet.Add(scratch[k]))
                            {
                                textureList.Add(scratch[k]);
                            }
                        }
                    }

                    // Convert to array for downstream APIs that expect string[].
                    string[] textureInAtlasIds = [.. textureList];
                    int textureInAtlasIdsCount = textureInAtlasIds.Length;

                    blockTypeRegistry.UseBlockTypes(blockTypeRegistry.BlockTypes);

                    foreach ((int id, BlockType? b) in blockTypeRegistry.BlockTypes)
                    {
                        _terrainChunkTesselator.TextureId[id] = [
                            textureList.IndexOf(b.TextureIdTop),
                            textureList.IndexOf(b.TextureIdBottom),
                            textureList.IndexOf(b.TextureIdFront),
                            textureList.IndexOf(b.TextureIdBack),
                            textureList.IndexOf(b.TextureIdLeft),
                            textureList.IndexOf(b.TextureIdRight),
                        ];
                        Game.TextureIdForInventory[id] = textureList.IndexOf(b.TextureIdForInventory);
                    }

                    Game.UseTerrainTextures(textureInAtlasIds, textureInAtlasIdsCount);
                    Game.HandRedraw = true;
                    Game.RedrawAllBlocks();
                    break;
                }

            case Packet_ServerIdEnum.ServerRedirect:
                _gameLogger.Client.Debug("[GAME] Received server redirect");
                Game.SendLeave(PacketLeaveReason.Leave);
                Game.ExitAndSwitchServer(packet.Redirect);
                break;
        }
    }

    private void ProcessIdentificationPacket(Packet_Server packet)
    {
        string invalidversionstr = Game.Language.InvalidVersionConnectAnyway();
        Game.ServerGameVersion = packet.Identification.MdProtocolVersion;
        if (Game.ServerGameVersion != _platform.GetGameVersion())
        {
            _gameLogger.Client.Debug("[GAME] Different game versions");
            string q = string.Format(invalidversionstr,
                _platform.GetGameVersion(), Game.ServerGameVersion);
            Game.InvalidVersionDrawMessage = q;
            Game.InvalidVersionPacketIdentification = packet;
        }
        else
        {
            Game.ProcessServerIdentification(packet);
        }

        Game.ReceivedMapLength = 0;
    }

    private void ProcessSeasonPacket(Packet_Server packet)
    {
        packet.Season.Hour -= 1;
        if (packet.Season.Hour < 0)
        {
            packet.Season.Hour = 12 * GameConstants.HourDetail;
        }

        if (_lightManager.NightLevels == null)
        {
            return;
        }

        if (packet.Season.Hour >= _lightManager.NightLevels.Length)
        {
            return;
        }

        int sunlight = _lightManager.NightLevels[packet.Season.Hour];
        _lightManager.SkySphereNight = sunlight < 8;
        //Game.SunMoonRenderer.day_length_in_seconds =
        //    60 * 60 * 24 / packet.Season.DayNightCycleSpeedup;
        //int hour = packet.Season.Hour / Game.HourDetail;
        //if (Game.SunMoonRenderer.GetHour() != hour)
        //    Game.SunMoonRenderer.SetHour(hour);
        if (_lightManager.Sunlight != sunlight)
        {
            _lightManager.Sunlight = sunlight;
            Game.RedrawAllBlocks();
        }
    }

    private static void GzipDecompress(byte[] compressed, int compressedLength, byte[] ret)
    {
        // MemoryStream(byte[], int, int) wraps the existing array without copying.
        // GZipStream reads from it and writes the decompressed bytes directly into
        // ret via the Read loop — no intermediate byte[] allocation at any point.
        using MemoryStream source = new(compressed, 0, compressedLength, writable: false);
        using GZipStream gz = new(source, CompressionMode.Decompress);

        int totalRead = 0;
        int bytesRead;
        while ((bytesRead = gz.Read(ret, totalRead, ret.Length - totalRead)) > 0)
        {
            totalRead += bytesRead;
        }
    }
}