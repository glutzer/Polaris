using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Vintagestory.API.Client;
using Vintagestory.Client.NoObf;
using Vintagestory.Common;
using static Polaris.TranspilerHelpers;

namespace Polaris;

[HarmonyPatch]
public partial class StrongBackInventoryPatches
{
    private static readonly FieldInfo BagSlotsField =
        typeof(InventoryPlayerBackpacks).GetField("bagSlots", BindingFlags.Public | BindingFlags.Instance)!;

    private static readonly FieldInfo BagInvField =
        typeof(InventoryPlayerBackpacks).GetField("bagInv", BindingFlags.Public | BindingFlags.Instance)!;

    public const int TotalBagSlots = 10;
    public const int VanillaBagSlots = 4;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(InventoryPlayerBackpacks), MethodType.Constructor,
        typeof(string), typeof(string), typeof(ICoreAPI))]
    public static void CtorClassNamePostfix(InventoryPlayerBackpacks __instance)
        => ExpandSlots(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(InventoryPlayerBackpacks), MethodType.Constructor,
        typeof(string), typeof(ICoreAPI))]
    public static void CtorInventoryIdPostfix(InventoryPlayerBackpacks __instance)
        => ExpandSlots(__instance);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(InventoryPlayerBackpacks), "NewSlot")]
    public static bool NewSlotPrefix(InventoryPlayerBackpacks __instance, int slotId, ref ItemSlot __result)
    {
        if (slotId < VanillaBagSlots) return true; // let original run for 0–3
        __result = new ItemSlotLockedBackpack(__instance, slotId - VanillaBagSlots);
        return false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(InventoryPlayerBackpacks), "FromTreeAttributes")]
    public static void FromTreeAttributesPostfix(InventoryPlayerBackpacks __instance)
    {
        ItemSlot[] bagSlots = (ItemSlot[])BagSlotsField.GetValue(__instance)!;
        if (bagSlots.Length >= TotalBagSlots) return;

        ItemSlot[] expanded = new ItemSlot[TotalBagSlots];
        for (int i = 0; i < bagSlots.Length; i++) expanded[i] = bagSlots[i];
        for (int i = bagSlots.Length; i < TotalBagSlots; i++)
            expanded[i] = new ItemSlotLockedBackpack(__instance, i - VanillaBagSlots);

        BagSlotsField.SetValue(__instance, expanded);

        BagInventory bagInv = (BagInventory)BagInvField.GetValue(__instance)!;
        bagInv.BagSlots = expanded;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(InventoryPlayerBackpacks), "CountForNetworkPacket", MethodType.Getter)]
    public static bool CountForNetworkPacketPrefix(ref int __result)
    {
        __result = TotalBagSlots;
        return false;
    }

    private static void ExpandSlots(InventoryPlayerBackpacks instance)
    {
        ItemSlot[] bagSlots = (ItemSlot[])BagSlotsField.GetValue(instance)!;
        if (bagSlots.Length >= TotalBagSlots) return;

        ItemSlot[] expanded = new ItemSlot[TotalBagSlots];
        for (int i = 0; i < VanillaBagSlots; i++) expanded[i] = bagSlots[i];
        for (int i = VanillaBagSlots; i < TotalBagSlots; i++)
            expanded[i] = new ItemSlotLockedBackpack(instance, i - VanillaBagSlots);

        BagSlotsField.SetValue(instance, expanded);

        BagInventory bagInv = (BagInventory)BagInvField.GetValue(instance)!;
        bagInv.BagSlots = expanded;
    }
}


[HarmonyPatch(typeof(HudHotbar), "ComposeGuis")]
public class HudHotbarComposeGuisPatch
{
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> il = [.. instructions];

        MethodInfo initArrayMethod = typeof(System.Runtime.CompilerServices.RuntimeHelpers)
            .GetMethod("InitializeArray")!;
        MethodInfo slotArrayHelper = typeof(StrongBackInventoryPatches)
            .GetMethod(nameof(StrongBackInventoryPatches.GetAllBackpackSlotIds))!;
        MethodInfo colCountHelper = typeof(StrongBackInventoryPatches)
            .GetMethod(nameof(StrongBackInventoryPatches.GetBackpackColCount))!;
        MethodInfo widthHelper = typeof(StrongBackInventoryPatches)
            .GetMethod(nameof(StrongBackInventoryPatches.GetHotbarDialogWidth))!;
        MethodInfo slotGridMethod = typeof(ElementStdBounds)
            .GetMethod("SlotGrid")!;

        bool didWidth = false;
        bool didSlotGrid = false;
        bool didAddGrid = false;

        for (int i = 0; i < il.Count; i++)
        {
            // 850f > GetHotbarDialogWidth()
            if (!didWidth
                && il[i].opcode == OpCodes.Ldc_R4
                && il[i].operand is float fw && fw == 850f)
            {
                il[i] = new CodeInstruction(OpCodes.Call, widthHelper);
                didWidth = true;
                continue;
            }

            if (!didSlotGrid
                && il[i].opcode == OpCodes.Ldc_I4_4
                && i + 2 < il.Count
                && il[i + 1].opcode == OpCodes.Ldc_I4_1
                && il[i + 2].opcode == OpCodes.Call
                && il[i + 2].operand is MethodInfo mg && mg == slotGridMethod)
            {
                il[i] = new CodeInstruction(OpCodes.Call, colCountHelper);
                didSlotGrid = true;
                continue;
            }

            if (!didAddGrid
                && il[i].opcode == OpCodes.Ldstr
                && il[i].operand is string sg && sg == "backpackgrid")
            {
                // Walk back to find the newarr for the slot-id array
                for (int j = i - 1; j >= 1; j--)
                {
                    if (il[j].opcode != OpCodes.Newarr) continue;

                    int sizeInstrIdx = j - 1; // ldc.i4 that pushes array size
                    if (!IsLdcI4(il[sizeInstrIdx])) continue;

                    // Determine end of array block
                    int blockEnd;
                    if (j + 3 < il.Count
                        && il[j + 1].opcode == OpCodes.Dup
                        && il[j + 2].opcode == OpCodes.Ldtoken
                        && il[j + 3].opcode == OpCodes.Call
                        && il[j + 3].operand is MethodInfo mia && mia == initArrayMethod)
                    {
                        blockEnd = j + 3; // Pattern A: InitializeArray
                    }
                    else
                    {
                        blockEnd = j;
                        for (int m = j + 1; m < i; m++)
                            if (il[m].opcode == OpCodes.Stelem_I4) blockEnd = m;
                    }

                    // The col-count ldc.i4.4 is immediately before the size push
                    int colCountIdx = sizeInstrIdx - 1;
                    if (colCountIdx >= 0 && IsLdcI4(il[colCountIdx]))
                    {
                        // Replace col-count with call GetBackpackColCount()
                        il[colCountIdx] = new CodeInstruction(OpCodes.Call, colCountHelper);

                        // Replace size+newarr+init block with call GetAllBackpackSlotIds()
                        int removeCount = blockEnd - sizeInstrIdx + 1;
                        il.RemoveRange(sizeInstrIdx, removeCount);
                        il.Insert(sizeInstrIdx, new CodeInstruction(OpCodes.Call, slotArrayHelper));
                    }

                    didAddGrid = true;
                    break;
                }
            }
        }

        return il;
    }
}


[HarmonyPatch(typeof(GuiDialogInventory), "ComposeSurvivalInvDialog")]
public class GuiDialogInventoryPatch
{
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> il = [.. instructions];

        MethodInfo initArrayMethod = typeof(System.Runtime.CompilerServices.RuntimeHelpers)
            .GetMethod("InitializeArray")!;
        MethodInfo helperMethod = typeof(StrongBackInventoryPatches)
            .GetMethod(nameof(StrongBackInventoryPatches.GetInventoryDialogSlotIds))!;

        for (int i = 0; i < il.Count; i++)
        {
            if (il[i].opcode == OpCodes.Ldstr && il[i].operand is string s && s == "slotgrid")
            {
                ReplaceConstArrayBeforeMarker(il, i, helperMethod, initArrayMethod, replaceColCount: false, newColCount: 6);
                break;
            }
        }

        return il;
    }
}

public partial class StrongBackInventoryPatches
{
    /// <summary>How many backpack slots the hotbar GUI should currently show (4–10).</summary>
    public static int CurrentBackpackSlotCount { get; set; } = VanillaBagSlots;

    public static int[] GetAllBackpackSlotIds()
    {
        int count = CurrentBackpackSlotCount;
        int[] ids = new int[count];
        for (int i = 0; i < count; i++) ids[i] = i;
        return ids;
    }

    public static int[] GetInventoryDialogSlotIds()
    {
        int[] ids = new int[TotalBagSlots];
        for (int i = 0; i < TotalBagSlots; i++) ids[i] = i;
        return ids;
    }

    public static int GetBackpackColCount() => CurrentBackpackSlotCount;

    public static float GetHotbarDialogWidth() => 640f + (CurrentBackpackSlotCount * 51f);
}

internal static class TranspilerHelpers
{
    internal static void ReplaceConstArrayBeforeMarker(
        List<CodeInstruction> il,
        int markerIdx,
        MethodInfo helperMethod,
        MethodInfo initArrayMethod,
        bool replaceColCount,
        int newColCount = 5)
    {
        for (int j = markerIdx - 1; j >= 1; j--)
        {
            if (il[j].opcode != OpCodes.Newarr) continue;

            int sizeIdx = j - 1;
            if (!IsLdcI4(il[sizeIdx])) continue;

            // Determine block end: either InitializeArray call or last stelem.i4
            int blockEnd;

            // Pattern A: dup / ldtoken / call InitializeArray
            if (j + 3 < il.Count
                && il[j + 1].opcode == OpCodes.Dup
                && il[j + 2].opcode == OpCodes.Ldtoken
                && il[j + 3].opcode == OpCodes.Call
                && il[j + 3].operand is MethodInfo m && m == initArrayMethod)
            {
                blockEnd = j + 3;
            }
            else
            {
                // Pattern B: individual stelem.i4 instructions
                blockEnd = j;
                for (int m2 = j + 1; m2 < markerIdx; m2++)
                    if (il[m2].opcode == OpCodes.Stelem_I4) blockEnd = m2;
            }

            // Replace sizeIdx..blockEnd with a single call to helperMethod
            int removeCount = blockEnd - sizeIdx + 1;
            il.RemoveRange(sizeIdx, removeCount);
            il.Insert(sizeIdx, new CodeInstruction(OpCodes.Call, helperMethod));

            // The marker index has shifted by (removeCount - 1)
            int newMarkerIdx = markerIdx - removeCount + 1;

            // Patch the col-count argument: first ldc.i4 between the helper call and the marker
            if (replaceColCount)
            {
                for (int k = sizeIdx + 1; k < newMarkerIdx; k++)
                {
                    if (IsLdcI4(il[k]))
                    {
                        SetLdcI4Value(il, k, newColCount);
                        break;
                    }
                }
            }

            return;
        }
    }

    private static void SetLdcI4Value(List<CodeInstruction> il, int idx, int value)
    {
        il[idx] = value switch
        {
            0 => new CodeInstruction(OpCodes.Ldc_I4_0),
            1 => new CodeInstruction(OpCodes.Ldc_I4_1),
            2 => new CodeInstruction(OpCodes.Ldc_I4_2),
            3 => new CodeInstruction(OpCodes.Ldc_I4_3),
            4 => new CodeInstruction(OpCodes.Ldc_I4_4),
            5 => new CodeInstruction(OpCodes.Ldc_I4_5),
            6 => new CodeInstruction(OpCodes.Ldc_I4_6),
            7 => new CodeInstruction(OpCodes.Ldc_I4_7),
            8 => new CodeInstruction(OpCodes.Ldc_I4_8),
            >= -128 and <= 127 => new CodeInstruction(OpCodes.Ldc_I4_S, (sbyte)value),
            _ => new CodeInstruction(OpCodes.Ldc_I4, value),
        };
    }

    internal static bool IsLdcI4(CodeInstruction ci)
        => ci.opcode == OpCodes.Ldc_I4
        || ci.opcode == OpCodes.Ldc_I4_0
        || ci.opcode == OpCodes.Ldc_I4_1
        || ci.opcode == OpCodes.Ldc_I4_2
        || ci.opcode == OpCodes.Ldc_I4_3
        || ci.opcode == OpCodes.Ldc_I4_4
        || ci.opcode == OpCodes.Ldc_I4_5
        || ci.opcode == OpCodes.Ldc_I4_6
        || ci.opcode == OpCodes.Ldc_I4_7
        || ci.opcode == OpCodes.Ldc_I4_8
        || ci.opcode == OpCodes.Ldc_I4_S
        || ci.opcode == OpCodes.Ldc_I4_M1;
}
