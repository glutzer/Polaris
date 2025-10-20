using System;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;

namespace Polaris;

public enum AscensionState
{
    Not,
    Ascending,
    Descending
}

/// <summary>
/// Sets view, moves camera.
/// </summary>
[GameSystem(forSide = EnumAppSide.Client)]
public class SystemPolarisStarScreen : GameSystem, IRenderer
{
    private float previousPitch;
    private AscensionState state = AscensionState.Not;
    private const float ASCEND_TIME = 1f;
    private float timeAscending;
    private readonly GuiPolarisMenu gui = new();

    public SystemPolarisStarScreen(bool isServer, ICoreAPI api) : base(isServer, api)
    {
    }

    public void BeginLookingAtStars()
    {
        if (timeAscending == 0f) state = AscensionState.Not;

        if (state == AscensionState.Not)
        {
            MainAPI.Capi.Event.RegisterRenderer(this, EnumRenderStage.Before);
            PlayerCamera mainCamera = MainAPI.Client.MainCamera;
            previousPitch = (float)mainCamera.GetProperty<double>("Pitch");
            timeAscending = 0f;
        }

        state = AscensionState.Ascending;
        MainAPI.GetClientSystem<SystemPolarisAmbient>().PlayTemporaryTrack();

        // Open gui.
        gui.FadeIn(2f);
    }

    public void StopLookingAtStars()
    {
        state = AscensionState.Descending;
        MainAPI.GetClientSystem<SystemPolarisAmbient>().ReturnToNormalMusic();
        gui.FadeOut(1f);
    }

    public void OnRenderFrame(float dt, EnumRenderStage stage)
    {
        PlayerCamera mainCamera = MainAPI.Client.MainCamera;

        if (state == AscensionState.Ascending)
        {
            timeAscending = Math.Clamp(timeAscending + (dt / ASCEND_TIME), 0f, 1f);
        }
        else
        {
            timeAscending = Math.Clamp(timeAscending - (dt / ASCEND_TIME), 0f, 1f);

            if (timeAscending <= 0f)
            {
                MainAPI.Capi.Event.UnregisterRenderer(this, EnumRenderStage.Before);
            }
        }

        MainAPI.Client.mousePitch = GameMath.Lerp(previousPitch, MathF.PI * 0.55f, timeAscending * timeAscending * timeAscending);
        mainCamera.SetProperty("Pitch", MainAPI.Client.mousePitch);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    public double RenderOrder => 0.0001;
    public int RenderRange => 0;
}