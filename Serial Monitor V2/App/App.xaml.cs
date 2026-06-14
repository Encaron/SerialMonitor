using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace 串口助手
{
    public partial class App : Application
    {
        static App()
        {
            // 静态构造器在 Application 构造器之前执行——尽早注册异常处理器
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                WriteCrashLogStatic(args.ExceptionObject as Exception);
            };
        }

        public App()
        {
            // 在 Application 构造器链中尽早注册
            DispatcherUnhandledException += (s, args) =>
            {
                WriteCrashLogStatic(args.Exception);
                args.Handled = true;
            };
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
        }

        private static string CrashLogPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         "SerialMonitor", "crash.log");

        private static void WriteCrashLogStatic(Exception ex)
        {
            try
            {
                string dir = Path.GetDirectoryName(CrashLogPath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex?.GetType().FullName}: {ex?.Message}");

                // 递归输出所有 InnerException
                int depth = 0;
                var inner = ex;
                while (inner != null)
                {
                    sb.AppendLine($"  --- Inner[{depth}] {inner.GetType().FullName}: {inner.Message}");
                    sb.AppendLine(inner.StackTrace ?? "(no stack trace)");
                    inner = inner.InnerException;
                    depth++;
                }
                sb.AppendLine();
                File.AppendAllText(CrashLogPath, sb.ToString());
            }
            catch
            {
                // 写崩溃日志本身失败 → 静默，避免递归爆炸
            }
        }

        private static void WriteCrashLog(Exception ex)
        {
            WriteCrashLogStatic(ex);
        }
    }
}
