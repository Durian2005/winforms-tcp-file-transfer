#nullable disable
namespace FileTransferServer;

partial class MainForm
{
    /// <summary>设计器所需的组件容器。</summary>
    private System.ComponentModel.IContainer components = null;

    private System.Windows.Forms.GroupBox grpServer;
    private System.Windows.Forms.Label lblPort;
    private System.Windows.Forms.TextBox txtPort;
    private System.Windows.Forms.Label lblDir;
    private System.Windows.Forms.TextBox txtSaveDir;
    private System.Windows.Forms.Button btnBrowse;
    private System.Windows.Forms.Label lblStatus;
    private System.Windows.Forms.Label lblPeer;
    private System.Windows.Forms.Button btnStart;
    private System.Windows.Forms.Button btnStop;

    private System.Windows.Forms.GroupBox grpProgress;
    private System.Windows.Forms.Label lblFileName;
    private System.Windows.Forms.ProgressBar prgBar;
    private System.Windows.Forms.Label lblPercent;
    private System.Windows.Forms.Label lblBytes;
    private System.Windows.Forms.Label lblSpeed;
    private System.Windows.Forms.Label lblTime;
    private System.Windows.Forms.Label lblCallback;

    private System.Windows.Forms.GroupBox grpLog;
    private System.Windows.Forms.TextBox txtLog;

    /// <summary>释放正在使用的资源。</summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            components?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        this.grpServer = new System.Windows.Forms.GroupBox();
        this.lblPort = new System.Windows.Forms.Label();
        this.txtPort = new System.Windows.Forms.TextBox();
        this.lblDir = new System.Windows.Forms.Label();
        this.txtSaveDir = new System.Windows.Forms.TextBox();
        this.btnBrowse = new System.Windows.Forms.Button();
        this.lblStatus = new System.Windows.Forms.Label();
        this.lblPeer = new System.Windows.Forms.Label();
        this.btnStart = new System.Windows.Forms.Button();
        this.btnStop = new System.Windows.Forms.Button();
        this.grpProgress = new System.Windows.Forms.GroupBox();
        this.lblFileName = new System.Windows.Forms.Label();
        this.prgBar = new System.Windows.Forms.ProgressBar();
        this.lblPercent = new System.Windows.Forms.Label();
        this.lblBytes = new System.Windows.Forms.Label();
        this.lblSpeed = new System.Windows.Forms.Label();
        this.lblTime = new System.Windows.Forms.Label();
        this.lblCallback = new System.Windows.Forms.Label();
        this.grpLog = new System.Windows.Forms.GroupBox();
        this.txtLog = new System.Windows.Forms.TextBox();
        this.grpServer.SuspendLayout();
        this.grpProgress.SuspendLayout();
        this.grpLog.SuspendLayout();
        this.SuspendLayout();
        // 
        // grpServer
        // 
        this.grpServer.Controls.Add(this.btnStop);
        this.grpServer.Controls.Add(this.btnStart);
        this.grpServer.Controls.Add(this.lblPeer);
        this.grpServer.Controls.Add(this.lblStatus);
        this.grpServer.Controls.Add(this.btnBrowse);
        this.grpServer.Controls.Add(this.txtSaveDir);
        this.grpServer.Controls.Add(this.lblDir);
        this.grpServer.Controls.Add(this.txtPort);
        this.grpServer.Controls.Add(this.lblPort);
        this.grpServer.Location = new System.Drawing.Point(12, 12);
        this.grpServer.Name = "grpServer";
        this.grpServer.Size = new System.Drawing.Size(716, 104);
        this.grpServer.TabIndex = 0;
        this.grpServer.TabStop = false;
        this.grpServer.Text = "服务器设置";
        // 
        // lblPort
        // 
        this.lblPort.AutoSize = true;
        this.lblPort.Location = new System.Drawing.Point(16, 32);
        this.lblPort.Name = "lblPort";
        this.lblPort.Size = new System.Drawing.Size(71, 17);
        this.lblPort.TabIndex = 0;
        this.lblPort.Text = "监听端口：";
        // 
        // txtPort
        // 
        this.txtPort.Location = new System.Drawing.Point(96, 29);
        this.txtPort.Name = "txtPort";
        this.txtPort.Size = new System.Drawing.Size(70, 23);
        this.txtPort.TabIndex = 1;
        this.txtPort.Text = "6000";
        // 
        // lblDir
        // 
        this.lblDir.AutoSize = true;
        this.lblDir.Location = new System.Drawing.Point(184, 32);
        this.lblDir.Name = "lblDir";
        this.lblDir.Size = new System.Drawing.Size(71, 17);
        this.lblDir.TabIndex = 2;
        this.lblDir.Text = "接收目录：";
        // 
        // txtSaveDir
        // 
        this.txtSaveDir.Location = new System.Drawing.Point(264, 29);
        this.txtSaveDir.Name = "txtSaveDir";
        this.txtSaveDir.Size = new System.Drawing.Size(336, 23);
        this.txtSaveDir.TabIndex = 3;
        // 
        // btnBrowse
        // 
        this.btnBrowse.Location = new System.Drawing.Point(608, 28);
        this.btnBrowse.Name = "btnBrowse";
        this.btnBrowse.Size = new System.Drawing.Size(92, 26);
        this.btnBrowse.TabIndex = 4;
        this.btnBrowse.Text = "浏览...";
        this.btnBrowse.UseVisualStyleBackColor = true;
        this.btnBrowse.Click += new System.EventHandler(this.BtnBrowse_Click);
        // 
        // lblStatus
        // 
        this.lblStatus.AutoSize = true;
        this.lblStatus.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(180)))), ((int)(((byte)(60)))), ((int)(((byte)(0)))));
        this.lblStatus.Location = new System.Drawing.Point(16, 70);
        this.lblStatus.Name = "lblStatus";
        this.lblStatus.Size = new System.Drawing.Size(83, 17);
        this.lblStatus.TabIndex = 5;
        this.lblStatus.Text = "状态：未监听";
        // 
        // lblPeer
        // 
        this.lblPeer.AutoSize = true;
        this.lblPeer.Location = new System.Drawing.Point(184, 70);
        this.lblPeer.Name = "lblPeer";
        this.lblPeer.Size = new System.Drawing.Size(119, 17);
        this.lblPeer.TabIndex = 6;
        this.lblPeer.Text = "客户端：尚未连接";
        // 
        // btnStart
        // 
        this.btnStart.Location = new System.Drawing.Point(496, 64);
        this.btnStart.Name = "btnStart";
        this.btnStart.Size = new System.Drawing.Size(100, 28);
        this.btnStart.TabIndex = 7;
        this.btnStart.Text = "开始监听";
        this.btnStart.UseVisualStyleBackColor = true;
        this.btnStart.Click += new System.EventHandler(this.BtnStart_Click);
        // 
        // btnStop
        // 
        this.btnStop.Enabled = false;
        this.btnStop.Location = new System.Drawing.Point(604, 64);
        this.btnStop.Name = "btnStop";
        this.btnStop.Size = new System.Drawing.Size(96, 28);
        this.btnStop.TabIndex = 8;
        this.btnStop.Text = "停止监听";
        this.btnStop.UseVisualStyleBackColor = true;
        this.btnStop.Click += new System.EventHandler(this.BtnStop_Click);
        // 
        // grpProgress
        // 
        this.grpProgress.Controls.Add(this.lblCallback);
        this.grpProgress.Controls.Add(this.lblTime);
        this.grpProgress.Controls.Add(this.lblSpeed);
        this.grpProgress.Controls.Add(this.lblBytes);
        this.grpProgress.Controls.Add(this.lblPercent);
        this.grpProgress.Controls.Add(this.prgBar);
        this.grpProgress.Controls.Add(this.lblFileName);
        this.grpProgress.Location = new System.Drawing.Point(12, 124);
        this.grpProgress.Name = "grpProgress";
        this.grpProgress.Size = new System.Drawing.Size(716, 150);
        this.grpProgress.TabIndex = 1;
        this.grpProgress.TabStop = false;
        this.grpProgress.Text = "接收进度（由进度回调函数驱动）";
        // 
        // lblFileName
        // 
        this.lblFileName.AutoEllipsis = true;
        this.lblFileName.Location = new System.Drawing.Point(16, 28);
        this.lblFileName.Name = "lblFileName";
        this.lblFileName.Size = new System.Drawing.Size(684, 20);
        this.lblFileName.TabIndex = 0;
        this.lblFileName.Text = "文件名：--";
        // 
        // prgBar
        // 
        this.prgBar.Location = new System.Drawing.Point(16, 56);
        this.prgBar.Maximum = 1000;
        this.prgBar.Name = "prgBar";
        this.prgBar.Size = new System.Drawing.Size(588, 24);
        this.prgBar.TabIndex = 1;
        // 
        // lblPercent
        // 
        this.lblPercent.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Bold);
        this.lblPercent.Location = new System.Drawing.Point(612, 57);
        this.lblPercent.Name = "lblPercent";
        this.lblPercent.Size = new System.Drawing.Size(88, 22);
        this.lblPercent.TabIndex = 2;
        this.lblPercent.Text = "0.0%";
        this.lblPercent.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
        // 
        // lblBytes
        // 
        this.lblBytes.AutoSize = true;
        this.lblBytes.Location = new System.Drawing.Point(16, 92);
        this.lblBytes.Name = "lblBytes";
        this.lblBytes.Size = new System.Drawing.Size(143, 17);
        this.lblBytes.TabIndex = 3;
        this.lblBytes.Text = "已接收：0 B / 0 B";
        // 
        // lblSpeed
        // 
        this.lblSpeed.AutoSize = true;
        this.lblSpeed.Location = new System.Drawing.Point(300, 92);
        this.lblSpeed.Name = "lblSpeed";
        this.lblSpeed.Size = new System.Drawing.Size(71, 17);
        this.lblSpeed.TabIndex = 4;
        this.lblSpeed.Text = "速度：--";
        // 
        // lblTime
        // 
        this.lblTime.AutoSize = true;
        this.lblTime.Location = new System.Drawing.Point(500, 92);
        this.lblTime.Name = "lblTime";
        this.lblTime.Size = new System.Drawing.Size(155, 17);
        this.lblTime.TabIndex = 5;
        this.lblTime.Text = "已用：--    剩余：--";
        // 
        // lblCallback
        // 
        this.lblCallback.AutoSize = true;
        this.lblCallback.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(100)))), ((int)(((byte)(160)))));
        this.lblCallback.Location = new System.Drawing.Point(16, 120);
        this.lblCallback.Name = "lblCallback";
        this.lblCallback.Size = new System.Drawing.Size(203, 17);
        this.lblCallback.TabIndex = 6;
        this.lblCallback.Text = "进度回调函数已被调用 0 次";
        // 
        // grpLog
        // 
        this.grpLog.Controls.Add(this.txtLog);
        this.grpLog.Location = new System.Drawing.Point(12, 284);
        this.grpLog.Name = "grpLog";
        this.grpLog.Size = new System.Drawing.Size(716, 304);
        this.grpLog.TabIndex = 2;
        this.grpLog.TabStop = false;
        this.grpLog.Text = "运行日志";
        // 
        // txtLog
        // 
        this.txtLog.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(250)))), ((int)(((byte)(250)))), ((int)(((byte)(250)))));
        this.txtLog.Font = new System.Drawing.Font("Consolas", 9F);
        this.txtLog.Location = new System.Drawing.Point(16, 26);
        this.txtLog.Multiline = true;
        this.txtLog.Name = "txtLog";
        this.txtLog.ReadOnly = true;
        this.txtLog.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
        this.txtLog.Size = new System.Drawing.Size(684, 266);
        this.txtLog.TabIndex = 0;
        this.txtLog.WordWrap = false;
        // 
        // MainForm
        // 
        this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 17F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(740, 600);
        this.Controls.Add(this.grpLog);
        this.Controls.Add(this.grpProgress);
        this.Controls.Add(this.grpServer);
        this.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
        this.MinimumSize = new System.Drawing.Size(756, 639);
        this.Name = "MainForm";
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
        this.Text = "TCP 文件传输服务器 —— 大文件传输 + 进度回调演示";
        this.grpServer.ResumeLayout(false);
        this.grpServer.PerformLayout();
        this.grpProgress.ResumeLayout(false);
        this.grpProgress.PerformLayout();
        this.grpLog.ResumeLayout(false);
        this.grpLog.PerformLayout();
        this.ResumeLayout(false);
    }
}
