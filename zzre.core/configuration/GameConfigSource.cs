using System.Collections.Generic;
using zzio;
using static zzio.GameConfig;

namespace zzre;

public sealed partial class GameConfigSection
{
    // only those keys that are relevant for zzio

    private const string ConfigurationSection = "zanzarah.game";

    [Configuration(Description = "Volume of sound effects\n(everything that is not music)", IsInteger = true, Min = 0, Max = 100)]
    public int SoundVolume;
    [Configuration(Description = "Volume of music", IsInteger = true, Min = 0, Max = 100)]
    public int MusicVolume;
    [Configuration(Description = "Whether right/left channels are swapped", IsInteger = true, Min = 0, Max = 1)]
    public bool ReverseX;

    [Configuration(Description = "Amount of particles", IsInteger = true, Min = (int)ParticleQuality.VeryLow, Max = (int)ParticleQuality.VeryHigh)]
    public ParticleQuality ParticleQuality;
    [Configuration(Description = "Amount of world details\n(e.g. FOModels)", IsInteger = true, Min = (int)WorldQuality.Low, Max = (int)WorldQuality.High)]
    public WorldQuality WorldQuality;
    [Configuration(Description = "Types of shadows used", IsInteger = true, Min = (int)ShadowQuality.Low, Max = (int)ShadowQuality.VeryHigh)]
    public ShadowQuality ShadowQuality;
    [Configuration(Description = "Amount of effects\n(e.g. scene effects)", IsInteger = true, Min = (int)WorldQuality.Low, Max = (int)WorldQuality.High)]
    public EffectQuality EffectQuality;
    [Configuration(Description = "Whether dynamic models use more geometry", IsInteger = true, Min = 0, Max = 1)]
    public bool ExtraGeometry;

    [Configuration(Description = "Speed of camera movements", Min = MinMouseSpeed, Max = MaxMouseSpeed)]
    public float MouseSpeed;
    [Configuration(Description = "Amount of mouse samples averaged", IsInteger = true, Min = MinMouseSmoothing, Max = MaxMouseSmoothing)]
    public int MouseSmoothing;
    [Configuration(Description = "Whether the vertical mouse axis is inverted", IsInteger = true, Min = 0, Max = 1)]
    public bool MouseInvertY;

    public GameConfigSection(GameConfig? config = null)
    {
        config ??= new();
        SoundVolume = config.soundVolume;
        MusicVolume = config.musicVolume;
        ReverseX = config.reverseX;

        ParticleQuality = config.particleQuality;
        WorldQuality = config.worldQuality;
        ShadowQuality = config.shadowQuality;
        EffectQuality = config.effectQuality;
        ExtraGeometry = config.extraGeometry;

        MouseSpeed = config.mouseSpeed;
        MouseSmoothing = config.mouseSmoothing;
        MouseInvertY = config.mouseInvertY;
    }
}

public sealed class ConfigurationSectionAsSource(string name, IConfigurationSection section) : IConfigurationSource
{
    public string Name => name; 
    public bool KeysHaveChanged => false;
    public bool ValuesHaveChanged => false;

    public IEnumerable<string> Keys => section.Keys;
    public ConfigurationValue this[string key] => section[key];
}

public static class GameConfigSource
{
    public static readonly IConfigurationSource Default =
        new ConfigurationSectionAsSource("Default GameConfig", new GameConfigSection());

    public static IConfigurationSource Create(zzio.GameConfig config) =>
        new ConfigurationSectionAsSource("GameConfig", new GameConfigSection(config));
}
