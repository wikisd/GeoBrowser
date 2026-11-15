using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace MyGeoBrowser2
{
    public class ScrapedMediaItem
    {
        public string Title { get; set; } = "";
        public string Url { get; set; } = "";
        public string MediaType { get; set; } = Lang.T("帖子/视频", "Post/Video");
    }

    public class DownloadManagerForm : Form
    {
        private Button btnStartChecked;
        private Button btnUpdatePlugins;
        private TextBox txtManualUrls;
        private ListView listTasks;
        private TextBox txtLog;
        private ComboBox cmbEngineMode;
        private Label lblPluginStatus;
        private string downloadDir;
        private string proxyUrl;
        private bool isRunning = false;
        private Func<Task<List<ScrapedMediaItem>>>? scrapeCallback;
        public event Action<string, bool>? OnTaskFinished;

        public void ForceShutdown()
        {
            this.FormClosing -= DownloadManagerForm_FormClosing;
            this.Close();
            this.Dispose();
        }

        public DownloadManagerForm(string saveFolder, string currentProxy, Func<Task<List<ScrapedMediaItem>>>? callback = null)
        {
            this.downloadDir = saveFolder;
            this.proxyUrl = currentProxy;
            this.scrapeCallback = callback;
            this.Text = Lang.T("精准多媒体下载管理器 (已启用去广告与纯净提取)", "Precision Media Download Manager (Ad-Free & Clean Extract)");
            this.Size = new Size(1060, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            Icon? exeIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (exeIcon != null) this.Icon = exeIcon;

            Panel pluginBar = new Panel { Dock = DockStyle.Top, Height = 38, BackColor = Color.FromArgb(240, 248, 255), Padding = new Padding(8, 5, 8, 5) };
            lblPluginStatus = new Label { Text = Lang.T("正在检查组件状态...", "Checking plugin status..."), AutoSize = true, Dock = DockStyle.Left, Font = new Font("Segoe UI", 9f, FontStyle.Bold), Padding = new Padding(0, 4, 10, 0) };
            btnUpdatePlugins = new Button { Text = Lang.T("一键更新 yt-dlp 和 gallery-dl", "Update yt-dlp & gallery-dl"), Width = 210, Dock = DockStyle.Right, BackColor = Color.FromArgb(111, 66, 193), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnUpdatePlugins.Click += async (s, e) => await UpdateAllPluginsAsync();
            pluginBar.Controls.Add(btnUpdatePlugins);
            pluginBar.Controls.Add(lblPluginStatus);

            Panel manualPanel = new Panel { Dock = DockStyle.Top, Height = 68, Padding = new Padding(5) };
            Label lblManual = new Label { Text = Lang.T("手动添加链接（可选，支持批量粘贴多行链接）：", "Manual URLs (Optional, supports multiple lines):"), Dock = DockStyle.Top, Height = 20, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            txtManualUrls = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9.5f) };
            Button btnAddManual = new Button { Text = Lang.T("添加到下方列表", "Add to List"), Width = 120, Dock = DockStyle.Right };
            btnAddManual.Click += (s, e) => AddManualTextToList();
            manualPanel.Controls.Add(txtManualUrls);
            manualPanel.Controls.Add(btnAddManual);
            manualPanel.Controls.Add(lblManual);

            Panel actionPanel = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Color.FromArgb(248, 249, 250), Padding = new Padding(5) };
            Button btnScrapeClean = new Button { Text = Lang.T("⚡ 精准抓取当前页纯净内容", "⚡ Extract Current Page"), Width = 195, Dock = DockStyle.Left, BackColor = Color.FromArgb(23, 162, 184), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            Button btnSelectAll = new Button { Text = Lang.T("全选", "Select All"), Width = 55, Dock = DockStyle.Left };
            Button btnUnselectAll = new Button { Text = Lang.T("全不选", "Deselect All"), Width = 65, Dock = DockStyle.Left };
            Button btnRemoveUnchecked = new Button { Text = Lang.T("移除未勾选", "Remove Unchecked"), Width = 85, Dock = DockStyle.Left };
            btnStartChecked = new Button { Text = Lang.T("▶ 下载已勾选项", "▶ Download Selected"), Width = 125, Dock = DockStyle.Left, BackColor = Color.FromArgb(0, 120, 215), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };

            cmbEngineMode = new ComboBox { Width = 190, Dock = DockStyle.Right, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9f) };
            cmbEngineMode.Items.Add(Lang.T("自动识别引擎", "Auto Detect Engine"));
            cmbEngineMode.Items.Add(Lang.T("仅用 yt-dlp (视频)", "yt-dlp Only (Video)"));
            cmbEngineMode.Items.Add(Lang.T("仅用 gallery-dl (图集)", "gallery-dl Only (Gallery)"));
            cmbEngineMode.SelectedIndex = 0;

            Button btnOpenFolder = new Button { Text = Lang.T("打开下载文件夹", "Open Folder"), Width = 115, Dock = DockStyle.Right };

            btnScrapeClean.Click += async (s, e) => await ScrapeCleanItemsAsync();
            btnSelectAll.Click += (s, e) => { foreach (ListViewItem item in listTasks.Items) item.Checked = true; };
            btnUnselectAll.Click += (s, e) => { foreach (ListViewItem item in listTasks.Items) item.Checked = false; };
            btnRemoveUnchecked.Click += (s, e) => { foreach (ListViewItem item in listTasks.Items.Cast<ListViewItem>().Where(i => !i.Checked).ToList()) listTasks.Items.Remove(item); };
            btnStartChecked.Click += async (s, e) => await StartCheckedTasksAsync();
            btnOpenFolder.Click += (s, e) => Process.Start("explorer.exe", downloadDir);

            actionPanel.Controls.Add(btnOpenFolder);
            actionPanel.Controls.Add(cmbEngineMode);
            actionPanel.Controls.Add(btnStartChecked);
            actionPanel.Controls.Add(btnRemoveUnchecked);
            actionPanel.Controls.Add(btnUnselectAll);
            actionPanel.Controls.Add(btnSelectAll);
            actionPanel.Controls.Add(btnScrapeClean);

            listTasks = new ListView { View = View.Details, CheckBoxes = true, FullRowSelect = true, GridLines = true, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9.5f) };
            listTasks.Columns.Add(Lang.T("勾选 / 标题摘要", "Select / Title"), 260);
            listTasks.Columns.Add(Lang.T("精准链接", "Target URL"), 380);
            listTasks.Columns.Add(Lang.T("状态", "Status"), 140);
            listTasks.Columns.Add(Lang.T("类型", "Type"), 130);

            txtLog = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Bottom, Height = 135, BackColor = Color.Black, ForeColor = Color.LightGreen, Font = new Font("Consolas", 9f) };

            this.Controls.Add(listTasks);
            this.Controls.Add(txtLog);
            this.Controls.Add(actionPanel);
            this.Controls.Add(manualPanel);
            this.Controls.Add(pluginBar);

            CheckPluginsStatus();
            this.FormClosing += DownloadManagerForm_FormClosing;
        }

        private void DownloadManagerForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide(); // 点击右上角 × 时仅隐藏窗口，后台继续静默下载
            }
        }

        private void CheckPluginsStatus()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            bool hasYt = File.Exists(Path.Combine(baseDir, "yt-dlp.exe"));
            bool hasFf = File.Exists(Path.Combine(baseDir, "ffmpeg.exe"));
            bool hasGl = File.Exists(Path.Combine(baseDir, "gallery-dl.exe"));

            lblPluginStatus.Text = Lang.T(
                $"组件检测: yt-dlp {(hasYt ? "✅" : "❌")} | ffmpeg {(hasFf ? "✅" : "❌")} | gallery-dl {(hasGl ? "✅" : "❌")}",
                $"Plugins: yt-dlp {(hasYt ? "✅" : "❌")} | ffmpeg {(hasFf ? "✅" : "❌")} | gallery-dl {(hasGl ? "✅" : "❌")}"
            );
        }


        public void UpdateProxy(string newProxy)
        {
            this.proxyUrl = newProxy;
            CheckPluginsStatus();
        }

        public void AddItemToList(string title, string url, string mediaType, bool autoChecked)
        {
            foreach (ListViewItem existing in listTasks.Items)
            {
                if (existing.SubItems.Count > 1 && existing.SubItems.Cast<ListViewItem.ListViewSubItem>().ElementAt(1).Text == url)
                    return;
            }
            var item = new ListViewItem(string.IsNullOrWhiteSpace(title) ? "单个媒体任务" : title);
            item.SubItems.Add(url);
            item.SubItems.Add(Lang.T("等待下载", "Pending"));
            item.SubItems.Add(mediaType);
            item.Checked = autoChecked;
            listTasks.Items.Add(item);
        }

        public async void AddSingleAndStartNow(string title, string url, string mediaType)
        {
            AddItemToList(title, url, mediaType, true);
            await StartCheckedTasksAsync();
        }

        private void AddManualTextToList()
        {
            var rawLines = txtManualUrls.Text.Split(new string[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in rawLines)
            {
                string cleanUrl = line.Trim();
                if (cleanUrl.StartsWith("http"))
                {
                    AddItemToList("手动添加", cleanUrl, DetectPlatform(cleanUrl), true);
                }
            }
            txtManualUrls.Clear();
        }

        private async Task ScrapeCleanItemsAsync()
        {
            if (scrapeCallback == null) return;
            AppendLog(">>> 正在精准过滤广告与侧边栏推荐，提取主区域有效内容...");
            var items = await scrapeCallback.Invoke();
            if (items.Count == 0)
            {
                AppendLog("[提示] 当前主区域未找到可提取的帖子或视频（可先向下滚动加载后再试）。");
                return;
            }

            foreach (var media in items)
            {
                AddItemToList(media.Title, media.Url, media.MediaType, true);
            }
            AppendLog($"[成功] 已过滤广告与无关推荐，精准提取 {items.Count} 条纯净内容，请在列表中勾选需要下载的项目。");
        }

        private async Task UpdateAllPluginsAsync()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            AppendLog(Lang.T("=== 开始通过系统 curl + 代理更新核心组件 ===", "=== Updating core plugins via curl + proxy ==="));

            await DownloadWithCurlAsync(
                "yt-dlp",
                "https://github.com",
                Path.Combine(baseDir, "yt-dlp.exe")
            );

            await DownloadWithCurlAsync(
                "gallery-dl",
                "https://github.com",
                Path.Combine(baseDir, "gallery-dl.exe")
            );

            string ffmpegPath = Path.Combine(baseDir, "ffmpeg.exe");
            if (File.Exists(ffmpegPath))
            {
                AppendLog(Lang.T(">>> 正在检测 ffmpeg.exe 状态...", ">>> Checking ffmpeg.exe status..."));
                await RunProcessAsync(ffmpegPath, "-version");
            }
            else
            {
                AppendLog(Lang.T("[提示] 未找到 ffmpeg.exe，请手动放入 GPL 静态版。", "[Notice] ffmpeg.exe not found. Please place static GPL build."));
            }

            AppendLog(Lang.T("=== 组件更新与检测全部结束 ===", "=== Plugin update & check finished ==="));
            CheckPluginsStatus();
        }

        private async Task DownloadWithCurlAsync(string name, string downloadUrl, string targetPath)
        {
            try
            {
                AppendLog(Lang.T($">>> 正在连接 GitHub 获取最新版 {name}.exe ...", $">>> Connecting to GitHub for latest {name}.exe ..."));
                string tempPath = targetPath + ".new";
                if (File.Exists(tempPath)) File.Delete(tempPath);

                // 将 socks5:// 转换为 socks5h://，让代理服务器远程解析 GitHub 域名，彻底解决更新失败
                string fixedProxy = proxyUrl.Replace("socks5://", "socks5h://");
                string proxyArg = string.IsNullOrEmpty(fixedProxy) ? "" : $"-x \"{fixedProxy}\" ";

                string curlArgs = $"-L -s -S --fail {proxyArg}-o \"{tempPath}\" \"{downloadUrl}\"";
                bool ok = await RunProcessAsync("curl.exe", curlArgs);

                if (ok && File.Exists(tempPath) && new FileInfo(tempPath).Length > 1024 * 1024)
                {
                    long sizeMb = new FileInfo(tempPath).Length / 1024 / 1024;
                    if (File.Exists(targetPath)) File.Delete(targetPath);
                    File.Move(tempPath, targetPath);
                    AppendLog(Lang.T($"[成功] {name}.exe 已更新至官方最新版本！（大小：{sizeMb} MB）", $"[Success] {name}.exe updated to latest official version! ({sizeMb} MB)"));
                }
                else
                {
                    if (File.Exists(tempPath)) File.Delete(tempPath);
                    AppendLog(Lang.T($"[失败] {name}.exe 更新失败，请检查代理是否能正常访问 GitHub。", $"[Failed] {name}.exe update failed. Check proxy connection to GitHub."));
                }
            }
            catch (Exception ex)
            {
                AppendLog(Lang.T($"[异常] {name}.exe 更新出错：{ex.Message}", $"[Error] {name}.exe update error: {ex.Message}"));
            }
        }

        private string DetectPlatform(string url)
        {
            string lower = url.ToLower();
            if (lower.Contains("tiktok.com")) return "TikTok (TK)";
            if (lower.Contains("instagram.com")) return "Instagram";
            if (lower.Contains("facebook.com") || lower.Contains("fb.watch")) return "Facebook";
            if (lower.Contains("youtube.com") || lower.Contains("youtu.be")) return "YouTube";
            if (lower.Contains("x.com") || lower.Contains("twitter.com")) return "X (Twitter)";
            return "单张图片/直链";
        }


        private async Task StartCheckedTasksAsync()
        {
            if (isRunning) return;
            isRunning = true;

            try
            {
                int mode = cmbEngineMode.SelectedIndex;
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string ytdlpPath = Path.Combine(baseDir, "yt-dlp.exe");
                string galleryPath = Path.Combine(baseDir, "gallery-dl.exe");

                foreach (ListViewItem item in listTasks.Items)
                {
                    if (!item.Checked) continue;
                    var urlSub = item.SubItems.Cast<ListViewItem.ListViewSubItem>().ElementAt(1);
                    var statusSub = item.SubItems.Cast<ListViewItem.ListViewSubItem>().ElementAt(2);
                    var typeSub = item.SubItems.Cast<ListViewItem.ListViewSubItem>().ElementAt(3);

                    if (statusSub.Text == Lang.T("等待下载", "Pending") || statusSub.Text.Contains(Lang.T("失败", "Failed")))
                    {
                        string targetUrl = urlSub.Text;
                        // 从标题 "[博主名] 摘要" 中提取出博主账号名（如果没有则用 Unknown）
                        string extractedAuthor = "Unknown";
                        // 1. 优先从标题 "[博主名] 摘要" 中提取账号名
                        if (item.Text.StartsWith("[") && item.Text.Contains("]"))
                        {
                            int endIdx = item.Text.IndexOf(']');
                            if (endIdx > 1)
                            {
                                string rawAuthor = item.Text.Substring(1, endIdx - 1).Trim().TrimStart('@');
                                foreach (char c in Path.GetInvalidFileNameChars())
                                    rawAuthor = rawAuthor.Replace(c.ToString(), "");
                                if (!string.IsNullOrWhiteSpace(rawAuthor) && rawAuthor != "当前播放")
                                    extractedAuthor = rawAuthor;
                            }
                        }

                        // 2. 如果标题里没有账号名，直接从链接 URL 中解析（例如 ://tiktok.com...）
                        if (extractedAuthor == "Unknown")
                        {
                            try
                            {
                                Uri uri = new Uri(targetUrl);
                                string[] segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                                if (segments.Length > 0 && segments.GetValue(0)!.ToString()!.StartsWith("@"))
                                {
                                    extractedAuthor = segments.GetValue(0)!.ToString()!.TrimStart('@');
                                }
                            }
                            catch { }
                        }
                        statusSub.Text = Lang.T("正在下载...", "Downloading...");
                        AppendLog(Lang.T($">>> 开始精准下载 (归档账号: {extractedAuthor}): {targetUrl}", $">>> Starting download (Account: {extractedAuthor}): {targetUrl}"));

                        bool ok = false;
                        if (typeSub.Text == Lang.T("单张原图", "Single Image"))
                        {
                            ok = await DownloadDirectImageAsync(targetUrl, extractedAuthor);
                        }
                        else if (mode == 2)
                        {
                            ok = await RunGalleryDlAsync(galleryPath, targetUrl, extractedAuthor);
                        }
                        else if (mode == 1)
                        {
                            ok = await RunYtDlpAsync(ytdlpPath, targetUrl, extractedAuthor);
                        }
                        else
                        {
                            ok = await RunYtDlpAsync(ytdlpPath, targetUrl, extractedAuthor);
                            if (!ok && File.Exists(galleryPath))
                            {
                                AppendLog(Lang.T("[切换] 自动切换 gallery-dl 抓取高清原图...", "[Fallback] Switching to gallery-dl for original images..."));
                                ok = await RunGalleryDlAsync(galleryPath, targetUrl, extractedAuthor);
                            }
                        }

                        statusSub.Text = ok ? Lang.T("下载完成 ✅", "Completed ✅") : Lang.T("下载失败 ❌", "Failed ❌");
                        OnTaskFinished?.Invoke(item.Text, ok);
                        if (ok)
                        {
                            item.Checked = false;
                            int delaySec = new Random().Next(3, 7);
                            AppendLog(Lang.T($"[防风控保护] 安全等待 {delaySec} 秒后继续下一个任务...", $"[Rate-Limit Guard] Waiting {delaySec}s before next task..."));
                            await Task.Delay(delaySec * 1000);
                        }
                    }
                }
            }
            finally { isRunning = false; }
        }

        private async Task<bool> DownloadDirectImageAsync(string imageUrl, string authorName = "Unknown")
        {
            try
            {
                var handler = new HttpClientHandler();
                if (!string.IsNullOrEmpty(proxyUrl))
                {
                    handler.Proxy = new WebProxy(proxyUrl);
                    handler.UseProxy = true;
                }
                using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
                byte[] bytes = await client.GetByteArrayAsync(imageUrl);

                string ext = ".jpg";
                if (imageUrl.ToLower().Contains(".png")) ext = ".png";
                else if (imageUrl.ToLower().Contains(".webp")) ext = ".webp";

                string todayFolder = Path.Combine(downloadDir, "DirectImages", authorName, DateTime.Now.ToString("yyyy-MM-dd"));
                if (!Directory.Exists(todayFolder)) Directory.CreateDirectory(todayFolder);

                string fileName = $"Image_{DateTime.Now:HHmmss_fff}{ext}";
                string fullPath = Path.Combine(todayFolder, fileName);
                await File.WriteAllBytesAsync(fullPath, bytes);
                AppendLog("[成功] 单张原图已归档至: " + fullPath);
                return true;
            }
            catch (Exception ex)
            {
                AppendLog("[图片下载失败] " + ex.Message);
                return false;
            }
        }

        private async Task<bool> RunYtDlpAsync(string exePath, string url, string authorName = "Unknown")
        {
            if (!File.Exists(exePath)) { AppendLog("[错误] 缺少 yt-dlp.exe"); return false; }

            string baseDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
            string today = DateTime.Now.ToString("yyyy-MM-dd");

            // 如果网页已抓出账号名则直接用网页账号名；否则让 yt-dlp 自动识别，兜底使用 Unknown
            string authorTemplate = authorName != "Unknown" ? authorName : "%(uploader_id,uploader,channel|Unknown)s";
            string outputTemplate = Path.Combine(downloadDir, "%(extractor_key)s", authorTemplate, today, "%(title).40s_%(id)s.%(ext)s");

            string args = $"--no-warnings --no-playlist -N 4 --windows-filenames --ffmpeg-location \"{baseDir}\" -f \"bestvideo[ext=mp4]+bestaudio[ext=m4a]/bestvideo+bestaudio/best\" --merge-output-format mp4 -o \"{outputTemplate}\"";

            string cookieFile = Path.Combine(baseDir, "cookies.txt");
            if (File.Exists(cookieFile)) args += $" --cookies \"{cookieFile}\"";
            if (!string.IsNullOrEmpty(proxyUrl)) args += $" --proxy \"{proxyUrl}\"";
            args += $" \"{url}\"";

            return await RunProcessAsync(exePath, args);
        }

        private async Task<bool> RunGalleryDlAsync(string exePath, string url, string authorName = "Unknown")
        {
            if (!File.Exists(exePath)) { AppendLog("[错误] 缺少 gallery-dl.exe"); return false; }

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            string configPath = Path.Combine(baseDir, "gallery-dl.conf");

            // 优先使用网页已抓出的账号名作为文件夹，确保 Facebook 100% 生成 平台\账号名\日期
            string dirAuthorRule = authorName != "Unknown" ? authorName : "{author[name]|user[name]|account|uploader|'Unknown'}";

            var configObj = new
            {
                extractor = new Dictionary<string, object>
                {
                    { "base-directory", downloadDir },
                    { "directory", new List<string> { "{category}", dirAuthorRule, today } }
                }
            };

            string jsonConfig = System.Text.Json.JsonSerializer.Serialize(configObj, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(configPath, jsonConfig, new UTF8Encoding(false));

            string args = $"--config \"{configPath}\"";
            string cookieFile = Path.Combine(baseDir, "cookies.txt");
            if (File.Exists(cookieFile)) args += $" --cookies \"{cookieFile}\"";
            if (!string.IsNullOrEmpty(proxyUrl)) args += $" --proxy \"{proxyUrl}\"";
            args += $" \"{url}\"";

            return await RunProcessAsync(exePath, args);
        }

        private Task<bool> RunProcessAsync(string exePath, string args)
        {
            return Task.Run(() =>
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = exePath,
                        Arguments = args,
                        WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    };
                    using var proc = new Process { StartInfo = psi };
                    proc.OutputDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) AppendLog(e.Data); };
                    proc.ErrorDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) AppendLog(Lang.T("[状态] ", "[Info] ") + e.Data); };
                    proc.Start();
                    proc.BeginOutputReadLine();
                    proc.BeginErrorReadLine();
                    proc.WaitForExit();

                    if (proc.ExitCode == 0)
                    {
                        AppendLog(Lang.T("[完成] 音视频处理与合并已全部结束！", "[Done] Media processing & merging completed!"));
                        return true;
                    }
                    return false;
                }
                catch (Exception ex)
                {
                    AppendLog(Lang.T("[异常] ", "[Exception] ") + ex.Message);
                    return false;
                }
            });
        }

        private void AppendLog(string text)
        {
            if (txtLog.InvokeRequired)
            {
                txtLog.Invoke(new Action(() => AppendLog(text)));
                return;
            }
            txtLog.AppendText(text + Environment.NewLine);
        }
    }
}

