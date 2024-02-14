using System;
using System.IO;
using System.Collections.Generic;
using zzio.effect.parts;
using System.Linq;
using zzio;
using System.Numerics;

namespace zzio.effect;


[System.Serializable]
public partial class EffectCombiner
{
    private static readonly IReadOnlyDictionary<string, Func<IEffectPart>> partTypeConstructors =
        new Dictionary<string, Func<IEffectPart>>()
        {
            { "BeamStar",           () => new BeamStar() },
            { "ElectricBolt",       () => new ElectricBolt() },
            { "Models",             () => new Models() },
            { "MovingPlanes",       () => new MovingPlanes() },
            { "Particle Beam",      () => new ParticleBeam() },
            { "ParticleCollector",  () => new ParticleCollector() },
            { "ParticleEmitter",    () => new ParticleEmitter() },
            { "PlaneBeam",          () => new PlaneBeam() },
            { "RandomPlanes",       () => new RandomPlanes() },
            { "Sound",              () => new Sound() },
            { "Sparks",             () => new Sparks() }
        };

    public string description = "";
    public bool isLooping;
    public Vector3 upwards, forwards, position;
    public IEffectPart[] parts = [];

    public float Duration => parts.Any() ? parts.Max(p => p.Duration) : 0f;

    public void Read(Stream stream)
    {
        List<IEffectPart> partsList = [];
        using BinaryReader r = new(stream);

        if (r.ReadZString() != "[Effect Combiner]")
            throw new InvalidDataException("File does not start with correct tag");

        bool shouldReadNext = true;
        while (shouldReadNext)
        {
            string sectionName = r.ReadZString();
            if (!sectionName.StartsWith('[') || !sectionName.EndsWith(']'))
                throw new InvalidDataException("Invalid section name format: \"" + sectionName + "\"");
            sectionName = sectionName[1..^1];

            switch(sectionName)
            {
                case "EOF":
                    shouldReadNext = false;
                    break;
                case "Effect_Combiner_Description":
                    description = r.ReadSizedCString(32);
                    break;
                case "Effect_Combiner_Parameter":
                    r.BaseStream.Seek(4, SeekOrigin.Current); // unused
                    isLooping = r.ReadBoolean();
                    break;
                case "Effect_Combiner_Position":
                    r.BaseStream.Seek(2 * 12, SeekOrigin.Current); // ignored data
                    break;
                case "Effect_Combiner_Form":
                    // in most cases also ignored
                    upwards = r.ReadVector3();
                    forwards = r.ReadVector3();
                    position = r.ReadVector3();
                    break;
                case var _ when partTypeConstructors.TryGetValue(sectionName, out var partTypeCtor):
                    var newPart = partTypeCtor();
                    newPart.Read(r);
                    partsList.Add(newPart);
                    break;
                default:
                    throw new InvalidDataException("Invalid section name: \"" + sectionName + "\"");
            }
        }

        parts = [.. partsList];
    }
}
