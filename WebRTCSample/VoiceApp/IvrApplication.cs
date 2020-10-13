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
                    s_TelephonyServer.RegisterDNIS();
                    s_TelephonyServer.RegisterWebRtcUrl();

                    // Subscribe to the connection events to allow you to reconnect if something happens to the internet connection.
                    // If you are running your own VE server, this is less likely to happen except when you restart your VE server.
                    //s_TelephonyServer.ConnectionLost += new ConnectionLost(s_TelephonyServer_ConnectionLost);
                    //s_TelephonyServer.ConnectionRestored += new ConnectionRestored(s_TelephonyServer_ConnectionRestored);


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
            s_TelephonyServer.RegisterDNIS();

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
                    Log.Write("Answering...");

                    e.ChannelResource.Answer();
                    e.ChannelResource.VoiceResource.PlayTTS("Hello.  Please call back using WEB R. T. C.  Good bye.");
                    e.ChannelResource.Disconnect();
                    return;
                }

                if (e.ChannelResource.Dnis.ToLower().EndsWith("basicivr.html"))
                {
                    BasicIvr basicIvr = new BasicIvr(e.ChannelResource);
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
