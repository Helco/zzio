using System;
using System.IO;
using System.Collections.Generic;

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

            if (Utils.readZString(reader) != "[ActorExDescriptionFile]")
                throw new InvalidDataException("Not an actorex description file");
            bool stopParsing = false;
            string sect;
            while(!stopParsing) {
                switch(sect = Utils.readZString(reader)) {
                    case ("[ModelFilename_Body]"): { actor.bodyModel = Utils.readZString(reader); }break;
                    case ("[AnimationPoolID_Body]"): { actor.bodyAnimationPoolID = reader.ReadInt32(); }break;
                    case ("[AnimationFilename_Body]"): {
                            bodyAnimations.Add(Utils.readZString(reader));
                            bodyAnimData.Add(reader.ReadInt32());
                        }break;
                    case ("[ModelFilename_Wings]"): { actor.wingsModel = Utils.readZString(reader); }break;
                    case ("[AnimationPoolID_Wings]"): { actor.wingsAnimationPoolID = reader.ReadInt32(); }break;
                    case ("[AnimationFilename_Wings]"): {
                            wingsAnimations.Add(Utils.readZString(reader));
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
            Utils.writeZString(writer, "[ActorExDescriptionFile]");

            Utils.writeZString(writer, "[ModelFilename_Body]");
            Utils.writeZString(writer, bodyModel);
            Utils.writeZString(writer, "[AnimationPoolID_Body]");
            writer.Write(bodyAnimationPoolID);
            for (int i=0; i<bodyAnimations.Length; i++)
            {
                Utils.writeZString(writer, "[AnimationFilename_Body]");
                Utils.writeZString(writer, bodyAnimations[i]);
                writer.Write(bodyAnimationData[i]);
            }

            Utils.writeZString(writer, "[ModelFilename_Wings]");
            Utils.writeZString(writer, wingsModel);
            Utils.writeZString(writer, "[AnimationPoolID_Wings]");
            writer.Write(wingsAnimationPoolID);
            for (int i = 0; i < wingsAnimations.Length; i++)
            {
                Utils.writeZString(writer, "[AnimationFilename_Wings]");
                Utils.writeZString(writer, wingsAnimations[i]);
                writer.Write(wingsAnimationData[i]);
            }

            Utils.writeZString(writer, "[AttachWingsToBone]");
            writer.Write(attachWingsToBone);
            Utils.writeZString(writer, "[HeadBoneID]");
            writer.Write(headBoneID);
            Utils.writeZString(writer, "[EffectBoneID]");
            writer.Write(effectBoneID);

            Utils.writeZString(writer, "[EOS]");
        }
    }
}