namespace FileTransferServer;

internal static class Program
{
    /// <summary>应用程序入口。</summary>
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        // 自检模式：只构造窗体并强制创建窗口句柄，不显示任何窗口。
        // 用于在无人值守 / 不方便弹窗的环境里验证界面代码没问题。
        if (args.Contains("--selfcheck"))
        {
            using var form = new MainForm();
            IntPtr handle = form.Handle;   // 强制走完 InitializeComponent 并创建句柄
            Console.WriteLine($"selfcheck OK: handle=0x{handle:X} title={form.Text}");
            return;
        }

        Application.Run(new MainForm());
    }
}
