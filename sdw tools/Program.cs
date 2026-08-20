using System;
using System.Windows.Forms;
using SdwEditor.Ui;

namespace SdwEditor
{
    internal static class Program
    {
        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Loc.Load();
            Application.Run(new MainForm());
        }
    }
}
