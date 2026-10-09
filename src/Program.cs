using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace MuxiaToolbox
{
    public static class Program
    {
        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        [DllImport("user32.dll", EntryPoint = "SetProcessDpiAwarenessContext")]
        static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);

        [STAThread]
        public static void Main()
        {
            EnableDpiAwareness();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e) { LogAndShow(e.Exception); };
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e) { LogAndShow(e.ExceptionObject as Exception); };
            try { Application.Run(new MainForm()); }
            catch (Exception ex) { LogAndShow(ex); }
        }

        static void EnableDpiAwareness()
        {
            try {
                // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2
                if (SetProcessDpiAwarenessContext(new IntPtr(-4))) return;
            } catch (EntryPointNotFoundException) { }
            catch { }
            try { SetProcessDPIAware(); } catch { }
        }

        static void LogAndShow(Exception ex)
        {
            if (ex == null) return;
            try {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ToolChest");
                Directory.CreateDirectory(folder);
                File.AppendAllText(Path.Combine(folder, "error.log"), DateTime.Now.ToString("s") + Environment.NewLine + ex + Environment.NewLine + Environment.NewLine, Encoding.UTF8);
                MessageBox.Show("发生错误，程序已记录日志并尝试继续运行。\r\n" + ex.Message, "工具宝匣", MessageBoxButtons.OK, MessageBoxIcon.Error);
            } catch { }
        }
    }
}

