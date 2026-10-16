# WinForms TCP File Transfer

A TCP file transfer application in **C# / WinForms (.NET 9)** — one server, one client, arbitrary-size
files, with transfer progress reported to the UI through a **progress callback**.

用 C# / WinForms 实现的 TCP 文件传输程序：一个服务端、一个客户端，可传输任意大小（4 GB 以上也没问题）
的本地文件，并全程通过**回调函数**把传输进度回推给界面刷新进度条。

> 安装、运行与实现原理的完整说明见 **[使用教程.md](使用教程.md)**。

---

## 特性

| 特性 | 说明 |
|---|---|
| **大文件** | 报文中的文件长度是 8 字节有符号整数，不受 2 GB / 4 GB 上限约束 |
| **回调驱动进度** | 核心库只认一个委托 `ProgressCallback`，不引用任何界面框架；回调返回 `false` 即取消传输 |
| **内容校验** | FNV-1a 64 边传边算，收发两端比对，可验证内容逐字节相同 |
| **半包 / 粘包安全** | 所有收发都走循环 `Send` / `Receive`，不做"一次调用必然发完"的假设 |
| **界面不假死** | 收发在后台线程执行，回调限流后用 `BeginInvoke` 切回 UI 线程刷新控件 |
| **服务端可复用** | `while` 循环 `accept`，收完一个立刻等下一个，客户端无需重启服务端 |
| **安全收尾** | 文件名做路径穿越清理、重名自动加序号、失败或取消时删除不完整文件 |

## 快速开始

**环境**：Windows 10/11 + [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)（已含 Windows 桌面运行时）。
**无需安装任何 Visual Studio 工作负载或额外组件。**

```bat
git clone https://github.com/Durian2005/winforms-tcp-file-transfer.git
cd winforms-tcp-file-transfer
dotnet build FileTransfer.sln -c Release

start "" "src\FileTransferServer\bin\Release\net9.0-windows\FileTransferServer.exe"
start "" "src\FileTransferClient\bin\Release\net9.0-windows\FileTransferClient.exe"
```

1. **服务端**：选好保存目录与监听端口，点【开始监听】。
2. **客户端**：选一个文件（也可以把文件直接拖进窗口），填服务端 IP 与端口，点【连接并发送】。
3. 两边的进度条由回调实时驱动；客户端可随时【取消】，接收端不会留下半截文件。

习惯用 IDE 的话，直接用 Visual Studio 2022（17.8+）打开 `FileTransfer.sln`，
右键工程 → 调试 → 启动新实例即可。

## 项目结构

```text
                    ┌──────────────────────────────┐
                    │ FileTransferServer (WinForms)│
                    │ MainForm：界面 + 回调函数实现 │
                    └───────────────┬──────────────┘
                                    │ 注册 ProgressCallback
                                    │ 订阅 Log / ClientConnected / TransferCompleted
                    ┌───────────────▼──────────────┐
                    │      TransferCore（核心库）   │
                    │  TcpFileServer   监听 + 循环 accept
                    │  TcpFileClient   连接 + 发送
                    │  FileSender      读文件 → 发送 → ★调用回调
                    │  FileReceiver    收数据 → 落盘 → ★调用回调
                    │  Protocol / NetOrder / Fnv1a64 / SocketIo
                    └───────────────▲──────────────┘
                                    │ 传入 ProgressCallback
                    ┌───────────────┴──────────────┐
                    │ FileTransferClient (WinForms)│
                    │ MainForm：界面 + 回调函数实现 │
                    └──────────────────────────────┘
```

核心库**完全不认识 WinForms**，只认识一个委托 `ProgressCallback`；界面怎么画进度、写不写日志，
核心库不关心 —— 这是本项目最主要的解耦点，也让核心链路可以脱离界面单独测试。

| 文件 | 行数 | 职责 |
|---|---|---|
| `src/TransferCore/Protocol.cs` | 71 | FTF1 报文格式常量、魔数校验 |
| `src/TransferCore/NetOrder.cs` | 73 | 网络字节序（大端）读写、容量与时间格式化 |
| `src/TransferCore/Fnv1a64.cs` | 57 | 增量式内容校验（边传边算，不必二次读盘） |
| `src/TransferCore/TransferProgress.cs` | 144 | 进度对象 + **`ProgressCallback` 委托定义** |
| `src/TransferCore/TransferResult.cs` | 89 | 传输结果（状态、字节数、耗时、校验结论、回调次数） |
| `src/TransferCore/SocketIo.cs` | 53 | **循环 `Send` / 循环 `Receive`**（处理部分发送、半包） |
| `src/TransferCore/FileSender.cs` | 204 | 发送一个文件 + 限流调用回调 |
| `src/TransferCore/FileReceiver.cs` | 278 | 接收一个文件 + 落盘 + 校验 + 应答 + 重名改名 |
| `src/TransferCore/TcpFileServer.cs` | 239 | 监听、循环 accept、多客户端、停止时中断传输 |
| `src/TransferCore/TcpFileClient.cs` | 157 | 带超时的 `Connect`、发送、强制取消 |
| `src/FileTransferServer/` | — | 服务端窗口程序 |
| `src/FileTransferClient/` | — | 客户端窗口程序 |
| `tools/TransferSelfTest/` | — | 无界面端到端自测，逐项断言 |
| `tools/GuiIntegrationTest/` | — | 界面层集成测试，不弹窗也能真实点击按钮跑完一次传输 |
| `使用教程.md` | — | 完整上机说明 |
| `docs/logs/` | — | 上述两个测试的真实运行日志 |

## 应用层协议 FTF1（字段均为大端序）

```text
请求（客户端 → 服务端）
+--------+--------+----------+---------------+--------------+----------+
| 魔数   | 名字长 | 文件长度 | 文件名(UTF-8) |  文件内容    | 校验值   |
| 4字节  | 4字节  |  8字节   | nameLen 字节  | fileSize 字节|  8字节   |
| "FTF1" | uint32 |  int64   |               |              | FNV-1a64 |
+--------+--------+----------+---------------+--------------+----------+

应答（服务端 → 客户端）
+--------+--------+------------------+
| 状态   | 消息长 | 消息(UTF-8)      |
| 1字节  | 4字节  | msgLen 字节      |
+--------+--------+------------------+
```

* **文件长度用 8 字节**：32 位整数只有 2 GB 上限，大文件会溢出，因此用 `long`（Int64）。
* **全部大端**：TCP/IP 规定的网络字节序是大端，而 x86 是小端，必须显式转换。
  `NetOrder.cs` 相当于 C 语言 `htonl` / `ntohl` 的 .NET 版本，并补上了 C 标准库没有的 64 位版本。
* **长度前缀分帧**：文件名与内容都以显式长度开头，而不是用分隔符 ——
  分隔符方案在文件内容恰好出现同样字节时会解析错位，长度前缀才是二进制安全的。
* **尾部校验值**：发送端边发边算，接收端边收边算，最后比对，因此大文件也能确认内容一个字节都没错。

## 关键实现

### 1. 进度条由回调函数驱动

```csharp
// src/TransferCore/TransferProgress.cs:144
public delegate bool ProgressCallback(TransferProgress progress);
```

核心层在 `FileSender.cs:193` 与 `FileReceiver.cs:265` 调用它；界面层负责实现
（服务端 `MainForm.cs:55`、客户端 `MainForm.cs:50`）。因为回调发生在工作线程上，
所以界面实现里用 `BeginInvoke` 切回 UI 线程再刷新进度条
（服务端 `MainForm.cs:66`、客户端 `MainForm.cs:60`）。

**回调的返回值就是"是否继续"** —— 返回 `false` 即取消传输，不需要另设取消通道。

### 2. 大文件不卡、进度条不假死

收发放在后台线程（客户端 `Task.Run`，服务端每个连接一个任务），UI 线程只负责绘制。
回调同时做限流：每 80 ms 最多一次（`FileSender.cs:99`、`FileReceiver.cs:117`），
否则 1 GB 按 256 KB 分块会产生 4096 次回调，把 UI 消息队列刷爆反而更卡。

### 3. 半包 / 拆包 / 粘包

`Send` 返回的字节数可能小于请求发送的长度，`Receive` 也可能只给到一半 ——
所以必须循环直到收发足量或连接结束（`SocketIo.cs:19` 的 `SendAll`、
`SocketIo.cs:35` 的 `ReceiveExact`）。这是 socket 编程最经典的坑，本项目所有收发都走这两个封装。

### 4. 服务端可复用 + 安全收尾

外层 `while` 循环 accept（`TcpFileServer.cs:145`），处理完一个连接立刻回到 `Accept`，
客户端不需要重启服务端；停止监听会关闭监听套接字与所有活动连接，把阻塞中的 `Receive` 打断
（`TcpFileServer.cs:109`）。接收端还会对文件名做清理（防 `..\..\` 路径穿越）、
重名自动加 `(1)` 后缀、传输失败或取消时删除不完整的文件。

## 验证

所有结论都可以在本地复现，原始日志见 [`docs/logs/`](docs/logs/)。

| 测试 | 命令 | 结果 |
|---|---|---|
| 核心链路端到端（含 512 MB 大文件、零字节文件、中途取消、连接失败） | `tools\TransferSelfTest\bin\Release\net9.0\TransferSelfTest.exe 512` | **17 项断言全部通过**；512 MB 耗时 1.38 s（约 370 MB/s），两端 SHA256 完全一致，回调 20 次 |
| 界面层（不弹窗、走真实 Click 事件） | `tools\GuiIntegrationTest\bin\Release\net9.0-windows\GuiIntegrationTest.exe` | **12 项断言全部通过**；进度条被回调刷到 1000/1000，百分比显示 100.0%，接收文件 SHA256 一致 |
| 编译 | `dotnet build FileTransfer.sln -c Release` | **0 错误 0 警告** |

## 已知限制

* **未加密、无身份认证** —— 这是面向局域网 / 本机的传输工具，不要直接暴露到公网。
  需要加固可以套一层 `SslStream`，或加一个口令握手。
* **没有断点续传**：传输中断后需要整个重传。
* **界面进度只显示"当前一次"传输**：服务端每个连接一个后台任务（理论上可并发），
  但 UI 上只有一组进度控件，多客户端同时传时进度会互相覆盖，没有做队列。
* **接收端不覆盖已有文件**：按"保存目录 + 原文件名"落盘，同名自动加序号。

## 许可

[MIT](LICENSE)
