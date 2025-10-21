using OpenTK.Graphics.OpenGL4;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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

[StructLayout(LayoutKind.Explicit)]
public struct StarLight
{
    [FieldOffset(0)]
    public Vector4 PosRange;

    [FieldOffset(16)]
    public Vector4 Color;
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

    private UboHandle<StarLight>? starUbo;
    public int StarCount { get; private set; }
    private bool lookingAtStars;

    private PolarisHud polarisHud = null!;

    public SystemPolarisStarScreen(bool isServer, ICoreAPI api) : base(isServer, api)
    {

    }

    public override void PreInitialize()
    {
        polarisHud = new PolarisHud();
        polarisHud.TryOpen();
    }

    public void UpdateUbo(List<StarLight> lights)
    {
        if (starUbo == null) return;
        starUbo.BufferData([.. lights]);
        StarCount = lights.Count;
    }

    public void BeginLightRendering()
    {
        starUbo = new(BufferUsageHint.DynamicDraw);
        UboRegistry.SetUbo("starLightData", starUbo);
        starUbo.BufferData(new StarLight());
    }

    public void StopLightRendering()
    {
        starUbo?.Dispose();
        starUbo = null;
        StarCount = 0;
        UboRegistry.SetUbo("starLightData", 0);
    }

    public void ToggleStars()
    {
        if (lookingAtStars)
        {
            StopLookingAtStars();
        }
        else
        {
            BeginLookingAtStars();
        }
    }

    public void BeginLookingAtStars()
    {
        if (state == AscensionState.Descending) return;

        lookingAtStars = true;

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
        BeginLightRendering();
        gui.FadeIn(1f);
    }

    public void StopLookingAtStars()
    {
        lookingAtStars = false;

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
                state = AscensionState.Not;
                StopLightRendering();
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