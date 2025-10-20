global using NutsLib;
global using OpenTK.Mathematics;
global using Vintagestory.API.Common;
using Vintagestory.API.Client;

namespace Polaris;

public class PolarisModSystem : ModSystem
{
    private bool activeMusic = false;

    public override void StartClientSide(ICoreClientAPI api)
    {
        api.Event.AfterActiveSlotChanged += (t) =>
        {
            if (activeMusic)
            {
                MainAPI.GetClientSystem<SystemPolarisStarScreen>().BeginLookingAtStars();
                activeMusic = false;
            }
            else
            {
                MainAPI.GetClientSystem<SystemPolarisStarScreen>().StopLookingAtStars();
                activeMusic = true;
            }
        };

        NuttyShaderRegistry.AddShader("polaris:stars", "polaris:stars", "polarisstars");
    }
}