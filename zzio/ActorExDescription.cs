using System;
using System.IO;
using System.Collections.Generic;
using zzio.utils;

namespace zzio
{

    [Serializable]
    public class ActorPartDescription
    {
        public string model = "";
        public int animationPoolID = -1;
        public (AnimationType type, string filename)[] animations = new (AnimationType, string)[0];

        public void Write(BinaryWriter writer, string partName)
        {
            writer.WriteZString(String.Format("[ModelFilename_{0}]", partName));
            writer.WriteZString(model);
            writer.WriteZString(String.Format("[AnimationPoolID_{0}]", partName));
            writer.Write(animationPoolID);

            foreach (var (type, filename) in animations)
            {
                writer.WriteZString(String.Format("[AnimationFilename_{0}]", partName));
                writer.WriteZString(filename);
                writer.Write((int)type);
            }
        }
    }

    [Serializable]
    public class ActorExDescription
    {
        // In Zanzarah everyone and everything has a body and wings.
        // If a creature happens to not have wings, they have to be explicitly disabled :)
        public ActorPartDescription body, wings;

        public int attachWingsToBone, headBoneID, effectBoneID;

        public bool HasWings => wings.model.Length > 0;

        private ActorExDescription()
        {
            body = new ActorPartDescription();
            wings = new ActorPartDescription();
            attachWingsToBone = headBoneID = effectBoneID = -1;
        }

        public static ActorExDescription ReadNew(Stream stream)
        {
            ActorExDescription actor = new ActorExDescription();
            using BinaryReader reader = new BinaryReader(stream);
            var bodyAnimations = new List<(AnimationType, string)>();
            var wingsAnimations = new List<(AnimationType, string)>();

            if (reader.ReadZString() != "[ActorExDescriptionFile]")
                throw new InvalidDataException("Not an actorex description file");
            bool stopParsing = false;
            string sectionName;
            while (!stopParsing)
            {
                switch (sectionName = reader.ReadZString())
                {
                    case ("[ModelFilename_Body]"):
                        actor.body.model = reader.ReadZString();
                        break;
                    case ("[AnimationPoolID_Body]"):
                        actor.body.animationPoolID = reader.ReadInt32();
                        break;
                    case ("[AnimationFilename_Body]"):
                        var name = reader.ReadZString();
                        bodyAnimations.Add((
                            EnumUtils.intToEnum<AnimationType>(reader.ReadInt32()),
                            name));
                        break;

                    case ("[ModelFilename_Wings]"):
                        actor.wings.model = reader.ReadZString();
                        break;
                    case ("[AnimationPoolID_Wings]"):
                        actor.wings.animationPoolID = reader.ReadInt32();
                        break;
                    case ("[AnimationFilename_Wings]"):
                        name = reader.ReadZString();
                        wingsAnimations.Add((
                            EnumUtils.intToEnum<AnimationType>(reader.ReadInt32()),
                            name));
                        break;

                    case ("[AttachWingsToBone]"):
                        actor.attachWingsToBone = reader.ReadInt32();
                        break;
                    case ("[HeadBoneID]"):
                        actor.headBoneID = reader.ReadInt32();
                        break;
                    case ("[EffectBoneID]"):
                        actor.effectBoneID = reader.ReadInt32();
                        break;

                    case ("[EOS]"):
                        stopParsing = true;
                        break;
                    default:
                        throw new InvalidDataException("Invalid actorex description section \"" + sectionName + "\"");
                }
            }

            actor.body.animations = bodyAnimations.ToArray();
            actor.wings.animations = wingsAnimations.ToArray();
            return actor;
        }

        public void Write(Stream stream)
        {
            using BinaryWriter writer = new BinaryWriter(stream);
            writer.WriteZString("[ActorExDescriptionFile]");

            body.Write(writer, "Body");
            if (wings.model.Length > 0)
                wings.Write(writer, "Wings");

            writer.WriteZString("[AttachWingsToBone]");
            writer.Write(attachWingsToBone);
            writer.WriteZString("[HeadBoneID]");
            writer.Write(headBoneID);
            writer.WriteZString("[EffectBoneID]");
            writer.Write(effectBoneID);

            writer.WriteZString("[EOS]");
        }
    }
}