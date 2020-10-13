using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VoiceElements.Client;
using VoiceElements.Common;
using System.Threading;
using System.Web.Script.Serialization;

namespace VoiceApp
{
    public class BasicAuthentication
    {
        public ChannelResource ChannelResource { get; set; }

        public VoiceResource VoiceResource { get; set; }

        public static Log Log
        {
            get
            {
                return IvrApplication.Log;
            }
        }

        public ManualResetEvent HangupMRE { get; set; }
        public ManualResetEvent ChallengeMRE { get; set; }

        public BasicAuthentication(ChannelResource channelResource)
        {
            ChannelResource = channelResource;
            ChannelResource.Disconnected += new Disconnected(ChannelResource_Disconnected);
            VoiceResource = channelResource.VoiceResource;
            HangupMRE = new ManualResetEvent(false);
            ChallengeMRE = new ManualResetEvent(false);
        }

        void ChannelResource_Disconnected(object sender, DisconnectedEventArgs e)
        {
            HangupMRE.Set();
        }

        public WebRtcChannel WebChannel { get; set; }

        public void RunScript()
        {
            try
            {
                WebChannel = ChannelResource as WebRtcChannel;
                if (WebChannel == null)
                {
                    Log.Write("Call is not WebRTC!");
                    return;
                }

                WebChannel.ChallengeEvent += new ChallengeEvent(WebChannel_ChallengeEvent);

                WebChannel.Answer();

                WebChannel = ChannelResource as WebRtcChannel;

                VoiceResource.PlayTTS("Welcome to the Inventive Labs Basic Authentication Demo.");

                VoiceResource.PlayTTS("Please Authenticate.  You may enter anything you wish.");

                WebChannel.SendChallenge(null);

                WaitHandle[] handles = new WaitHandle[] { IvrApplication.ShutdownEvent, HangupMRE, ChallengeMRE };

                int handle = WaitHandle.WaitAny(handles, 30000);
                if (handle == WaitHandle.WaitTimeout)
                {
                    VoiceResource.PlayTTS("Your session has timed out. Please try again later.  Good Bye.");
                    return;
                }

                if (handle == 0) return;
                if (handle == 1) return;

                VoiceResource.PlayTTS("Thanks you for authenticating.");
                VoiceResource.PlayTTS(String.Format("Your Username was <spell>{0}</spell>", m_UserName));
                VoiceResource.PlayTTS(String.Format("Your Password was <spell>{0}</spell>", m_Password));
                VoiceResource.PlayTTS("Goodbye.");

            }
            catch (HangupException hex)
            {

            }
            catch (Exception ex)
            {
                Log.WriteException(ex, "Unexpected exception! in inbound call");
            }
            finally
            {
                try
                {
                    ChannelResource.Disconnect();
                }
                catch { }

                try
                {
                    ChannelResource.Dispose();
                }
                catch { }
            }
        }

        private string m_UserName = "";
        private string m_Password = "";

        void WebChannel_ChallengeEvent(object sender, ChallengeEventArgs e)
        {
            Log.Write("Challenge Returned - UN: {0} PW: {1}", e.Username, e.Password);
            m_UserName = e.Username;
            m_Password = e.Password;

            ChallengeMRE.Set();
        }
    }
}
