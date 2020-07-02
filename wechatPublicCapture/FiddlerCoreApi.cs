using Fiddler;
using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace wechatPublicCapture
{
    public class FiddlerCoreApi
    {
        #region Fields

        private static string _hostname;
        private static string cert;
        private static bool isStartDarkweb;
        private static string key;

        #endregion Fields

        #region Constructors

        static FiddlerCoreApi()
        {
        }

        #endregion Constructors

        #region Properties

        public static ushort CurrentPort { get; private set; }

        public static Action<string> MPHandler;

        public static bool IsStarted => FiddlerApplication.IsStarted();

        #endregion Properties

        #region Methods

        /// <summary>
        /// 动态关闭系统代理
        /// </summary>
        public static void CloseSystemProxy()
        {
            if (FiddlerApplication.IsStarted())
            {
                FiddlerApplication.oProxy.Detach();
            }
        }

        /// <summary>
        /// 动态开启系统代理
        /// </summary>
        public static void OpenSystemProxy()
        {
            if (FiddlerApplication.IsStarted())
            {
                FiddlerApplication.oProxy.Attach();
            }
        }

        public static void ProxyToTor()
        {
            isStartDarkweb = true;
        }

        public static void Start(ushort listenPort, string hostname = "localhost")
        {
            if (FiddlerApplication.IsStarted())
            {
                throw new Exception("Fiddler Proxy 已启动.");
            }
            CurrentPort = listenPort;
            _hostname = hostname;
            isStartDarkweb = false;
            FiddlerApplication.SetAppDisplayName("WFS6910");
            FiddlerApplication.ResponseHeadersAvailable += ResponseHeadersAvailable;
            FiddlerApplication.Log.OnLogString += Log_OnLogString;
            FiddlerApplication.Prefs.SetBoolPref("fiddler.network.streaming.abortifclientaborts", true);
            InstallCertificate();

            var startupSettings =
                new FiddlerCoreStartupSettingsBuilder()
                    .ListenOnPort(CurrentPort)
                    .RegisterAsSystemProxy()
                    .DecryptSSL()
                    //.AllowRemoteClients()
                    //.ChainToUpstreamGateway()
                    .MonitorAllConnections()
                    //.HookUsingPACFile()
                    //.CaptureLocalhostTraffic()
                    //.CaptureFTP()
                    .OptimizeThreadPool()
                    //.SetUpstreamGatewayTo("http=CorpProxy:80;https=SecureProxy:443;ftp=ftpGW:20")
                    .Build();
            FiddlerApplication.Startup(startupSettings);
            FiddlerApplication.oProxy.DetachedUnexpectedly += OProxy_DetachedUnexpectedly;
        }

        public static void Stop()
        {
            UninstallCertificate();
            FiddlerApplication.ResponseHeadersAvailable -= ResponseHeadersAvailable;
            FiddlerApplication.Log.OnLogString -= Log_OnLogString;
            if (IsStarted)
            {
                FiddlerApplication.oProxy.DetachedUnexpectedly -= OProxy_DetachedUnexpectedly;
                FiddlerApplication.Shutdown();
            }
        }

        private static bool InstallCertificate()
        {
            if (!CertMaker.rootCertExists())
            {
                if (!CertMaker.createRootCert())
                    return false;

                if (!CertMaker.trustRootCert())
                    return false;

                cert = FiddlerApplication.Prefs.GetStringPref("fiddler.certmaker.bc.cert", null);
                key = FiddlerApplication.Prefs.GetStringPref("fiddler.certmaker.bc.key", null);
            }

            return true;
        }

        /// <summary>
        /// 安装、获取证书
        /// </summary>
        private static void InstallCertificate0()
        {
            //生成证书
            if (!CertMaker.rootCertExists())
            {
                CertMaker.createRootCert();
                CertMaker.trustRootCert();
            }

            //获取证书
            X509Certificate2 oRootcert = CertMaker.GetRootCertificate();

            //把证书安装到受信任的根证书颁发机构
            X509Store certStore = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
            certStore.Open(OpenFlags.ReadWrite);

            try
            {
                certStore.Add(oRootcert);
            }
            finally
            {
                certStore.Close();
            }
            //证书赋值
            FiddlerApplication.oDefaultClientCertificate = oRootcert;
            //在解密HTTPS通信时，控制服务器证书错误是否被忽略。
            CONFIG.IgnoreServerCertErrors = false;
        }

        private static void Log_OnLogString(object sender, LogEventArgs e)
        {
            Console.WriteLine(e.LogString);
        }

        private static void OProxy_DetachedUnexpectedly(object sender, EventArgs e)
        {
            Stop();
            Start(CurrentPort);
        }

        private static void ResponseHeadersAvailable(Session oS)
        {
            // This block enables streaming for files larger than 5mb
            if (oS.oResponse.headers.Exists("Content-Length"))
            {
                int iLen = 0;
                if (int.TryParse(oS.oResponse["Content-Length"], out iLen))
                {
                    // File larger than 5mb? Don't save its content
                    if (iLen > 5000000)
                    {
                        oS.bBufferResponse = false;
                        oS["log-drop-response-body"] = "save memory";
                    }
                }
            }
            if (oS.fullUrl.Contains("mp.weixin.qq.com"))
            {
                if (oS.fullUrl.Contains("/s?__biz=")
                    || oS.fullUrl.Contains("/mp/homepage?__biz=")
                    || oS.fullUrl.Contains("/mp/profile_ext?action=home&__biz="))
                {
                    MPHandler?.Invoke(oS.fullUrl);
                }
            }
        }

        private static bool UninstallCertificate()
        {
            //if (CertMaker.rootCertExists())
            //{
            //    if (!CertMaker.removeFiddlerGeneratedCerts(true))
            //        return false;
            //}
            cert = null;
            key = null;
            return true;
        }

        #endregion Methods
    }
}
