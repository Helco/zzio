using System;
using System.IO;
using System.Collections.Generic;
using zzio.effect.parts;
using System.Linq;
using zzio;
using System.Numerics;

namespace zzio.effect
{

    [System.Serializable]
    public partial class EffectCombiner
    {
        private static readonly Dictionary<string, Func<IEffectPart>> partTypeConstructors =
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

        // all handlers except the effect parts
        private static readonly Dictionary<string, Action<EffectCombiner, BinaryReader>> sectionHandlers =
            new Dictionary<string, Action<EffectCombiner, BinaryReader>>()
            {
                { "Effect_Combiner_Description", (eff, r) => {
                    eff.description = r.ReadSizedCString(32);
                } },
                { "Effect_Combiner_Parameter", (eff, r) => {
                    r.BaseStream.Seek(4, SeekOrigin.Current);
                    eff.isLooping = r.ReadBoolean();
                } },
                { "Effect_Combiner_Position", (eff, r) => {
                    r.BaseStream.Seek(2 * 12, SeekOrigin.Current);
                } },
                { "Effect_Combiner_Form", (eff, r) => {
                    eff.upwards = r.ReadVector3();
                    eff.forwards = r.ReadVector3();
                    eff.position = r.ReadVector3();
                } }
            };

        public string description = "";
        public bool isLooping = false;
        public Vector3 upwards, forwards, position;
        public IEffectPart[] parts = Array.Empty<IEffectPart>();

        public float Duration => parts.Any() ? parts.Max(p => p.Duration) : 0f;

        public void Read(Stream stream)
        {
            List<IEffectPart> partsList = new List<IEffectPart>();
            using BinaryReader r = new BinaryReader(stream);

            if (r.ReadZString() != "[Effect Combiner]")
                throw new InvalidDataException("File does not start with correct tag");

            bool shouldReadNext = true;
            while (shouldReadNext)
            {
                string sectionName = r.ReadZString();
                if (!sectionName.StartsWith("[") || !sectionName.EndsWith("]"))
                    throw new InvalidDataException("Invalid section name format: \"" + sectionName + "\"");
                sectionName = sectionName.Substring(1, sectionName.Length - 2);

                if (sectionName == "EOF")
                {
                    shouldReadNext = false;
                }
                else if (sectionHandlers.TryGetValue(sectionName, out var sectionHandler))
                {
                    sectionHandler(this, r);
                }
                else if (partTypeConstructors.TryGetValue(sectionName, out var partTypeCtor))
                {
                    var newPart = partTypeCtor();
                    newPart.Read(r);
                    partsList.Add(newPart);
                }
                else
                {
                    throw new InvalidDataException("Invalid section name: \"" + sectionName + "\"");
                }
            }

            parts = partsList.ToArray();
        }
    }
}
