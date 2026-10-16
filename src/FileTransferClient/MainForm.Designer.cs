#nullable disable
namespace FileTransferClient;

partial class MainForm
{
    /// <summary>设计器所需的组件容器。</summary>
    private System.ComponentModel.IContainer components = null;

    private System.Windows.Forms.GroupBox grpServer;
    private System.Windows.Forms.Label lblHost;
    private System.Windows.Forms.TextBox txtHost;
    private System.Windows.Forms.Label lblPort;
    private System.Windows.Forms.TextBox txtPort;
    private System.Windows.Forms.Button btnSend;
    private System.Windows.Forms.Button btnCancel;
    private System.Windows.Forms.Label lblStatus;
    private System.Windows.Forms.Label lblSent;

    private System.Windows.Forms.GroupBox grpFile;
    private System.Windows.Forms.TextBox txtFile;
    private System.Windows.Forms.Button btnBrowse;
    private System.Windows.Forms.Label lblFileSize;

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
        this.lblHost = new System.Windows.Forms.Label();
        this.txtHost = new System.Windows.Forms.TextBox();
        this.lblPort = new System.Windows.Forms.Label();
        this.txtPort = new System.Windows.Forms.TextBox();
        this.btnSend = new System.Windows.Forms.Button();
        this.btnCancel = new System.Windows.Forms.Button();
        this.lblStatus = new System.Windows.Forms.Label();
        this.lblSent = new System.Windows.Forms.Label();
        this.grpFile = new System.Windows.Forms.GroupBox();
        this.txtFile = new System.Windows.Forms.TextBox();
        this.btnBrowse = new System.Windows.Forms.Button();
        this.lblFileSize = new System.Windows.Forms.Label();
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
        this.grpFile.SuspendLayout();
        this.grpProgress.SuspendLayout();
        this.grpLog.SuspendLayout();
        this.SuspendLayout();
        // 
        // grpServer
        // 
        this.grpServer.Controls.Add(this.lblSent);
        this.grpServer.Controls.Add(this.lblStatus);
        this.grpServer.Controls.Add(this.btnCancel);
        this.grpServer.Controls.Add(this.btnSend);
        this.grpServer.Controls.Add(this.txtPort);
        this.grpServer.Controls.Add(this.lblPort);
        this.grpServer.Controls.Add(this.txtHost);
        this.grpServer.Controls.Add(this.lblHost);
        this.grpServer.Location = new System.Drawing.Point(12, 12);
        this.grpServer.Name = "grpServer";
        this.grpServer.Size = new System.Drawing.Size(716, 104);
        this.grpServer.TabIndex = 0;
        this.grpServer.TabStop = false;
        this.grpServer.Text = "服务器设置";
        // 
        // lblHost
        // 
        this.lblHost.AutoSize = true;
        this.lblHost.Location = new System.Drawing.Point(16, 32);
        this.lblHost.Name = "lblHost";
        this.lblHost.Size = new System.Drawing.Size(76, 17);
        this.lblHost.TabIndex = 0;
        this.lblHost.Text = "服务器 IP：";
        // 
        // txtHost
        // 
        this.txtHost.Location = new System.Drawing.Point(100, 29);
        this.txtHost.Name = "txtHost";
        this.txtHost.Size = new System.Drawing.Size(140, 23);
        this.txtHost.TabIndex = 1;
        this.txtHost.Text = "127.0.0.1";
        // 
        // lblPort
        // 
        this.lblPort.AutoSize = true;
        this.lblPort.Location = new System.Drawing.Point(256, 32);
        this.lblPort.Name = "lblPort";
        this.lblPort.Size = new System.Drawing.Size(47, 17);
        this.lblPort.TabIndex = 2;
        this.lblPort.Text = "端口：";
        // 
        // txtPort
        // 
        this.txtPort.Location = new System.Drawing.Point(308, 29);
        this.txtPort.Name = "txtPort";
        this.txtPort.Size = new System.Drawing.Size(70, 23);
        this.txtPort.TabIndex = 3;
        this.txtPort.Text = "6000";
        // 
        // btnSend
        // 
        this.btnSend.Location = new System.Drawing.Point(400, 27);
        this.btnSend.Name = "btnSend";
        this.btnSend.Size = new System.Drawing.Size(124, 28);
        this.btnSend.TabIndex = 4;
        this.btnSend.Text = "连接并发送";
        this.btnSend.UseVisualStyleBackColor = true;
        this.btnSend.Click += new System.EventHandler(this.BtnSend_Click);
        // 
        // btnCancel
        // 
        this.btnCancel.Enabled = false;
        this.btnCancel.Location = new System.Drawing.Point(534, 27);
        this.btnCancel.Name = "btnCancel";
        this.btnCancel.Size = new System.Drawing.Size(96, 28);
        this.btnCancel.TabIndex = 5;
        this.btnCancel.Text = "取消";
        this.btnCancel.UseVisualStyleBackColor = true;
        this.btnCancel.Click += new System.EventHandler(this.BtnCancel_Click);
        // 
        // lblStatus
        // 
        this.lblStatus.AutoSize = true;
        this.lblStatus.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(100)))), ((int)(((byte)(160)))));
        this.lblStatus.Location = new System.Drawing.Point(16, 70);
        this.lblStatus.Name = "lblStatus";
        this.lblStatus.Size = new System.Drawing.Size(71, 17);
        this.lblStatus.TabIndex = 6;
        this.lblStatus.Text = "状态：就绪";
        // 
        // lblSent
        // 
        this.lblSent.AutoSize = true;
        this.lblSent.Location = new System.Drawing.Point(400, 70);
        this.lblSent.Name = "lblSent";
        this.lblSent.Size = new System.Drawing.Size(167, 17);
        this.lblSent.TabIndex = 7;
        this.lblSent.Text = "本次会话已发送 0 个文件";
        // 
        // grpFile
        // 
        this.grpFile.Controls.Add(this.lblFileSize);
        this.grpFile.Controls.Add(this.btnBrowse);
        this.grpFile.Controls.Add(this.txtFile);
        this.grpFile.Location = new System.Drawing.Point(12, 124);
        this.grpFile.Name = "grpFile";
        this.grpFile.Size = new System.Drawing.Size(716, 72);
        this.grpFile.TabIndex = 1;
        this.grpFile.TabStop = false;
        this.grpFile.Text = "待发送文件（也可以直接把文件拖到窗口里）";
        // 
        // txtFile
        // 
        this.txtFile.Location = new System.Drawing.Point(16, 26);
        this.txtFile.Name = "txtFile";
        this.txtFile.Size = new System.Drawing.Size(560, 23);
        this.txtFile.TabIndex = 0;
        // 
        // btnBrowse
        // 
        this.btnBrowse.Location = new System.Drawing.Point(584, 25);
        this.btnBrowse.Name = "btnBrowse";
        this.btnBrowse.Size = new System.Drawing.Size(116, 26);
        this.btnBrowse.TabIndex = 1;
        this.btnBrowse.Text = "选择文件...";
        this.btnBrowse.UseVisualStyleBackColor = true;
        this.btnBrowse.Click += new System.EventHandler(this.BtnBrowse_Click);
        // 
        // lblFileSize
        // 
        this.lblFileSize.AutoSize = true;
        this.lblFileSize.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(90)))), ((int)(((byte)(90)))));
        this.lblFileSize.Location = new System.Drawing.Point(16, 52);
        this.lblFileSize.Name = "lblFileSize";
        this.lblFileSize.Size = new System.Drawing.Size(119, 17);
        this.lblFileSize.TabIndex = 2;
        this.lblFileSize.Text = "文件大小：未选择";
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
        this.grpProgress.Location = new System.Drawing.Point(12, 204);
        this.grpProgress.Name = "grpProgress";
        this.grpProgress.Size = new System.Drawing.Size(716, 150);
        this.grpProgress.TabIndex = 2;
        this.grpProgress.TabStop = false;
        this.grpProgress.Text = "发送进度（由进度回调函数驱动）";
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
        this.lblBytes.Text = "已发送：0 B / 0 B";
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
        this.grpLog.Location = new System.Drawing.Point(12, 362);
        this.grpLog.Name = "grpLog";
        this.grpLog.Size = new System.Drawing.Size(716, 226);
        this.grpLog.TabIndex = 3;
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
        this.txtLog.Size = new System.Drawing.Size(684, 188);
        this.txtLog.TabIndex = 0;
        this.txtLog.WordWrap = false;
        // 
        // MainForm
        // 
        this.AllowDrop = true;
        this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 17F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(740, 600);
        this.Controls.Add(this.grpLog);
        this.Controls.Add(this.grpProgress);
        this.Controls.Add(this.grpFile);
        this.Controls.Add(this.grpServer);
        this.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
        this.MinimumSize = new System.Drawing.Size(756, 639);
        this.Name = "MainForm";
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
        this.Text = "TCP 文件传输客户端 —— 大文件传输 + 进度回调演示";
        this.DragDrop += new System.Windows.Forms.DragEventHandler(this.MainForm_DragDrop);
        this.DragEnter += new System.Windows.Forms.DragEventHandler(this.MainForm_DragEnter);
        this.grpServer.ResumeLayout(false);
        this.grpServer.PerformLayout();
        this.grpFile.ResumeLayout(false);
        this.grpFile.PerformLayout();
        this.grpProgress.ResumeLayout(false);
        this.grpProgress.PerformLayout();
        this.grpLog.ResumeLayout(false);
        this.grpLog.PerformLayout();
        this.ResumeLayout(false);
    }
}
