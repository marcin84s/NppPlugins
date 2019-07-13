using System;
using System.Windows.Forms;
using Kbg.NppPluginNET.PluginInfrastructure;
using System.Text.RegularExpressions;

namespace Kbg.NppPluginNET
{
    class Main
    {
        internal const string PluginName = "MyBatisLogs";
        private const string PREPARING_MARKER = "==>  Preparing: ";
        private const string PARAMETERS_MARKER = "==> Parameters: ";

        public static void OnNotification(ScNotification notification)
        {  
            // This method is invoked whenever something is happening in notepad++
            // use eg. as
            // if (notification.Header.Code == (uint)NppMsg.NPPN_xxx)
            // { ... }
            // or
            //
            // if (notification.Header.Code == (uint)SciMsg.SCNxxx)
            // { ... }
        }

        internal static void CommandMenuInit()
        {
            PluginBase.SetCommand(0, "Merge Parameters in New Tab", mergeParamsInNewTab, new ShortcutKey(true, true, false, Keys.P));
        }

        internal static void mergeParamsInNewTab()
        {
            IScintillaGateway scintillaGateway = PluginBase.GetGatewayFactory()();

            string selection = scintillaGateway.GetSelText();
            if (string.IsNullOrEmpty(selection))
            {
                MessageBox.Show("no selection");
                return;
            }

            try
            {
                string sql;
                int lineNumber = 0;
                if (IsOneLineSelection(scintillaGateway, ref lineNumber))
                {
                    sql = FindSql(scintillaGateway, selection, lineNumber);
                }
                else
                {
                     sql = FindInSelection(selection);
                }
                Win32.SendMessage(PluginBase.nppData._nppHandle, (uint)NppMsg.NPPM_MENUCOMMAND, 0, NppMenuCmd.IDM_FILE_NEW);

                scintillaGateway.SetText(sql);
            } 
            catch(Exception e)
            {
                MessageBox.Show(e.Message, "error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static string FindSql(IScintillaGateway scintillaGateway, string selection, int lineNumber)
        {
            string line = scintillaGateway.GetLine(lineNumber);
            if (line.Contains(PREPARING_MARKER))
            {
                string parametersLine = FindLineWithMarker(scintillaGateway, selection, PARAMETERS_MARKER, lineNumber, 1);
                return FillParameters(SubstringAfterText(line, PREPARING_MARKER), parametersLine);
            }
            else if (line.Contains(PARAMETERS_MARKER))
            {
                string preparingLine = FindLineWithMarker(scintillaGateway, selection, PREPARING_MARKER, lineNumber, -1);
                return FillParameters(preparingLine, SubstringAfterText(line, PARAMETERS_MARKER));
            }
            else
            {
                throw new Exception(string.Format("current line contains neither '{0}' nor '{1}'", PREPARING_MARKER, PARAMETERS_MARKER));
            }
        }

        private static string FindLineWithMarker(IScintillaGateway scintillaGateway, string selection, string marker, int lineNumber, int direction)
        {
            string result = null;

            for (int i = 1; i < 20; i++)
            {
                int checkLine = lineNumber + direction * i;
                if (checkLine < 0)
                {
                    break;
                }
                string line = scintillaGateway.GetLine(checkLine);
                if (!string.IsNullOrEmpty(line) && line.Contains(selection) && line.Contains(marker))
                {
                    result = line;
                    break;
                }
            }

            if (string.IsNullOrEmpty(result))
            {
                throw new Exception(string.Format("cannot find marker '{0}' in the vicinity of current line", marker));
            }
            else 
            {
                return SubstringAfterText(result, marker);
            }
        }

        private static string SubstringAfterText(string text, string find)
        {
            int indexMarker = text.IndexOf(find);
            if (indexMarker >= 0)
            {
                return text.Substring(indexMarker + find.Length);
            }

            return string.Empty;
        }

        private static bool IsOneLineSelection(IScintillaGateway scintillaGateway, ref int lineNumber)
        {
            Position posStart = scintillaGateway.GetSelectionStart();
            Position posEnd = scintillaGateway.GetSelectionEnd();

            int lineA = scintillaGateway.LineFromPosition(posStart);
            int lineB = scintillaGateway.LineFromPosition(posEnd);

            lineNumber = lineA;
            return lineA == lineB;
        }

        private static string FindInSelection(string selection)
        {
            string preparingLabel = "==>  Preparing: ";
            string parametersLabel = "==> Parameters: ";

            int preparingIndex = selection.IndexOf(preparingLabel);
            int parametersIndex = selection.IndexOf(parametersLabel);

            if (preparingIndex == -1 || parametersIndex == -1 || preparingIndex > parametersIndex)
            {
                throw new Exception(String.Format("Cannot find either {0} or {1} in selection", preparingLabel, parametersIndex));
            }

            int endSql = selection.IndexOf("\r", preparingIndex);
            if (endSql == -1 && endSql < parametersIndex)
            {
                throw new Exception(string.Format("Wrong position of end of line character after {}", preparingLabel));
            }

            int startSql = preparingIndex + preparingLabel.Length;
            string sql = selection.Substring(startSql, endSql - startSql);

            string parametersText = selection.Substring(parametersIndex + parametersLabel.Length);

            return FillParameters(sql, parametersText);
        }

        private static string FillParameters(string sql, string parametersText)
        {
            Regex regex = new Regex(@"^(.*?\(\w+\))");

            while (sql.Contains("?"))
            {
                if (parametersText.StartsWith("null"))
                {
                    sql = ReplaceFirstOccurrence(sql, "?", "null");
                    if (parametersText.Length > 6)
                    {
                        parametersText = parametersText.Substring(6);
                    }
                }
                else
                {
                    Match match = regex.Match(parametersText);
                    if (!match.Success)
                    {
                        throw new Exception(string.Format("regex match failed {0}", parametersText));
                    }

                    Tuple<string, string> param = getParam(match);
                    if (param.Item2 == "Integer" || param.Item2 == "Long")
                    {
                        sql = ReplaceFirstOccurrence(sql, "?", param.Item1);
                    }
                    else
                    {
                        sql = ReplaceFirstOccurrence(sql, "?", string.Format("'{0}'", param.Item1));
                    }

                    if (parametersText.Length > match.Length + 2)
                    {
                        parametersText = parametersText.Substring(match.Length + 2);
                    }
                }
            }

            return sql;
        }

        private static string ReplaceFirstOccurrence(string Source, string Find, string Replace)
        {
            int Place = Source.IndexOf(Find);
            string result = Source.Remove(Place, Find.Length).Insert(Place, Replace);
            return result;
        }

        private static Tuple<string, string> getParam(Match match)
        {
            string[] param = match.Value.Split('(');
            if (param.Length != 2)
            {
                throw new Exception(string.Format("wrong parameter {0}", match.Value));
            }
            string value = param[0];
            int endTypeIndex = param[1].IndexOf(')');
            string type = param[1].Substring(0, endTypeIndex);

            return new Tuple<string, string>(value, type);
        }
    }
}