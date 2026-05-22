namespace MeinKraft.Mods.War;

/// <summary>
/// This class contains all block definitions specific for War Mod (weapons)
/// </summary>
public class WarBlocks : IMod
{
    public void PreStart(IServerModManager m) => m.RequireMod("CoreBlocks");
    public void Start(IServerModManager m, IModEvents modEvents)
    {
        SoundSet solidSounds = new()
        {
            Walk = ["walk1", "walk2", "walk3", "walk4"],
            Break = ["destruct"],
            Build = ["build"],
            Clone = ["clone"],
            Shoot = [],
            ShootEnd = ["M1GarandGun-SoundBible.com-1519788442", "M1GarandGun-SoundBible.com-15197884422"],
            Reload = ["shotgun-reload-old_school-RA_The_Sun_God-580332022"],
        };
        SoundSet grenadesounds = new()
        {
            Shoot = ["grenadestart"],
            ShootEnd = ["grenadethrow"],
            Reload = solidSounds.Reload,
        };

        m.SetBlockType(154, "Pistol", new BlockType()
        {
            AllTextures = "Pistol",
            DrawType = DrawType.Solid,
            WalkableType = WalkableType.Solid,
            Sounds = solidSounds,
            handimage = "pistolhand.png",
            IsPistol = true,
            AimRadius = 15,
            Recoil = 0.04f,
            Delay = 0.5f,
            WalkSpeedWhenUsed = 1f,
            IronSightsEnabled = true,
            IronSightsMoveSpeed = 1f,
            IronSightsImage = "pistolhandsights.png",
            IronSightsAimRadius = 15,
            IronSightsFov = 0.8f,
            AmmoMagazine = 12,
            AmmoTotal = 120,
            ReloadDelay = 2,
            ExplosionRange = 0.2f,
            ExplosionTime = 0.2f,
            DamageBody = 15,
            DamageHead = 50,
        });
        m.SetBlockType(155, "SubmachineGun", new BlockType()
        {
            AllTextures = "SubmachineGun",
            DrawType = DrawType.Solid,
            WalkableType = WalkableType.Solid,
            Sounds = solidSounds,
            handimage = "submachinegunhand.png",
            IsPistol = true,
            AimRadius = 20,
            Recoil = 0.04f,
            Delay = 0.1f,
            WalkSpeedWhenUsed = 1f,
            IronSightsEnabled = true,
            IronSightsMoveSpeed = 1f,
            IronSightsImage = "submachinegunhandsights.png",
            IronSightsAimRadius = 20,
            IronSightsFov = 0.8f,
            AmmoMagazine = 30,
            AmmoTotal = 120,
            ReloadDelay = 2,
            ExplosionRange = 0.2f,
            ExplosionTime = 0.2f,
            DamageBody = 15,
            DamageHead = 40,
        });
        m.SetBlockType(156, "Shotgun", new BlockType()
        {
            AllTextures = "Shotgun",
            DrawType = DrawType.Solid,
            WalkableType = WalkableType.Solid,
            Sounds = solidSounds,
            handimage = "shotgunhand.png",
            IsPistol = true,
            AimRadius = 50,
            Recoil = 0.08f,
            Delay = 1f,
            BulletsPerShot = 6,
            WalkSpeedWhenUsed = 1f,
            IronSightsEnabled = true,
            IronSightsMoveSpeed = 1f,
            IronSightsImage = "shotgunhandsights.png",
            IronSightsAimRadius = 50,
            IronSightsFov = 0.8f,
            AmmoMagazine = 30,
            AmmoTotal = 120,
            ReloadDelay = 2,
            ExplosionRange = 0.2f,
            ExplosionTime = 0.2f,
            DamageBody = 35,
            DamageHead = 60,
        });
        m.SetBlockType(157, "Rifle", new BlockType()
        {
            AllTextures = "Rifle",
            DrawType = DrawType.Solid,
            WalkableType = WalkableType.Solid,
            Sounds = solidSounds,
            handimage = "riflehand.png",
            IsPistol = true,
            AimRadius = 20,
            Recoil = 0.04f,
            Delay = 2f,
            WalkSpeedWhenUsed = 1f,
            IronSightsEnabled = true,
            IronSightsMoveSpeed = 0.4f,
            IronSightsImage = "riflehandsights.png",
            IronSightsAimRadius = 10,
            IronSightsFov = 0.5f,
            AmmoMagazine = 6,
            AmmoTotal = 48,
            ReloadDelay = 2,
            ExplosionRange = 0.2f,
            ExplosionTime = 0.2f,
            DamageBody = 35,
            DamageHead = 100,
        });
        m.SetBlockType(158, "MedicalKit", new BlockType()
        {
            AllTextures = "MedicalKit",
            DrawType = DrawType.Transparent,
            WalkableType = WalkableType.Empty,
            Sounds = solidSounds,
            handimage = null,
            IsPistol = false,
            WalkSpeedWhenUsed = 1f,
        });
        m.SetBlockType(159, "AmmoPack", new BlockType()
        {
            TextureIdTop = "AmmoTop",
            TextureIdBack = "AmmoPack",
            TextureIdFront = "AmmoPack",
            TextureIdLeft = "AmmoPack",
            TextureIdRight = "AmmoPack",
            TextureIdForInventory = "AmmoPack",
            TextureIdBottom = "AmmoTop",
            DrawType = DrawType.Transparent,
            WalkableType = WalkableType.Empty,
            Sounds = solidSounds,
            handimage = null,
            IsPistol = false,
            WalkSpeedWhenUsed = 1f,
        });

        m.SetBlockType(160, "Grenade", new BlockType()
        {
            AllTextures = "Grenade",
            DrawType = DrawType.Solid,
            WalkableType = WalkableType.Solid,
            Sounds = grenadesounds,
            handimage = "grenadehand.png",
            IsPistol = true,
            AimRadius = 20,
            Recoil = 0.04f,
            Delay = 0.5f,
            WalkSpeedWhenUsed = 1f,
            IronSightsEnabled = false,
            IronSightsMoveSpeed = 0.4f,
            IronSightsImage = "grenadehand.png",
            IronSightsAimRadius = 10,
            IronSightsFov = 0.5f,
            AmmoMagazine = 6,
            AmmoTotal = 6,
            ReloadDelay = 2,
            ExplosionRange = 10f,
            ExplosionTime = 1f,
            ProjectileSpeed = 25f,
            ProjectileBounce = true,
            DamageBody = 200,
            PistolType = PistolType.Grenade,
        });
    }
}
