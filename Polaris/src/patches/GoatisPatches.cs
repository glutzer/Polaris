using HarmonyLib;
using Vintagestory.API.Common.Entities;

namespace Polaris;

// https://www.youtube.com/watch?v=TseuoePz16g
public class GoatisPatches
{
    [HarmonyPatch(typeof(CollectibleObject), "GetNutritionProperties")]
    public class EatPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(CollectibleObject __instance, ref FoodNutritionProperties __result, Entity forEntity)
        {
            if (forEntity.GetSkillLevel("primalist") > 0)
            {
                if (__instance.NutritionProps == null && __instance.GetBehavior<RawFoodBehavior>() is RawFoodBehavior rawFoodBehavior)
                {
                    __result = rawFoodBehavior.NutritionalProps!;
                    return false;
                }
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(EntityAgent), "ReceiveSaturation")]
    public class SatPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(EntityAgent __instance, ref float saturation, EnumFoodCategory foodCat/*, float saturationLossDelay, float nutritionGainMultiplier*/)
        {
            if (__instance.GetSkillLevel("primalist") > 0)
            {
                if (foodCat == EnumFoodCategory.Grain)
                {
                    saturation = 0f;
                }
            }

            return true;
        }
    }
}