using System;
using System.Collections.Generic;
using System.Text;
using VoiceElements.Common;
using VoiceElements.Client;
using System.Threading;
using System.IO;
using WebServer;

namespace VoiceApp
{
    public class IvrApplication
    {
        private static object s_SyncVar = new object();

        public static object SyncVar
        {
            get { return IvrApplication.s_SyncVar; }
        }

        private static Log s_Log;
        public static Log Log
        {
            get { return s_Log; }
        }

        private static State s_State;

        public static State State
        {
            get { return s_State; }
        }

        private static Thread s_MainCodeThread;

        public static Thread MainCodeThread
        {
            get { return s_MainCodeThread; }
        }

        private static ManualResetEvent s_ShutdownEvent = new ManualResetEvent(false);
        public static ManualResetEvent ShutdownEvent
        {
            get { return s_ShutdownEvent; }
        }


        private static AutoResetEvent s_ThreadEvent = new AutoResetEvent(false);

        public static AutoResetEvent ThreadEvent
        {
            get { return s_ThreadEvent; }
        }

        private static string s_WorkingFolder = null;

        private static NancyServer s_WebServer = new NancyServer();
        private static NancyServer WebServer
        {
            get
            {
                return s_WebServer;
            }
        }

        public static string WorkingFolder
        {
            get { return s_WorkingFolder; }
        }

        /// <summary>
        /// We are storing the inbound calls in a dictionary. This allows us to uniquely identify each one.
        /// </summary>
        public static Dictionary<string, InboundCall> s_InboundCalls = new Dictionary<string, InboundCall>();

        /// <summary>
        /// 
        /// </summary>
        public static Dictionary<string, BasicIvr> s_WebRtc = new Dictionary<string, BasicIvr>();

        private static Conference s_Conference = null;

        public static void NotifyCall(string callId, string phoneNumber)
        {
            List<BasicIvr> webRtcToNotify = new List<BasicIvr>();
            // we don't want to notify in the lock, otherwise it holds up the lock from releasing.
            lock(IvrApplication.SyncVar)
            {
                foreach(BasicIvr webRtc in s_WebRtc.Values)
                {
                    webRtcToNotify.Add(webRtc);
                }
            }

            foreach(BasicIvr webRtc in webRtcToNotify)
            {
                // You may also consider sending new notifications on separate threads. Sometimes the send can be slowed down
                try
                {
                    webRtc.NotifyCall(callId, phoneNumber);
                }
                catch(Exception ex)
                {
                    IvrApplication.Log.WriteException(ex, "Error in IvrApplication::NotifyCall");
                }
            }
        }

        public static void CancelCall(string callId)
        {
            List<BasicIvr> webRtcToNotify = new List<BasicIvr>();
            // we don't want to notify in the lock, otherwise it holds up the lock from releasing.
            lock (IvrApplication.SyncVar)
            {
                foreach (BasicIvr webRtc in s_WebRtc.Values)
                {
                    webRtcToNotify.Add(webRtc);
                }
            }

            foreach (BasicIvr webRtc in webRtcToNotify)
            {
                // You may also consider sending new notifications on separate threads. Sometimes the send can be slowed down
                try
                {
                    webRtc.NotifyHangup();
                }
                catch (Exception ex)
                {
                    IvrApplication.Log.WriteException(ex, "Error in IvrApplication::NotifyCall");
                }
            }
        }

        public static void NotifyHangup(string webRtcId)
        {
            BasicIvr webRtc;
            s_WebRtc.TryGetValue(webRtcId, out webRtc);

            if(webRtc != null)
            {
                webRtc.NotifyHangup();
            }
        }

        public static void RouteCall(string callId, BasicIvr webRtcLeg)
        {
            try
            {
                InboundCall call;
                bool multipleCalls = false;

                lock (IvrApplication.s_SyncVar)
                {
                    s_InboundCalls.TryGetValue(callId, out call);
                    multipleCalls = s_InboundCalls.Count > 1;
                }

                if (call != null)
                {
                    // Set which WebRTC leg it is attached to that way we can notify it when the call terminates.
                    call.CurrentWebRTCId = webRtcLeg.UniqueId;
                    call.WaitEvent.Set();
                }

                if (!multipleCalls)
                {
                    call.ChannelResource.Answer();
                    webRtcLeg.ChannelResource.RouteFull(call.ChannelResource);
                }
                else
                {
                    if (s_Conference == null)
                    {
                        s_Conference = s_TelephonyServer.GetConference();
                        s_Conference.ConferenceChanged += S_Conference_ConferenceChanged;
                        s_Conference.ConferenceNotifyMode = ConferenceNotifyMode.On;

                        //webRtcLeg.ChannelResource.ConferenceAttributes.ConfereeType = ConfereeType.;
                        webRtcLeg.ChannelResource.VoiceResource.PlayTTS("Connecting monitor");
                        s_Conference.Add(webRtcLeg.ChannelResource);
                    }

                    lock (IvrApplication.s_SyncVar)
                    {
                        foreach (var c in s_InboundCalls.Values)
                        {
                            c.ChannelResource.ConferenceAttributes.ConfereeType = ConfereeType.Normal;
                            c.ChannelResource.VoiceResource.PlayTTS("Connecting to conference");
                            s_Conference.Add(c.ChannelResource);
                        }
                    }
                    
                }
            }
            catch(Exception ex)
            {

            }

            //throw new NotImplementedException();
        }

        private static void S_Conference_ConferenceChanged(ConferenceChangedEventArgs ccea)
        {
            if (s_Conference.Participants.Count == 0 && s_Conference.Monitors.Count == 0)
            {
                s_Conference.Dispose();
                s_Conference = null;
            }
        }

        public static void RejectCall(string callId)
        {
            InboundCall call;
            lock(IvrApplication.s_SyncVar)
            {
                s_InboundCalls.TryGetValue(callId, out call);
            }

            if(call != null)
            {
                // Signal that the call has been responded to
                call.WaitEvent.Set();

                // Hangup the call, since the call was rejected
                call.HangupEvent.Set();
            }
        }




        public static void DialCall(string phoneNumber, BasicIvr webRtc)
        {
            try
            {
                ChannelResource channelResource = s_TelephonyServer.GetChannel(typeof(SipChannel));
                InboundCall inbound = new InboundCall(channelResource, channelResource.VoiceResource);
                lock(s_SyncVar)
                {
                    s_InboundCalls.Add(inbound.UniqueId, inbound);
                }
                webRtc.CurrentCallId = inbound.UniqueId;
                inbound.CurrentWebRTCId = webRtc.UniqueId;
                inbound.PhoneNumber = phoneNumber;
                webRtc.ChannelResource.StopListening(); // stop listening to any other routable resources
                webRtc.ChannelResource.RouteFull(inbound.ChannelResource);
                webRtc.SendDialStart();

                // !!
                if (channelResource is SipChannel sipChannel)
                {
                    sipChannel.TransportProtocol = VoiceElements.Interface.TransportProtocol.TCP;
                    sipChannel.OverrideDestination = "pool01.sdudev.local:5069";
                    //sipChannel.OutgoingSipHeaders = new string[] { "" };
                }

                System.Threading.Thread dialCall = new Thread(inbound.RunOutboundScript);
                dialCall.Start();
            }
            catch (Exception ex)
            {

            }
            //throw new NotImplementedException();
        }

        public static void HangupCall(string callId)
        {
            InboundCall call;
            lock(IvrApplication.s_SyncVar)
            {
                s_InboundCalls.TryGetValue(callId, out call);
            }

            if(call != null)
            {
                call.HangupEvent.Set();
            }
        }

        static IvrApplication()
        {
            // Constructor
            s_Log = new Log("IvrApplication.Log");
            Log.Write("IvrApplication Constructor Complete");
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);
        }

        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception)
            {
                Log.WriteException((Exception)e.ExceptionObject, "Domain Level Unhandled Exception");
            }
            else
            {
                Log.Write("Domain Level Unhandled Exception - No Exception Object");
            }
        }

        public static void Start()
        {

            lock (SyncVar)
            {
                if (State == State.Stopped)
                {
                    s_State = State.Starting;
                    ThreadStart ts = new ThreadStart(MainCode);
                    s_MainCodeThread = new Thread(ts);
                    s_MainCodeThread.Name = "IvrApplication";
                    s_MainCodeThread.Start();
                    Log.Write("IvrApplication Starting...");

                    ThreadStart tsWebServer = new ThreadStart(WebServer.Start);
                    Thread threadWebServer = new Thread(tsWebServer);
                    threadWebServer.Name = "WebServer";
                    Log.Write("Starting WebServer");
                    threadWebServer.Start();
                }
                else
                {
                    Log.Write("IvrApplication is in the " + State.ToString() + " state.  Cannot start IvrApplication at this time.");
                }
            }
        }

        public static void StopImmediate()
        {
            lock (SyncVar)
            {
                if (State == State.Running || State == State.StoppingControlled)
                {
                    s_State = State.StoppingImmediate;
                    ThreadEvent.Set();
                    Log.Write("IvrApplication StoppingImmediate.");
                }
                else
                {
                    Log.Write("IvrApplication is in the " + State.ToString() + " state.  Cannot stop IvrApplication at this time.");
                }
            }
        }

        public static void StopControlled()
        {
            lock (SyncVar)
            {
                if (State == State.Running)
                {
                    s_State = State.StoppingControlled;
                    ThreadEvent.Set();
                    Log.Write("IvrApplication StoppingControlled.");
                }
                else
                {
                    Log.Write("IvrApplication is in the " + State.ToString() + " state.  Cannot stop IvrApplication at this time.");
                }
            }
        }

        public static TelephonyServer s_TelephonyServer = null;

        public static void MainCode()
        {

            try
            {
                s_WorkingFolder = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

                Log.Write("IvrApplication::MainCode() Starting...");

                // Start Other Threads...
                try
                {

                    // UPDATE YOUR SERVER ADDRESS HERE
                    System.Net.IPAddress[] ips = System.Net.Dns.GetHostAddresses(Properties.Settings.Default.TelephonyServer);

                    if (ips == null || ips.Length == 0) throw new Exception("Error: Could not resolve Telephony Server specified!");

                    string sIpaddress = @"gtcp://" + ips[0].ToString() + ":54331";

                    Log.Write("Connecting to: {0}", sIpaddress);

                    // CHANGE YOUR USERNAME AND PASSWORD HERE
                    s_TelephonyServer = new TelephonyServer(sIpaddress, Properties.Settings.Default.Username, Properties.Settings.Default.Password);

                    // CHANGE YOUR CACHE MODE HERE
                    //
                    // Client Session mode means that the server will stream and cache the files to/from your client machine.
                    // Files are flushed after you disconnect.
                    //
                    // Server mode means that the files reside on the server and will use the full path name to find them there.
                    // Server mode can only be used on your own dedicate VE server.

                    s_TelephonyServer.CacheMode = VoiceElements.Interface.CacheMode.ClientSession;
                    //s_TelephonyServer.CacheMode = VoiceElements.Interface.CacheMode.Server;

                    // SUBSCRIBE to the new call event.
                    s_TelephonyServer.NewCall += new VoiceElements.Client.NewCall(s_TelephonyServer_NewCall);
                    s_TelephonyServer.RegisterDNIS(Properties.Settings.Default.RegisterDNIS);
                    s_TelephonyServer.RegisterWebRtcUrl(Properties.Settings.Default.RegisterWebRTCURL);

                    // Subscribe to the connection events to allow you to reconnect if something happens to the internet connection.
                    // If you are running your own VE server, this is less likely to happen except when you restart your VE server.
                    s_TelephonyServer.ConnectionLost += new ConnectionLost(s_TelephonyServer_ConnectionLost);
                    s_TelephonyServer.ConnectionRestored += new ConnectionRestored(s_TelephonyServer_ConnectionRestored);


                }
                catch (Exception ex)
                {
                    try
                    {
                        if (s_TelephonyServer != null)
                        {
                            s_TelephonyServer.Dispose();
                        }
                    }
                    catch (Exception) { }

                    Log.Write("IvrApplication::MainCode() Exception: " + ex.Message + "\r\n" + ex.StackTrace);
                    throw ex;
                }

                Log.Write("VoiceElementsClient Version: {0}", s_TelephonyServer.GetClientVersion());
                Log.Write("VoiceElementsServer Version: {0}", s_TelephonyServer.GetServerVersion());

                lock (SyncVar)
                {
                    s_State = State.Running;
                }

                Log.Write("IvrApplication::MainCode() Running...");

                while (true)
                {

                    // Waits for some asyncronous event.
                    ThreadEvent.WaitOne(10000, false);

                    // At this point you are in control.  You can farm out calls from a database, 
                    // or you could code the IvrInteractive Form and create a GUI for handling you calls.
                    // Follow the example from the Sampler on how to make an outbound class for new calls.

                    lock (SyncVar)
                    {
                        if (State != State.Running) break;
                    }
                }

                s_TelephonyServer.Dispose();
                s_TelephonyServer = null;
                // Must be shutting down...

                if (State == State.StoppingControlled)
                {
                    Log.Write("IvrApplication::MainCode() StoppingControlled...");
                }

                if (State == State.StoppingImmediate)
                {
                    Log.Write("IvrApplication::MainCode() StoppingImmediate...");
                }

                lock (SyncVar)
                {
                    s_State = State.Stopped;
                    Log.Write("IvrApplication::MainCode() Stopped.");
                }

            }
            catch (Exception ex)
            {
                Log.Write("IvrApplication::MainCode() Exception" + ex.Message + "\r\n" + ex.StackTrace);
                s_State = State.Stopped;
            }
            finally
            {
                // Stop the web server
                s_WebServer.Stop();
                s_MainCodeThread = null;
            }
        }

        static void s_TelephonyServer_ConnectionRestored(object sender, ConnectionRestoredEventArgs e)
        {

            // When the connection is restored you must reset your cache mode and re-register the DNIS.

            s_TelephonyServer.CacheMode = VoiceElements.Interface.CacheMode.ClientSession;
            //s_TelephonyServer.CacheMode = VoiceElements.Interface.CacheMode.Server;
            s_TelephonyServer.RegisterDNIS(Properties.Settings.Default.RegisterDNIS);

            Log.Write("The Connection to the server was successfully restored!");

        }

        static void s_TelephonyServer_ConnectionLost(object sender, ConnectionLostEventArgs e)
        {

            // You could also send an email to yourself to let you know that the server was down.
            Log.Write("The Connection to the server was lost.");
        }

        static void ChannelResource_Disconnected(object sender, DisconnectedEventArgs e)
        {
            Log.Write("Disconnected (Meaning Caller Hung-up) Event Recevied.");
        }

        static void s_TelephonyServer_NewCall(object sender, VoiceElements.Client.NewCallEventArgs e)
        {
            try
            {
                Log.Write("NewCall Arrival! DNIS: {0}  ANI: {1}  Caller ID Name: {2}", e.ChannelResource.Dnis, e.ChannelResource.Ani, e.ChannelResource.CallerIdName);

                WebRtcChannel webRtcChannel = e.ChannelResource as WebRtcChannel;
                if (webRtcChannel == null)
                {
                    if (e.ChannelResource is SipChannel sipChannel)
                    {
                        Log.Write($"SIP channel - TransportProtocol: {sipChannel.TransportProtocol}");
                    }

                    Log.Write("Answering...");

                    InboundCall inboundCall = new InboundCall(e.ChannelResource, e.ChannelResource.VoiceResource);
                    // Add the new call to the dictionary
                    lock (IvrApplication.SyncVar)
                    {
                        IvrApplication.s_InboundCalls.Add(inboundCall.UniqueId, inboundCall);
                    }
                    inboundCall.RunScript();

                    return;
                }

                if (e.ChannelResource.Dnis.ToLower().EndsWith("basicivr.html"))
                {
                    BasicIvr basicIvr = new BasicIvr(e.ChannelResource);
                    lock(IvrApplication.SyncVar)
                    {
                        IvrApplication.s_WebRtc.Add(basicIvr.UniqueId, basicIvr);
                    }
                    basicIvr.RunScript();
                    return;
                }
                else if(e.ChannelResource.Dnis.ToLower().EndsWith("basicauthentication.html"))
                {
                    BasicAuthentication basicAuthentication = new BasicAuthentication(e.ChannelResource);
                    basicAuthentication.RunScript();
                    return;
                }
                else
                {
                    webRtcChannel.Answer();
                    webRtcChannel.VoiceResource.PlayTTS("I'm sorry we couldn't find your application. Goodbye!");
                    return;
                }
            }
            catch (HangupException)
            {
                Log.Write("The Caller Hungup.");
            }
            catch (Exception ex)
            {
                Log.WriteException(ex, "IvrApplication::NewCall");
            }
            finally
            {
                // Unsubscribe the event.
                e.ChannelResource.Disconnected -= new Disconnected(ChannelResource_Disconnected);
                // Hangup
                e.ChannelResource.Disconnect();
                // Always Dispose the object.
                e.ChannelResource.Dispose();

                Log.Write("Call complete.");

            }
        }




    }
}
