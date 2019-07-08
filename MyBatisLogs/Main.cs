using System;
using System.Windows.Forms;
using Kbg.NppPluginNET.PluginInfrastructure;
using System.Text.RegularExpressions;

namespace Kbg.NppPluginNET
{
    class Main
    {
        internal const string PluginName = "MyBatisLogs";

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

            /*
            Position posStart = scintillaGateway.GetSelectionStart();
            Position posEnd = scintillaGateway.GetSelectionEnd();

            int lineA = scintillaGateway.LineFromPosition(posStart);
            int lineB = scintillaGateway.LineFromPosition(posEnd);

            string line = scintillaGateway.GetLine(lineA);
            */
            string selection = scintillaGateway.GetSelText();
            if (string.IsNullOrEmpty(selection))
            {
                MessageBox.Show("no selection");
            }

            try
            {
                string sql = FindInSelection(selection);
                Win32.SendMessage(PluginBase.nppData._nppHandle, (uint)NppMsg.NPPM_MENUCOMMAND, 0, NppMenuCmd.IDM_FILE_NEW);

                scintillaGateway.SetText(sql);
            } 
            catch(Exception e)
            {
                MessageBox.Show(e.Message, "error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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