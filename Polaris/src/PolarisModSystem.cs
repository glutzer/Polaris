global using NutsLib;
global using OpenTK.Mathematics;
global using Vintagestory.API.Common;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.Client;

namespace Polaris;

public class PolarisModSystem : ModSystem
{
    public override void StartServerSide(ICoreServerAPI api)
    {
        api.Event.OnPlayerInteractEntity += Event_OnPlayerInteractEntity;
    }

    private void Event_OnPlayerInteractEntity(Entity entity, IPlayer byPlayer, ItemSlot slot, Vec3d hitPosition, int mode, ref EnumHandling handling)
    {
        SystemPolarisPassiveTree.Instance(byPlayer.Entity.Api).AddExperience("Survival", byPlayer.PlayerUID, Random.Shared.NextSingle() * 10f);
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        ScreenManager.hotkeyManager.RegisterHotKey("starMap", "Star Map", (int)GlKeys.O, triggerOnUpAlso: true);
        MainAPI.Capi.Input.SetHotKeyHandler("starMap", key =>
        {
            if (key.OnKeyUp)
            {

            }
            else
            {
                MainAPI.GetGameSystem<SystemPolarisStarScreen>(api.Side).ToggleStars();
            }

            return true;
        });

        NuttyShaderRegistry.AddShader("polaris:stars", "polaris:stars", "polarisstars");
    }
}