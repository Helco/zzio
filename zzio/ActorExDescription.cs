using System;
using System.IO;
using System.Collections.Generic;
using zzio.utils;

namespace zzio {
    [System.Serializable]
    public class ActorExDescription {
        //In Zanzarah everyone and everything has a body and the wings have to be explicitly disabled :)
        public string bodyModel;
        public int bodyAnimationPoolID;
        public string[] bodyAnimations;
        public int[] bodyAnimationData;

        public string wingsModel;
        public int wingsAnimationPoolID;
        public string[] wingsAnimations;
        public int[] wingsAnimationData;

        public int attachWingsToBone, headBoneID, effectBoneID;

        private ActorExDescription() {
            bodyAnimationPoolID = wingsAnimationPoolID = attachWingsToBone = headBoneID = effectBoneID = -1;
        }

        public static ActorExDescription read(byte[] buffer) {
            ActorExDescription actor = new ActorExDescription();
            BinaryReader reader = new BinaryReader(new MemoryStream(buffer));
            List<string> bodyAnimations = new List<string>(), wingsAnimations = new List<string>();
            List<int> bodyAnimData = new List<int>(), wingsAnimData = new List<int>();

            if (reader.ReadZString() != "[ActorExDescriptionFile]")
                throw new InvalidDataException("Not an actorex description file");
            bool stopParsing = false;
            string sect;
            while(!stopParsing) {
                switch(sect = reader.ReadZString()) {
                    case ("[ModelFilename_Body]"): { actor.bodyModel = reader.ReadZString(); }break;
                    case ("[AnimationPoolID_Body]"): { actor.bodyAnimationPoolID = reader.ReadInt32(); }break;
                    case ("[AnimationFilename_Body]"): {
                            bodyAnimations.Add(reader.ReadZString());
                            bodyAnimData.Add(reader.ReadInt32());
                        }break;
                    case ("[ModelFilename_Wings]"): { actor.wingsModel = reader.ReadZString(); }break;
                    case ("[AnimationPoolID_Wings]"): { actor.wingsAnimationPoolID = reader.ReadInt32(); }break;
                    case ("[AnimationFilename_Wings]"): {
                            wingsAnimations.Add(reader.ReadZString());
                            wingsAnimData.Add(reader.ReadInt32());
                        }break;
                    case ("[AttachWingsToBone]"): { actor.attachWingsToBone = reader.ReadInt32(); }break;
                    case ("[HeadBoneID]"): { actor.headBoneID = reader.ReadInt32(); }break;
                    case ("[EffectBoneID]"): { actor.effectBoneID = reader.ReadInt32(); }break;
                    case ("[EOS]"): { stopParsing = true; }break;
                    default: { throw new InvalidDataException("Invalid actorex description section \"" + sect + "\""); }
                }
            }

            actor.bodyAnimations = bodyAnimations.ToArray();
            actor.bodyAnimationData = bodyAnimData.ToArray();
            actor.wingsAnimations = wingsAnimations.ToArray();
            actor.wingsAnimationData = wingsAnimData.ToArray();
            return actor;
        }

        public byte[] write()
        {
            MemoryStream stream = new MemoryStream();
            write(stream);
            return stream.ToArray();
        }

        public void write(Stream stream)
        {
            BinaryWriter writer = new BinaryWriter(stream);
            writer.WriteZString("[ActorExDescriptionFile]");

            writer.WriteZString("[ModelFilename_Body]");
            writer.WriteZString(bodyModel);
            writer.WriteZString("[AnimationPoolID_Body]");
            writer.Write(bodyAnimationPoolID);
            for (int i=0; i<bodyAnimations.Length; i++)
            {
                writer.WriteZString("[AnimationFilename_Body]");
                writer.WriteZString(bodyAnimations[i]);
                writer.Write(bodyAnimationData[i]);
            }

            writer.WriteZString("[ModelFilename_Wings]");
            writer.WriteZString(wingsModel);
            writer.WriteZString("[AnimationPoolID_Wings]");
            writer.Write(wingsAnimationPoolID);
            for (int i = 0; i < wingsAnimations.Length; i++)
            {
                writer.WriteZString("[AnimationFilename_Wings]");
                writer.WriteZString(wingsAnimations[i]);
                writer.Write(wingsAnimationData[i]);
            }

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