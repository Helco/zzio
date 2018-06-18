using System;
using System.IO;
using System.Collections.Generic;
using zzio.effect.parts;
using zzio.primitives;

namespace zzio.effect
{

    [System.Serializable]
    public partial class EffectCombiner
    {
        private static Dictionary<string, Func<IEffectPart>> partTypeConstructors =
            new Dictionary<string, Func<IEffectPart>>()
            {
                { "BeamStar",           () => new BeamStar() },
                { "ElectricBolt",       () => new ElectricBolt() },
                { "Models",             () => new Models() },
                { "MovingPlanes",       () => new MovingPlanes() },
                { "Particle Beam",       () => new ParticleBeam() },
                { "ParticleCollector",  () => new ParticleCollector() },
                { "ParticleEmitter",    () => new ParticleEmitter() },
                { "PlaneBeam",          () => new PlaneBeam() },
                { "RandomPlanes",       () => new RandomPlanes() },
                { "Sound",              () => new Sound() },
                { "Sparks",             () => new Sparks() }
            };

        // all handlers except the effect parts
        private static Dictionary<string, Action<EffectCombiner, BinaryReader>> sectionHandlers =
            new Dictionary<string, Action<EffectCombiner, BinaryReader>>()
            {
                { "Effect_Combiner_Description", (eff, r) => {
                    eff.description = Utils.readCAString(r, 32);
                } },
                { "Effect_Combiner_Parameter", (eff, r) => {
                    r.BaseStream.Seek(4, SeekOrigin.Current);
                    eff.isLooping = r.ReadBoolean();
                } },
                { "Effect_Combiner_Position", (eff, r) => {
                    r.BaseStream.Seek(2 * 12, SeekOrigin.Current);
                } },
                { "Effect_Combiner_Form", (eff, r) => {
                    eff.upwards = Vector.read(r);
                    eff.forwards = Vector.read(r);
                    eff.position = Vector.read(r);
                } }
            };

        public string description = "";
        public bool isLooping = false;
        public Vector upwards = new Vector(),
            forwards = new Vector(),
            position = new Vector();
        public IEffectPart[] parts = null;

        public EffectCombiner() { }

        public void Read(Stream stream)
        {
            List<IEffectPart> partsList = new List<IEffectPart>();
            BinaryReader r = new BinaryReader(stream);

            if (Utils.readZString(r) != "[Effect Combiner]")
                throw new InvalidDataException("File does not start with correct tag");

            bool shouldReadNext = true;
            while (shouldReadNext)
            {
                string sectionName = Utils.readZString(r);
                if (!sectionName.StartsWith("[") || !sectionName.EndsWith("]"))
                    throw new InvalidDataException("Invalid section name format: \"" + sectionName + "\"");
                sectionName = sectionName.Substring(1, sectionName.Length - 2);

                if (sectionName == "EOF")
                {
                    shouldReadNext = false;
                }
                else if (sectionHandlers.ContainsKey(sectionName))
                {
                    sectionHandlers[sectionName](this, r);
                }
                else if (partTypeConstructors.ContainsKey(sectionName))
                {
                    IEffectPart newPart = partTypeConstructors[sectionName]();
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
