using System;
using System.IO;
using System.Collections.Generic;
using zzio.utils;

namespace zzio
{
    [Serializable]
    public struct AnimationRef
    {
        public string filename;
        public AnimationType type;
    }

    [Serializable]
    public class ActorPart
    {
        public string model = "";
        public int animationPoolID = -1;
        public AnimationRef[] animations = new AnimationRef[0];

        public void Write(BinaryWriter writer, string partName)
        {
            writer.WriteZString(String.Format("[ModelFilename_{0}]", partName));
            writer.WriteZString(model);
            writer.WriteZString(String.Format("[AnimationPoolID_{0}]", partName));
            writer.Write(animationPoolID);

            foreach (AnimationRef animRef in animations)
            {
                writer.WriteZString(String.Format("[AnimationFilename_{0}]", partName));
                writer.WriteZString(animRef.filename);
                writer.Write((int)animRef.type);
            }
        }
    }

    [Serializable]
    public class ActorExDescription
    {
        // In Zanzarah everyone and everything has a body and wings.
        // If a creature happens to not have wings, they have to be explicitly disabled :)
        public ActorPart body, wings;

        public int attachWingsToBone, headBoneID, effectBoneID;

        private ActorExDescription()
        {
            body = new ActorPart();
            wings = new ActorPart();
            attachWingsToBone = headBoneID = effectBoneID = -1;
        }

        public static ActorExDescription ReadNew(Stream stream)
        {
            ActorExDescription actor = new ActorExDescription();
            BinaryReader reader = new BinaryReader(stream);
            List<AnimationRef>
                bodyAnimations = new List<AnimationRef>(),
                wingsAnimations = new List<AnimationRef>();

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
                        bodyAnimations.Add(new AnimationRef
                        {
                            filename = reader.ReadZString(),
                            type = EnumUtils.intToEnum<AnimationType>(reader.ReadInt32())
                        });
                        break;

                    case ("[ModelFilename_Wings]"):
                        actor.wings.model = reader.ReadZString();
                        break;
                    case ("[AnimationPoolID_Wings]"):
                        actor.wings.animationPoolID = reader.ReadInt32();
                        break;
                    case ("[AnimationFilename_Wings]"):
                        wingsAnimations.Add(new AnimationRef
                        {
                            filename = reader.ReadZString(),
                            type = EnumUtils.intToEnum<AnimationType>(reader.ReadInt32())
                        });
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
            BinaryWriter writer = new BinaryWriter(stream);
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