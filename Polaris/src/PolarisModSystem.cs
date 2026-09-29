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

    private ICoreAPI? _api;

    public override void Start(ICoreAPI api)
    {
        _api = api;
        CommandArgumentParsers parsers = api.ChatCommands.Parsers;

        // Commands.
        api.ChatCommands
            .Create("constxp")
            .RequiresPrivilege("ban")
            .WithArgs(parsers.Word("constellation"), parsers.Float("experience"), parsers.OptionalWord("player"))
            .HandleWith(HandleExperience);
    }

    private TextCommandResult HandleExperience(TextCommandCallingArgs args)
    {
        if (args.Parsers[0].GetValue() is not string constellation) return TextCommandResult.Error("Invalid constellation.");
        if (args.Parsers[1].GetValue() is not float exp) return TextCommandResult.Error("Invalid args.");

        IServerPlayer? player = null;
        if (args.Parsers[2].GetValue() is string playerName && _api != null)
        {
            player = _api.World.AllOnlinePlayers
                .OfType<IServerPlayer>()
                .FirstOrDefault(p => p.PlayerName == playerName);
        }

        player ??= args.Caller.Player as IServerPlayer;
        if (player == null) return TextCommandResult.Error("No player specified and no caller player found.");

        SystemPolaris.AddExperience(constellation, player, exp);

        return TextCommandResult.Success($"Added {exp:F1} experience to {constellation}.");
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        api.Input.RegisterHotKey("polarisAchievements", "Achievements", GlKeys.P);
        api.Input.SetHotKeyHandler("polarisAchievements", key =>
        {
            MainAPI.GetClientSystem<SystemPolarisStarScreen>().ToggleStars(true);
            return true;
        });
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
        float strongBack = data.GetAllAllocatedNodes(system)
            .SelectMany(n => n.Stats.OfType<ExtraStatAdditive>())
            .Where(s => s.StatName == "strongBack")
            .Sum(s => s.Amount);
        int newCount = Math.Clamp(StrongBackInventoryPatches.VanillaBagSlots + (int)strongBack,
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
