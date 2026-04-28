using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using DbProcedureCaller.API;
using DbProcedureCaller.Core;

namespace DbProcedureCaller
{
    public class ModularMainForm : Form
    {
        private HttpListener httpListener;
        private Thread httpServerThread;
        private NotifyIcon notifyIcon;
        private string serverPort;
        private ApiHandler apiHandler;

        public ModularMainForm()
        {
            InitializeComponent();
            this.Text = "统计系统";
            InitializeTrayIcon();
            LogHelper.Init();
            apiHandler = new ApiHandler();
            LoadServerConfig();
        }

        private void LoadServerConfig()
        {
            try
            {
                string exeDir = Path.GetDirectoryName(Application.ExecutablePath);
                string configFile = Path.Combine(exeDir, "server_config.dat");

                if (!File.Exists(configFile))
                {
                    string rootConfigFile = Path.Combine(exeDir, "..", "..", "..", "server_config.dat");
                    rootConfigFile = Path.GetFullPath(rootConfigFile);
                    if (File.Exists(rootConfigFile))
                    {
                        configFile = rootConfigFile;
                    }
                }

                if (!File.Exists(configFile))
                {
                    string cwdConfigFile = Path.Combine(Environment.CurrentDirectory, "server_config.dat");
                    if (File.Exists(cwdConfigFile))
                    {
                        configFile = cwdConfigFile;
                    }
                }

                LogHelper.LogInfo($"尝试加载配置文件: {configFile}");

                if (File.Exists(configFile))
                {
                    string encrypted = File.ReadAllText(configFile).Trim();
                    string decrypted = Decrypt(encrypted);
                    LogHelper.LogInfo($"解密后内容: {decrypted}");
                    if (!string.IsNullOrEmpty(decrypted))
                    {
                        serverPort = decrypted.Trim();
                        LogHelper.LogInfo($"从配置文件加载端口: {serverPort}");
                    }
                }
                else
                {
                    LogHelper.LogInfo("配置文件不存在");
                }

                if (string.IsNullOrEmpty(serverPort))
                {
                    serverPort = Config.AppConstants.DefaultPort;
                    LogHelper.LogInfo("使用默认端口: " + serverPort);
                }
            }
            catch (Exception ex)
            {
                serverPort = Config.AppConstants.DefaultPort;
                LogHelper.LogException(ex, "加载端口配置失败");
            }
        }

        private string Decrypt(string encrypted)
        {
            try
            {
                byte[] data = Convert.FromBase64String(encrypted);
                return Encoding.UTF8.GetString(data);
            }
            catch
            {
                return string.Empty;
            }
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            EnsureDatabaseConfigured();
            StartHttpServer();
        }

        private void EnsureDatabaseConfigured()
        {
            string configFile = @"d:\AI\tran\config.dat";
            if (!File.Exists(configFile))
            {
                LogHelper.LogInfo("数据库配置文件不存在，启动时弹出配置窗口...");
                
                try
                {
                    string connStr = DbProcedureCaller.Config.ConnectionStrings.PromptForConnectionString();
                    if (!string.IsNullOrEmpty(connStr))
                    {
                        LogHelper.LogInfo("数据库配置完成");
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.LogError($"显示配置窗口失败: {ex.Message}");
                }
            }
            else
            {
                LogHelper.LogInfo("数据库配置文件已存在");
            }
        }

        private void HttpServerStarted()
        {
            string localIP = GetLocalIP();
            string message = string.Format("统计服务器已启动！本地访问：http://localhost:{0}/, 网络访问：http://{1}:{0}/", serverPort, localIP);
            LogHelper.LogInfo(message);
            UpdateStatusLabel(message);
        }

        private void StartHttpServer()
        {
            try
            {
                StartHttpServerWithRetry();
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "启动HTTP服务器失败");
                UpdateStatusLabel("启动HTTP服务器失败: " + ex.Message);
                MessageBox.Show("启动HTTP服务器失败: " + ex.Message, "启动错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void StartHttpServerWithRetry()
        {
            int port = int.Parse(serverPort);
            int maxRetries = 2;
            bool started = false;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    LogHelper.LogInfo("开始启动HTTP服务器...");
                    LogHelper.LogInfo("使用端口: " + serverPort);
                    UpdateStatusLabel("开始启动HTTP服务器...");
                    UpdateStatusLabel("使用端口: " + serverPort);

                    string url1 = string.Format("http://localhost:{0}/", serverPort);
                    string url2 = string.Format("http://127.0.0.1:{0}/", serverPort);

                    httpListener = new HttpListener();
                    httpListener.Prefixes.Add(url1);
                    httpListener.Prefixes.Add(url2);
                    LogHelper.LogInfo("添加HTTP前缀完成");
                    UpdateStatusLabel("添加HTTP前缀完成");
                    httpListener.Start();
                    LogHelper.LogInfo("HTTP服务器启动成功");
                    UpdateStatusLabel("HTTP服务器启动成功");

                    httpServerThread = new Thread(HttpServerThread);
                    httpServerThread.IsBackground = true;
                    httpServerThread.Start();
                    LogHelper.LogInfo("HTTP服务器线程已启动");
                    UpdateStatusLabel("HTTP服务器线程已启动");

                    HttpServerStarted();
                    started = true;
                    break;
                }
                catch (HttpListenerException ex) when (ex.ErrorCode == 5 || ex.Message.Contains("conflicts with an existing registration"))
                {
                    if (attempt == maxRetries)
                    {
                        throw new Exception($"端口 {serverPort} 被占用，无法启动服务器");
                    }

                    LogHelper.LogInfo($"端口 {serverPort} 被占用，尝试结束占用进程...");
                    UpdateStatusLabel($"端口 {serverPort} 被占用，尝试结束占用进程...");

                    bool killed = KillProcessUsingPort(port);
                    if (killed)
                    {
                        MessageBox.Show($"端口 {serverPort} 被占用，已自动结束占用进程，正在重试...", "端口占用", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        System.Threading.Thread.Sleep(1000);
                    }
                    else
                    {
                        throw new Exception($"端口 {serverPort} 被占用，无法结束占用进程");
                    }
                }
                catch (Exception ex)
                {
                    throw;
                }
            }

            if (!started)
            {
                throw new Exception($"端口 {serverPort} 启动失败");
            }
        }

        private bool KillProcessUsingPort(int port)
        {
            try
            {
                System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo();
                psi.FileName = "netstat";
                psi.Arguments = $"-ano | findstr :{port}";
                psi.RedirectStandardOutput = true;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;

                bool foundSystemProcess = false;

                using (System.Diagnostics.Process process = System.Diagnostics.Process.Start(psi))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    if (!string.IsNullOrEmpty(output))
                    {
                        string[] lines = output.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string line in lines)
                        {
                            string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 5)
                            {
                                string pidStr = parts[4].Trim();
                                if (int.TryParse(pidStr, out int pid))
                                {
                                    try
                                    {
                                        System.Diagnostics.Process p = System.Diagnostics.Process.GetProcessById(pid);
                                        if (p.ProcessName != "System")
                                        {
                                            p.Kill();
                                            p.WaitForExit(3000);
                                            LogHelper.LogInfo($"已结束进程 {pid} ({p.ProcessName})");
                                            return true;
                                        }
                                        else
                                        {
                                            LogHelper.LogInfo($"进程 {pid} 是系统进程，尝试释放HTTP.sys端口...");
                                            foundSystemProcess = true;
                                        }
                                    }
                                    catch
                                    {
                                        continue;
                                    }
                                }
                            }
                        }
                    }
                }

                if (foundSystemProcess)
                {
                    return ReleaseHttpSysPort(port);
                }

                LogHelper.LogInfo("未找到占用端口的进程");
                return false;
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "结束占用端口进程失败");
                return false;
            }
        }

        private bool ReleaseHttpSysPort(int port)
        {
            try
            {
                LogHelper.LogInfo($"尝试释放HTTP.sys端口 {port}...");
                
                System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo();
                psi.FileName = "netsh";
                psi.Arguments = $"http delete urlacl url=http://*:{port}/";
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.Verb = "runas";

                using (System.Diagnostics.Process process = System.Diagnostics.Process.Start(psi))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    
                    if (process.ExitCode == 0)
                    {
                        LogHelper.LogInfo($"HTTP.sys端口 {port} 释放成功");
                        return true;
                    }
                    else
                    {
                        LogHelper.LogInfo($"HTTP.sys端口释放失败: {error}");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogException(ex, "释放HTTP.sys端口失败");
                return false;
            }
        }

        private void HttpServerThread()
        {
            try
            {
                while (httpListener.IsListening)
                {
                    HttpListenerContext context = httpListener.GetContext();
                    HttpListenerRequest request = context.Request;
                    HttpListenerResponse response = context.Response;

                    response.AddHeader("Access-Control-Allow-Origin", "*");
                    response.AddHeader("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
                    response.AddHeader("Access-Control-Allow-Headers", "Content-Type, Authorization");

                    if (request.HttpMethod == "OPTIONS")
                    {
                        response.StatusCode = 200;
                        response.ContentLength64 = 0;
                        response.OutputStream.Close();
                        continue;
                    }

                    string url = request.Url.LocalPath;
                    string httpMethod = request.HttpMethod;
                    byte[] responseBytes = GetHtmlContent(url, request);

                    if (IsJsonEndpoint(url, httpMethod))
                    {
                        response.ContentType = "application/json; charset=utf-8";
                    }
                    else
                    {
                        response.ContentType = "text/html; charset=utf-8";
                    }

                    response.ContentLength64 = responseBytes.Length;
                    response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
                    response.OutputStream.Close();
                }
            }
            catch (Exception ex)
            {
                if (!(ex is ThreadAbortException))
                {
                    UpdateStatusLabel("HTTP服务器错误: " + ex.Message);
                }
            }
        }

        private bool IsJsonEndpoint(string url, string httpMethod = "GET")
        {
            if (url == "/login" && httpMethod == "GET")
                return false;
            
            if (url == "/login" && httpMethod == "POST")
                return true;

            return url == "/call-procedure" ||
                   url == "/get-users" ||
                   url == "/get-port" ||
                   url == "/set-port" ||
                   url == "/get-system-types" ||
                   url == "/get-stat-menu" ||
                   url == "/get-stat-page-config" ||
                   url.StartsWith("/get-reporters") ||
                   url.StartsWith("/get-reviewers") ||
                   url.StartsWith("/get-categories") ||
                   url.StartsWith("/get-departments") ||
                   url.StartsWith("/get-patient-types") ||
                   url.StartsWith("/get-result-status") ||
                   url == "/get-hospital-info" ||
                   url == "/daily-analysis" ||
                   url == "/department-statistics" ||
                   url == "/doctor-statistics" ||
                   url == "/category-statistics" ||
                   url == "/add-user" ||
                   url == "/update-user" ||
                   url == "/delete-user" ||
                   url.StartsWith("/api/");
        }

        private byte[] GetHtmlContent(string url, HttpListenerRequest request)
        {
            if (IsJsonEndpoint(url, request.HttpMethod))
            {
                return apiHandler.HandleRequest(url, request.HttpMethod, request.InputStream);
            }

            if (url == "/" || url == "/index.html")
            {
                string templatePath = Path.Combine(Application.StartupPath, "templates", "index.html");
                if (File.Exists(templatePath))
                {
                    return Encoding.UTF8.GetBytes(File.ReadAllText(templatePath));
                }
            }

            string fileName = url.TrimStart('/');
            if (!fileName.EndsWith(".html") && !fileName.EndsWith(".htm") && !fileName.EndsWith(".css") && !fileName.EndsWith(".js"))
            {
                fileName += ".html";
            }
            string filePath = Path.Combine(Application.StartupPath, "templates", fileName);
            if (File.Exists(filePath))
            {
                return Encoding.UTF8.GetBytes(File.ReadAllText(filePath));
            }

            string notFoundHtml = @"<!DOCTYPE html>
<html lang='zh-CN'>
<head>
    <meta charset='UTF-8'>
    <title>404 Not Found</title>
</head>
<body>
    <h1>404 Not Found</h1>
    <p>您访问的页面不存在</p>
    <a href='/'>返回首页</a>
</body>
</html>";
            return Encoding.UTF8.GetBytes(notFoundHtml);
        }

        private string GetLocalIP()
        {
            try
            {
                var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        return ip.ToString();
                    }
                }
                return "127.0.0.1";
            }
            catch
            {
                return "127.0.0.1";
            }
        }

        private void UpdateStatusLabel(string message)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<string>(UpdateStatusLabel), message);
            }
            else
            {
                lblStatus.Text = message;
            }
        }

        private void InitializeTrayIcon()
        {
            notifyIcon = new NotifyIcon();
            notifyIcon.Icon = SystemIcons.Application;
            notifyIcon.Text = "统计系统";
            notifyIcon.Visible = true;
            notifyIcon.MouseClick += new MouseEventHandler(NotifyIcon_MouseClick);

            ContextMenuStrip contextMenu = new ContextMenuStrip();
            ToolStripMenuItem showMenuItem = new ToolStripMenuItem("显示");
            showMenuItem.Click += new EventHandler(ShowMenuItem_Click);
            ToolStripMenuItem exitMenuItem = new ToolStripMenuItem("退出");
            exitMenuItem.Click += new EventHandler(ExitMenuItem_Click);
            contextMenu.Items.Add(showMenuItem);
            contextMenu.Items.Add(exitMenuItem);
            notifyIcon.ContextMenuStrip = contextMenu;
        }

        private void NotifyIcon_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                this.Show();
                this.WindowState = FormWindowState.Normal;
                this.BringToFront();
            }
        }

        private void ShowMenuItem_Click(object sender, EventArgs e)
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.BringToFront();
        }

        private void ExitMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
            }
        }

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            StopHttpServer();
            if (notifyIcon != null)
            {
                notifyIcon.Dispose();
            }
        }

        private void StopHttpServer()
        {
            try
            {
                if (httpListener != null && httpListener.IsListening)
                {
                    httpListener.Stop();
                    httpListener.Close();
                }

                if (httpServerThread != null && httpServerThread.IsAlive)
                {
                    httpServerThread.Interrupt();
                }
            }
            catch (Exception ex)
            {
                UpdateStatusLabel("停止HTTP服务器失败: " + ex.Message);
            }
        }

        private void btnExit_Click(object sender, EventArgs e)
        {
            this.Hide();
        }

        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.Button btnExit;

        private void InitializeComponent()
        {
            this.lblStatus = new System.Windows.Forms.Label();
            this.btnExit = new System.Windows.Forms.Button();
            this.SuspendLayout();
            this.lblStatus.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblStatus.Location = new System.Drawing.Point(10, 20);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(480, 40);
            this.lblStatus.TabIndex = 0;
            this.lblStatus.Text = "统计服务器正在启动...";
            this.btnExit.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnExit.Location = new System.Drawing.Point(200, 80);
            this.btnExit.Name = "btnExit";
            this.btnExit.Size = new System.Drawing.Size(100, 35);
            this.btnExit.TabIndex = 1;
            this.btnExit.Text = "退出";
            this.btnExit.UseVisualStyleBackColor = true;
            this.btnExit.Click += new System.EventHandler(this.btnExit_Click);
            this.ClientSize = new System.Drawing.Size(500, 130);
            this.Controls.Add(this.btnExit);
            this.Controls.Add(this.lblStatus);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ModularMainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.MainForm_FormClosed);
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.ResumeLayout(false);
        }
    }

    static class ModularProgram
    {
        [STAThread]
        static void Main()
        {
            bool createdNew;
            using (System.Threading.Mutex mutex = new System.Threading.Mutex(true, "DbProcedureCallerModularMutex", out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show("程序已经在运行中，不能启动多个实例。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                try
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(new ModularMainForm());
                }
                catch (Exception ex)
                {
                    LogHelper.LogException(ex, "程序启动失败");
                    MessageBox.Show("程序启动失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}