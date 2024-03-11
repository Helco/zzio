using System;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;

namespace zzio;

public enum BindingId
{
    // TODO: Improve binding ID names
    Forward = 0,
    Back = 1,
    Right = 2,
    Left = 3,
    LookHor = 4,
    LookVer = 5,
    Shoot = 6,
    Jump = 7,
    MouseUnk = 8,
    NumPad0 = 9,
    Escape = 10,
    F11 = 11,
    P = 12,
    WizForm1 = 13,
    WizForm2 = 14,
    WizForm3 = 15,
    WizForm4 = 16,
    WizForm5 = 17,
    Next = 18,
    Prior = 19,
    MouseWheel = 20,
    Return = 21,
    F1 = 22,
    F2 = 23,
    F3 = 24,
    F4 = 25,
    T = 26,
    Tab = 27,
    F5 = 28,
}

public sealed class GameConfig
{
    private const uint Magic = 0x24;
    public const float MinGamma = 0.3f;
    public const float MaxGamma = 2f;
    public const float MinMouseSpeed = 0.1f;
    public const float MaxMouseSpeed = 3f;
    public const int MinMouseSmoothing = 1;
    public const int MaxMouseSmoothing = 8;

    public enum Resolution
    {
        R640x480x16 = 0,
        R800x600x16,
        R1024x768x16,
        R640x480x32,
        R800x600x32,
        R1024x768x32,

        Unknown = -1
    }

    public enum SoundDriver
    {
        MilesFast2D = 0,
        DirectSoundSoftware,
        DirectSoundHardware,

        Unknown = -1
    }

    public enum SoundQuality
    {
        Low = 0,
        Medium,
        High,

        Unknown = -1
    }

    public enum InputDevice
    {
        Keyboard = 0,
        Mouse,

        Unknown = -1
    }

    public enum InputType // not too sure about this one...
    {
        Button,
        NormalAxis,
        ScaledAxis, // seems to behave like NormalAxis

        Unknown = -1
    }

    public enum MouseCode
    {
        HorizontalAxis = 0,
        VerticalAxis = 4,
        WheelAxis = 8,
        LeftButton = 12,
        MiddleButton = 13,
        RightButton = 14
    }

    public enum ParticleQuality
    {
        VeryLow = 2,
        Low = 5,
        Normal = 10,
        High = 15,
        VeryHigh = 20
    }

    public enum WorldQuality
    {
        Low = 0,
        Normal = 1,
        High = 2
    }

    public enum ShadowQuality
    {
        Low = 0,
        Normal = 1,
        High = 2,
        VeryHigh = 3
    }

    public enum EffectQuality
    {
        Low = 0,
        Normal = 1,
        High = 2
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly record struct Binding(
        InputDevice device,
        InputType type,
        int RawCode)
    {
        public DirectInputKey KeyCode => (DirectInputKey)RawCode;
        public MouseCode MouseCode => (MouseCode)RawCode;

        public Binding(DirectInputKey key)
            : this(InputDevice.Keyboard, InputType.Button, (int)key) { }

        public Binding(MouseCode code) : this(InputDevice.Mouse,
            code switch
            {
                MouseCode.HorizontalAxis => InputType.ScaledAxis,
                MouseCode.VerticalAxis => InputType.NormalAxis,
                MouseCode.WheelAxis => InputType.NormalAxis,
                _ => InputType.Button
            }, (int)code)
        { }
    }

    public Resolution resolution;
    public bool isFullscreen;
    public uint deviceType; // Renderware and System specific index
    public float gamma = 1f; // from 0.3 to 2.0

    public SoundDriver soundDriver;
    public SoundQuality soundQuality;
    public int soundVolume = 70; // from 0 to 100 inclusive
    public int musicVolume = 35; // from 0 to 100 inclusive
    public bool reverseX;

    public ParticleQuality particleQuality = ParticleQuality.Normal;
    public int unknownQuality;
    public WorldQuality worldQuality= WorldQuality.Normal;
    public ShadowQuality shadowQuality = ShadowQuality.Normal;
    public EffectQuality effectQuality = EffectQuality.Normal;
    public bool extraGeometry;

    public float mouseSpeed = 1f;
    public int mouseSmoothing = 1; // from 1 to 8 inclusive
    public bool mouseInvertY = true;

    public Binding[] bindings = [.. DefaultBindings];

    public static GameConfig ReadNew(Stream stream)
    {
        GameConfig config = new();
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        if (reader.ReadUInt32() != Magic)
            throw new InvalidDataException("Invalid magic value in game config");
        config.resolution = EnumUtils.intToEnum<Resolution>(reader.ReadInt32());
        config.isFullscreen = reader.ReadInt32() != 0;
        config.deviceType = reader.ReadUInt32();
        config.gamma = reader.ReadSingle();

        config.soundDriver = EnumUtils.intToEnum<SoundDriver>(reader.ReadInt32());
        config.soundQuality = EnumUtils.intToEnum<SoundQuality>(reader.ReadInt32());
        config.soundVolume = reader.ReadInt32();
        config.musicVolume = reader.ReadInt32();
        config.reverseX = reader.ReadBoolean();

        config.particleQuality = EnumUtils.intToEnum<ParticleQuality>(reader.ReadInt32());
        config.unknownQuality = reader.ReadInt32();
        config.worldQuality = EnumUtils.intToEnum<WorldQuality>(reader.ReadInt32());
        config.shadowQuality = EnumUtils.intToEnum<ShadowQuality>(reader.ReadInt32());
        config.effectQuality = EnumUtils.intToEnum<EffectQuality>(reader.ReadInt32());
        config.extraGeometry = reader.ReadBoolean();

        if (reader.ReadZString() != "[ConfigInput]")
            throw new InvalidDataException("Expected game config input header");
        bool readNextSection = true;
        while(readNextSection)
        {
            switch(reader.ReadZString())
            {
                case "[ConfigInputOptions]":
                    config.mouseSpeed = reader.ReadSingle();
                    config.mouseSmoothing = reader.ReadInt32();
                    config.reverseX = reader.ReadInt32() != 0;
                    break;
                case "[ConfigInputBindings]":
                    var presentBindingCount = reader.ReadUInt32();
                    config.bindings = new Binding[Math.Max(DefaultBindings.Length, presentBindingCount)];
                    DefaultBindings.CopyTo(config.bindings.AsSpan());
                    for (int i = 0; i < presentBindingCount; i++)
                        config.bindings[i] = new(
                            EnumUtils.intToEnum<InputDevice>(reader.ReadInt32()),
                            EnumUtils.intToEnum<InputType>(reader.ReadInt32()),
                            reader.ReadInt32());
                    break;
                case "[ConfigInputEnd]":
                    readNextSection = false;
                    break;
                default:
                    throw new InvalidDataException("Unexpected section in game config input");
            }
        }
        // In files written by zanzarah there would be a ZString containing some system info
        // This info is not read by zanzarah, therefore we don't need it.

        return config;
    }

    public void Write(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(Magic);
        writer.Write((int)resolution);
        writer.Write(isFullscreen ? 1 : 0);
        writer.Write(deviceType);
        writer.Write(gamma);

        writer.Write((int)soundDriver);
        writer.Write((int)soundQuality);
        writer.Write(soundVolume);
        writer.Write(musicVolume);
        writer.Write(reverseX);

        writer.Write((int)particleQuality);
        writer.Write(unknownQuality);
        writer.Write((int)worldQuality);
        writer.Write((int)shadowQuality);
        writer.Write((int)effectQuality);
        writer.Write(extraGeometry);

        writer.WriteZString("[ConfigInput]");
        writer.WriteZString("[ConfigInputOptions]");
        writer.Write(mouseSpeed);
        writer.Write(mouseSmoothing);
        writer.Write(reverseX ? 1 : 0);
        writer.WriteZString("[ConfigInputBindings]");
        writer.Write(bindings.Length);
        writer.WriteStructureArray(bindings, sizeof(int) * 3);
        writer.WriteZString("[ConfigInputEnd]");
    }

    private static readonly Binding[] DefaultBindings =
    [
        /* Forward */    new(DirectInputKey.Up),
        /* Back */       new(DirectInputKey.Down),
        /* Right */      new(DirectInputKey.Right),
        /* Left */       new(DirectInputKey.Left),
        /* LookHor */    new(MouseCode.HorizontalAxis),
        /* LookVer */    new(MouseCode.VerticalAxis),
        /* Shoot */      new(MouseCode.LeftButton),
        /* Jump */       new(MouseCode.RightButton),
        /* MouseUnk */   new(MouseCode.MiddleButton),
        /* NumPad0 */    new(DirectInputKey.Numpad0),
        /* Escape */     new(DirectInputKey.Escape),
        /* F11 */        new(DirectInputKey.F11),
        /* P */          new(DirectInputKey.P),
        /* WizForm1 */   new(DirectInputKey.D1),
        /* WizForm2 */   new(DirectInputKey.D2),
        /* WizForm3 */   new(DirectInputKey.D3),
        /* WizForm4 */   new(DirectInputKey.D4),
        /* WizForm5 */   new(DirectInputKey.D5),
        /* Next */       new(DirectInputKey.Next),
        /* Prior */      new(DirectInputKey.Prior),
        /* MouseWheel */ new(MouseCode.WheelAxis),
        /* Return */     new(DirectInputKey.Return),
        /* F1 */         new(DirectInputKey.F1),
        /* F2 */         new(DirectInputKey.F2),
        /* F3 */         new(DirectInputKey.F3),
        /* F4 */         new(DirectInputKey.F4),
        /* T */          new(DirectInputKey.T),
        /* Tab */        new(DirectInputKey.Tab),
        /* F5 */         new(DirectInputKey.F5),
    ];
}

public static class GameConfigExtensions
{
    public static (int width, int height, int bits) GetInfo(this GameConfig.Resolution resolution) => resolution switch
    {
        GameConfig.Resolution.R640x480x16 => (640, 480, 16),
        GameConfig.Resolution.R640x480x32 => (640, 480, 32),
        GameConfig.Resolution.R800x600x16 => (800, 600, 16),
        GameConfig.Resolution.R800x600x32 => (800, 600, 32),
        GameConfig.Resolution.R1024x768x16 => (1024, 768, 16),
        GameConfig.Resolution.R1024x768x32 => (1024, 768, 32),
        _ => throw new ArgumentOutOfRangeException(nameof(resolution))
    };

    public static (int rate, int channels, int bits) GetInfo(this GameConfig.SoundQuality quality) => quality switch
    {
        GameConfig.SoundQuality.Low => (11025, 1, 8),
        GameConfig.SoundQuality.Medium => (22050, 2, 16),
        GameConfig.SoundQuality.High => (44100, 2, 16),
        _ => throw new ArgumentOutOfRangeException(nameof(quality))
    };


}
