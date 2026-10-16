using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using TransferCore;

namespace TransferSelfTest;

/// <summary>
/// 端到端自测程序（无界面）：
/// 直接使用与图形界面完全相同的 TransferCore 核心库，把"大文件传输 + 进度回调"
/// 这条链路完整跑一遍并自动断言，用来证明核心代码是真的能跑通的。
///
/// 用法：
///   TransferSelfTest            使用默认 512 MB 大文件做测试
///   TransferSelfTest 1024       用 1024 MB 做测试
///   TransferSelfTest 64         用 64 MB（机器慢时用小一点）
/// </summary>
internal static class Program
{
    private const int TestPort = 6100;

    private static int _passed;
    private static int _failed;

    private static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        long sizeMb = 512;
        if (args.Length > 0 && long.TryParse(args[0], out long parsed) && parsed > 0)
        {
            sizeMb = parsed;
        }

        string root = Path.Combine(Path.GetTempPath(), "FileTransferSelfTest");
        string sendDir = Path.Combine(root, "send");
        string receiveDir = Path.Combine(root, "receive");

        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }

        Directory.CreateDirectory(sendDir);
        Directory.CreateDirectory(receiveDir);

        Console.WriteLine("======================================================");
        Console.WriteLine(" TCP 大文件传输 + 进度回调  端到端自测");
        Console.WriteLine("======================================================");
        Console.WriteLine($"测试目录：{root}");
        Console.WriteLine();

        // ---------------- 准备测试文件 ----------------
        long bigSize = sizeMb * 1024 * 1024;
        string bigFile = Path.Combine(sendDir, "大文件测试.bin");
        Console.Write($"生成 {sizeMb} MB 测试文件 ... ");
        var createWatch = Stopwatch.StartNew();
        CreateTestFile(bigFile, bigSize);
        Console.WriteLine($"完成（{createWatch.Elapsed.TotalSeconds:0.0}s，{NetOrder.FormatSize(new FileInfo(bigFile).Length)}）");

        string smallFile = Path.Combine(sendDir, "小文件.bin");
        CreateTestFile(smallFile, 1024);          // 1 KB
        string emptyFile = Path.Combine(sendDir, "空文件.bin");
        File.WriteAllBytes(emptyFile, Array.Empty<byte>());   // 0 字节边界情况
        Console.WriteLine();

        // ---------------- 启动服务端 ----------------
        using var server = new TcpFileServer();
        int serverProgressCallbacks = 0;
        server.Log += message => Console.WriteLine($"  [服务端] {message}");
        server.ProgressCallback = progress =>
        {
            Interlocked.Increment(ref serverProgressCallbacks);
            return true;
        };

        server.Start(TestPort, receiveDir);
        Console.WriteLine();

        try
        {
            // ============ 用例 1：大文件 ============
            Console.WriteLine($"[用例 1] 发送大文件 {NetOrder.FormatSize(bigSize)}");
            int clientCallbacks = 0;
            long lastBytes = -1;
            bool monotonic = true;
            double lastPercent = -1;

            using (var client = new TcpFileClient())
            {
                client.Log += message => Console.WriteLine($"  [客户端] {message}");
                var watch = Stopwatch.StartNew();

                TransferResult result = await Task.Run(() => client.SendFile(
                    "127.0.0.1", TestPort, bigFile,
                    progress =>
                    {
                        Interlocked.Increment(ref clientCallbacks);

                        // 断言：进度必须单调不减
                        if (progress.BytesTransferred < lastBytes)
                        {
                            monotonic = false;
                        }

                        lastBytes = progress.BytesTransferred;
                        lastPercent = progress.Percent;

                        // 每 10% 打印一行，模拟界面刷新进度条
                        if (progress.Percent >= 0 && (int)progress.Percent % 10 == 0)
                        {
                            Console.WriteLine(
                                $"    进度 {progress.Percent,5:0.0}%  " +
                                $"{NetOrder.FormatSize(progress.BytesTransferred)}/" +
                                $"{NetOrder.FormatSize(progress.TotalBytes)}  " +
                                $"{NetOrder.FormatSize((long)progress.BytesPerSecond)}/s  " +
                                $"剩余 {NetOrder.FormatDuration(progress.Remaining.TotalSeconds)}");
                        }

                        return true;
                    },
                    CancellationToken.None));

                watch.Stop();

                // 发送端拿不到"服务端保存路径"（那是服务端的事），所以直接去接收目录里找
                string expectedPath = Path.Combine(receiveDir, Path.GetFileName(bigFile));

                Assert("传输状态为成功", result.Status == TransferStatus.Success, result.Message);
                Assert("传输字节数与文件大小一致", result.BytesTransferred == bigSize,
                    $"{result.BytesTransferred} vs {bigSize}");
                Assert("服务端校验值一致", result.ChecksumMatched == true, result.ChecksumMatched?.ToString() ?? "null");
                Assert("进度百分比最终到达 100%", Math.Abs(lastPercent - 100) < 0.001, lastPercent.ToString("0.000"));
                Assert("进度单调不减", monotonic, "出现回退");
                Assert("回调函数被多次调用（>1）", clientCallbacks > 1, $"实际 {clientCallbacks} 次");
                Assert("服务端落盘文件存在", File.Exists(expectedPath), expectedPath);

                if (File.Exists(expectedPath))
                {
                    Assert("落盘文件大小一致", new FileInfo(expectedPath).Length == bigSize,
                        new FileInfo(expectedPath).Length.ToString());

                    Console.Write("  计算两端 SHA256 比对 ... ");
                    string hashSend = ComputeSha256(bigFile);
                    string hashRecv = ComputeSha256(expectedPath);
                    Console.WriteLine("完成");
                    Assert("两端 SHA256 完全一致（内容逐字节相同）", hashSend == hashRecv,
                        $"\n      发送端 {hashSend}\n      接收端 {hashRecv}");
                }

                Console.WriteLine($"  用时 {watch.Elapsed.TotalSeconds:0.00}s，" +
                                  $"平均 {(double)bigSize / watch.Elapsed.TotalSeconds / 1024 / 1024:0.0} MB/s，" +
                                  $"客户端回调 {clientCallbacks} 次，服务端回调 {serverProgressCallbacks} 次");
            }

            // ============ 用例 2：小文件（复用同一个服务端，证明 accept 循环没退出） ============
            Console.WriteLine();
            Console.WriteLine("[用例 2] 同一服务端连续接收小文件（验证 accept 循环可复用）");
            using (var client = new TcpFileClient())
            {
                TransferResult r1 = await Task.Run(() => client.SendFile("127.0.0.1", TestPort, smallFile, null));
                TransferResult r2 = await Task.Run(() => client.SendFile("127.0.0.1", TestPort, emptyFile, null));
                Assert("1 KB 文件传输成功", r1.Status == TransferStatus.Success, r1.Message);
                Assert("0 字节文件传输成功", r2.Status == TransferStatus.Success, r2.Message);
                Assert("服务端已累计接收 3 个文件", server.ReceivedFileCount == 3, server.ReceivedFileCount.ToString());
            }

            // ============ 用例 3：中途取消 ============
            Console.WriteLine();
            Console.WriteLine("[用例 3] 传输中途调用回调返回 false，验证可取消");
            int callbacksBeforeCancel = 0;
            using (var client = new TcpFileClient())
            {
                TransferResult result = await Task.Run(() => client.SendFile(
                    "127.0.0.1", TestPort, bigFile,
                    progress =>
                    {
                        callbacksBeforeCancel++;
                        return callbacksBeforeCancel < 3;   // 第 3 次回调时请求取消
                    }));

                Assert("取消后状态为 Cancelled", result.Status == TransferStatus.Cancelled, result.Status.ToString());
                Assert("取消时确实只发了一部分", result.BytesTransferred < bigSize,
                    $"{result.BytesTransferred} / {bigSize}");
                Console.WriteLine($"  已发送 {NetOrder.FormatSize(result.BytesTransferred)} 后取消");
            }

            // 等一下让服务端把"没写完的半截文件"清理掉
            await Task.Delay(1000);
            int leftover = Directory.GetFiles(receiveDir).Length;
            Assert("取消后接收目录没有残留的半截文件", leftover == 3, $"目录里有 {leftover} 个文件");

            // ============ 用例 4：连接失败要报错而不是卡死 ============
            Console.WriteLine();
            Console.WriteLine("[用例 4] 连接一个没人监听的端口，应在超时后报错");
            using (var client = new TcpFileClient { ConnectTimeoutMs = 1500 })
            {
                var watch = Stopwatch.StartNew();
                TransferResult result = await Task.Run(() => client.SendFile("127.0.0.1", 1, smallFile, null));
                watch.Stop();
                Assert("连接失败时返回 Failed", result.Status == TransferStatus.Failed, result.Status.ToString());
                Assert("失败耗时不长（没有卡死）", watch.Elapsed.TotalSeconds < 10,
                    $"{watch.Elapsed.TotalSeconds:0.0}s");
                Console.WriteLine($"  {result.Message}（{watch.Elapsed.TotalSeconds:0.00}s）");
            }
        }
        finally
        {
            server.Stop();
        }

        Console.WriteLine();
        Console.WriteLine("======================================================");
        Console.WriteLine($" 测试完成：通过 {_passed} 项，失败 {_failed} 项");
        Console.WriteLine($" 结果：{(_failed == 0 ? "全部通过 ✔" : "存在失败用例 ✘")}");
        Console.WriteLine("======================================================");

        return _failed == 0 ? 0 : 1;
    }

    /// <summary>生成指定大小的测试文件（用可复现的伪随机内容，避免全零文件"看起来不真实"）。</summary>
    private static void CreateTestFile(string path, long size)
    {
        const int block = 1024 * 1024;
        byte[] buffer = new byte[block];
        var random = new Random(20261016);
        random.NextBytes(buffer);

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
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024,
            FileOptions.SequentialScan);
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
            Console.WriteLine($"  [失败] {title} —— {detail}");
        }
    }
}
