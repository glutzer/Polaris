using HarmonyLib;
using System;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Polaris;

public class HorticulturePatches
{
    [HarmonyPatch(typeof(BlockCrop), "GetDrops")]
    public class CropGetDropsPatch
    {
        public const float CROP_EXP_MULTI = 10f;

        [HarmonyPostfix]
        public static void Postfix(BlockCrop __instance, IWorldAccessor world, BlockPos pos, IPlayer? byPlayer, ref ItemStack[] __result)
        {
            if (world.Side != EnumAppSide.Server) return;
            if (byPlayer?.Entity == null) return;
            if (__result == null || __result.Length == 0) return;

            float xp = ComputeCropXp(__instance) * CROP_EXP_MULTI;
            if (xp <= 0f) return;

            SystemPolaris.AddExperience("Horticulture", byPlayer, xp);
        }

        private static float ComputeCropXp(BlockCrop block)
        {
            int currentCropStage = block.CurrentCropStage;
            if (block.Drops.Length <= 1 || block.CropProps == null || currentCropStage < 0)
                return 0f;

            float monthsPerStep = block.CropProps.TotalGrowthMonths / block.CropProps.GrowthStages;
            float xp = block.CropProps.HarvestGrowthStageLoss > 0
                ? block.CropProps.HarvestGrowthStageLoss * monthsPerStep * 0.5f
                : block.CropProps.TotalGrowthMonths * 0.5f;

            float penalty = (float)Math.Pow(0.5f, block.CropProps.GrowthStages - currentCropStage);
            return Math.Clamp(xp * penalty, 0f, 3f);
        }
    }
}
