using HarmonyLib;
using Vintagestory.GameContent;

namespace Polaris;

public class TradingPatches
{
    [HarmonyPatch(typeof(InventoryTrader), "TryBuySell")]
    public class TryBuySellPatch
    {
        [HarmonyPrefix]
        public static void Prefix(InventoryTrader __instance, IPlayer buyingPlayer, out (int gears, IPlayer player) __state)
        {
            __state = (__instance.GetTotalCost() + __instance.GetTotalGain(), buyingPlayer);
        }

        [HarmonyPostfix]
        public static void Postfix(EnumTransactionResult __result, InventoryTrader __instance, (int gears, IPlayer player) __state)
        {
            if (__instance.Api.Side != EnumAppSide.Server) return;
            if (__result != EnumTransactionResult.Success) return;
            if (__state.gears <= 0) return;

            SystemPolaris.AddExperience("Trade", __state.player, __state.gears * ExpGlobals.TradeExperiencePerGear);
        }
    }
}
