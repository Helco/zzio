using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace zzio.script
{
    public abstract class Parser
    {
        private static Regex regexComment = new Regex("\\/\\/(.+)");
        private static Regex regexOpLine = new Regex("^.(\\.-?[0-9a-zA-Z_]+){0,3}$");

        protected StringReader source;
        protected string curLine;
        protected int curLineNo;
        protected char curOp;
        protected string[] curArgs;
        protected string[] curComments;
        protected bool hasError;

        public Parser()
        {
            source = null;
            hasError = false;
        }

        protected void reset(string source)
        {
            this.source = new StringReader(source);
            curLine = "";
            curLineNo = 0;
            curOp = (char)0;
            curArgs = null;
            curComments = null;
            hasError = false;
        }

        protected bool parseNextLine()
        {
            hasError = false;
            curOp = (char)0;
            curArgs = null;
            curComments = null;

            List<string> curCommentList = new List<string>();

            //get next line with content
            bool hasContent = true;
            do
            {
                do
                {
                    curLineNo++;
                    curLine = source.ReadLine();
                } while (curLine != null && (curLine = curLine.Trim()).Length == 0);
                if (curLine == null)
                    return false;

                //remove comment
                Match matchComment = regexComment.Match(curLine);
                if (matchComment.Success)
                {
                    curCommentList.Add(matchComment.Groups[1].Value.Trim());
                    curLine = curLine.Substring(0, matchComment.Index).Trim();
                    hasContent = curLine.Length > 0;
                }
            } while (!hasContent);

            //parse curLine
            if (!regexOpLine.IsMatch(curLine))
            {
                hasError = true;
                return false;
            }
            curOp = curLine[0];
            curArgs = curLine.Length > 1 ? curLine.Substring(2).Split('.') : new string[0];
            curComments = curCommentList.ToArray();

            foreach (string arg in curArgs)
            {
                if (arg[0] == '-' && arg.IndexOfAny("abcdefABCDEF".ToCharArray()) > 0)
                {
                    hasError = true;
                    return false;
                }
            }
            return true;
        }
    }
}
