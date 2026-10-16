# 网络编程技术实验三 · TCP 大文件传输（进度回调演示）

用 **Visual Studio 2022 + WinForms（C# / .NET 9）** 实现的 socket 文件传输程序：
一个**服务端**、一个**客户端**，支持传输任意大小（≥ 4 GB 也没问题）的本地文件，
并且**全程通过回调函数（ProgressCallback）把进度回推给界面刷新进度条**。

> 详细的上机步骤请看 **[使用教程.md](使用教程.md)**；本文件只讲"这是什么、怎么最快跑起来、为什么这么设计"。

---

## 一、交付物清单

| 文件 / 目录 | 说明 |
|---|---|
| `FileTransfer.sln` | 解决方案入口，用 VS 2022 双击打开即可 |
| `src/TransferCore/` | **核心库**（纯 .NET，不含界面）：协议、字节序、校验、发送端、接收端、服务器、客户端 |
| `src/FileTransferServer/` | **服务端窗口程序**（WinForms）：选目录 → 开始监听 → 收文件 + 进度条 |
| `src/FileTransferClient/` | **客户端窗口程序**（WinForms）：选文件 → 连接并发送 + 进度条 + 取消 |
| `tools/TransferSelfTest/` | 无界面端到端自测（大文件 / 小文件 / 取消 / 连接失败），自动断言 |
| `tools/GuiIntegrationTest/` | 界面层集成测试：**不弹窗**也能真实点击按钮跑完一次传输并断言进度条 |
| `使用教程.md` | 详细使用教程（环境、编译、演示步骤、原理、排错、答辩问答） |
| `docs/logs/` | 上面两个测试的真实运行日志（可复现） |

---

## 二、为什么选 WinForms 而不是 MFC（本机实测依据）

题目允许"mfc 或 winform 等窗口开发，优先选本机环境支持的、最好不下载新的支持"。
实测本机（VS 2022 Community 17.12.35527.113）结果如下：

| 检查项 | 实测结果 | 结论 |
|---|---|---|
| `Microsoft.VisualStudio.Workload.ManagedDesktop`（.NET 桌面开发） | **已安装** | WinForms 可直接用 |
| `Microsoft.VisualStudio.Workload.NativeDesktop`（C++ 桌面开发） | 已安装 | 有 C++ 编译器 |
| v143 工具集的 MFC（`VC/Tools/MSVC/14.42.34433/atlmfc/include/afxwin.h`） | **不存在**，该目录下只有 `atls.lib`（只有 ATL） | 走 MFC 需要额外下载"MFC 组件" |
| v141 工具集（`14.16.27023`）的 MFC | 齐全，但属于旧版本残留 | 用它要跨代配对 SDK，脆弱 |
| .NET SDK / WindowsDesktop 运行时 | `9.0.101` / `9.0.0`，均已安装 | **零下载** |
| `dotnet build` 一个 WinForms 模板工程 | 成功，0 错 0 警告 | 可编译、可验证 |

⇒ **MFC 在本机其实"没装全"**（只装了 ATL），选它反而违背"不下载新支持"；
所以采用 **WinForms（C# / .NET 9）**，无需安装任何额外组件。

---

## 三、最快跑起来（3 步）

```text
1) 双击 FileTransfer.sln  （VS 2022 打开）
2) 生成 → 重新生成解决方案（或按 Ctrl+Shift+B）
3) 演示时把服务端和客户端都跑起来：
   - 在"解决方案资源管理器"里右键 FileTransferServer → 调试 → 启动新实例
   - 再右键 FileTransferClient → 调试 → 启动新实例
   - 服务端点【开始监听】；客户端选一个大文件、IP 填 127.0.0.1、点【连接并发送】
```

命令行（不想开 VS 时）：

```bat
cd /d "路径\winforms-tcp-file-transfer"
dotnet build FileTransfer.sln -c Release
start "" "src\FileTransferServer\bin\Release\net9.0-windows\FileTransferServer.exe"
start "" "src\FileTransferClient\bin\Release\net9.0-windows\FileTransferClient.exe"
```

---

## 四、程序结构（职责边界）

```text
                    ┌──────────────────────────────┐
                    │  FileTransferServer (WinForms)│
                    │  MainForm：界面 + 回调函数实现 │
                    └───────────────┬──────────────┘
                                    │ 注册 ProgressCallback
                                    │ 读事件 Log / ClientConnected / TransferCompleted
                    ┌───────────────▼──────────────┐
                    │      TransferCore（核心库）    │
                    │  TcpFileServer   监听 + 循环 accept
                    │  TcpFileClient   连接 + 发送
                    │  FileSender      读文件→发送→★调用回调
                    │  FileReceiver    收数据→落盘→★调用回调
                    │  Protocol / NetOrder / Fnv1a64 / SocketIo
                    └───────────────▲──────────────┘
                                    │ 传入 ProgressCallback
                    ┌───────────────┴──────────────┐
                    │  FileTransferClient (WinForms)│
                    │  MainForm：界面 + 回调函数实现 │
                    └──────────────────────────────┘
```

核心库**完全不认识 WinForms**，只认识一个委托 `ProgressCallback`；
界面上怎么写进度、写不写日志，核心库不关心 —— 这就是"回调解耦"。

| 文件 | 行数 | 职责 |
|---|---|---|
| `Protocol.cs` | 71 | FTF1 报文格式常量、魔数校验 |
| `NetOrder.cs` | 70 | 网络字节序（大端）读写、容量/时间格式化 |
| `Fnv1a64.cs` | 56 | 增量式内容校验（边传边算，不用二次读盘） |
| `TransferProgress.cs` | 144 | 进度对象 + **`ProgressCallback` 委托定义** |
| `TransferResult.cs` | 71 | 传输结果（状态、字节数、耗时、校验结论、回调次数） |
| `SocketIo.cs` | 53 | **循环 Send / 循环 Receive**（解决半包、部分发送） |
| `FileSender.cs` | 204 | 发送一个文件 + 限流调用回调 |
| `FileReceiver.cs` | 278 | 接收一个文件 + 落盘 + 校验 + 应答 + 重名改名 |
| `TcpFileServer.cs` | 239 | 监听、循环 accept、多客户端、停止时中断传输 |
| `TcpFileClient.cs` | 157 | 带超时的 connect、发送、强制取消（RST） |

---

## 五、自定义应用层协议 FTF1（全部大端序）

```text
请求（客户端 → 服务端）：
+--------+--------+----------+---------------+--------------+----------+
| 魔数   | 名字长 | 文件长度 | 文件名(UTF-8) |  文件内容    | 校验值   |
| 4字节  | 4字节  |  8字节   | nameLen 字节  | fileSize 字节|  8字节   |
| "FTF1" | uint32 |  int64   |               |              | FNV-1a64 |
+--------+--------+----------+---------------+--------------+----------+

应答（服务端 → 客户端）：
+--------+--------+------------------+
| 状态   | 消息长 | 消息(UTF-8)      |
| 1字节  | 4字节  | msgLen 字节      |
+--------+--------+------------------+
```

* **文件长度用 8 字节**：`int` 只有 2 GB 上限，大文件会溢出，所以用 `long`；
* **全部大端**：TCP/IP 规定的网络字节序是大端，x86 是小端，必须显式转换
  （和实验二"字节序"直接呼应，`NetOrder.cs` 就是 C 语言 `htonl/ntohl` 的 .NET 版本，
  另外补了 C 标准库没有的 64 位版本）；
* **尾部校验值**：发送端边发边算，接收端边收边算，最后比对 —— 大文件也能验证"内容一个字节都没错"。

---

## 六、四个关键技术点（教程里有行号级讲解）

1. **回调函数显示进度条**
   `TransferProgress.cs:144` 定义 `public delegate bool ProgressCallback(TransferProgress progress);`
   核心层在 `FileSender.cs:193` / `FileReceiver.cs:265` 调用它；
   界面层实现它（服务端 `MainForm.cs:55`、客户端 `MainForm.cs:50`），
   因为回调发生在工作线程，所以用 `BeginInvoke` 切回 UI 线程再刷进度条
   （服务端 `MainForm.cs:66`、客户端 `MainForm.cs:60`）。
   回调返回 `false` 即表示取消 —— 一步到位实现"取消传输"。

2. **大文件不卡、进度条不假死**
   收发放在后台线程（客户端 `Task.Run`，服务端 `Task.Run` 每个连接一个任务），
   UI 线程只负责画；回调限流到每 80 ms 最多一次（`FileSender.cs:99`），
   避免 256 KB 一次的回调把消息队列刷爆。

3. **半包 / 拆包 / 粘包**
   `Send` 返回的字节数可能小于请求发送的长度，`Receive` 也可能只给到一半
   —— 所以必须循环（`SocketIo.cs:23`、`SocketIo.cs:39`）。这是 socket 编程最经典的坑。

4. **服务端可复用 + 安全收尾**
   外层 `while` 循环 accept（`TcpFileServer.cs:145`），客户端不用重启服务器；
   停止监听会关闭监听套接字和所有活动连接，把阻塞中的 `Receive` 打断（`TcpFileServer.cs:105`）；
   接收端对文件名做清理（防 `..\..\` 路径穿越）、重名自动加 `(1)` 后缀、传输失败删除半截文件。

---

## 七、验证结论（可复现）

| 测试 | 命令 | 结果 |
|---|---|---|
| 核心链路端到端（含 512 MB 大文件、取消、连接失败） | `tools\TransferSelfTest\bin\Release\net9.0\TransferSelfTest.exe 512` | **17 项断言全部通过**，512 MB 耗时 1.38 s（约 370 MB/s），两端 SHA256 完全一致，回调 20 次 |
| 界面层（不弹窗真实点击按钮） | `tools\GuiIntegrationTest\bin\Release\net9.0-windows\GuiIntegrationTest.exe` | **12 项断言全部通过**，进度条被回调刷到 1000/1000，百分比显示 100.0%，接收文件 SHA256 一致 |
| 编译 | `dotnet build FileTransfer.sln -c Release` | **0 错误 0 警告** |

原始日志见 [`docs/logs/`](docs/logs/)。

---

## 八、已知限制（如实说明）

* 传输是**串行**的：一个客户端传完才轮到下一个（`TcpFileServer` 用后台任务处理，理论可并发，
  但 UI 上只显示"当前进度"，多客户端同时传时进度会互相覆盖 —— 演示场景够用，没有做队列）。
* 没有做断点续传、没有加密、没有身份认证 —— 这是**局域网/本机演示程序**，不要直接暴露到公网。
* 接收端按"保存目录 + 原文件名"落盘，同名自动加序号，**不覆盖已有文件**。
