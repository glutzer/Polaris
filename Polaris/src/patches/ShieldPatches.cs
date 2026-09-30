using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace Polaris;

[HarmonyPatch(typeof(ModSystemWearableStats), "applyShieldProtection")]
public static class ShieldPatches
{
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo readFloat = AccessTools.Method(typeof(JsonObject), nameof(JsonObject.AsFloat), [typeof(float)]);
        MethodInfo modifyChance = AccessTools.Method(typeof(ShieldPatches), nameof(ModifyPassiveBlockChance));
        MethodInfo modifyAbsorption = AccessTools.Method(typeof(ShieldPatches), nameof(ModifyDamageAbsorption));
        var code = new List<CodeInstruction>(instructions);
        int chances = 0;
        int absorptions = 0;
        for (int i = 0; i < code.Count; i++)
        {
            yield return code[i];
            // Vanilla reads melee and projectile chances with default 0, and absorption with default 2.
            if (!code[i].Calls(readFloat) || i == 0 || code[i - 1].opcode != OpCodes.Ldc_R4) continue;
            float defaultValue = (float)code[i - 1].operand;
            if (defaultValue is not 0f and not 2f) continue;
            yield return new CodeInstruction(OpCodes.Ldarg_1);
            yield return new CodeInstruction(OpCodes.Call, defaultValue == 0f ? modifyChance : modifyAbsorption);
            if (defaultValue == 0f) chances++;
            else absorptions++;
        }
        if (chances != 2 || absorptions != 2)
            throw new InvalidOperationException("Could not identify both melee and projectile shield chance and absorption calculations.");
    }

    public static float ModifyPassiveBlockChance(float chance, IPlayer player)
    {
        // Match vanilla's active/passive distinction, including sneaking while aiming.
        return player.Entity.Controls.Sneak && player.Entity.Attributes.GetInt("aiming") != 1
            ? chance
            : player.Entity.TryGetExtraStat("passiveBlockChance", out float multiplier)
            ? Math.Clamp(chance * multiplier, 0f, 1f) : chance;
    }

    public static float ModifyDamageAbsorption(float absorption, IPlayer player)
    {
        return player.Entity.TryGetExtraStat("shieldDamageAbsorption", out float multiplier)
            ? Math.Max(0f, absorption * multiplier) : absorption;
    }
}
