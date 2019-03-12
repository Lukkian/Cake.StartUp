using System;
using System.Windows.Forms;

namespace WindowsFormsApp
{
    public static class WinFormsExtensions
    {
        public static void AppendLine(this TextBox source, string value)
        {
            var time = string.IsNullOrWhiteSpace(value) ? string.Empty : $"[{DateTime.Now:T}]: ";
            if (source.Text.Length == 0)
                source.Text = $"{time}{value}";
            else
                source.AppendText($"{Environment.NewLine}{time}{value}");
        }
    }
}