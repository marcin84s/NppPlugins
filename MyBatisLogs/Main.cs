﻿using System;
using System.Windows.Forms;
using Kbg.NppPluginNET.PluginInfrastructure;
using System.Text.RegularExpressions;
using System.Collections.Generic;

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
            PluginBase.SetCommand(1, "Remove whitespaces", removeWhitespaces, new ShortcutKey(true, true, false, Keys.K));
            PluginBase.SetCommand(2, "Base64 --> Hex", base64toHex, new ShortcutKey(true, true, true, Keys.B));
            PluginBase.SetCommand(2, "Hex --> Base64", hexToBase64, new ShortcutKey(true, true, true, Keys.H));
        }

        internal static void hexToBase64()
        {
            IScintillaGateway scintillaGateway = PluginBase.GetGatewayFactory()();
            string selection = scintillaGateway.GetSelText();

            if (string.IsNullOrEmpty(selection))
            {
                Tuple<string, Position, Position> hex = extractHexFromCurrentPosition(scintillaGateway);
                if (hex != null)
                {
                    string base64 = convertToBase64(hex.Item1);
                    if (base64 != null)
                    {
                        scintillaGateway.SetSel(hex.Item2, hex.Item3);
                        scintillaGateway.ReplaceSel(base64);
                    }
                }
            }
            else
            {
                if (selection.Length % 2 != 0)
                {
                    MessageBox.Show("selection length not divisible by 2");
                    return;
                }

                string base64 = convertToBase64(selection);
                if (base64 != null)
                {
                    scintillaGateway.ReplaceSel(base64);
                }
            }
        }

        internal static Tuple<string, Position, Position> extractHexFromCurrentPosition(IScintillaGateway scintillaGateway)
        {
            Position position = scintillaGateway.GetCurrentPos();
            int ch = scintillaGateway.GetCharAt(position);
            if (!isValidHex((char)ch))
            {
                MessageBox.Show("invalid hex character under cursor");
                return null;
            }

            // scan backward
            Position positionOfHex = findBeginningOfFragment(scintillaGateway, position, isValidHex);
            // get hex from known start position until last valid character
            return getFragmentStartingFrom(scintillaGateway, positionOfHex.Value, isValidHex);
        }

        internal static string convertToBase64(string input)
        {
            Dictionary<string, byte> hexindex = new Dictionary<string, byte>();
            for (int i = 0; i <= 255; i++)
            {
                hexindex.Add(i.ToString("X2"), (byte)i);
            }

            List<byte> bytes = new List<byte>();
            char[] chars = input.ToCharArray();
            for (int i = 0; i < chars.Length; i += 2)
            {
                char first = chars[i];
                char second = chars[i + 1];
                if (isValidHex(first) && isValidHex(second))
                {
                    string code = "" + first + second;
                    byte b = hexindex[code.ToUpper()];
                    bytes.Add(b);
                }
                else
                {
                    MessageBox.Show("invalid character");
                    return null;
                }
            }

            return Convert.ToBase64String(bytes.ToArray());
        }

        internal static bool isValidHex(char ch)
        {
            bool digit = ch >= '0' && ch <= '9';
            bool lower = ch >= 'a' && ch <= 'f';
            bool upper = ch >= 'A' && ch <= 'F';
            return digit || lower || upper;
        }

        internal static void base64toHex()
        {
            IScintillaGateway scintillaGateway = PluginBase.GetGatewayFactory()();
            string selection = scintillaGateway.GetSelText();

            try
            {
                if (string.IsNullOrEmpty(selection))
                {
                    Tuple<string, Position, Position> base64AndRange = extractBase64FromCurrentPosition(scintillaGateway);
                    if (base64AndRange != null) { 
                        string hex = convertBase64ToHex(base64AndRange.Item1);
                        scintillaGateway.SetSel(base64AndRange.Item2, base64AndRange.Item3);
                        scintillaGateway.ReplaceSel(hex);
                    }
                }
                else
                {
                    scintillaGateway.ReplaceSel(convertBase64ToHex(selection));
                }
            }
            catch (FormatException e)
            {
                MessageBox.Show("FormatException " + e.Message);
            }
        }

        internal static string convertBase64ToHex(string input)
        {
            byte[] bytes = Convert.FromBase64String(input);
            return BitConverter.ToString(bytes).Replace("-", "");
        }

        internal static Tuple<string, Position, Position> extractBase64FromCurrentPosition(IScintillaGateway scintillaGateway)
        {
            int max = scintillaGateway.GetLength();

            Position position = scintillaGateway.GetCurrentPos();
            int ch = scintillaGateway.GetCharAt(position);
            if (!isValidBase64Character((char)ch))
            {
                MessageBox.Show("invalid base64 character under cursor");
                return null;
            }

            // scan backward
            Position positionOfBase64 = findBeginningOfFragment(scintillaGateway, position, isValidBase64Character);
            // get base64 from known start position until last valid character
            return getFragmentStartingFrom(scintillaGateway, positionOfBase64.Value, isValidBase64Character);
        }

        internal static Tuple<string, Position, Position> getFragmentStartingFrom(IScintillaGateway scintillaGateway, int start, Predicate<char> predicate)
        {
            int length = scintillaGateway.GetLength();

            int lastValid = start;
            List<char> chars = new List<char>();
            for (int pos = start; pos < length; pos++)
            {
                int ch = scintillaGateway.GetCharAt(new Position(pos));
                if (predicate((char)ch)) {
                    chars.Add((char)ch);
                    lastValid = pos;
                }
                else
                {
                    break;
                }
            }

            return new Tuple<string, Position, Position>(new string(chars.ToArray()), new Position(start), new Position(lastValid + 1));
        }

        internal static Position findBeginningOfFragment(IScintillaGateway scintillaGateway, Position start, Predicate<char> predicate)
        {
            for (int pos = start.Value; ; pos--)
            {
                if (pos == 0)
                {
                    if (predicate((char)scintillaGateway.GetCharAt(new Position(0))))
                    {
                        return new Position(0);
                    }
                    else
                    {
                        return new Position(1);
                    }
                }

                if (!predicate((char)scintillaGateway.GetCharAt(new Position(pos)))) {
                    return new Position(pos + 1);
                }
            }
        }

        internal static bool isValidBase64Character(char ch)
        {
            bool digit = ch >= '0' && ch <= '9';
            bool letter = ch >= 'A' && ch <= 'Z' || ch >= 'a' && ch <= 'z';
            bool otherAllowed = ch == '+' || ch == '/' || ch == '=';
            return digit || letter || otherAllowed;
        }

        internal static void removeWhitespaces()
        {
            IScintillaGateway scintillaGateway = PluginBase.GetGatewayFactory()();
            string selection = scintillaGateway.GetSelText();

            string result = "";
            bool addedSpace = false;
            foreach (char ch in selection)
            {
                if (Char.IsWhiteSpace(ch))
                {
                    if (!addedSpace) {
                        addedSpace = true;
                        result += " ";
                    }
                }
                else
                {
                    addedSpace = false;
                    result += ch;
                }
            }

            scintillaGateway.ReplaceSel(result);
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