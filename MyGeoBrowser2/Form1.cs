using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace MyGeoBrowser2
{
    public partial class Form1 : Form
    {
        private TabControl tabControl;
        private TextBox urlBox;
        private CheckBox chkUseProxy;
        private ComboBox cmbProxyType;
        private TextBox txtProxyAddress;
        private Label lblStatus;

        private CoreWebView2Environment? sharedEnv;
        private double currentLat = 34.0522;
        private double currentLon = -118.2437;
        private string currentTimezone = "America/Los_Angeles";

        private string configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "proxy_config.txt");
        private string downloadFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Downloads");
        private DownloadManagerForm? downloadForm;

        public Form1()
        {
            this.Text = Lang.T("Geo Browser - 全能媒体下载版 (INS / FB / YT / X / TK)", "Geo Browser - Media Downloader Edition (INS / FB / YT / X / TK)");
            this.Size = new Size(1500, 920);
            this.StartPosition = FormStartPosition.CenterScreen;
            Icon? exeIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (exeIcon != null) this.Icon = exeIcon;
            if (!Directory.Exists(downloadFolder)) Directory.CreateDirectory(downloadFolder);

            Panel navBar = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Color.FromArgb(245, 245, 245), Padding = new Padding(5) };

            Button btnBack = new Button { Text = "←", Width = 32, Dock = DockStyle.Left };
            Button btnRefresh = new Button { Text = "↻", Width = 32, Dock = DockStyle.Left };
            Button btnNewTab = new Button { Text = Lang.T("+ 新标签", "+ New Tab"), Width = 92, Dock = DockStyle.Left };
            Button btnCloseOthers = new Button { Text = Lang.T("关其他页", "Close Others"), Width = 102, Dock = DockStyle.Left };

            Button btnGo = new Button { Text = Lang.T("前往", "Go"), Width = 50, Dock = DockStyle.Right };
            Button btnDownloadMgr = new Button { Text = Lang.T("下载管理/更新", "Downloads / Update"), Width = 152, Dock = DockStyle.Right, BackColor = Color.FromArgb(40, 167, 69), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            Button btnScrapeClean = new Button { Text = Lang.T("⚡ 抓本页列表", "⚡ Extract Page"), Width = 128, Dock = DockStyle.Right, BackColor = Color.FromArgb(23, 162, 184), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            Button btnDownloadCurrent = new Button { Text = Lang.T("下载当前视频", "Download Video"), Width = 128, Dock = DockStyle.Right, BackColor = Color.FromArgb(255, 140, 0), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            Button btnIns = new Button { Text = "INS", Width = 42, Dock = DockStyle.Right };
            Button btnFb = new Button { Text = "FB", Width = 38, Dock = DockStyle.Right };
            Button btnYt = new Button { Text = "YT", Width = 38, Dock = DockStyle.Right };
            Button btnX = new Button { Text = "X", Width = 32, Dock = DockStyle.Right };
            Button btnTk = new Button { Text = "TK", Width = 38, Dock = DockStyle.Right };
            Button btnCheck = new Button { Text = Lang.T("检测", "Check"), Width = 52, Dock = DockStyle.Right };

            urlBox = new TextBox { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 11) };

            btnBack.Click += (s, e) => { var wv = GetActiveWebView(); if (wv != null && wv.CanGoBack) wv.GoBack(); };
            btnRefresh.Click += (s, e) => GetActiveWebView()?.Reload();
            btnNewTab.Click += async (s, e) => await AddNewTabAsync("https://google.com");
            btnCloseOthers.Click += (s, e) => CloseOtherTabs();
            btnGo.Click += (s, e) => NavigateActiveTab(urlBox.Text);
            urlBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; NavigateActiveTab(urlBox.Text); } };

            btnIns.Click += async (s, e) => await AddNewTabAsync("https://instagram.com");
            btnFb.Click += async (s, e) => await AddNewTabAsync("https://facebook.com");
            btnYt.Click += async (s, e) => await AddNewTabAsync("https://youtube.com");
            btnX.Click += async (s, e) => await AddNewTabAsync("https://x.com");
            btnTk.Click += async (s, e) => await AddNewTabAsync("https://tiktok.com");
            btnCheck.Click += async (s, e) => await AddNewTabAsync("https://whoer.net");

            btnDownloadCurrent.Click += (s, e) =>
            {
                var wv = GetActiveWebView();
                if (wv?.Source == null) return;
                string url = wv.Source.ToString();
                if (url.Contains("google.com") || url.Contains("whoer.net"))
                {
                    lblStatus.Text = Lang.T("当前页面不是音视频或帖子页面，请先打开具体视频或帖子。", "Current page is not a media/post page.");
                    return;
                }
                TriggerSingleDownload(Lang.T("当前页视频/帖子", "Current Page Media"), url, Lang.T("独立帖子/视频", "Independent Post/Video"), true);
            };
            btnScrapeClean.Click += async (s, e) => await OpenAndScrapeCleanAsync();
            btnDownloadMgr.Click += (s, e) => OpenDownloadManagerWindow();

            navBar.Controls.Add(urlBox);
            navBar.Controls.Add(btnGo);
            navBar.Controls.Add(btnDownloadMgr);
            navBar.Controls.Add(btnScrapeClean);
            navBar.Controls.Add(btnDownloadCurrent);
            navBar.Controls.Add(btnCheck);
            navBar.Controls.Add(btnTk);
            navBar.Controls.Add(btnX);
            navBar.Controls.Add(btnYt);
            navBar.Controls.Add(btnFb);
            navBar.Controls.Add(btnIns);
            navBar.Controls.Add(btnCloseOthers);
            navBar.Controls.Add(btnNewTab);
            navBar.Controls.Add(btnRefresh);
            navBar.Controls.Add(btnBack);
            Panel proxyBar = new Panel { Dock = DockStyle.Top, Height = 38, BackColor = Color.FromArgb(232, 240, 254), Padding = new Padding(8, 5, 8, 5) };
            chkUseProxy = new CheckBox { Text = Lang.T("启用独立代理 (IP:端口)：", "Enable Proxy (IP:Port):"), AutoSize = true, Dock = DockStyle.Left, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), Padding = new Padding(0, 3, 5, 0) };
            cmbProxyType = new ComboBox { Width = 90, Dock = DockStyle.Left, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9.5f) };
            cmbProxyType.Items.Add("socks5");
            cmbProxyType.Items.Add("http");
            cmbProxyType.SelectedIndex = 0;

            txtProxyAddress = new TextBox { Width = 175, Dock = DockStyle.Left, Font = new Font("Segoe UI", 10), PlaceholderText = Lang.T("例如 192.168.1.1:7890", "e.g. 192.168.1.1:7890") };
            Button btnSaveRestart = new Button { Text = Lang.T("保存代理并重启", "Save & Restart"), Width = 115, Dock = DockStyle.Left, BackColor = Color.FromArgb(0, 120, 215), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            Button btnSyncLocation = new Button { Text = Lang.T("🔄 同步当前IP定位", "🔄 Sync IP Location"), Width = 138, Dock = DockStyle.Left, BackColor = Color.FromArgb(255, 111, 0), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            Button btnSettings = new Button { Text = Lang.T("⚙️ 默认主页设置", "⚙️ Startup Page"), Width = 125, Dock = DockStyle.Left, BackColor = Color.FromArgb(90, 98, 104), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            Button btnLang = new Button { Text = Lang.T("🌐 EN", "🌐 中文"), Width = 72, Dock = DockStyle.Left, BackColor = Color.FromArgb(111, 66, 193), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            Label lblTip = new Label { Text = Lang.T("提示：修改代理后点“保存代理并重启”；换节点后点“同步当前IP定位”。", "Tip: Click 'Save & Restart' after changing proxy; click 'Sync IP Location' after switching nodes."), AutoSize = true, Dock = DockStyle.Left, ForeColor = Color.DimGray, Padding = new Padding(10, 5, 0, 0) };

            btnSaveRestart.Click += (s, e) => { SaveProxyConfig(); Application.Restart(); Environment.Exit(0); };
            btnSyncLocation.Click += async (s, e) => await RefreshLocationForAllTabsAsync();
            btnSettings.Click += (s, e) => OpenHomeSettingsDialog();
            btnLang.Click += (s, e) => { SaveProxyConfig(); Lang.ToggleLanguage(); Application.Restart(); Environment.Exit(0); };

            proxyBar.Controls.Add(lblTip);
            proxyBar.Controls.Add(btnLang);
            proxyBar.Controls.Add(btnSettings);
            proxyBar.Controls.Add(btnSyncLocation);
            proxyBar.Controls.Add(btnSaveRestart);
            proxyBar.Controls.Add(txtProxyAddress);
            proxyBar.Controls.Add(cmbProxyType);
            proxyBar.Controls.Add(chkUseProxy);




            Panel bottomBar = new Panel { Dock = DockStyle.Bottom, Height = 26, BackColor = Color.FromArgb(240, 240, 240), Padding = new Padding(8, 4, 8, 4) };
            lblStatus = new Label { Text = Lang.T("正在初始化浏览器与检测代理...", "Initializing browser and checking proxy..."), Dock = DockStyle.Fill, ForeColor = Color.DarkSlateGray, Font = new Font("Segoe UI", 9f) };
            bottomBar.Controls.Add(lblStatus);

            tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5f),
                DrawMode = TabDrawMode.OwnerDrawFixed,
                SizeMode = TabSizeMode.Fixed,
                ItemSize = new Size(185, 28)
            };

            tabControl.DrawItem += (s, e) =>
            {
                if (e.Index < 0 || e.Index >= tabControl.TabPages.Count) return;
                TabPage page = tabControl.TabPages.Cast<TabPage>().ElementAt(e.Index);
                Rectangle rect = e.Bounds;
                bool isSelected = (e.Index == tabControl.SelectedIndex);

                using Brush bgBrush = new SolidBrush(isSelected ? Color.White : Color.FromArgb(230, 230, 230));
                e.Graphics.FillRectangle(bgBrush, rect);

                Rectangle textRect = new Rectangle(rect.X + 6, rect.Y + 5, rect.Width - 28, rect.Height - 6);
                TextRenderer.DrawText(e.Graphics, page.Text, tabControl.Font, textRect, Color.Black, TextFormatFlags.Left | TextFormatFlags.EndEllipsis);

                Rectangle closeRect = new Rectangle(rect.Right - 22, rect.Top + 5, 16, 16);
                using Brush closeBrush = new SolidBrush(Color.Crimson);
                using Font closeFont = new Font("Segoe UI", 9.5f, FontStyle.Bold);
                e.Graphics.DrawString("×", closeFont, closeBrush, closeRect.X + 1, closeRect.Y - 2);
            };

            tabControl.MouseDown += (s, e) =>
            {
                for (int i = 0; i < tabControl.TabPages.Count; i++)
                {
                    Rectangle rect = tabControl.GetTabRect(i);
                    Rectangle closeRect = new Rectangle(rect.Right - 24, rect.Top + 3, 20, 22);
                    if (closeRect.Contains(e.Location) || e.Button == MouseButtons.Middle && rect.Contains(e.Location))
                    {
                        CloseTabAt(i);
                        break;
                    }
                }
            };

            tabControl.SelectedIndexChanged += (s, e) => { var wv = GetActiveWebView(); if (wv?.Source != null) urlBox.Text = wv.Source.ToString(); };

            this.Controls.Add(tabControl);
            this.Controls.Add(bottomBar);
            this.Controls.Add(proxyBar);
            this.Controls.Add(navBar);

            LoadProxyConfig();
            this.Load += Form1_Load;
        }

        private string GetCurrentProxyUrl()
        {
            if (chkUseProxy.Checked && !string.IsNullOrWhiteSpace(txtProxyAddress.Text))
            {
                string scheme = cmbProxyType.SelectedItem?.ToString() ?? "socks5";
                return scheme + "://" + txtProxyAddress.Text.Trim();
            }
            return "";
        }
        private async Task ExportCookiesToFileAsync()
        {
            try
            {
                var wv = GetActiveWebView();
                if (wv?.CoreWebView2 == null) return;
                var cookies = await wv.CoreWebView2.CookieManager.GetCookiesAsync("");
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("# Netscape HTTP Cookie File");
                foreach (var c in cookies)
                {
                    string domain = c.Domain.StartsWith(".") ? c.Domain : "." + c.Domain;
                    string flag = "TRUE";
                    string path = string.IsNullOrEmpty(c.Path) ? "/" : c.Path;
                    string secure = c.IsSecure ? "TRUE" : "FALSE";
                    long expires = c.Expires.Year < 1970 ? 0 : new DateTimeOffset(c.Expires).ToUnixTimeSeconds();
                    sb.AppendLine($"{domain}\t{flag}\t{path}\t{secure}\t{expires}\t{c.Name}\t{c.Value}");
                }
                string cookiePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cookies.txt");
                await File.WriteAllTextAsync(cookiePath, sb.ToString());
            }
            catch { }
        }

        private void EnsureDownloadForm()
        {
            if (downloadForm == null || downloadForm.IsDisposed)
            {
                downloadForm = new DownloadManagerForm(downloadFolder, GetCurrentProxyUrl(), ScrapeCleanItemsFromActiveTabAsync);
                downloadForm.OnTaskFinished += (taskTitle, isSuccess) =>
                {
                    if (this.InvokeRequired)
                        this.Invoke(new Action(() => ShowBottomRightToast(taskTitle, isSuccess)));
                    else
                        ShowBottomRightToast(taskTitle, isSuccess);
                };
            }
            downloadForm.UpdateProxy(GetCurrentProxyUrl());
        }

        private void OpenDownloadManagerWindow()
        {
            EnsureDownloadForm();
            downloadForm!.Show();
            downloadForm.WindowState = FormWindowState.Normal;
            downloadForm.BringToFront();
        }

        private async void TriggerSingleDownload(string title, string url, string mediaType, bool showWindow)
        {
            await ExportCookiesToFileAsync();
            EnsureDownloadForm();
            if (showWindow)
            {
                downloadForm!.Show();
                downloadForm.BringToFront();
            }
            downloadForm!.AddSingleAndStartNow(title, url, mediaType);
            lblStatus.Text = Lang.T(
                $"已加入后台静默下载：{title}（可点右上角“下载管理/更新”查看进度）",
                $"Added to background queue: {title} (Click 'Downloads / Update' to view progress)"
            );
        }

        private async Task OpenAndScrapeCleanAsync()
        {
            await ExportCookiesToFileAsync();
            EnsureDownloadForm();
            downloadForm!.Show();
            downloadForm.BringToFront();
            var items = await ScrapeCleanItemsFromActiveTabAsync();
            foreach (var item in items)
            {
                downloadForm!.AddItemToList(item.Title, item.Url, item.MediaType, true);
            }
        }
        private async Task<List<ScrapedMediaItem>> ScrapeCleanItemsFromActiveTabAsync()
        {
            var result = new List<ScrapedMediaItem>();
            var wv = GetActiveWebView();
            if (wv?.CoreWebView2 == null) return result;

            string js = @"
                (() => {
                    const items = [];
                    const seen = new Set();
                    const host = location.hostname;
                    const cleanText = (str, len = 36) => (str || '').replace(/\s+/g, ' ').trim().slice(0, len);
                    const stripQuery = (urlStr) => {
                        try { const u = new URL(urlStr); return u.origin + u.pathname; } catch { return urlStr; }
                    };

                    if (host.includes('youtube.com')) {
                        const pageAuthor = cleanText(document.querySelector('#channel-header #text, ytd-channel-name #text, #owner #channel-name a')?.innerText, 20);
                        // 1. 如果当前正处于长视频播放页或 Shorts 播放页，优先抓取当前正在看的这一条
                        if (location.pathname === '/watch' && location.search.includes('v=')) {
                            const mainTitle = cleanText(document.querySelector('h1.ytd-watch-metadata')?.innerText || document.title, 45);
                            const cleanWatchUrl = location.origin + '/watch?v=' + new URLSearchParams(location.search).get('v');
                            seen.add(cleanWatchUrl);
                            items.push({ Title: '🎬 [当前播放] ' + mainTitle, Url: cleanWatchUrl, MediaType: 'YouTube主视频' });
                        } else if (location.pathname.startsWith('/shorts/')) {
                            const shortId = location.pathname.split('/')[2];
                            if (shortId) {
                                const cleanShortUrl = location.origin + '/shorts/' + shortId;
                                seen.add(cleanShortUrl);
                                const shortTitle = cleanText(document.title.replace('- YouTube', ''), 45);
                                items.push({ Title: '🎬 [当前播放] ' + shortTitle, Url: cleanShortUrl, MediaType: 'YouTube Shorts' });
                            }
                        }
                        // 2. 抓取主页面及个人频道主页的视频和 Shorts 列表（仅排除右侧推荐栏 #secondary 和广告）
                        const mainContainer = document.querySelector('ytd-browse, ytd-search, #contents') || document;
                        const links = mainContainer.querySelectorAll('a[href*=""/watch?v=""], a[href*=""/shorts/""]');
                        links.forEach(a => {
                            if (a.closest('#secondary') || a.closest('ytd-ad-slot-renderer') || a.closest('tp-yt-app-drawer')) return;
                            let vUrl = '';
                            let isShort = false;
                            if (a.href.includes('/watch?v=')) {
                                const vid = new URL(a.href).searchParams.get('v');
                                if (vid) vUrl = location.origin + '/watch?v=' + vid;
                            } else if (a.href.includes('/shorts/')) {
                                const sid = new URL(a.href).pathname.split('/')[2];
                                if (sid) { vUrl = location.origin + '/shorts/' + sid; isShort = true; }
                            }
                            if (vUrl && !seen.has(vUrl)) {
                                const card = a.closest('ytd-rich-item-renderer, ytd-video-renderer, ytd-grid-video-renderer, ytm-shorts-lockup-view-model');
                                const rawTitle = cleanText(card?.querySelector('#video-title, h3')?.innerText || a.title || a.innerText, 38);
                                if (!rawTitle || rawTitle.length < 2) return; // 过滤纯头像空链接
                                seen.add(vUrl);
                                const author = cleanText(card?.querySelector('#channel-name a')?.innerText, 18) || pageAuthor;
                                const typeLabel = isShort ? 'YouTube Shorts' : 'YouTube视频';
                                items.push({ Title: author ? `[${author}] ${rawTitle}` : rawTitle, Url: vUrl, MediaType: typeLabel });
                            }
                        });
                    }
                      else if (host.includes('x.com') || host.includes('twitter.com')) {
                        const articles = document.querySelectorAll('article[data-testid=""tweet""]');
                        articles.forEach(art => {
                            if (art.innerText.includes('Promoted') || art.innerText.includes('广告')) return;
                            const timeLink = art.querySelector('a[href*=""/status/""] time')?.parentElement;
                            const rawUrl = timeLink ? timeLink.href : '';
                            const url = stripQuery(rawUrl);
                            if (url && !seen.has(url)) {
                                seen.add(url);
                                 user = cleanText(art.querySelector('[data-testidconst=""User-Name""]')?.innerText?.split('\n')?.[0], 18);
                                const txt = cleanText(art.querySelector('[data-testid=""tweetText""]')?.innerText, 32);
                                const label = txt ? (user ? `[${user}] ${txt}` : txt) : (user ? `[${user}] 推文媒体` : 'X推文媒体');
                                items.push({ Title: label, Url: url, MediaType: 'X推文视频/图' });
                            }
                        });
                    } 
                    else if (host.includes('tiktok.com')) {
                        const pathParts = location.pathname.split('/').filter(Boolean);
                        const pageUser = (pathParts.at(0) && pathParts.at(0).startsWith('@')) ? pathParts.at(0).replace('@', '') : '';

                        const links = document.querySelectorAll('a[href*=""/video/""], a[href*=""/photo/""]');
                        links.forEach(a => {
                            if (a.closest('header') || a.closest('[data-e2e=""nav-list""]')) return;
                            const card = a.closest('[data-e2e=""recommend-list-item-container""], [data-e2e=""user-post-item""], div[class*=""DivItemContainer""]');
                            if (card && (card.innerText.includes('Sponsored') || card.innerText.includes('广告'))) return;

                            const url = stripQuery(a.href);
                            if (url && !seen.has(url)) {
                                seen.add(url);
                                let author = pageUser;
                                if (!author) {
                                    try {
                                        const segs = new URL(url).pathname.split('/').filter(Boolean);
                                        if (segs.at(0) && segs.at(0).startsWith('@')) author = segs.at(0).replace('@', '');
                                    } catch {}
                                }
                                const desc = cleanText(card?.querySelector('[data-e2e=""video-desc""]')?.innerText || a.title || a.querySelector('img')?.alt, 34);
                                const isPhoto = url.includes('/photo/');
                                const typeLabel = isPhoto ? 'TK图集' : 'TK视频';
                                const label = desc ? (author ? `[${author}] ${desc}` : desc) : (author ? `[${author}] ${typeLabel}` : typeLabel);
                                items.push({ Title: label, Url: url, MediaType: typeLabel });
                            }
                        });
                    }
                    else if (host.includes('instagram.com')) {
                        const mainArea = document.querySelector('main') || document;
                        const links = mainArea.querySelectorAll('a[href*=""/p/""], a[href*=""/reel/""], a[href*=""/reels/""]');
                        links.forEach(a => {
                            if (a.closest('aside') || a.closest('header')) return;
                            const art = a.closest('article');
                            if (art && (art.innerText.includes('Sponsored') || art.innerText.includes('赞助'))) return;
                            const url = stripQuery(a.href);
                            if (url && !seen.has(url)) {
                                seen.add(url);
                                const author = cleanText(art?.querySelector('header a')?.innerText, 18);
                                const caption = cleanText(art?.querySelector('h1')?.innerText || a.querySelector('img')?.alt, 32);
                                const type = url.includes('/reel') ? 'INS视频' : 'INS图集/贴';
                                const label = caption ? (author ? `[${author}] ${caption}` : caption) : (author ? `[${author}] ${type}` : `${type} (${a.pathname})`);
                                items.push({ Title: label, Url: url, MediaType: type });
                            }
                        });
                    } 
                    else if (host.includes('facebook.com')) {
                        const mainArea = document.querySelector('[role=""main""]') || document;
                        const allLinks = mainArea.querySelectorAll('a[href]');
                        allLinks.forEach(a => {
                            if (a.closest('[role=""banner""]') || a.closest('[role=""navigation""]')) return;
                            const rawHref = a.href || '';
                            const isTarget = rawHref.includes('/posts/') || rawHref.includes('/reel/') || rawHref.includes('/videos/') || rawHref.includes('/watch') || rawHref.includes('/photo') || rawHref.includes('/permalink/') || rawHref.includes('story_fbid=');
                            if (!isTarget) return;

                            const card = a.closest('[role=""article""]') || a.closest('div[data-pagelet]');
                            if (card && (card.innerText.includes('Sponsored') || card.innerText.includes('赞助内容'))) return;

                            let url = rawHref;
                            if (!url.includes('/photo') && !url.includes('/watch') && !url.includes('story_fbid=')) {
                                url = stripQuery(url);
                            }

                            if (url && !seen.has(url)) {
                                seen.add(url);
                                const author = cleanText(card?.querySelector('h2 a, h3 a, h4 a, strong span, strong')?.innerText, 20);
                                const msg = cleanText(card?.querySelector('[data-ad-preview=""message""], [dir=""auto""]')?.innerText, 32);
                                const imgAlt = cleanText(card?.querySelector('img[alt]')?.alt, 30);
                                const isVid = url.includes('/reel/') || url.includes('/videos/') || url.includes('/watch');
                                const type = isVid ? 'FB视频' : 'FB图集/帖子';
                                const desc = (msg && msg !== author) ? msg : (imgAlt || type);
                                const label = author ? `[${author}] ${desc}` : desc;
                                items.push({ Title: label, Url: url, MediaType: type });
                            }
                        });
                    }
                    return JSON.stringify(items);
                })()
            ";
            try
            {
                string jsonRaw = await wv.CoreWebView2.ExecuteScriptAsync(js);
                string unescaped = JsonSerializer.Deserialize<string>(jsonRaw) ?? "[]";
                var parsed = JsonSerializer.Deserialize<List<ScrapedMediaItem>>(unescaped);
                if (parsed != null) result.AddRange(parsed);
            }
            catch { }
            return result;
        }

        private void LoadProxyConfig()
        {
            if (File.Exists(configFilePath))
            {
                try
                {
                    string[] lines = File.ReadAllLines(configFilePath);
                    if (lines.Length >= 3)
                    {
                        chkUseProxy.Checked = bool.Parse(lines.GetValue(0)?.ToString() ?? "false");
                        cmbProxyType.SelectedItem = lines.GetValue(1)?.ToString() ?? "socks5";
                        txtProxyAddress.Text = lines.GetValue(2)?.ToString() ?? "192.168.1.1:7890";
                        return;
                    }
                }
                catch { }
            }
            chkUseProxy.Checked = false;
            cmbProxyType.SelectedIndex = 0;
            txtProxyAddress.Text = "192.168.1.1:7890";
        }

        private void SaveProxyConfig()
        {
            var lines = new List<string> { chkUseProxy.Checked.ToString(), cmbProxyType.SelectedItem?.ToString() ?? "socks5", txtProxyAddress.Text.Trim() };
            File.WriteAllLines(configFilePath, lines);
        }

        private WebView2? GetActiveWebView()
        {
            if (tabControl.SelectedTab != null)
            {
                foreach (Control ctrl in tabControl.SelectedTab.Controls)
                    if (ctrl is WebView2 wv) return wv;
            }
            return null;
        }

        private void NavigateActiveTab(string url)
        {
            var wv = GetActiveWebView();
            if (wv == null || string.IsNullOrWhiteSpace(url)) return;
            if (!url.StartsWith("http://") && !url.StartsWith("https://")) url = "https://" + url;
            wv.CoreWebView2?.Navigate(url);
        }

        private void CloseOtherTabs()
        {
            int current = tabControl.SelectedIndex;
            for (int i = tabControl.TabPages.Count - 1; i >= 0; i--)
            {
                if (i != current) CloseTabAt(i, true);
            }
        }

        private void CloseTabAt(int index, bool allowLast = false)
        {
            if (!allowLast && tabControl.TabPages.Count <= 1) return;
            TabPage page = tabControl.TabPages.Cast<TabPage>().ElementAt(index);
            foreach (Control ctrl in page.Controls)
            {
                if (ctrl is WebView2 wv)
                {
                    wv.CoreWebView2?.Stop();
                    wv.Dispose();
                }
            }
            tabControl.TabPages.RemoveAt(index);
            GC.Collect();
        }

        private async void Form1_Load(object? sender, EventArgs e)
        {
            try
            {
                string proxyUrl = GetCurrentProxyUrl();
                string browserArgs = "--webrtc-ip-handling-policy=disable_non_proxied_udp --enforce-webrtc-ip-permission-check --disable-features=WebRtcHideLocalIpsWithMdns";
                if (!string.IsNullOrEmpty(proxyUrl))
                {
                    browserArgs += " --proxy-server=" + proxyUrl;
                    lblStatus.Text = Lang.T("正在通过代理 [" + proxyUrl + "] 检测出口位置...", "Detecting exit location via proxy [" + proxyUrl + "]...");
                }
                else lblStatus.Text = Lang.T("未启用独立代理，正在使用系统默认网络检测位置...", "Proxy disabled, detecting location via default network...");

                var locResult = await GetCurrentIpLocationAsync(proxyUrl);
                currentLat = locResult.lat;
                currentLon = locResult.lon;
                currentTimezone = locResult.timezone;
                lblStatus.Text = Lang.T(
                    $"当前状态：{locResult.statusMsg} | 注入坐标：{currentLat}, {currentLon} | 时区：{currentTimezone}",
                    $"Status: {locResult.statusMsg} | Geolocation: {currentLat}, {currentLon} | Timezone: {currentTimezone}"
                );

                var options = new CoreWebView2EnvironmentOptions { Language = "en-US", AdditionalBrowserArguments = browserArgs };
                string userDataFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BrowserData");
                sharedEnv = await CoreWebView2Environment.CreateAsync(null, userDataFolder, options);

                await AddNewTabAsync(GetSavedHomeUrl());
            }
            catch (Exception ex) { MessageBox.Show("初始化失败：\n" + ex.Message); }
        }

        private string GetSavedHomeUrl()
        {
            string homeFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "home_url.txt");
            if (File.Exists(homeFile))
            {
                string saved = File.ReadAllText(homeFile).Trim();
                if (!string.IsNullOrWhiteSpace(saved)) return saved;
            }
            return "https://whoer.net"; // 默认先打开检测页，避免节点被单站限流
        }

        private void OpenHomeSettingsDialog()
        {
            string homeFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "home_url.txt");
            Form dlg = new Form { Text = Lang.T("浏览器默认启动主页设置", "Startup Homepage Settings"), Size = new Size(460, 210), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false };

            Label lbl = new Label { Text = Lang.T("选择快捷预设或直接输入软件启动时打开的默认网址：", "Select a preset or enter custom startup URL:"), Left = 20, Top = 18, AutoSize = true };
            ComboBox cmbPreset = new ComboBox { Left = 20, Top = 45, Width = 400, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbPreset.Items.Add("https://whoer.net (IP & Fingerprint Check)");
            cmbPreset.Items.Add("https://google.com (Google Search)");
            cmbPreset.Items.Add("https://instagram.com (Instagram)");
            cmbPreset.Items.Add("https://youtube.com (YouTube)");
            cmbPreset.Items.Add("https://x.com (X / Twitter)");
            cmbPreset.Items.Add("https://tiktok.com (TikTok)");

            TextBox txtCustomUrl = new TextBox { Left = 20, Top = 82, Width = 400, Text = GetSavedHomeUrl() };
            cmbPreset.SelectedIndexChanged += (s, e) =>
            {
                string sel = cmbPreset.SelectedItem?.ToString() ?? "";
                int spaceIdx = sel.IndexOf(' ');
                if (spaceIdx > 0) txtCustomUrl.Text = sel.Substring(0, spaceIdx);
            };

            Button btnOk = new Button { Text = Lang.T("保存设置", "Save"), Left = 230, Top = 122, Width = 90, Height = 30, BackColor = Color.FromArgb(0, 120, 215), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            Button btnCancel = new Button { Text = Lang.T("取消", "Cancel"), Left = 330, Top = 122, Width = 90, Height = 30 };

            btnOk.Click += (s, e) =>
            {
                string url = txtCustomUrl.Text.Trim();
                if (!string.IsNullOrWhiteSpace(url))
                {
                    if (!url.StartsWith("http://") && !url.StartsWith("https://")) url = "https://" + url;
                    File.WriteAllText(homeFile, url);
                    lblStatus.Text = Lang.T("默认启动主页已保存为：", "Startup homepage saved: ") + url;
                }
                dlg.Close();
            };
            btnCancel.Click += (s, e) => dlg.Close();

            dlg.Controls.AddRange(new Control[] { lbl, cmbPreset, txtCustomUrl, btnOk, btnCancel });
            dlg.ShowDialog(this);
        }


        private async Task AddNewTabAsync(string initialUrl)
        {
            if (sharedEnv == null) return;
            TabPage tabPage = new TabPage(Lang.T("加载中...", "Loading..."));
            WebView2 webView = new WebView2 { Dock = DockStyle.Fill };
            tabPage.Controls.Add(webView);
            tabControl.TabPages.Add(tabPage);
            tabControl.SelectedTab = tabPage;

            await webView.EnsureCoreWebView2Async(sharedEnv);
            webView.CoreWebView2.Settings.IsPasswordAutosaveEnabled = true;
            // 彻底拦截 WebRTC 接口，防止 UDP 探测泄露真实本地 IP
            // 静默伪装 WebRTC：保留正常接口不报错，但彻底屏蔽本地与公网 IP 候选地址收集
            string blockWebRtcScript = @"
                (() => {
                    const OrigPC = window.RTCPeerConnection || window.webkitRTCPeerConnection;
                    if (!OrigPC) return;
                    const FakePC = function(config, constraints) {
                        if (config && config.iceServers) {
                            config.iceServers = []; // 清空所有 STUN/TURN 探测服务器
                        }
                        const pc = new OrigPC(config, constraints);
                        const origCreateOffer = pc.createOffer.bind(pc);
                        pc.createOffer = function(options) {
                            return origCreateOffer(options).then(offer => {
                                if (offer && offer.sdp) {
                                    // 过滤掉所有包含真实 IP 的 candidate 行
                                    offer.sdp = offer.sdp.replace(/a=candidate:.*\r?\n/g, '');
                                }
                                return offer;
                            });
                        };
                        return pc;
                    };
                    FakePC.prototype = OrigPC.prototype;
                    window.RTCPeerConnection = FakePC;
                    if (window.webkitRTCPeerConnection) window.webkitRTCPeerConnection = FakePC;
                })();
            ";
            await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(blockWebRtcScript);
            webView.CoreWebView2.Settings.IsGeneralAutofillEnabled = true;

            webView.CoreWebView2.DocumentTitleChanged += (s, args) =>
            {
                string title = webView.CoreWebView2.DocumentTitle;
                tabPage.Text = title.Length > 14 ? title.Substring(0, 14) + ".." : title;
                tabControl.Invalidate();
            };
            webView.SourceChanged += (s, args) => { if (tabControl.SelectedTab == tabPage) urlBox.Text = webView.Source.ToString(); };
            webView.CoreWebView2.NewWindowRequested += (s, args) =>
            {
                // 如果是第三方登录授权弹窗（带有指定窗口大小或非普通新标签跳转），创建保留父子关联的真实弹窗
                if (args.WindowFeatures.HasSize || args.Uri.Contains("oauth") || args.Uri.Contains("login") || args.Uri.Contains("auth") || args.Uri.Contains("accounts.google"))
                {
                    var deferral = args.GetDeferral();
                    Form popupForm = new Form
                    {
                        Text = "账号安全验证与授权",
                        Size = new Size(520, 680),
                        StartPosition = FormStartPosition.CenterParent
                    };
                    WebView2 popupWebView = new WebView2 { Dock = DockStyle.Fill };
                    popupForm.Controls.Add(popupWebView);
                    popupForm.Show();

                    popupWebView.CoreWebView2InitializationCompleted += async (sender, e) =>
                    {
                        if (e.IsSuccess)
                        {
                            await popupWebView.CoreWebView2.CallDevToolsProtocolMethodAsync("Emulation.setGeolocationOverride", JsonSerializer.Serialize(new { latitude = currentLat, longitude = currentLon, accuracy = 50 }));
                            await popupWebView.CoreWebView2.CallDevToolsProtocolMethodAsync("Emulation.setTimezoneOverride", JsonSerializer.Serialize(new { timezoneId = currentTimezone }));

                            // 授权完成后网页调用 window.close() 时，自动关闭此弹窗并返回主页面
                            popupWebView.CoreWebView2.WindowCloseRequested += (ws, we) =>
                            {
                                popupForm.Close();
                                popupWebView.Dispose();
                            };

                            args.NewWindow = popupWebView.CoreWebView2;
                        }
                        deferral.Complete();
                    };

                    _ = popupWebView.EnsureCoreWebView2Async(sharedEnv);
                }
                else
                {
                    // 普通网页链接依然在主窗口开新标签页
                    args.Handled = true;
                    _ = AddNewTabAsync(args.Uri);
                }
            };
            webView.CoreWebView2.PermissionRequested += (s, args) => { if (args.PermissionKind == CoreWebView2PermissionKind.Geolocation) args.State = CoreWebView2PermissionState.Allow; };

            // 精准指哪打哪右键菜单 (已支持 TK / INS / FB / YT / X)
            webView.CoreWebView2.ContextMenuRequested += (s, args) =>
            {
                var menuList = args.MenuItems;

                if (args.ContextMenuTarget.Kind == CoreWebView2ContextMenuTargetKind.Image && args.ContextMenuTarget.HasSourceUri)
                {
                    string imgUri = args.ContextMenuTarget.SourceUri;
                    var itemImg = webView.CoreWebView2.Environment.CreateContextMenuItem(Lang.T("🖼️ 精准下载此单张原图", "🖼️ Download Original Image"), null, CoreWebView2ContextMenuItemKind.Command);
                    itemImg.CustomItemSelected += (sender, e) => TriggerSingleDownload(Lang.T("单张原图", "Single Image"), imgUri, Lang.T("单张原图", "Single Image"), false);
                    menuList.Insert(0, itemImg);
                }

                if (args.ContextMenuTarget.HasLinkUri)
                {
                    string linkUri = args.ContextMenuTarget.LinkUri;
                    var itemLink = webView.CoreWebView2.Environment.CreateContextMenuItem(Lang.T("🎯 精准下载此独立视频/帖子", "🎯 Download Single Video / Post"), null, CoreWebView2ContextMenuItemKind.Command);
                    itemLink.CustomItemSelected += (sender, e) => TriggerSingleDownload(Lang.T("独立视频/帖子", "Single Video / Post"), linkUri, Lang.T("独立帖子/视频", "Independent Post/Video"), false);
                    menuList.Insert(0, itemLink);
                }

                string currentPageUrl = webView.Source.ToString();
                if (currentPageUrl.Contains("/watch?v=") || currentPageUrl.Contains("/shorts/") || currentPageUrl.Contains("/p/") || currentPageUrl.Contains("/reel/") || currentPageUrl.Contains("/status/"))
                {
                    var itemCurrent = webView.CoreWebView2.Environment.CreateContextMenuItem(Lang.T("🎬 下载当前正在看的视频/帖子", "🎬 Download Current Video / Post"), null, CoreWebView2ContextMenuItemKind.Command);
                    itemCurrent.CustomItemSelected += (sender, e) => TriggerSingleDownload(Lang.T("当前观看媒体", "Current Media"), currentPageUrl, Lang.T("独立帖子/视频", "Independent Post/Video"), false);
                    menuList.Insert(0, itemCurrent);
                }
            };

            await webView.CoreWebView2.CallDevToolsProtocolMethodAsync("Emulation.setGeolocationOverride", JsonSerializer.Serialize(new { latitude = currentLat, longitude = currentLon, accuracy = 50 }));
            await webView.CoreWebView2.CallDevToolsProtocolMethodAsync("Emulation.setTimezoneOverride", JsonSerializer.Serialize(new { timezoneId = currentTimezone }));

            webView.CoreWebView2.Navigate(initialUrl);
        }

        private async Task RefreshLocationForAllTabsAsync()
        {
            string proxyUrl = GetCurrentProxyUrl();
            lblStatus.Text = Lang.T("正在重新检测当前代理出口 IP 与地理位置...", "Syncing proxy IP and geolocation...");
            var locResult = await GetCurrentIpLocationAsync(proxyUrl);
            currentLat = locResult.lat;
            currentLon = locResult.lon;
            currentTimezone = locResult.timezone;

            // 遍历当前所有已打开的标签页，实时注入最新的经纬度与时区
            foreach (TabPage page in tabControl.TabPages)
            {
                foreach (Control ctrl in page.Controls)
                {
                    if (ctrl is WebView2 wv && wv.CoreWebView2 != null)
                    {
                        await wv.CoreWebView2.CallDevToolsProtocolMethodAsync("Emulation.setGeolocationOverride", JsonSerializer.Serialize(new { latitude = currentLat, longitude = currentLon, accuracy = 80 }));
                        await wv.CoreWebView2.CallDevToolsProtocolMethodAsync("Emulation.setTimezoneOverride", JsonSerializer.Serialize(new { timezoneId = currentTimezone }));
                    }
                }
            }

            lblStatus.Text = $"已同步最新位置：{locResult.statusMsg} | 居民区微调坐标：{currentLat:F4}, {currentLon:F4} | 时区：{currentTimezone}";
        }

        private (double newLat, double newLon) ApplyResidentialOffset(double rawLat, double rawLon)
        {
            // 在原城市中心坐标基础上，向随机方向偏移 1.8 ~ 3.5 公里（避开市政厅、政府大楼等公共地标）
            Random rng = new Random();
            double distanceKm = 1.8 + rng.NextDouble() * 1.7;
            double angleRad = rng.NextDouble() * 2 * Math.PI;

            double deltaLat = (distanceKm * Math.Cos(angleRad)) / 111.32;
            double deltaLon = (distanceKm * Math.Sin(angleRad)) / (111.32 * Math.Cos(rawLat * Math.PI / 180.0));

            return (Math.Round(rawLat + deltaLat, 5), Math.Round(rawLon + deltaLon, 5));
        }

        private async Task<(double lat, double lon, string timezone, string statusMsg)> GetCurrentIpLocationAsync(string proxyUrl)
        {
            var handler = new HttpClientHandler();
            if (!string.IsNullOrEmpty(proxyUrl))
            {
                handler.Proxy = new WebProxy(proxyUrl);
                handler.UseProxy = true;
            }
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(8) };
            client.DefaultRequestHeaders.Add("User-Agent", "curl/8.0.0");

            // 接口 1：ipinfo.io（稳定返回 JSON，不易触发纯文本拦截）
            try
            {
                string json = await client.GetStringAsync("https://ipinfo.io");
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("loc", out var locP))
                {
                    string[] parts = (locP.GetString() ?? "").Split(',');
                    if (parts.Length == 2 &&
                        double.TryParse(parts[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double lat) &&
                        double.TryParse(parts[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double lon))
                    {
                        var (safeLat, safeLon) = ApplyResidentialOffset(lat, lon);
                        string tz = root.TryGetProperty("timezone", out var tzP) ? tzP.GetString() ?? "America/Los_Angeles" : "America/Los_Angeles";
                        string ip = root.TryGetProperty("ip", out var ipP) ? ipP.GetString() ?? "" : "";
                        string city = root.TryGetProperty("city", out var cityP) ? cityP.GetString() ?? "" : "";
                        string country = root.TryGetProperty("country", out var cP) ? cP.GetString() ?? "" : "";
                        return (safeLat, safeLon, tz, Lang.T($"代理已连通 ({ip} - {city}, {country})", $"Proxy Connected ({ip} - {city}, {country})"));
                    }
                }
            }
            catch { }

            // 接口 2：ip-api.com 备用接口
            try
            {
                string json = await client.GetStringAsync("http://ip-api.com");
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("lat", out var latP) && root.TryGetProperty("lon", out var lonP))
                {
                    var (safeLat, safeLon) = ApplyResidentialOffset(latP.GetDouble(), lonP.GetDouble());
                    string tz = root.TryGetProperty("timezone", out var tzP) ? tzP.GetString() ?? "America/Los_Angeles" : "America/Los_Angeles";
                    string ip = root.TryGetProperty("query", out var ipP) ? ipP.GetString() ?? "" : "";
                    string city = root.TryGetProperty("city", out var cityP) ? cityP.GetString() ?? "" : "";
                    string country = root.TryGetProperty("countryCode", out var cP) ? cP.GetString() ?? "" : "";
                    return (safeLat, safeLon, tz, Lang.T($"代理已连通 ({ip} - {city}, {country})", $"Proxy Connected ({ip} - {city}, {country})"));
                }
            }
            catch { }

            // 接口 3：api.ip.sb 兜底接口
            try
            {
                string json = await client.GetStringAsync("https://ip.sb");
                if (json.TrimStart().StartsWith("{"))
                {
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("latitude", out var latP) && root.TryGetProperty("longitude", out var lonP))
                    {
                        var (safeLat, safeLon) = ApplyResidentialOffset(latP.GetDouble(), lonP.GetDouble());
                        string tz = root.TryGetProperty("timezone", out var tzP) ? tzP.GetString() ?? "America/Los_Angeles" : "America/Los_Angeles";
                        string ip = root.TryGetProperty("ip", out var ipP) ? ipP.GetString() ?? "" : "";
                        string city = root.TryGetProperty("city", out var cityP) ? cityP.GetString() ?? "" : "";
                        string country = root.TryGetProperty("country_code", out var cP) ? cP.GetString() ?? "" : "";
                        return (safeLat, safeLon, tz, Lang.T($"代理已连通 ({ip} - {city}, {country})", $"Proxy Connected ({ip} - {city}, {country})"));
                    }
                }
            }
            catch { }

            var (defLat, defLon) = ApplyResidentialOffset(34.0522, -118.2437);
            return (defLat, defLon, "America/Los_Angeles", Lang.T("代理已通(查询接口受限，已使用微调美国坐标)", "Proxy Connected (Default US Geolocation Applied)"));
        }
        private void ShowBottomRightToast(string taskTitle, bool isSuccess)
        {
            Form toast = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.Manual,
                Size = new Size(320, 82),
                BackColor = isSuccess ? Color.FromArgb(40, 167, 69) : Color.FromArgb(220, 53, 69),
                TopMost = true,
                ShowInTaskbar = false
            };

            // 定位到主浏览器窗口的右下角
            int x = this.Location.X + this.Width - toast.Width - 22;
            int y = this.Location.Y + this.Height - toast.Height - 38;
            toast.Location = new Point(Math.Max(0, x), Math.Max(0, y));

            Label lblTitle = new Label
            {
                Text = isSuccess ? Lang.T("✅ 后台媒体下载完成", "✅ Download Completed") : Lang.T("❌ 后台媒体下载失败", "❌ Download Failed"),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Location = new Point(12, 10),
                AutoSize = true
            };

            string shortName = taskTitle.Length > 34 ? taskTitle.Substring(0, 34) + "..." : taskTitle;
            Label lblDesc = new Label
            {
                Text = shortName + Lang.T("\n(5秒后自动关闭，点击可打开下载目录)", "\n(Auto-closes in 5s, click to open folder)"),
                ForeColor = Color.WhiteSmoke,
                Font = new Font("Segoe UI", 8.5f),
                Location = new Point(12, 34),
                Size = new Size(295, 38)
            };

            EventHandler openDirClick = (s, e) =>
            {
                System.Diagnostics.Process.Start("explorer.exe", downloadFolder);
                toast.Close();
            };
            toast.Click += openDirClick;
            lblTitle.Click += openDirClick;
            lblDesc.Click += openDirClick;

            toast.Controls.Add(lblTitle);
            toast.Controls.Add(lblDesc);
            toast.Show(this);

            // 5 秒后自动关闭小弹窗
            System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 5000 };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                timer.Dispose();
                if (!toast.IsDisposed)
                {
                    toast.Close();
                    toast.Dispose();
                }
            };
            timer.Start();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // 主浏览器关闭时，彻底销毁后台下载管理器
            if (downloadForm != null && !downloadForm.IsDisposed)
            {
                downloadForm.ForceShutdown();
            }
            base.OnFormClosing(e);
        }

    }
}


