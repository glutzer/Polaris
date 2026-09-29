using HarmonyLib;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Polaris;

public class TemporalWardPatches
{
    // Prefix on SystemTemporalStability.DoSpawn — blocks storm-mob spawns within 10m of any player with the temporalWard skill.
    [HarmonyPatch(typeof(SystemTemporalStability), "DoSpawn")]
    public class TemporalWardDoSpawnPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(EntityProperties entityType, Vec3d spawnPosition)
        {
            foreach (Entity entity in MainAPI.Sapi.World.GetEntitiesAround(spawnPosition, 8f, 8f, e => e is EntityPlayer player))
            {
                if (entity is not EntityPlayer ePlayer || ePlayer.Player == null) continue;
                if (ePlayer.GetSkillLevel("temporalWard") <= 0) continue;
                return false; // Do nothing.
            }

            return true;
        }
    }
}
