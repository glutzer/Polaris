using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Vintagestory.GameContent;

namespace Polaris;

[HarmonyPatch(typeof(CollectibleBehaviorQuenchable), "IsGettingCooled")]
public static class QuenchingPatches
{
    // Apply the smith's bonus only to the break roll, leaving the tool's stored stress and tempering intact.
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo getChance = AccessTools.Method(typeof(CollectibleBehaviorQuenchable), nameof(CollectibleBehaviorQuenchable.GetShatterChance));
        MethodInfo reduceChance = AccessTools.Method(typeof(QuenchingPatches), nameof(ReduceBreakChance));
        bool matched = false;
        foreach (CodeInstruction instruction in instructions)
        {
            yield return instruction;
            if (instruction.Calls(getChance))
            {
                // IsGettingCooled's second argument is the slot holding the tool being quenched.
                yield return new CodeInstruction(OpCodes.Ldarg_2);
                yield return new CodeInstruction(OpCodes.Call, reduceChance);
                matched = true;
            }
        }
        if (!matched) throw new InvalidOperationException("Could not find the quenching shatter chance calculation.");
    }

    public static float ReduceBreakChance(float chance, ItemSlot slot)
    {
        return slot.Inventory is not InventoryBasePlayer inventory ||
            inventory.Api.Side != EnumAppSide.Server || inventory.Player?.Entity is not EntityPlayer player ||
            !player.TryGetExtraStat("quenchBreakChanceReduction", out float reduction)
            ? chance
            : chance * Math.Clamp(1f - reduction, 0f, 1f);
    }
}
