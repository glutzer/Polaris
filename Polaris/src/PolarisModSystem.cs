global using NutsLib;
global using OpenTK.Mathematics;
global using Vintagestory.API.Common;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Server;
using Vintagestory.Client;

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
    }

    public override void Dispose()
    {
        harmony?.UnpatchAll();
        harmony = null;

        CraftingPatches.LastSlotActivator = null;
    }
}