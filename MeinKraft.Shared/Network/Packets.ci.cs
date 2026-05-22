using MeinKraft;

public class ClientPackets
{
    private static readonly Packet_ClientPingReply s_pingReplyInner = new();
    private static readonly Packet_Client s_pingReplyPacket = new()
    {
        Id = PacketType.PingReply,
        PingReply = s_pingReplyInner
    };

    private static readonly Packet_ClientServerQuery s_serverQueryInner = new();
    private static readonly Packet_Client s_serverQueryPacket = new()
    {
        Id = PacketType.ServerQuery,
        Query = s_serverQueryInner
    };

    private static readonly Packet_ClientPositionAndOrientation s_positionPayload = new();
    private static readonly Packet_Client s_positionPacket = new()
    {
        Id = PacketType.PositionAndOrientation,
        PositionAndOrientation = s_positionPayload
    };

    public static Packet_Client CreateLoginPacket(string username, string verificationKey)
    {
        Packet_ClientIdentification p = new();
        {
            p.Username = username;
            p.MdProtocolVersion = GameVersion.Version;
            p.VerificationKey = verificationKey;
        }

        Packet_Client pp = new()
        {
            Id = PacketType.PlayerIdentification,
            Identification = p
        };
        return pp;
    }

    public static Packet_Client CreateLoginPacket_(string username, string verificationKey, string serverPassword)
    {
        Packet_ClientIdentification p = new();
        {
            p.Username = username;
            p.MdProtocolVersion = GameVersion.Version;
            p.VerificationKey = verificationKey;
            p.ServerPassword = serverPassword;
        }

        Packet_Client pp = new()
        {
            Id = PacketType.PlayerIdentification,
            Identification = p
        };
        return pp;
    }

    public static Packet_Client Oxygen(int currentOxygen)
    {
        Packet_Client packet = new()
        {
            Id = PacketType.Oxygen,
            Oxygen = new Packet_ClientOxygen
            {
                CurrentOxygen = currentOxygen
            }
        };
        return packet;
    }

    public static Packet_Client Reload()
    {
        Packet_Client p = new()
        {
            Id = PacketType.Reload,
            Reload = new Packet_ClientReload()
        };
        return p;
    }

    public static Packet_Client Chat(string s, int isTeamchat)
    {
        Packet_ClientMessage p = new()
        {
            Message = s,
            IsTeamchat = isTeamchat
        };
        Packet_Client pp = new()
        {
            Id = PacketType.Message,
            Message = p
        };
        return pp;
    }

    public static Packet_Client PingReply() => s_pingReplyPacket;

    public static Packet_Client SetBlock(int x, int y, int z, PacketBlockSetMode mode, int type, int materialslot)
    {
        Packet_ClientSetBlock p = new();
        {
            p.X = x;
            p.Y = y;
            p.Z = z;
            p.Mode = mode;
            p.BlockType = type;
            p.MaterialSlot = materialslot;
        }

        Packet_Client pp = new()
        {
            Id = PacketType.SetBlock,
            SetBlock = p
        };
        return pp;
    }

    public static Packet_Client SpecialKeyRespawn()
    {
        Packet_Client p = new();
        {
            p.Id = PacketType.SpecialKey;
            p.SpecialKey_ = new Packet_ClientSpecialKey
            {
                Key_ = SpecialKey.Respawn
            };
        }

        return p;
    }

    public static Packet_Client FillArea(int startx, int starty, int startz, int endx, int endy, int endz, int blockType, int ActiveMaterial)
    {
        Packet_ClientFillArea p = new();
        {
            p.X1 = startx;
            p.Y1 = starty;
            p.Z1 = startz;
            p.X2 = endx;
            p.Y2 = endy;
            p.Z2 = endz;
            p.BlockType = blockType;
            p.MaterialSlot = ActiveMaterial;
        }

        Packet_Client pp = new()
        {
            Id = PacketType.FillArea,
            FillArea = p
        };
        return pp;
    }

    public static Packet_Client InventoryClick(Packet_InventoryPosition pos)
    {
        Packet_ClientInventoryAction p = new()
        {
            A = pos,
            Action = PacketInventoryActionType.Click
        };
        Packet_Client pp = new()
        {
            Id = PacketType.InventoryAction,
            InventoryAction = p
        };
        return pp;
    }

    public static Packet_Client WearItem(Packet_InventoryPosition from, Packet_InventoryPosition to)
    {
        Packet_ClientInventoryAction p = new()
        {
            A = from,
            B = to,
            Action = PacketInventoryActionType.WearItem
        };
        Packet_Client pp = new()
        {
            Id = PacketType.InventoryAction,
            InventoryAction = p
        };
        return pp;
    }

    public static Packet_Client MoveToInventory(Packet_InventoryPosition from)
    {
        Packet_ClientInventoryAction p = new()
        {
            A = from,
            Action = PacketInventoryActionType.MoveToInventory
        };
        Packet_Client pp = new()
        {
            Id = PacketType.InventoryAction,
            InventoryAction = p
        };
        return pp;
    }

    public static Packet_Client Death(DeathReason reason, int sourcePlayer)
    {
        Packet_Client p = new()
        {
            Id = PacketType.Death,
            Death = new Packet_ClientDeath()
        };
        {
            p.Death.Reason = reason;
            p.Death.SourcePlayer = sourcePlayer;
        }

        return p;
    }

    public static Packet_Client Health(int currentHealth)
    {
        Packet_Client p = new();
        {
            p.Id = PacketType.Health;
            p.Health = new Packet_ClientHealth
            {
                CurrentHealth = currentHealth
            };
        }

        return p;
    }

    public static Packet_Client RequestBlob(string[] required)
    {
        Packet_ClientRequestBlob p = new(); //{ RequestBlobMd5 = needed };
        if (GameVersion.ServerVersionAtLeast(GameVersion.Version, 2014, 4, 13))
        {
            p.RequestedMd5 = required;
        }

        Packet_Client pp = new()
        {
            Id = PacketType.RequestBlob,
            RequestBlob = p
        };
        return pp;
    }

    public static Packet_Client Leave(PacketLeaveReason reason)
    {
        Packet_Client p = new()
        {
            Id = PacketType.Leave,
            Leave = new Packet_ClientLeave
            {
                Reason = reason
            }
        };
        return p;
    }

    public static Packet_Client Craft(int x, int y, int z, int recipeId)
    {
        Packet_ClientCraft cmd = new()
        {
            X = x,
            Y = y,
            Z = z,
            RecipeId = recipeId
        };
        Packet_Client p = new()
        {
            Id = PacketType.Craft,
            Craft = cmd
        };
        return p;
    }

    public static Packet_Client DialogClick(string widgetId, string[] textValues, int textValuesCount)
    {
        Packet_Client p = new()
        {
            Id = PacketType.DialogClick,
            DialogClick_ = new Packet_ClientDialogClick
            {
                WidgetId = widgetId
            }
        };
        p.DialogClick_.TextBoxValue = textValues;
        return p;
    }

    public static Packet_Client GameResolution(int width, int height)
    {
        Packet_ClientGameResolution p = new()
        {
            Width = width,
            Height = height
        };
        Packet_Client pp = new()
        {
            Id = PacketType.GameResolution,
            GameResolution = p
        };
        return pp;
    }

    public static Packet_Client SpecialKeyTabPlayerList()
    {
        Packet_Client p = new()
        {
            Id = PacketType.SpecialKey,
            SpecialKey_ = new Packet_ClientSpecialKey
            {
                Key_ = SpecialKey.TabPlayerList
            }
        };
        return p;
    }

    public static Packet_Client SpecialKeySelectTeam()
    {
        Packet_Client p = new();
        {
            p.Id = PacketType.SpecialKey;
            p.SpecialKey_ = new Packet_ClientSpecialKey
            {
                Key_ = SpecialKey.SelectTeam
            };
        }

        return p;
    }

    public static Packet_Client SpecialKeySetSpawn()
    {
        Packet_Client p = new();
        {
            p.Id = PacketType.SpecialKey;
            p.SpecialKey_ = new Packet_ClientSpecialKey
            {
                Key_ = SpecialKey.SetSpawn
            };
        }

        return p;
    }

    public static Packet_Client ActiveMaterialSlot(int ActiveMaterial)
    {
        Packet_Client p = new();
        {
            p.Id = PacketType.ActiveMaterialSlot;
            p.ActiveMaterialSlot = new Packet_ClientActiveMaterialSlot
            {
                ActiveMaterialSlot = ActiveMaterial
            };
        }

        return p;
    }

    public static Packet_Client MonsterHit(int damage)
    {
        Packet_ClientHealth p = new()
        {
            CurrentHealth = damage
        };
        Packet_Client packet = new()
        {
            Id = PacketType.MonsterHit,
            Health = p
        };
        return packet;
    }

    public static Packet_Client PositionAndOrientation(int playerId,
     float positionX, float positionY, float positionZ,
     float orientationX, float orientationY, float orientationZ, byte stance)
    {
        // Overwrite the cached payload in-place — zero allocations.
        // Safe because the caller serialises this packet before the next tick.
        s_positionPayload.PlayerId = playerId;
        s_positionPayload.X = (int)(positionX * 32);
        s_positionPayload.Y = (int)(positionY * 32);
        s_positionPayload.Z = (int)(positionZ * 32);
        s_positionPayload.Heading = (int)RadToAngle256(orientationY);
        s_positionPayload.Pitch = (int)RadToAngle256(orientationX);
        s_positionPayload.Stance = stance;
        return s_positionPacket;
    }

    public static Packet_Client ServerQuery() => s_serverQueryPacket;

    public static float RadToAngle256(float value) => value / (2 * MathF.PI) * 255;

    public static Packet_Client UseEntity(int entityId)
    {
        Packet_Client p = new()
        {
            Id = PacketType.EntityInteraction,
            EntityInteraction = new Packet_ClientEntityInteraction
            {
                EntityId = entityId,
                InteractionType = PacketEntityInteractionType.Use
            }
        };
        return p;
    }

    public static Packet_Client HitEntity(int entityId)
    {
        Packet_Client p = new()
        {
            Id = PacketType.EntityInteraction,
            EntityInteraction = new Packet_ClientEntityInteraction
            {
                EntityId = entityId,
                InteractionType = PacketEntityInteractionType.Hit
            }
        };
        return p;
    }
}

public class ServerPackets
{
    public static Packet_Server Message(string text)
    {
        Packet_Server p = new()
        {
            Id = Packet_ServerIdEnum.Message,
            Message = new Packet_ServerMessage
            {
                Message = text
            }
        };
        return p;
    }

    public static Packet_Server LevelInitialize()
    {
        Packet_Server p = new()
        {
            Id = Packet_ServerIdEnum.LevelInitialize,
            LevelInitialize = new Packet_ServerLevelInitialize()
        };
        return p;
    }

    public static Packet_Server LevelFinalize()
    {
        Packet_Server p = new()
        {
            Id = Packet_ServerIdEnum.LevelFinalize,
            LevelFinalize = new Packet_ServerLevelFinalize()
        };
        return p;
    }

    public static Packet_Server PlayerStats(int health, int maxHealth, int oxygen, int maxOxygen)
    {
        Packet_Server p = new()
        {
            Id = Packet_ServerIdEnum.PlayerStats,
            PlayerStats = new Packet_ServerPlayerStats
            {
                CurrentHealth = health,
                MaxHealth = maxHealth,
                CurrentOxygen = oxygen,
                MaxOxygen = maxOxygen
            }
        };
        return p;
    }

    public static Packet_Server Ping()
    {
        Packet_Server p = new()
        {
            Id = Packet_ServerIdEnum.Ping,
            Ping = new Packet_ServerPing()
        };
        return p;
    }

    public static Packet_Server DisconnectPlayer(string disconnectReason)
    {
        Packet_Server p = new()
        {
            Id = Packet_ServerIdEnum.DisconnectPlayer,
            DisconnectPlayer = new Packet_ServerDisconnectPlayer
            {
                DisconnectReason = disconnectReason
            }
        };
        return p;
    }

    public static Packet_Server AnswerQuery(Packet_ServerQueryAnswer answer)
    {
        Packet_Server p = new()
        {
            Id = Packet_ServerIdEnum.QueryAnswer,
            QueryAnswer = answer
        };
        return p;
    }

    public static Packet_Server EntitySpawn(int id, Packet_ServerEntity entity)
    {
        Packet_Server p = new()
        {
            Id = Packet_ServerIdEnum.EntitySpawn,
            EntitySpawn = new Packet_ServerEntitySpawn
            {
                Id = id,
                Entity_ = entity
            }
        };
        return p;
    }

    public static Packet_Server EntityPositionAndOrientation(int id, Packet_PositionAndOrientation positionAndOrientation)
    {
        Packet_Server p = new()
        {
            Id = Packet_ServerIdEnum.EntityPosition,
            EntityPosition = new Packet_ServerEntityPositionAndOrientation
            {
                Id = id,
                PositionAndOrientation = positionAndOrientation
            }
        };
        return p;
    }

    public static Packet_Server EntityDespawn(int id)
    {
        Packet_Server p = new()
        {
            Id = Packet_ServerIdEnum.EntityDespawn,
            EntityDespawn = new Packet_ServerEntityDespawn
            {
                Id = id
            }
        };
        return p;
    }
}
