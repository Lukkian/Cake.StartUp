using System;
using System.Globalization;
using System.Windows.Forms;

namespace WindowsFormsApp
{
    public static class WinFormsExtensions
    {
        public static void AppendLine(this TextBox source, string value)
        {
            var time = string.IsNullOrWhiteSpace(value) ? string.Empty : $"[{DateTime.Now.ToString("T", CultureInfo.InvariantCulture)}]: ";
            if (source.Text.Length == 0)
                source.Text = $"{time}{value}";
            else
                source.AppendText($"{Environment.NewLine}{time}{value}");
        }
    }
}