using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using VoiceElements;
using VoiceElements.Common;
using VoiceElements.Client;
using System.Web.Script.Serialization;

namespace VoiceApp
{
    public class BasicIvr
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
        public AutoResetEvent ProcessCommandMRE { get; set; }

        private string m_RecordName = null;

        public BasicIvr(ChannelResource channelResource)
        {
            ChannelResource = channelResource;
            ChannelResource.Disconnected += new Disconnected(ChannelResource_Disconnected);
            VoiceResource = channelResource.VoiceResource;
            HangupMRE = new ManualResetEvent(false);
            ProcessCommandMRE = new AutoResetEvent(false);
            m_RecordName = System.IO.Path.GetTempPath() + "BasicIvr_" + DateTime.Now.ToString("yyyyMMdd.HHmm.ss.fff") + "_TestFile.wav";

            UnlockUI unlockMessage = new UnlockUI();
            unlockMessageString = Serializer.Serialize(unlockMessage);

        }

        void ChannelResource_Disconnected(object sender, DisconnectedEventArgs e)
        {
            HangupMRE.Set();
        }

        private object m_SyncVar = new object();

        public WebRtcChannel WebChannel { get; set; }

        public static JavaScriptSerializer Serializer = new JavaScriptSerializer(new CustomTypeResolver());

        public DateTime nextComfortTime = DateTime.Now.AddSeconds(7.0d);

        private CustomSocketMessage m_CurrentMessage;

        private string unlockMessageString = null;

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

                WebChannel.CustomMessageEvent += new CustomMessageEvent(WebChannel_CustomMessageEvent);

                WebChannel.Answer();

                VoiceResource.PlayTTS("Welcome to Inventive Labs.  Press any of the buttons on the web page to control the IVR.");
                nextComfortTime = DateTime.Now.AddSeconds(7.0d);
                //nextComfortTime = DateTime.Now.AddMinutes(1.0d);
                int counter = 0;

                WebChannel.SendCustomMessage(unlockMessageString);

                WaitHandle[] waitHandles = new WaitHandle[2] { HangupMRE, ProcessCommandMRE };

                while (true)
                {
                    int handle = WaitHandle.WaitAny(waitHandles, 50);
                    if (handle == 0) return; // Hangup Event
                    if (IvrApplication.State != State.Running) return;
                    if (handle == WaitHandle.WaitTimeout)
                    {
                        if (nextComfortTime < DateTime.Now)
                        {
                            nextComfortTime = DateTime.Now.AddSeconds(7.0d);
                            counter++;
                            VoiceResource.PlayTTS(counter.ToString());
                            VoiceResource.PlayTone(523, -9, 659, -9, 150);
                        }
                        continue;
                    }

                    // We got signalled...
                    if (m_CurrentMessage != null)
                    {
                        switch (m_CurrentMessage.GetType().FullName)
                        {
                            case "VoiceApp.BasicIvr+PlayTextToSpeech":
                                PlayTextToSpeech playTextToSpeech = (PlayTextToSpeech)m_CurrentMessage;
                                if (playTextToSpeech.ttsdata == null || playTextToSpeech.ttsdata.Length == 0)
                                {
                                    VoiceResource.PlayTTS("Key in some text and it will be read back to you.");
                                    nextComfortTime = DateTime.Now.AddSeconds(7.0d);
                                    WebChannel.SendCustomMessage(unlockMessageString);
                                    break;
                                }
                                VoiceResource.PlayTTS(playTextToSpeech.ttsdata);
                                nextComfortTime = DateTime.Now.AddSeconds(7.0d);
                                WebChannel.SendCustomMessage(unlockMessageString);
                                break;
                            case "VoiceApp.BasicIvr+RecordMessage":
                                VoiceResource.PlayTTS("Record your message up to 20 seconds at the beep. Press Stop when you are done.");
                                VoiceResource.MaximumTime = 20;
                                VoiceResource.TerminationDigits = "";
                                VoiceResource.Record(m_RecordName);
                                nextComfortTime = DateTime.Now.AddSeconds(7.0d);
                                WebChannel.SendCustomMessage(unlockMessageString);
                                break;
                            case "VoiceApp.BasicIvr+PlayMessage":

                                System.IO.FileInfo fi = new System.IO.FileInfo(m_RecordName);
                                if (!fi.Exists)
                                {
                                    VoiceResource.PlayTTS("Please record a message first and try again.");
                                    nextComfortTime = DateTime.Now.AddSeconds(7.0d);
                                    WebChannel.SendCustomMessage(unlockMessageString);
                                    break;
                                }
                                VoiceResource.Play(m_RecordName);
                                nextComfortTime = DateTime.Now.AddSeconds(7.0d);
                                WebChannel.SendCustomMessage(unlockMessageString);
                                break;
                            case "VoiceApp.BasicIvr+StreamMusic":
                                VoiceResource.Play("music.wav");
                                nextComfortTime = DateTime.Now.AddSeconds(7.0d);
                                WebChannel.SendCustomMessage(unlockMessageString);
                                break;
                            case "VoiceApp.BasicIvr+Goodbye":
                                VoiceResource.PlayTTS("Goodbye.");
                                return;
                            case "VoiceApp.BasicIvr+DialPadTest":
                                VoiceResource.PlayTTS("Press any series of numbers on the dial pad followed by the # key.");
                                VoiceResource.GetDigits(10, 15, "#", 5, true);
                                string digits = VoiceResource.DigitBuffer;
                                Log.Write("Digits: {0}", digits);
                                VoiceResource.PlayTTS(string.Format("You pressed: <spell>{0}</spell>", digits));
                                nextComfortTime = DateTime.Now.AddSeconds(7.0d);
                                WebChannel.SendCustomMessage(unlockMessageString);
                                break;
                            case "VoiceApp.BasicIvr+SpeechRecognition":
                                VoiceResource.SpeechRecognitionMode = VoiceElements.Interface.SpeechRecognitionMode.MultiplePlays;
                                VoiceResource.SpeechRecognitionPermitBargeIn = true;
                                VoiceResource.SpeechRecognitionGrammarFile = "Colors.xml";
                                VoiceResource.SpeechRecognitionEnabled = true;
                                string word = "";
                                try
                                {
                                    VoiceResource.PlayTTS("Please say your favorite color.");
                                    VoiceResource.GetResponse(10, 15, "#", 5, true);
                                    word = VoiceResource.SpeechRecognitionReturnedWord;
                                }
                                finally
                                {
                                    VoiceResource.SpeechRecognitionEnabled = false;
                                }
                                Log.Write("Speech: {0}", word);
                                VoiceResource.PlayTTS(string.Format("You said: {0}", word));
                                nextComfortTime = DateTime.Now.AddSeconds(7.0d);
                                WebChannel.SendCustomMessage(unlockMessageString);
                                break;
                            default:
                                Log.Write("Unable to handle message: {0}", m_CurrentMessage.GetType().FullName);
                                break;
                        }
                    }
                }
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

        void WebChannel_CustomMessageEvent(object sender, CustomMessageEventArgs e)
        {
            try
            {
                Log.Write("Received Message: {0}", e.Message);

                m_CurrentMessage = Serializer.Deserialize<CustomSocketMessage>(e.Message);

                Log.Write("Message Type:" + m_CurrentMessage.GetType().FullName);
                switch (m_CurrentMessage.GetType().FullName)
                {
                    case "VoiceApp.BasicIvr+Stop":
                        VoiceResource.Stop();
                        break;
                    default:
                        ProcessCommandMRE.Set();
                        break;
                }

            }
            catch (Exception ex)
            {
                Log.WriteException(ex, "BasicIvr::WebChannel_CustomMessageEvent");
            }
        }

        public class PlayTextToSpeech : CustomSocketMessage
        {
            public string ttsdata { get; set; }
        }

        public class RecordMessage : CustomSocketMessage
        {
            public string data { get; set; }
        }

        public class PlayMessage : CustomSocketMessage
        {
            public string data { get; set; }
        }

        public class Stop : CustomSocketMessage
        {
            public string data { get; set; }
        }

        public class StreamMusic : CustomSocketMessage
        {
            public string data { get; set; }
        }

        public class Goodbye : CustomSocketMessage
        {
            public string data { get; set; }
        }

        public class UnlockUI : CustomSocketMessage
        {
            public string data { get; set; }
        }

        public class DialPadTest : CustomSocketMessage
        {
            public string data { get; set; }
        }

        public class SpeechRecognition : CustomSocketMessage
        {
            public string data { get; set; }
        }


    }

    


}
