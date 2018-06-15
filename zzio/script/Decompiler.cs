using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace zzio.script
{
    public class Decompiler : Parser
    {
        ZZMappedDatabase database;
        List<string> errors;
        List<string> comments;
        string result;

        public Decompiler(ZZMappedDatabase database) {
            errors = new List<string>();
            comments = new List<string>();
            this.database = database;
        }

        public bool decompile(string source)
        {
            reset(source);
            errors.Clear();
            comments.Clear();
            result = "";

            bool isInIfBlock = false;

            while(true)
            {
                Command command = new Command();
                comments.Clear();
                if (parseNextLine())
                {
                    //check op code
                    command = Command.byShortOp(curOp);
                    if (!command.isValid)
                        addErrorMessage("Invalid op code \"" + curOp + "\"");
                    else
                    {
                        if (curArgs.Length > command.maxArgs)
                            addErrorMessage("Too many arguments");

                        if (command.isBranchCmd)
                        {
                            if (isInIfBlock && command.shortName != '8')
                                addErrorMessage("Cascaded branch block");
                            else
                                isInIfBlock = true;
                        }
                        if (curOp == '7')
                            isInIfBlock = false;

                        if (command.argTypes != null)
                        {
                            for (int i = 0; i < Math.Min(curArgs.Length, command.argTypes.Length); i++)
                            {
                                switch (command.argTypes[i])
                                {
                                    case (ArgType.TextUID):
                                        {
                                            if (!Utils.isUID(curArgs[i]))
                                                addErrorMessage("Invalid UID format for argument " + (i + 1));
                                            else
                                            {
                                                ZZDBMappedTextRow txt = database[Convert.ToUInt32(curArgs[i], 16)] as ZZDBMappedTextRow;
                                                if (txt == null)
                                                    addErrorMessage("Invalid UID for argument " + (i + 1));
                                                else
                                                    comments.Add("Arg " + (i + 1) + ": " + txt.Text);
                                            }
                                        }
                                        break;
                                    case (ArgType.DialogUID):
                                        {
                                            if (!Utils.isUID(curArgs[i]))
                                                addErrorMessage("Invalid UID format for argument " + (i + 1));
                                            else
                                            {
                                                ZZDBMappedDialogRow txt = database[Convert.ToUInt32(curArgs[i], 16)] as ZZDBMappedDialogRow;
                                                if (txt == null)
                                                    addErrorMessage("Invalid UID for argument " + (i + 1));
                                                else
                                                    comments.Add("Arg " + (i + 1) + ": " + txt.Text);
                                            }
                                        }
                                        break;
                                    case (ArgType.NpcUID):
                                        {
                                            if (!Utils.isUID(curArgs[i]))
                                                addErrorMessage("Invalid UID format for argument " + (i + 1));
                                            else
                                            {
                                                ZZDBMappedNpcRow txt = database[Convert.ToUInt32(curArgs[i], 16)] as ZZDBMappedNpcRow;
                                                if (txt == null)
                                                    addErrorMessage("Invalid UID for argument " + (i + 1));
                                                else
                                                    comments.Add("Arg " + (i + 1) + ": \"" + txt.Name + "\" - " + txt.Unknown);
                                            }
                                        }
                                        break;
                                    case (ArgType.FairyUID):
                                        {
                                            if (!Utils.isUID(curArgs[i]))
                                                addErrorMessage("Invalid UID format for argument " + (i + 1));
                                            else
                                            {
                                                ZZDBMappedFairyRow txt = database[Convert.ToUInt32(curArgs[i], 16)] as ZZDBMappedFairyRow;
                                                if (txt == null)
                                                    addErrorMessage("Invalid UID for argument " + (i + 1));
                                                else
                                                    comments.Add("Arg " + (i + 1) + ": " + txt.Name);
                                            }
                                        }
                                        break;
                                    case (ArgType.SpellUID):
                                        {
                                            if (!Utils.isUID(curArgs[i]))
                                                addErrorMessage("Invalid UID format for argument " + (i + 1));
                                            else
                                            {
                                                ZZDBMappedSpellRow txt = database[Convert.ToUInt32(curArgs[i], 16)] as ZZDBMappedSpellRow;
                                                if (txt == null)
                                                    addErrorMessage("Invalid UID for argument " + (i + 1));
                                                else
                                                    comments.Add("Arg " + (i + 1) + ": " + txt.Name);
                                            }
                                        }
                                        break;
                                    case (ArgType.ItemUID):
                                        {
                                            if (!Utils.isUID(curArgs[i]))
                                                addErrorMessage("Invalid UID format for argument " + (i + 1));
                                            else
                                            {
                                                ZZDBMappedItemRow txt = database[Convert.ToUInt32(curArgs[i], 16)] as ZZDBMappedItemRow;
                                                if (txt == null)
                                                    addErrorMessage("Invalid UID for argument " + (i + 1));
                                                else
                                                    comments.Add("Arg " + (i + 1) + ": " + txt.Name);
                                            }
                                        }
                                        break;
                                    case (ArgType.FairyId):
                                        {
                                            uint id;
                                            try { id = (Convert.ToUInt32(curArgs[i]) << 16) | 0x0200; }
                                            catch (Exception) { addErrorMessage("Invalid CardId format for argument " + (i + 1)); break; }
                                            ZZDBMappedFairyRow fairy = database.byCardId(id) as ZZDBMappedFairyRow;
                                            if (fairy == null)
                                                addErrorMessage("Invalid fairy CardId for argument " + (i + 1));
                                            else
                                                comments.Add("Arg " + (i + 1) + ": " + fairy.Name);
                                        }
                                        break;
                                    case (ArgType.SpellId):
                                        {
                                            uint id;
                                            try { id = (Convert.ToUInt32(curArgs[i]) << 16) | 0x0100; }
                                            catch (Exception) { addErrorMessage("Invalid CardId format for argument " + (i + 1)); break; }
                                            ZZDBMappedSpellRow spell = database.byCardId(id) as ZZDBMappedSpellRow;
                                            if (spell == null)
                                                addErrorMessage("Invalid spell CardId for argument " + (i + 1));
                                            else
                                                comments.Add("Arg " + (i + 1) + ": " + spell.Name);
                                        }
                                        break;
                                    case (ArgType.ItemId):
                                        {
                                            uint id;
                                            try { id = (Convert.ToUInt32(curArgs[i]) << 16); }
                                            catch (Exception) { addErrorMessage("Invalid CardId format for argument " + (i + 1)); break; }
                                            ZZDBMappedFairyRow item = database.byCardId(id) as ZZDBMappedFairyRow;
                                            if (item == null)
                                                addErrorMessage("Invalid fairy CardId for argument " + (i + 1));
                                            else
                                                comments.Add("Arg " + (i + 1) + ": " + item.Name);
                                        }
                                        break;
                                    case (ArgType.CameraPos):
                                        {
                                            uint id;
                                            try { id = (Convert.ToUInt32(curArgs[i]) << 16); }
                                            catch (Exception) { addErrorMessage("Invalid CardId format for argument " + (i + 1)); break; }
                                            string msg = DecompilerHelper.getCameraPosDescription(id);
                                            if (msg == null)
                                                addErrorMessage("Invalid camera position for argument " + (i + 1));
                                            else
                                                comments.Add("Arg " + (i + 1) + ": " + msg);
                                        }
                                        break;
                                    case (ArgType.ModifyWizform):
                                        {
                                            uint id;
                                            try { id = (Convert.ToUInt32(curArgs[i]) << 16); }
                                            catch (Exception) { addErrorMessage("Invalid CardId format for argument " + (i + 1)); break; }
                                            string msg = DecompilerHelper.getModifyWizformDescription(id);
                                            if (msg == null)
                                                addErrorMessage("Invalid modify wizform type for argument " + (i + 1));
                                            else
                                                comments.Add("Arg " + (i + 1) + ": " + msg);
                                        }
                                        break;
                                }
                            }
                        } //if (command.foreignKeys != null)
                    }
                }
                else if (!hasError)
                    break; //end of source
                else
                    addErrorMessage("Invalid code line format");

                string prefix = isInIfBlock && !command.isBranchCmd ? "  " : "";
                if (curComments != null)
                    comments.AddRange(curComments);
                if (comments.Count > 1)
                {
                    result += "\n";
                    foreach (string comment in comments)
                        result += prefix + "// " + comment + "\n";
                }
                else if (command.shortName == '$' && result != "")
                    result += "\n";
                if (command.isValid)
                {
                    result += prefix + command.longName;
                    foreach (string arg in curArgs)
                        result += "." + arg;
                }
                else
                    result += prefix + curLine;
                if (comments.Count == 1)
                    result += " // " + comments[0];
                if (command.shortName == '#')
                    result += "\n";
                result += "\n";
            }

            return errors.Count == 0;
        }

        public string[] getErrorMessages() { return errors.ToArray(); }
        public string getResult() { return result; }

        private void addErrorMessage(string msg)
        {
            errors.Add("Line " + this.curLineNo + ": " + msg);
            comments.Add("ERROR: " + msg);
        }
    }
}
