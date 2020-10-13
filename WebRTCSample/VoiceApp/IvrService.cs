using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.ServiceProcess;
using System.Text;
using System.IO;

namespace VoiceApp
{
    partial class IvrService : ServiceBase
    {
        public IvrService()
        {
            InitializeComponent();

            Process myProcess;
            myProcess = System.Diagnostics.Process.GetCurrentProcess();

            string pathname = Path.GetDirectoryName(myProcess.MainModule.FileName);

            //eventLog1.WriteEntry(pathname);

            Directory.SetCurrentDirectory(pathname);

        }

        protected override void OnStart(string[] args)
        {
            // TODO: Add code here to start your service.
            IvrApplication.Start();
        }

        protected override void OnStop()
        {
            // TODO: Add code here to perform any tear-down necessary to stop your service.
            IvrApplication.StopImmediate();
        }
    }
}
