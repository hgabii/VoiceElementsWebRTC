using System;
using System.Collections;
using System.ComponentModel;
using System.Configuration.Install;

namespace VoiceApp
{
    [RunInstaller(true)]
    public partial class ServiceInstaller : Installer
    {
        public ServiceInstaller()
        {
            InitializeComponent();
        }

        private System.ServiceProcess.ServiceProcessInstaller serviceProcessInstaller1;
        private System.ServiceProcess.ServiceInstaller serviceInstaller1;


        public override void Install(IDictionary mySavedState)
        {
            string ServiceName = this.Context.Parameters["ServiceName"];

            if (ServiceName != null)
            {
                this.serviceInstaller1.ServiceName = ServiceName;
                this.serviceInstaller1.DisplayName = Program.DisplayName + " (" + ServiceName + ")";

            }

            base.Install(mySavedState);
        }


        public override void Uninstall(IDictionary mySavedState)
        {

            string ServiceName = this.Context.Parameters["ServiceName"];

            if (ServiceName != null)
            {
                this.serviceInstaller1.ServiceName = ServiceName;
                this.serviceInstaller1.DisplayName = Program.DisplayName + " (" + ServiceName + ")";
            }

            base.Uninstall(mySavedState);

        }

        private void serviceProcessInstaller1_AfterInstall(object sender, System.Configuration.Install.InstallEventArgs e)
        {

        }



    }
}