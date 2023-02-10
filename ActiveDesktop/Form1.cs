using CefSharp;
using CefSharp.JavascriptBinding;
using CefSharp.WinForms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Forms;

namespace ActiveDesktop
{
    public partial class Form1 : Form
    {
        [DllImport("user32.dll")]
        static extern bool GetCursorPos(ref Point lpPoint);
        public Form1()
        {
            InitializeComponent();

            chromiumWebBrowser1.LoadUrl("http://localhost:12345/web/index.html");

            chromiumWebBrowser1.JavascriptObjectRepository.ResolveObject += (sender, e) =>
            {
                var repo = e.ObjectRepository;
                if (e.ObjectName == "boundAsync")
                {
                    repo.NameConverter = null;
                    repo.NameConverter = new CamelCaseJavascriptNameConverter();
                    repo.Register("boundAsync", new BoundObject(), isAsync: true, options: BindingOptions.DefaultBinder);
                }
            };

            backgroundWorker1.RunWorkerAsync();

            int screenIndex = 1;

            this.StartPosition = FormStartPosition.Manual;
            this.Location = new Point(Math.Abs(Screen.AllScreens[screenIndex].Bounds.Location.X), Screen.AllScreens[screenIndex].Bounds.Location.Y);
            this.Width = Screen.AllScreens[screenIndex].Bounds.Width;
            this.Height = Screen.AllScreens[screenIndex].Bounds.Height;

            IntPtr progman = W32.FindWindow(this.Name, null);
            IntPtr result = IntPtr.Zero;
            W32.SendMessageTimeout(progman, 0x052C, new IntPtr(0), IntPtr.Zero, W32.SendMessageTimeoutFlags.SMTO_NORMAL, 1000, out result);

            IntPtr workerw = IntPtr.Zero;

            W32.EnumWindows(new W32.EnumWindowsProc((tophandle, topparamhandle) =>
            {
                IntPtr p = W32.FindWindowEx(tophandle,
                                            IntPtr.Zero,
                                            "SHELLDLL_DefView",
                                            IntPtr.Zero);

                if (p != IntPtr.Zero)
                {
                    // Gets the WorkerW Window after the current one.
                    workerw = W32.FindWindowEx(IntPtr.Zero,
                                               tophandle,
                                               "WorkerW",
                                               IntPtr.Zero);
                }

                return true;
            }), IntPtr.Zero);

            W32.SetParent(this.Handle, workerw);      
        }

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            HttpListener listener= new HttpListener();
            listener.Prefixes.Add("http://localhost:12345/");
            listener.Start();

            listener.BeginGetContext(new AsyncCallback(GetContextCallback), listener);
        }

        private static void GetContextCallback(IAsyncResult result)
        {
            HttpListener listener = (HttpListener)result.AsyncState;
            HttpListenerContext context = listener.EndGetContext(result);

            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

            string fileName = request.Url.AbsolutePath.Substring(1);
            fileName = Path.Combine(Application.StartupPath, fileName);

            if (File.Exists(fileName))
            {
                byte[] buffer = File.ReadAllBytes(fileName);
                response.ContentType = MimeMapping.GetMimeMapping(fileName);
                context.Response.ContentLength64 = buffer.Length;
                context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            }
            else
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
            }

            response.Close();

            listener.BeginGetContext(new AsyncCallback(GetContextCallback), listener);
        }

        public class BoundObject
        {
            public string GetMouseCoordinates()
            {
                var pt = new Point();
                GetCursorPos(ref pt);
                return "{ \"x\": " + pt.X.ToString() + ", \"y\": " + pt.Y.ToString() + " }";
            }
        }
    }
}
