using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nancy;
using Nancy.Conventions;
using Nancy.ModelBinding;
using Nancy.Hosting.Self;
using Nancy.ViewEngines.Razor;
using System.Threading;
using System.Net;
using System.Reflection;
using System.IO;

namespace WebServer
{
    public class NancyCustomRootPathProvider : IRootPathProvider
    {
        public string GetRootPath()
        {
#if DEBUG
            string directory = Directory.GetParent(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)).Parent.Parent.FullName + @"\WebServer";
            return directory;
#else
            return Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
#endif
        }
    }

    public class NancyBootstrapper : DefaultNancyBootstrapper
    {

        protected override void ConfigureConventions(NancyConventions nancyConventions)
        {
            nancyConventions.StaticContentsConventions.Add(StaticContentConventionBuilder.AddDirectory("/", "content"));

            base.ConfigureConventions(nancyConventions);
        }

        protected override IRootPathProvider RootPathProvider
        {
            get
            {
                return new NancyCustomRootPathProvider();
                //return base.RootPathProvider;
            }
        }

        protected override void ApplicationStartup(Nancy.TinyIoc.TinyIoCContainer container, Nancy.Bootstrapper.IPipelines pipelines)
        {
            pipelines.AfterRequest.AddItemToEndOfPipeline((ctx) =>
            {
                ctx.Response.WithHeader("Access-Control-Allow-Origin", "*")
                                .WithHeader("Access-Control-Allow-Methods", "POST,GET")
                                .WithHeader("Access-Control-Allow-Headers", "Accept, Origin, Content-type");

            });

            base.ApplicationStartup(container, pipelines);
        }

    }

    public class NancyServer
    {
        private ManualResetEvent _RunningEvent = new ManualResetEvent(false);
        public void Stop()
        {
            _RunningEvent.Set();
        }

        internal ManualResetEvent IsStartedEvent
        {
            get;
            private set;
        }
        public NancyServer()
        {
            Nancy.Json.JsonSettings.RetainCasing = true;
            IsStartedEvent = new ManualResetEvent(false);
        }

        protected IPAddress GetIpAddress()
        {
            IPHostEntry host;
            IPAddress localIP = null;
            host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (IPAddress ip in host.AddressList)
            {
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    localIP = ip;
                }
            }
            return localIP;
        }

        public string GetContentDirectory()
        {
            string returnValue = "content";
            if (String.IsNullOrEmpty(Properties.Settings.Default.ContentDirectory))
            {
                returnValue = Properties.Settings.Default.ContentDirectory;
            }

            return returnValue;
        }

        public Uri GetUri()
        {
            IPAddress address = GetIpAddress();

            if (!String.IsNullOrEmpty(Properties.Settings.Default.WebServerIp))
            {
                IPAddress.TryParse(Properties.Settings.Default.WebServerIp, out address);
            }

            Uri publicAddress = new Uri("http://" + address + ":" + Properties.Settings.Default.WebServerPort.ToString());

            return publicAddress;
        }

        public void Start()
        {
            try
            {
                HostConfiguration configuration = new HostConfiguration();

                configuration.UrlReservations = new UrlReservations();
                configuration.UrlReservations.CreateAutomatically = true;

                List<Uri> uris = new List<Uri>();

                Uri localUri = new Uri("http://127.0.0.1:" + Properties.Settings.Default.WebServerPort.ToString());
                Uri localHostUri = new Uri("http://localhost:" + Properties.Settings.Default.WebServerPort.ToString());
                Uri publicAddress = GetUri();
                uris.Add(localUri);
                uris.Add(localHostUri);
                // If you want to bind to HTTPS, you could modify the above function to allow for that.
                //uris.Add(publicAddress);

                using (var host = new NancyHost(configuration, uris.ToArray()))
                {

                    host.Start();
                    IsStartedEvent.Set();
                    _RunningEvent.WaitOne();
                    host.Stop();
                }
            }
            catch (Exception ex)
            {

            }
        }
    }
}
