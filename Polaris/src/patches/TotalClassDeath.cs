using HarmonyLib;
using Vintagestory.GameContent;

namespace Polaris;

public class TotalClassDeath
{
    [HarmonyPatch(typeof(GuiDialogCreateCharacter), "changeClass")]
    public class CharPatch1
    {
        [HarmonyPrefix]
        public static bool Prefix(ref int dir)
        {
            // Make anything except commoner non-selectable.
            dir = 0;
            return true;
        }
    }

    [HarmonyPatch(typeof(CharacterSystem), "onCharacterSelection")]
    public class CharPatch2
    {
        [HarmonyPrefix]
        public static bool Prefix(CharacterSelectionPacket p)
        {
            // Make anything except commoner non-selectable.
            p.CharacterClass = "commoner";
            return true;
        }
    }

    [HarmonyPatch(typeof(CharacterSystem), "Event_MatchesGridRecipe")]
    public class CraftingPatch
    {
        // Simply add a check to a level that's the same as the trait.
        [HarmonyPrefix]
        public static bool Prefix(ref bool __result, IPlayer player, GridRecipe recipe)
        {
            if (recipe.RequiresTrait == null)
            {
                __result = true;
                return false;
            }

            int level = player.Entity.GetSkillLevel(recipe.RequiresTrait);

            __result = level > 0;

            return false;
        }
    }
}