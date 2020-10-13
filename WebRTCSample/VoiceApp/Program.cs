using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Diagnostics;
using System.ServiceProcess;

namespace VoiceApp
{
    static class Program
    {

        // This is the name of the service that will be installed on your system.
        private static string s_ServiceName = "Ivr";

        public static string ServiceName
        {
            get { return Program.s_ServiceName; }
        }

        // This is the descriptive text associated with your service.
        private static string s_DisplayName = "Ivr Service";

        public static string DisplayName
        {
            get { return Program.s_DisplayName; }
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [MTAThread]
        static void Main()
        {
            string sProcessName = Process.GetCurrentProcess().ProcessName;

            if (Environment.UserInteractive)
            {
                if (sProcessName.ToLower() != "services.exe")
                {
                    // Im an interactive session.
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(new IvrInteractive());
                    return;
                }

            }

            ServiceBase[] ServicesToRun;

            ServicesToRun = new ServiceBase[] { new IvrService() };

            ServiceBase.Run(ServicesToRun);
        }
    }
}