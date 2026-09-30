using HarmonyLib;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Polaris;

public class SmithingPatches
{
    private static Vec3i? FindFreeVoxel(int quantityLayers, byte[,,] Voxels, SmithingRecipe selectedRecipe)
    {
        for (int num = 5; num >= 0; num--)
        {
            for (int i = 0; i < 16; i++)
            {
                for (int j = 0; j < 16; j++)
                {
                    bool num2 = num < quantityLayers && selectedRecipe.Voxels[j, num, i];
                    EnumVoxelMaterial enumVoxelMaterial = (EnumVoxelMaterial)Voxels[j, num, i];
                    if (!num2 && enumVoxelMaterial == EnumVoxelMaterial.Metal)
                    {
                        return new Vec3i(j, num, i);
                    }
                }
            }
        }

        return null;
    }

    private static void MoveVoxelToCorrectPosition(BlockEntityAnvil anvil, Vec3i voxelPos, BlockSelection blockSel)
    {
        SmithingRecipe selectedRecipe = anvil.SelectedRecipe;
        int quantityLayers = selectedRecipe.QuantityLayers;
        byte[,,] Voxels = anvil.Voxels;

        // Try to find a free voxel that's metal.
        Vec3i? freeVoxel = FindFreeVoxel(quantityLayers, Voxels, selectedRecipe);
        if (freeVoxel == null) return;

        for (int i = 0; i < 16; i++)
        {
            for (int j = 0; j < 16; j++)
            {
                for (int k = 0; k < 6; k++)
                {
                    EnumVoxelMaterial enumVoxelMaterial = (EnumVoxelMaterial)Voxels[i, k, j];

                    //if (enumVoxelMaterial == EnumVoxelMaterial.Slag)
                    //{
                    //    Voxels[i, k, j] = 0;
                    //    return;
                    //}

                    if (k < quantityLayers && selectedRecipe.Voxels[i, k, j] && enumVoxelMaterial == EnumVoxelMaterial.Empty)
                    {
                        Voxels[i, k, j] = 1;
                        Voxels[freeVoxel.X, freeVoxel.Y, freeVoxel.Z] = 0;
                        return;
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(BlockEntityAnvil), "OnUseOver", [typeof(IPlayer), typeof(Vec3i), typeof(BlockSelection)])]
    public class AnvilPatch
    {
        [HarmonyPrefix]
        public static bool HarmonyPrefix(BlockEntityAnvil __instance, IPlayer byPlayer, Vec3i voxelPos, BlockSelection blockSel)
        {
            ItemSlot activeHotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (activeHotbarSlot.Itemstack == null || !__instance.CanWorkCurrent)
            {
                return true;
            }
            int toolMode = activeHotbarSlot.Itemstack.Collectible.GetToolMode(activeHotbarSlot, byPlayer, blockSel);

            // Heavy hit.
            if (toolMode == 0)
            {
                int masterSmithLevel = byPlayer.Entity.GetSkillLevel("masterSmith");
                while (masterSmithLevel > 0)
                {
                    MoveVoxelToCorrectPosition(__instance, voxelPos, blockSel);
                    masterSmithLevel--;
                }
            }

            __instance.WorkItemStack?.Attributes.SetInt("polarisHits", __instance.WorkItemStack.Attributes.GetInt("polarisHits") + 1);

            return true;
        }
    }

    [HarmonyPatch(typeof(BlockEntityAnvil), "CheckIfFinished")]
    public class AnvilCheckIfFinishedPatch
    {
        [HarmonyPrefix]
        public static void Prefix(BlockEntityAnvil __instance, out AnvilSmithState __state, IPlayer byPlayer)
        {
            __state = new AnvilSmithState
            {
                Player = byPlayer,
                HitCount = __instance.WorkItemStack?.Attributes.GetInt("polarisHits") ?? 0,
                HadWorkItem = __instance.WorkItemStack != null,
                Output = __instance.Api.Side == EnumAppSide.Server ? __instance.SelectedRecipe?.Output.ResolvedItemstack?.Clone() : null
            };
        }

        [HarmonyPostfix]
        public static void Postfix(BlockEntityAnvil __instance, AnvilSmithState __state)
        {
            // Recipe completes when WorkItemStack transitions from non-null to null.
            if (!__state.HadWorkItem || __instance.WorkItemStack != null) return;
            if (__state.Player == null || __instance.Api.Side != EnumAppSide.Server) return;

            float exp = ExpGlobals.SmithingCompletionExperience + (ExpGlobals.SmithingExperiencePerHit * __state.HitCount);
            SystemPolaris.AddExperience("Smithing", __state.Player, exp);
            if (__state.Output?.Collectible.Code.Path == "ingot-iron")
            {
                SystemPolaris.AddExperience("Smithing", __state.Player, __state.Output.StackSize * ExpGlobals.MetalProductionExperiencePerIngot);
                SystemPolaris.TriggerAchievement("metalworker", __state.Player, "iron");
            }
        }
    }

    public class AnvilSmithState
    {
        public IPlayer? Player;
        public int HitCount;
        public bool HadWorkItem;
        public ItemStack? Output;
    }

    [HarmonyPatch(typeof(BlockBloomery), "GetDrops")]
    public class BloomeryPatch
    {
        [HarmonyPrefix]
        public static bool HarmonyPrefix(IPlayer? byPlayer, ref float dropQuantityMultiplier)
        {
            if (byPlayer == null) return true;

            if (byPlayer.Entity.TryGetExtraStat("bloomeryDrops", out float dropMulti))
            {
                dropQuantityMultiplier *= dropMulti;
            }

            return true;
        }
    }
}
