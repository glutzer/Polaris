global using NutsLib;
global using OpenTK.Mathematics;
global using Vintagestory.API.Common;
using HarmonyLib;
using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Server;
using Vintagestory.Client;
using Vintagestory.Client.NoObf;

namespace Polaris;

public class PolarisModSystem : ModSystem
{
    private static Harmony? harmony;

    public override void StartPre(ICoreAPI api)
    {
        if (harmony == null)
        {
            harmony = new Harmony("polaris");
            harmony.PatchAll();
        }
    }

    public override void Start(ICoreAPI api)
    {
        CommandArgumentParsers parsers = api.ChatCommands.Parsers;

        // Commands.
        api.ChatCommands
            .Create("constellationxp")
            .RequiresPrivilege("ban")
            .WithArgs(parsers.Word("constellation"), parsers.Float("experience"), parsers.OnlinePlayer("player"))
            .HandleWith(HandleExperience);
    }

    public static TextCommandResult HandleExperience(TextCommandCallingArgs args)
    {
        if (args.Parsers[0].GetValue() is not string constellation) return TextCommandResult.Error("Invalid constellation.");

        if (args.Parsers[1].GetValue() is not float exp || args.Parsers[2].GetValue() is not IServerPlayer player)
        {
            return TextCommandResult.Error("Invalid args.");
        }

        SystemPolaris.AddExperience(constellation, player, exp);

        return TextCommandResult.Success($"Added {exp:F1} experience to {constellation}.");
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
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

        MainAPI.GetGameSystem<SystemPolaris>(api.Side).OnClientDataUpdated += data => RecomposeHotbarBackpackSlots(api, data);
    }

    private static void RecomposeHotbarBackpackSlots(ICoreClientAPI api, PlayerPolarisData data)
    {
        SystemPolaris system = MainAPI.GetGameSystem<SystemPolaris>(api.Side);
        int strongBack = data.GetAllAllocatedNodes(system).Count(n => n.Code.StartsWith("strongBack"));
        int newCount = Math.Clamp(StrongBackInventoryPatches.VanillaBagSlots + strongBack,
            StrongBackInventoryPatches.VanillaBagSlots,
            StrongBackInventoryPatches.TotalBagSlots);

        if (StrongBackInventoryPatches.CurrentBackpackSlotCount == newCount) return;

        StrongBackInventoryPatches.CurrentBackpackSlotCount = newCount;

        foreach (GuiDialog dialog in api.Gui.LoadedGuis)
        {
            if (dialog is HudHotbar hud)
            {
                hud.ComposeGuis();
                break;
            }
        }
    }

    public override void Dispose()
    {
        harmony?.UnpatchAll();
        harmony = null;

        CraftingPatches.LastSlotActivator = null;
    }
}