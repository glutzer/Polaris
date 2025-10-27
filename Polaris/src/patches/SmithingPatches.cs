using HarmonyLib;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
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
            if (byPlayer.Entity.Api is ICoreServerAPI)
            {
                Polaris.AddExperience("Smithing", byPlayer, 10000);
            }

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

            return true;
        }
    }
}