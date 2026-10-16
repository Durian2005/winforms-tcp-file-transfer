using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using FileTransferClient;
using TransferCore;

namespace GuiIntegrationTest;

/// <summary>
/// 界面层集成测试（**不会弹出任何窗口**）：
///
/// 直接构造真实的服务端 / 客户端窗体（构造时会执行 InitializeComponent，
/// 但不调用 Show()，所以桌面上看不到窗口），然后用"点击按钮"的方式跑完一次
/// 完整的大文件传输，最后断言：
///
///   1. 进度条确实被进度回调函数刷到了 100%；
///   2. "回调函数已被调用 N 次" 的计数确实在增长；
///   3. 接收端落盘文件与发送端 SHA256 一致。
///
/// 这一层验证的是"回调函数 → 跨线程 → 进度条"这条链路真的通，
/// 而不是仅仅"能编译"。
/// </summary>
internal static class Program
{
    private const int TestPort = 6200;

    private static int _passed;
    private static int _failed;

    [STAThread]
    private static int Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        ApplicationConfiguration.Initialize();

        string root = Path.Combine(Path.GetTempPath(), "FileTransferGuiTest");
        string receiveDir = Path.Combine(root, "receive");
        string sendFile = Path.Combine(root, "界面测试大文件.bin");
        const long size = 192L * 1024 * 1024;   // 192 MB

        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }

        Directory.CreateDirectory(receiveDir);

        Console.WriteLine("======================================================");
        Console.WriteLine(" 界面层集成测试（无窗口，真实点击按钮）");
        Console.WriteLine("======================================================");

        Console.Write($"生成 {NetOrder.FormatSize(size)} 测试文件 ... ");
        CreateTestFile(sendFile, size);
        Console.WriteLine("完成");
        Console.WriteLine();

        // ------------------------------------------------------------------
        // 构造两个真实的窗体（不 Show，桌面上不会出现窗口）
        // ------------------------------------------------------------------
        using var serverForm = new FileTransferServer.MainForm();
        using var clientForm = new FileTransferClient.MainForm();

        IntPtr serverHandle = serverForm.Handle;    // 强制创建句柄，让 BeginInvoke 能用
        IntPtr clientHandle = clientForm.Handle;
        Console.WriteLine($"服务端窗体已构造（句柄 0x{serverHandle:X}）：{serverForm.Text}");
        Console.WriteLine($"客户端窗体已构造（句柄 0x{clientHandle:X}）：{clientForm.Text}");
        Console.WriteLine();

        // ------------------------------------------------------------------
        // 用反射填好界面上的参数（等价于人工填表），然后点按钮
        // ------------------------------------------------------------------
        SetText(serverForm, "txtPort", TestPort.ToString());
        SetText(serverForm, "txtSaveDir", receiveDir);
        SetText(clientForm, "txtHost", "127.0.0.1");
        SetText(clientForm, "txtPort", TestPort.ToString());

        // 客户端选择文件（调用窗体自己的 SetSelectedFile 方法）
        Invoke(clientForm, "SetSelectedFile", sendFile);

        // ---- 点击服务端的"开始监听" ----
        ClickButton(serverForm, "btnStart");
        PumpMessages(300);
        string status = GetText(serverForm, "lblStatus");
        Assert($"点击[开始监听]后状态变为监听中（{status}）", status.Contains("正在监听"), status);
        Console.WriteLine();

        // ---- 点击客户端的"连接并发送" ----
        Console.WriteLine($"点击[连接并发送]，发送 {NetOrder.FormatSize(size)} ...");
        var watch = Stopwatch.StartNew();
        ClickButton(clientForm, "btnSend");
        PumpUntilIdle(clientForm, TimeSpan.FromMinutes(3));
        watch.Stop();
        Console.WriteLine($"传输结束，耗时 {watch.Elapsed.TotalSeconds:0.00}s，"
                          + $"平均 {size / watch.Elapsed.TotalSeconds / 1024 / 1024:0.0} MB/s");
        // 再泵一会儿消息，把最后几个 BeginInvoke 的界面刷新执行完
        PumpMessages(800);
        Console.WriteLine();

        // ------------------------------------------------------------------
        // 断言
        // ------------------------------------------------------------------
        var bar = (ProgressBar)GetField(serverForm, "prgBar")!;
        var percentLabel = (Label)GetField(serverForm, "lblPercent")!;
        var callbackLabel = (Label)GetField(serverForm, "lblCallback")!;
        var bytesLabel = (Label)GetField(serverForm, "lblBytes")!;

        Console.WriteLine("服务端界面上的最终状态：");
        Console.WriteLine($"  进度条最大值 = {bar.Maximum}，当前值 = {bar.Value}");
        Console.WriteLine($"  百分比标签   = {percentLabel.Text}");
        Console.WriteLine($"  字节标签     = {bytesLabel.Text}");
        Console.WriteLine($"  回调节点     = {callbackLabel.Text}");
        Console.WriteLine();

        // 服务端最后会被"停止监听"覆盖成 0？这里记录的是最后一个文件的最终进度，
        // 因为 Stop() 会重置，所以断言放在停止之前。
        Assert("服务端进度条已被回调刷到 1000（即 100.0%）", bar.Value == bar.Maximum, bar.Value.ToString());
        Assert("服务端百分比标签显示 100.0%", percentLabel.Text.Trim() == "100.0%", percentLabel.Text);
        Assert("服务端字节标签显示已接收满整个文件",
            bytesLabel.Text.Contains(NetOrder.FormatSize(size)), bytesLabel.Text);

        int serverCallbacks = ExtractNumber(callbackLabel.Text);
        Assert($"服务端进度回调被调用了多次（实际 {serverCallbacks} 次）", serverCallbacks > 3, serverCallbacks.ToString());

        var clientBar = (ProgressBar)GetField(clientForm, "prgBar")!;
        var clientPercent = (Label)GetField(clientForm, "lblPercent")!;
        var clientCallback = (Label)GetField(clientForm, "lblCallback")!;
        var clientStatus = (Label)GetField(clientForm, "lblStatus")!;

        Console.WriteLine("客户端界面上的最终状态：");
        Console.WriteLine($"  进度条值   = {clientBar.Value}");
        Console.WriteLine($"  百分比标签 = {clientPercent.Text}");
        Console.WriteLine($"  状态标签   = {clientStatus.Text}");
        Console.WriteLine($"  回调节点   = {clientCallback.Text}");
        Console.WriteLine();

        Assert("客户端进度条已被回调刷到 1000", clientBar.Value == clientBar.Maximum, clientBar.Value.ToString());
        Assert("客户端百分比标签显示 100.0%", clientPercent.Text.Trim() == "100.0%", clientPercent.Text);
        Assert("客户端状态标签显示发送成功", clientStatus.Text.Contains("成功"), clientStatus.Text);
        int clientCallbacks = ExtractNumber(clientCallback.Text);
        Assert($"客户端进度回调被调用了多次（实际 {clientCallbacks} 次）", clientCallbacks > 3, clientCallbacks.ToString());

        string[] received = Directory.GetFiles(receiveDir);
        Assert("接收目录里正好有 1 个文件", received.Length == 1, received.Length.ToString());

        if (received.Length == 1)
        {
            string hashSend = ComputeSha256(sendFile);
            string hashRecv = ComputeSha256(received[0]);
            Assert("接收文件大小一致", new FileInfo(received[0]).Length == size,
                new FileInfo(received[0]).Length.ToString());
            Assert("接收文件 SHA256 与源文件一致", hashSend == hashRecv,
                $"\n      源  {hashSend}\n      收  {hashRecv}");
            Console.WriteLine($"  接收文件：{Path.GetFileName(received[0])}");
        }

        // ---- 收尾：点击服务端"停止监听" ----
        ClickButton(serverForm, "btnStop");
        PumpMessages(400);

        Console.WriteLine();
        Console.WriteLine("======================================================");
        Console.WriteLine($" 测试完成：通过 {_passed} 项，失败 {_failed} 项");
        Console.WriteLine($" 结果：{(_failed == 0 ? "全部通过 ✔" : "存在失败用例 ✘")}");
        Console.WriteLine("======================================================");
        return _failed == 0 ? 0 : 1;
    }

    // ------------------------------------------------------------------
    //  反射辅助（窗体的控件字段是 private，测试里用反射访问）
    // ------------------------------------------------------------------
    private static object? GetField(object target, string name)
    {
        FieldInfo? field = target.GetType().GetField(
            name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        return field?.GetValue(target);
    }

    private static void SetText(object form, string fieldName, string value)
    {
        object? control = GetField(form, fieldName);
        if (control is TextBox box)
        {
            box.Text = value;
        }
        else
        {
            throw new InvalidOperationException($"字段 {fieldName} 不是 TextBox");
        }
    }

    private static string GetText(object form, string fieldName)
        => GetField(form, fieldName) switch
        {
            Label label => label.Text,
            TextBox box => box.Text,
            _ => "(非文本控件)"
        };

    /// <summary>
    /// 等价于"用户点了这个按钮"。
    ///
    /// 注意：不能用 <c>Button.PerformClick()</c>，因为窗体没有 Show() 时控件
    /// CanSelect 为 false，PerformClick 会被直接忽略。这里改为反射调用
    /// <c>Control.OnClick</c> —— 它是真正触发 Click 事件的方法，所以
    /// Designer 里 <c>this.btnStart.Click += BtnStart_Click;</c> 绑定的处理器
    /// 会被真实调用，等于一次真正的点击。
    /// </summary>
    private static void ClickButton(object form, string fieldName)
    {
        if (GetField(form, fieldName) is not Button button)
        {
            throw new InvalidOperationException($"字段 {fieldName} 不是 Button");
        }

        MethodInfo onClick = typeof(Control).GetMethod(
            "OnClick", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("找不到 Control.OnClick");

        onClick.Invoke(button, new object[] { EventArgs.Empty });
    }

    private static void Invoke(object target, string methodName, params object[] arguments)
    {
        MethodInfo? method = target.GetType().GetMethod(
            methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (method is null)
        {
            throw new InvalidOperationException($"找不到方法 {methodName}");
        }

        method.Invoke(target, arguments);
    }

    /// <summary>把 UI 线程消息队列里的消息处理掉（让 BeginInvoke 的界面刷新真正执行）。</summary>
    private static void PumpMessages(int milliseconds)
    {
        var watch = Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < milliseconds)
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }
    }

    /// <summary>一直泵消息，直到客户端界面上的"连接并发送"按钮重新可用（说明传输结束了）。</summary>
    private static void PumpUntilIdle(object clientForm, TimeSpan timeout)
    {
        var button = (Button)GetField(clientForm, "btnSend")!;
        var watch = Stopwatch.StartNew();
        while (!button.Enabled && watch.Elapsed < timeout)
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }
    }

    private static int ExtractNumber(string text)
    {
        var digits = new StringBuilder();
        foreach (char c in text)
        {
            if (char.IsDigit(c))
            {
                digits.Append(c);
            }
            else if (digits.Length > 0)
            {
                break;
            }
        }

        return digits.Length > 0 && int.TryParse(digits.ToString(), out int value) ? value : -1;
    }

    private static void CreateTestFile(string path, long size)
    {
        const int block = 1024 * 1024;
        byte[] buffer = new byte[block];
        new Random(20261016).NextBytes(buffer);

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, block);
        long written = 0;
        while (written < size)
        {
            int count = (int)Math.Min(block, size - written);
            stream.Write(buffer, 0, count);
            written += count;
        }
    }

    private static string ComputeSha256(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            1024 * 1024, FileOptions.SequentialScan);
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(stream));
    }

    private static void Assert(string title, bool condition, string detail)
    {
        if (condition)
        {
            _passed++;
            Console.WriteLine($"  [通过] {title}");
        }
        else
        {
            _failed++;
            Console.WriteLine($"  [失败] {title} —— 实际值：{detail}");
        }
    }
}
