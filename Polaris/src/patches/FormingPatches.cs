using HarmonyLib;
using System;
using Vintagestory.GameContent;

namespace Polaris;

public static class FormingPatches
{



    [HarmonyPatch(typeof(BlockEntityClayForm), nameof(BlockEntityClayForm.CheckIfFinished))]
    public static class ClayCompletionPatch
    {
        [HarmonyPrefix]
        public static void Prefix(BlockEntityClayForm __instance, out ClayFormingRecipe? __state)
        {
            __state = __instance.Api.Side == EnumAppSide.Server ? __instance.SelectedRecipe : null;
        }

        [HarmonyPostfix]
        public static void Postfix(BlockEntityClayForm __instance, IPlayer byPlayer, ClayFormingRecipe? __state)
        {
            // Successful completion clears the selected recipe; incomplete work does not.
            if (__state == null || __instance.SelectedRecipe != null || byPlayer == null) return;

            int requiredVoxels = 0;
            foreach (bool voxel in __state.Voxels)
                if (voxel) requiredVoxels++;

            // Same required-clay calculation shown by the game's recipe selector:
            // the initial form supplies 64 voxels and additional clay supplies 25 each.
            int requiredClay = (int)Math.Ceiling(Math.Max(1f, (requiredVoxels - 64) / 25f));
            SystemPolaris.AddExperience("Forming", byPlayer, requiredClay * ExpGlobals.FormingExperiencePerClay);
        }
    }

    [HarmonyPatch(typeof(BlockEntityKnappingSurface), nameof(BlockEntityKnappingSurface.CheckIfFinished))]
    public static class KnappingCompletionPatch
    {
        [HarmonyPrefix]
        public static void Prefix(BlockEntityKnappingSurface __instance, out bool __state)
        {
            __state = __instance.Api.Side == EnumAppSide.Server && __instance.SelectedRecipe != null;
        }

        [HarmonyPostfix]
        public static void Postfix(BlockEntityKnappingSurface __instance, IPlayer byPlayer, bool __state)
        {
            if (!__state || __instance.SelectedRecipe != null || byPlayer == null) return;
            SystemPolaris.AddExperience("Forming", byPlayer, ExpGlobals.KnappingExperience);
        }
    }
}
