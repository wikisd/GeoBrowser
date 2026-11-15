# GeoBrowser

一款基于 WebView2 (.NET) 开发的轻量级 Windows 桌面浏览器，支持独立网络代理设置、动态地理位置与时区同步，以及多平台媒体精准下载与自动分类归档。

[English Documentation](#english-documentation)

---
<img width="1486" height="913" alt="31" src="https://github.com/user-attachments/assets/d4d588e4-66e3-4548-9954-2a41b8041f17" />

## ✨ 核心功能

- **独立代理与位置同步**
  - 支持独立配置 HTTP / SOCKS5 代理，不影响系统全局网络设置。
  - 支持一键同步当前代理出口 IP 的城市与时区定位，并自动在周边居民区范围内生成自然坐标偏移。
- **多标签页与内存管理**
  - 每个标签页均带有独立的 `×` 关闭按钮，支持一键关闭其他后台标签，及时释放底层浏览器进程内存。
- **多平台媒体精准下载**
  - 全面支持 **Instagram、Facebook、YouTube、X (Twitter) 与 TikTok**。
  - **右键精准直下**：在网页任意图片或视频链接上右键点击，即可单条静默加入后台下载（完成后右下角弹出 5 秒自动关闭提示框）。
  - **批量纯净提取**：自动过滤推广广告与侧边栏推荐，提取页面有效内容列表供勾选下载，并自动按 **`Downloads \ 平台 \ 账号名 \ 日期`** 归档保存。

## 📦 运行依赖组件

使用媒体下载功能前，请将以下开源组件放置于 `GeoBrowser.exe` 同级目录下（软件内置 `yt-dlp` 与 `gallery-dl` 一键在线更新功能）：

- `yt-dlp.exe`
- `ffmpeg.exe` & `ffprobe.exe`（需使用静态单文件版 / Static GPL Build）
- `gallery-dl.exe`

## 🚀 快速上手

1. **配置代理**：勾选顶部 **“启用独立代理”**，选择 `socks5` 或 `http`，输入 `IP:端口`（例如 `192.168.1.1:7890`），点击 **“保存代理并重启”**。
2. **同步定位**：切换网络节点后，无需重启软件，点击 **“🔄 同步当前IP定位”** 即可实时刷新所有页面的地理坐标与时区。
3. **下载媒体**：
   - 在网页上右键点击图片或视频链接，直接加入后台静默下载。
   - 或点击顶部 **“⚡ 抓本页列表”**，勾选需要的内容批量下载。
4. **多开隔离**：将整个软件文件夹复制一份，即可同时运行多套完全独立的账号与缓存环境（`BrowserData`）。

## 🌐 IPv6 网络注意事项

- **仅使用 IPv4 代理**：若本地网卡开启了 IPv6，但代理服务器仅转发 IPv4 流量，建议在 Windows 网卡属性中取消勾选 `Internet 协议版本 6 (TCP/IPv6)`，防止双栈网站直接通过本地 IPv6 连接。
- **使用 IPv6 代理**：请确保代理服务端已开启 IPv6 路由转发；填写 IPv6 代理地址时需使用英文中括号括起，格式为 `[IPv6地址]:端口`。

## 📄 免责声明

本工具仅供个人网页浏览、网络环境测试及个人离线归档公开媒体使用。使用下载功能时，请遵守各平台的服务条款及当地版权法律法规。

---

<a id="english-documentation"></a>

# GeoBrowser (English)

A lightweight Windows desktop browser built on WebView2 (.NET), featuring isolated proxy configuration, dynamic geolocation/timezone synchronization, and multi-platform media downloading with automatic folder organization.

## ✨ Key Features

- **Isolated Proxy & Location Sync**
  - Supports custom HTTP and SOCKS5 proxies without affecting system-wide network settings.
  - One-click synchronization of browser geolocation and timezone based on the current proxy exit IP, with automatic neighborhood-level coordinate offset.
- **Multi-Tab Memory Management**
  - Independent tab management with quick-close buttons (`×`) and a "Close Other Tabs" option to release underlying WebView2 process memory promptly.
- **Precision Media Downloader**
  - Supports **Instagram, Facebook, YouTube, X (Twitter), and TikTok**.
  - **Context Menu Download**: Right-click any image or video link to download single items silently in the background with 5-second toast notifications.
  - **Batch Extraction**: Extracts clean post and video lists from feeds or creator profiles while filtering out promoted content, with automatic folder archiving (`Downloads \ Platform \ Account \ Date`).

## 📦 Prerequisites

Place the following open-source binaries in the same directory as `GeoBrowser.exe` before running media download tasks (built-in one-click updater is included for `yt-dlp` and `gallery-dl`):

- `yt-dlp.exe`
- `ffmpeg.exe` & `ffprobe.exe` (Static GPL Build)
- `gallery-dl.exe`

## 🚀 Quick Start

1. **Setup Proxy**: Check **"启用独立代理"**, select `socks5` or `http`, enter `IP:Port` (e.g., `192.168.1.1:7890`), and click **"保存代理并重启"**.
2. **Sync Location**: Whenever switching proxy nodes, click **"🔄 同步当前IP定位"** to update browser coordinates and timezone in real time.
3. **Download Media**:
   - Right-click any media/link on a webpage to download immediately in the background.
   - Or click **"⚡ 抓本页列表"** on the top bar to batch-select posts from the current page.
4. **Multi-Instance**: Copy the entire application folder to run multiple completely isolated browser profiles (`BrowserData`).

## 🌐 IPv6 Network Notes

- **IPv4 Proxy Only**: If your local network has IPv6 enabled while your proxy server only forwards IPv4 traffic, disable `Internet Protocol Version 6 (TCP/IPv6)` in your Windows network adapter settings to prevent dual-stack websites from bypassing the proxy via local IPv6.
- **IPv6 Proxy Support**: Ensure IPv6 routing is enabled on your proxy server. When entering an IPv6 proxy address, enclose it in square brackets (e.g., `[2001:db8::1]:7890`).

## 📄 Disclaimer

This tool is intended for personal web browsing, network testing, and archiving publicly accessible media for personal offline viewing. Please comply with the terms of service of respective websites and local copyright laws.
