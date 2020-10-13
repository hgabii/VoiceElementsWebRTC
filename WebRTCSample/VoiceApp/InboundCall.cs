using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VoiceElements.Client;
using VoiceElements.Common;
using System.Threading;

namespace VoiceApp
{
    public class InboundCall
    {
        public ChannelResource ChannelResource
        {
            get;
            set;
        }

        public VoiceResource VoiceResource
        {
            get;
            set;
        }

        public InboundCall(ChannelResource channelResource, VoiceResource voiceResource)
        {
            this.ChannelResource = channelResource;
            this.VoiceResource = voiceResource;
            WaitEvent = new ManualResetEvent(false);
            HangupEvent = new ManualResetEvent(false);
            UniqueId = Guid.NewGuid().ToString();
        }

        public ManualResetEvent WaitEvent
        {
            get;
            set;
        }

        public ManualResetEvent HangupEvent
        {
            get;
            set;
        }

        public string UniqueId
        {
            get;
            set;
        }

        public string CurrentWebRTCId
        {
            get;
            set;
        }

        public string PhoneNumber
        {
            get;
            set;
        }

        public void RunOutboundScript()
        {
            try
            {
                ChannelResource.CallProgress = CallProgress.DialOnly;
                ChannelResource.Disconnected += ChannelResource_Disconnected;
                ChannelResource.Dial(PhoneNumber);
                HangupEvent.WaitOne();
            }
            catch { }
            finally{
                finalizer();

                try
                {
                    // Disconnect the call
                    ChannelResource.Disconnect();

                    // Always Dispose the object.
                    ChannelResource.Dispose();

                    IvrApplication.Log.Write("Call complete.");
                }
                catch { }
            }

        }

        public void RunScript()
        {
            try
            {
                VoiceResource.PlayTTS("Please wait while we try to connect your call");
                ChannelResource.Disconnected += ChannelResource_Disconnected;
                // Trigger the notification to the webrtc legs that a new call is ready to be answered.
                IvrApplication.NotifyCall(this.UniqueId, ChannelResource.Ani);

                // We'll wait to see if someone answers the call for 15 seconds. If they do not, we will hang up.
                bool isSignalled = WaitEvent.WaitOne(15000);
                if (!isSignalled)
                {
                    IvrApplication.CancelCall(this.UniqueId);
                    VoiceResource.PlayTTS("I'm sorry, we couldn't connect your call");
                    ChannelResource.Disconnect();
                }
                else
                {
                    // wait until the call gets hung up
                    HangupEvent.WaitOne();
                }
            }
            catch(Exception ex)
            {
                IvrApplication.Log.WriteException(ex, "Writing error messages");
            }
            finally
            {
                finalizer();


            }

        }

        private void finalizer()
        {
            try
            {
                ChannelResource.StopListening();
            }
            catch { }

            try
            {
                ChannelResource.RouteFull(VoiceResource);
            }
            catch { }
            ChannelResource.Disconnected -= ChannelResource_Disconnected;
            lock (IvrApplication.SyncVar)
            {
                IvrApplication.s_InboundCalls.Remove(this.UniqueId);
            }
        }

        void ChannelResource_Disconnected(object sender, DisconnectedEventArgs e)
        {
            HangupEvent.Set();
            if(!String.IsNullOrEmpty(CurrentWebRTCId))
            {
                IvrApplication.NotifyHangup(CurrentWebRTCId);
            }
            else
            {
                /// Notify WebRTC that the call couldn't connect.
                IvrApplication.CancelCall(this.UniqueId);
            }
            //throw new NotImplementedException();
        }
    }
}
