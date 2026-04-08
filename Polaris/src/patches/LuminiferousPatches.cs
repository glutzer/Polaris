using HarmonyLib;
using System;

namespace Polaris;

public class LuminiferousPatches
{
    private const byte GlowHue = 4;
    private const byte GlowSaturation = 2;
    private static readonly byte[] GlowMaxBrightness = [15, 20, 25];

    [HarmonyPatch(typeof(EntityPlayer), "LightHsv", MethodType.Getter)]
    public class LuminiferousGlowPatch
    {
        [HarmonyPostfix]
        public static void Postfix(EntityPlayer __instance, ref byte[]? __result)
        {
            int skillLevel = __instance.GetSkillLevel("luminiferous");
            if (skillLevel <= 0) return;

            int lightLevel = __instance.World.BlockAccessor.GetLightLevel(
                __instance.Pos?.AsBlockPos, EnumLightLevelType.MaxTimeOfDayLight);

            byte maxBrightness = GlowMaxBrightness[Math.Min(skillLevel, GlowMaxBrightness.Length) - 1];
            byte glow = (byte)(maxBrightness * (1f - (lightLevel / 32f)));
            if (glow == 0) return;

            byte[] abilityHsv = [GlowHue, GlowSaturation, glow];

            if (__result == null)
            {
                __result = abilityHsv;
                return;
            }

            byte existingV = __result[2];
            float total = abilityHsv[2] + existingV;
            float t = total > 0 ? existingV / total : 0.5f;

            __result[0] = (byte)((__result[0] * t) + (abilityHsv[0] * (1f - t)));
            __result[1] = (byte)((__result[1] * t) + (abilityHsv[1] * (1f - t)));
            __result[2] = Math.Max(existingV, abilityHsv[2]);
        }
    }
}
