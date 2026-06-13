using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace 串口助手
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 未处理异常 → 写入崩溃日志
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                WriteCrashLog(args.ExceptionObject as Exception);
            };

            DispatcherUnhandledException += (s, args) =>
            {
                WriteCrashLog(args.Exception);
                MessageBox.Show(
                    $"程序遇到未处理的错误：\n\n{args.Exception.Message}\n\n" +
                    $"详细日志已写入：\n{CrashLogPath}",
                    "Serial Monitor — Error", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };
        }

        private static string CrashLogPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         "SerialMonitor", "crash.log");

        private static void WriteCrashLog(Exception ex)
        {
            try
            {
                string dir = Path.GetDirectoryName(CrashLogPath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string log = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex?.GetType().FullName}: {ex?.Message}\n{ex?.StackTrace}\n\n";
                File.AppendAllText(CrashLogPath, log);
            }
            catch
            {
                // 写崩溃日志本身失败 → 静默，避免递归爆炸
            }
        }
    }
}
