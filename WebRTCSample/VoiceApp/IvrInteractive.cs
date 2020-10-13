using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using VoiceElements.Common;

namespace VoiceApp
{
    public partial class IvrInteractive : Form
    {
        public IvrInteractive()
        {
            InitializeComponent();

            // Subscribe to 
            IvrApplication.Log.MessageLogged += new MessageLogged(Log_MessageLogged);

        }

        // This logs messages from the TelephonyBank to your application log.
        void Log_MessageLogged(string message)
        {
            if (this.InvokeRequired)
            {
                MessageLogged ml = new MessageLogged(Log_MessageLogged);
                this.Invoke(ml, message);
                return;
            }

            if (txtLog.Text.Length > 15000)
            {
                txtLog.Text = txtLog.Text.Substring(txtLog.Text.Length - 15000) + message;
            }
            else
            {
                txtLog.Text = txtLog.Text + message;
            }

            txtLog.SelectionStart = txtLog.Text.Length;
            txtLog.ScrollToCaret();
        }

        private void IvrInteractive_FormClosing(object sender, FormClosingEventArgs e)
        {
            DialogResult dr = MessageBox.Show("Do you really want to shutdown the application?", "Close?", MessageBoxButtons.YesNo);
            if (dr == DialogResult.No)
            {
                e.Cancel = true;
                return;
            }

            // Stop Logging messages
            IvrApplication.Log.MessageLogged -= new MessageLogged(Log_MessageLogged);

            // This destroys the connection to the Telephony Bank.
            IvrApplication.StopImmediate();  
           
        }

        private void IvrInteractive_Load(object sender, EventArgs e)
        {
            // When the form loads, it starts the connection to the Telephony Bank.
            // You could move this to a start button if you dont want the connection established immediately.
            IvrApplication.Start(); 
        }
    }
}