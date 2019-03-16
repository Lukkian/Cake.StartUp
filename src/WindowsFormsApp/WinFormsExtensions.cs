using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace WindowsFormsApp
{
    public static class WinFormsExtensions
    {
        public static void AppendLine(this TextBox source, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            var time = $"[{DateTime.Now.ToString("T", CultureInfo.InvariantCulture)}]: ";
            if (source.Text.Length == 0)
                source.Text = $"{time}{value}";
            else
                source.AppendText($"{Environment.NewLine}{time}{value}");
        }

        public static string TakeLastLine(this string text)
        {
            return TakeLastLines(text, 1).FirstOrDefault();
        }

        public static IEnumerable<string> TakeLastLines(this string text, int count)
        {
            var lines = new List<string>();
            var match = Regex.Match(text, "^.*$", RegexOptions.Multiline | RegexOptions.RightToLeft);

            while (match.Success && lines.Count < count)
            {
                lines.Insert(0, match.Value);
                match = match.NextMatch();
            }

            return lines;
        }
    }
}