using System;
using System.Windows.Forms;
using LteTestStation.Forms;

namespace LteTestStation;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }    
}