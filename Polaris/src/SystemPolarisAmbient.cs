using Vintagestory.API.Client;

namespace Polaris;

/// <summary>
/// Copied from BlockEntityMusicTrigger. Retarded sound system.
/// </summary>
[GameSystem(forSide = EnumAppSide.Client)]
public class SystemPolarisAmbient : GameSystem
{
    public ILoadedSound? starSound;

    public SystemPolarisAmbient(bool isServer, ICoreAPI api) : base(isServer, api)
    {
    }

    public void PlayTemporaryTrack()
    {
        starSound ??= MainAPI.Capi.World.LoadSound(new SoundParams()
        {
            Location = "polaris:sounds/stars",
            ShouldLoop = true,
            DisposeOnFinish = false,
            RelativePosition = false,
            Volume = 0.5f,
            SoundType = EnumSoundType.Music
        });

        if (MainAPI.Capi.CurrentMusicTrack is MusicTrack track1)
        {
            track1.Sound?.FadeOut(1f, null);
        }

        if (MainAPI.Capi.CurrentMusicTrack is SurfaceMusicTrack track2)
        {
            track2.Sound?.FadeOut(1f, null);
        }

        starSound.Start();
        starSound.FadeIn(1f, null);
    }

    public void ReturnToNormalMusic()
    {
        if (MainAPI.Capi.CurrentMusicTrack is MusicTrack track1)
        {
            track1.Sound?.FadeIn(1f, null);
        }

        if (MainAPI.Capi.CurrentMusicTrack is SurfaceMusicTrack track2)
        {
            track2.Sound?.FadeIn(1f, null);
        }

        starSound?.FadeOut(1f, sound => starSound.Stop());
    }
}