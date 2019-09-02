using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Monoscope
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);
         
            Application.Run(new Form1());
        }

        static void OnProcessExit(object sender, EventArgs e)
        {
            Console.WriteLine("I'm out of here");
        }

    }
}
