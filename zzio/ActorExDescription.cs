using System;
using System.IO;
using System.Collections.Generic;
using zzio.utils;

namespace zzio
{
    [Serializable]
    public class ActorPart
    {
        public string model = "";
        public int animationPoolID = -1;
        public string[] animations = new string[0];
        public int[] animationData = new int[0];

        public void Write(BinaryWriter writer, string partName)
        {
            writer.WriteZString(String.Format("[ModelFilename_{0}]", partName));
            writer.WriteZString(model);
            writer.WriteZString(String.Format("[AnimationPoolID_{0}]", partName));
            writer.Write(animationPoolID);

            for (int i = 0; i < animations.Length; i++)
            {
                writer.WriteZString(String.Format("[AnimationFilename_{0}]", partName));
                writer.WriteZString(animations[i]);
                writer.Write(animationData[i]);
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
            List<string> bodyAnimations = new List<string>(),
                wingsAnimations = new List<string>();
            List<int> bodyAnimData = new List<int>(),
                wingsAnimData = new List<int>();

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
                        bodyAnimations.Add(reader.ReadZString());
                        bodyAnimData.Add(reader.ReadInt32());
                        break;

                    case ("[ModelFilename_Wings]"):
                        actor.wings.model = reader.ReadZString();
                        break;
                    case ("[AnimationPoolID_Wings]"):
                        actor.wings.animationPoolID = reader.ReadInt32();
                        break;
                    case ("[AnimationFilename_Wings]"):
                        wingsAnimations.Add(reader.ReadZString());
                        wingsAnimData.Add(reader.ReadInt32());
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
            actor.body.animationData = bodyAnimData.ToArray();
            actor.wings.animations = wingsAnimations.ToArray();
            actor.wings.animationData = wingsAnimData.ToArray();
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