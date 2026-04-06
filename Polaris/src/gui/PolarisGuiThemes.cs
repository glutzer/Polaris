using System;
using System.Collections.Generic;

namespace Polaris;

public static class PolarisGuiThemes
{
    public static Font Font => FontRegistry.GetFont("lora");

    public static Vector4 TemporalColor => new(0f, 1f, 0.55f, 1f);
    public static Vector4 TemporalColorDark => new(0f, 0.9f, 0.45f, 0.5f);
    public static Vector4 VintageBrown => new(0.3f, 0.25f, 0.2f, 1f);

    public static Vector3 Red => new(1f, 0f, 0f);
    public static Vector3 Green => new(0f, 1f, 0f);
    public static Vector3 Blue => new(0f, 0f, 1f);

    public static Vector4 ButtonColor => new(0.4f, 0.4f, 0.4f, 1f);
    public static Vector4 TextColor => new(0, 0.7f, 0.4f, 1f);
    public static Vector4 DarkColor => new(0.1f, 0.1f, 0.1f, 1f);

    private static readonly Dictionary<string, object> cache = [];

    public static Texture Blank => GetOrCreate("blank", () => Texture.Create("polaris:textures/gui/blank.png"));
    public static NineSliceTexture ExpSides => GetOrCreate("expsides", () => Texture.Create("polaris:textures/gui/expsides.png").AsNineSlice(12, 12));
    public static NineSliceTexture ExpInner => GetOrCreate("expinner", () => Texture.Create("polaris:textures/gui/expinner.png").AsNineSlice(12, 12));

    private static T GetOrCreate<T>(string path, Func<T> makeTex)
    {
        if (cache.TryGetValue(path, out object? value))
        {
            return (T)value;
        }
        else
        {
            object tex = makeTex()!;
            cache.Add(path, tex);
            return (T)tex;
        }
    }

    public static void ClearCache()
    {
        foreach (object obj in cache)
        {
            if (obj is IDisposable tex)
            {
                tex.Dispose();
            }
        }

        cache.Clear();
    }
}