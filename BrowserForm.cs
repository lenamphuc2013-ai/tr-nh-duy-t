using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Webview;

internal sealed class BrowserForm : Form
{
    private readonly FlowLayoutPanel tabStrip = new();
    private readonly Panel browserHost = new();
    private readonly TextBox addressBox = new();
    private Button backButton = null!;
    private Button forwardButton = null!;
    private Button reloadButton = null!;
    private Button bookmarkButton = null!;
    private Button menuButton = null!;
    private readonly List<BrowserTab> tabs = new();
    private readonly List<HistoryEntry> history = new();
    private readonly string profilePath = Path.Combine(Webview.appData, "BrowserProfile");
    private readonly string browserDataPath = Path.Combine(Webview.appData, "Browser");
    private readonly string bookmarksPath = Path.Combine(Webview.appData, "Browser", "bookmarks.json");
    private readonly string historyPath = Path.Combine(Webview.appData, "Browser", "history.json");
    private CoreWebView2Environment? environment;
    private BrowserTab? activeTab;
    private Button? newTabButton;
    private bool isClosing;
    private bool isBrowserFullscreen;
    private FormBorderStyle previousBorderStyle;
    private FormWindowState previousWindowState;

    public BrowserForm()
    {
        Text = "Orbit Browser";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(760, 520);
        Size = new Size(1280, 820);
        BackColor = Color.FromArgb(18, 20, 29);
        ForeColor = Color.FromArgb(232, 234, 242);
        Font = new Font("Segoe UI", 9F);
        KeyPreview = true;
        Icon = SystemIcons.Application;

        BuildInterface();
        KeyDown += BrowserForm_KeyDown;
        FormClosing += (_, _) => isClosing = true;
        Shown += BrowserForm_Shown;
    }

    private void BuildInterface()
    {
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 94,
            BackColor = Color.FromArgb(25, 27, 39),
            Padding = new Padding(10, 8, 10, 8)
        };

        tabStrip.Dock = DockStyle.Top;
        tabStrip.Height = 38;
        tabStrip.WrapContents = false;
        tabStrip.AutoScroll = true;
        tabStrip.BackColor = header.BackColor;
        tabStrip.Padding = new Padding(0);

        newTabButton = MakeButton("+", "Mở tab mới", 38);
        newTabButton.Font = new Font("Segoe UI", 15F, FontStyle.Regular);
        newTabButton.Click += async (_, _) => await CreateTabAsync("about:blank");
        tabStrip.Controls.Add(newTabButton);

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 40,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = header.BackColor,
            Padding = new Padding(0, 4, 0, 0)
        };

        backButton = MakeButton("‹", "Quay lại");
        forwardButton = MakeButton("›", "Đi tới");
        reloadButton = MakeButton("↻", "Tải lại");
        bookmarkButton = MakeButton("☆", "Thêm bookmark");
        menuButton = MakeButton("⋮", "Menu trình duyệt");
        backButton.Font = new Font("Segoe UI", 17F);
        forwardButton.Font = new Font("Segoe UI", 17F);
        reloadButton.Font = new Font("Segoe UI", 14F);
        menuButton.Font = new Font("Segoe UI", 15F);

        backButton.Click += (_, _) => { if (activeTab?.WebView.CoreWebView2.CanGoBack == true) activeTab.WebView.CoreWebView2.GoBack(); };
        forwardButton.Click += (_, _) => { if (activeTab?.WebView.CoreWebView2.CanGoForward == true) activeTab.WebView.CoreWebView2.GoForward(); };
        reloadButton.Click += (_, _) =>
        {
            if (activeTab is not null)
            {
                if (activeTab.WebView.CoreWebView2?.IsLoading == true) activeTab.WebView.CoreWebView2.Stop();
                else activeTab.WebView.CoreWebView2?.Reload();
            }
        };
        bookmarkButton.Click += (_, _) => AddBookmark();
        menuButton.Click += (_, _) => ShowBrowserMenu(menuButton);

        addressBox.BorderStyle = BorderStyle.None;
        addressBox.BackColor = Color.FromArgb(39, 42, 57);
        addressBox.ForeColor = Color.FromArgb(238, 239, 246);
        addressBox.Font = new Font("Segoe UI", 10F);
        addressBox.Width = 650;
        addressBox.Height = 30;
        addressBox.Margin = new Padding(8, 7, 8, 0);
        addressBox.KeyDown += AddressBox_KeyDown;

        toolbar.Controls.Add(backButton);
        toolbar.Controls.Add(forwardButton);
        toolbar.Controls.Add(reloadButton);
        toolbar.Controls.Add(addressBox);
        toolbar.Controls.Add(bookmarkButton);
        toolbar.Controls.Add(menuButton);
        header.Controls.Add(tabStrip);
        header.Controls.Add(toolbar);

        browserHost.Dock = DockStyle.Fill;
        browserHost.BackColor = Color.White;
        Controls.Add(browserHost);
        Controls.Add(header);

        Resize += (_, _) => addressBox.Width = Math.Max(220, toolbar.ClientSize.Width - 270);
    }

    private async void BrowserForm_Shown(object? sender, EventArgs e)
    {
        try
        {
            Directory.CreateDirectory(profilePath);
            Directory.CreateDirectory(browserDataPath);
            LoadHistory();
            environment = await CoreWebView2Environment.CreateAsync(null, profilePath);
            await CreateTabAsync("https://www.google.com");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Không thể khởi tạo trình duyệt WebView2.\n\n{ex.Message}",
                "Orbit Browser", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close();
        }
    }

    private async Task<BrowserTab> CreateTabAsync(string url, bool activate = true)
    {
        if (environment is null)
            throw new InvalidOperationException("WebView2 chưa được khởi tạo.");

        var page = new Panel { Dock = DockStyle.Fill, Visible = false };
        var webView = new WebView2
        {
            Dock = DockStyle.Fill,
            DefaultBackgroundColor = Color.White
        };
        page.Controls.Add(webView);
        browserHost.Controls.Add(page);
        var tab = new BrowserTab(webView, page, url);
        tabs.Add(tab);

        var tabHeader = new Panel
        {
            Width = 190,
            Height = 30,
            Margin = new Padding(0, 2, 5, 0),
            BackColor = Color.FromArgb(39, 42, 57)
        };
        var title = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Tab mới",
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            Padding = new Padding(10, 0, 0, 0),
            ForeColor = ForeColor,
            Cursor = Cursors.Hand
        };
        var close = new Button
        {
            Dock = DockStyle.Right,
            Width = 30,
            Text = "×",
            FlatStyle = FlatStyle.Flat,
            ForeColor = ForeColor,
            BackColor = tabHeader.BackColor,
            Cursor = Cursors.Hand,
            TabStop = false
        };
        close.FlatAppearance.BorderSize = 0;
        title.Click += (_, _) => ActivateTab(tab);
        close.Click += async (_, _) => await CloseTabAsync(tab);
        tabHeader.Controls.Add(title);
        tabHeader.Controls.Add(close);
        tab.Header = tabHeader;
        tab.TitleLabel = title;
        tabStrip.Controls.Add(tabHeader);

        try
        {
            await webView.EnsureCoreWebView2Async(environment);
        }
        catch
        {
            tabs.Remove(tab);
            tabStrip.Controls.Remove(tabHeader);
            browserHost.Controls.Remove(page);
            page.Dispose();
            throw;
        }

        var core = webView.CoreWebView2;
        webView.CoreWebView2Controller.AcceleratorKeyPressed += (_, args) =>
            HandleBrowserShortcut(args);
        core.Settings.AreDefaultContextMenusEnabled = true;
        core.Settings.AreDevToolsEnabled = true;
        core.Settings.IsZoomControlEnabled = true;
        core.Settings.IsStatusBarEnabled = true;
        core.NavigationStarting += (_, args) =>
        {
            if (tabs.Contains(tab))
            {
                if (ReferenceEquals(activeTab, tab)) addressBox.Text = args.Uri;
                reloadButton.Text = "×";
            }
        };
        core.NavigationCompleted += (_, args) =>
        {
            if (!tabs.Contains(tab)) return;
            reloadButton.Text = "↻";
            UpdateNavigationButtons();
            UpdateTabTitle(tab);
            if (args.IsSuccess && Uri.TryCreate(core.Source, UriKind.Absolute, out var currentUri))
            {
                history.Insert(0, new HistoryEntry(
                    string.IsNullOrWhiteSpace(core.DocumentTitle) ? currentUri.Host : core.DocumentTitle,
                    core.Source));
                if (history.Count > 100) history.RemoveAt(history.Count - 1);
                SaveHistory();
            }
        };
        core.DocumentTitleChanged += (_, _) => UpdateTabTitle(tab);
        core.SourceChanged += (_, _) =>
        {
            if (ReferenceEquals(activeTab, tab) && Uri.TryCreate(core.Source, UriKind.Absolute, out var currentUri))
                addressBox.Text = currentUri.IsFile ? currentUri.LocalPath : core.Source;
        };
        core.NewWindowRequested += async (_, args) =>
        {
            args.Handled = true;
            var popupTab = await CreateTabAsync("about:blank");
            args.NewWindow = popupTab.WebView.CoreWebView2;
            ActivateTab(popupTab);
        };
        core.DownloadStarting += (_, args) =>
        {
            using var dialog = new SaveFileDialog
            {
                FileName = Path.GetFileName(args.ResultFilePath),
                Filter = "Tất cả tệp (*.*)|*.*",
                AddExtension = true,
                OverwritePrompt = true
            };
            if (dialog.ShowDialog(this) == DialogResult.OK)
                args.ResultFilePath = dialog.FileName;
            else
                args.Cancel = true;
        };

        webView.Visible = true;
        if (activate) ActivateTab(tab);
        if (!string.IsNullOrWhiteSpace(url) && url != "about:blank")
            Navigate(tab, url);
        return tab;
    }

    private static Button MakeButton(string text, string tooltip, int width = 38)
    {
        var button = new Button
        {
            Text = text,
            Width = width,
            Height = 32,
            Margin = new Padding(0, 0, 2, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(25, 27, 39),
            ForeColor = Color.FromArgb(225, 228, 239),
            Cursor = Cursors.Hand,
            TabStop = false,
            AccessibleName = tooltip
        };
        button.FlatAppearance.BorderSize = 0;
        var toolTip = new ToolTip();
        toolTip.SetToolTip(button, tooltip);
        return button;
    }

    private void ActivateTab(BrowserTab tab)
    {
        if (!tabs.Contains(tab)) return;
        foreach (var item in tabs)
        {
            item.Page.Visible = ReferenceEquals(item, tab);
            item.Header.BackColor = ReferenceEquals(item, tab)
                ? Color.FromArgb(69, 72, 99)
                : Color.FromArgb(39, 42, 57);
        }
        activeTab = tab;
        tab.Page.BringToFront();
        addressBox.Text = tab.WebView.CoreWebView2?.Source ?? tab.InitialUrl;
        UpdateNavigationButtons();
        UpdateBookmarkButton();
    }

    private async Task CloseTabAsync(BrowserTab tab)
    {
        if (!tabs.Remove(tab)) return;
        tabStrip.Controls.Remove(tab.Header);
        browserHost.Controls.Remove(tab.Page);
        tab.Page.Dispose();
        if (ReferenceEquals(activeTab, tab))
        {
            activeTab = null;
            if (tabs.Count == 0 && !isClosing)
                await CreateTabAsync("about:blank");
            else if (tabs.Count > 0)
                ActivateTab(tabs[^1]);
        }
    }

    private void AddressBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Enter) return;
        e.SuppressKeyPress = true;
        if (activeTab is not null) Navigate(activeTab, addressBox.Text);
        browserHost.Focus();
    }

    private static void Navigate(BrowserTab tab, string input)
    {
        var value = input.Trim();
        if (string.IsNullOrWhiteSpace(value)) return;
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps ||
             uri.Scheme == Uri.UriSchemeFile || uri.Scheme == "about"))
        {
            tab.WebView.CoreWebView2.Navigate(uri.AbsoluteUri);
            return;
        }

        if (File.Exists(value))
        {
            tab.WebView.CoreWebView2.Navigate(new Uri(Path.GetFullPath(value)).AbsoluteUri);
            return;
        }

        if (!value.Contains(' ') && value.Contains('.'))
        {
            tab.WebView.CoreWebView2.Navigate("https://" + value);
            return;
        }

        tab.WebView.CoreWebView2.Navigate("https://www.google.com/search?q=" + Uri.EscapeDataString(value));
    }

    private void BrowserForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.L)
        {
            addressBox.Focus();
            addressBox.SelectAll();
            e.SuppressKeyPress = true;
        }
        else if (e.Control && e.KeyCode == Keys.T)
        {
            _ = CreateTabAsync("about:blank");
            e.SuppressKeyPress = true;
        }
        else if (e.Control && e.KeyCode == Keys.W && activeTab is not null)
        {
            _ = CloseTabAsync(activeTab);
            e.SuppressKeyPress = true;
        }
        else if (e.Control && e.KeyCode == Keys.Tab && tabs.Count > 1 && activeTab is not null)
        {
            var index = tabs.IndexOf(activeTab);
            ActivateTab(tabs[(index + 1) % tabs.Count]);
            e.SuppressKeyPress = true;
        }
        else if (e.Alt && e.KeyCode == Keys.Left && activeTab?.WebView.CoreWebView2.CanGoBack == true)
            activeTab.WebView.CoreWebView2.GoBack();
        else if (e.Alt && e.KeyCode == Keys.Right && activeTab?.WebView.CoreWebView2.CanGoForward == true)
            activeTab.WebView.CoreWebView2.GoForward();
        else if (e.KeyCode == Keys.F5)
            activeTab?.WebView.CoreWebView2?.Reload();
        else if (e.KeyCode == Keys.F11)
            ToggleFullscreen();
        else if (e.KeyCode == Keys.Escape && isBrowserFullscreen)
            ToggleFullscreen();
    }

    private void HandleBrowserShortcut(CoreWebView2AcceleratorKeyPressedEventArgs e)
    {
        if (e.KeyEventKind != CoreWebView2KeyEventKind.KeyDown) return;

        var modifiers = Control.ModifierKeys;
        var key = (Keys)e.VirtualKey;
        if (modifiers == Keys.Control && key == Keys.L)
        {
            e.Handled = true;
            BeginInvoke(new Action(() =>
            {
                addressBox.Focus();
                addressBox.SelectAll();
            }));
        }
        else if (modifiers == Keys.Control && key == Keys.T)
        {
            e.Handled = true;
            BeginInvoke(new Action(() => _ = CreateTabAsync("about:blank")));
        }
        else if (modifiers == Keys.Control && key == Keys.W && activeTab is not null)
        {
            var tab = activeTab;
            e.Handled = true;
            BeginInvoke(new Action(() => _ = CloseTabAsync(tab)));
        }
        else if (modifiers == Keys.Control && key == Keys.Tab && tabs.Count > 1 && activeTab is not null)
        {
            var index = tabs.IndexOf(activeTab);
            e.Handled = true;
            BeginInvoke(new Action(() => ActivateTab(tabs[(index + 1) % tabs.Count])));
        }
        else if (modifiers == Keys.Alt && key == Keys.Left && activeTab?.WebView.CoreWebView2.CanGoBack == true)
        {
            e.Handled = true;
            BeginInvoke(new Action(() => activeTab?.WebView.CoreWebView2?.GoBack()));
        }
        else if (modifiers == Keys.Alt && key == Keys.Right && activeTab?.WebView.CoreWebView2.CanGoForward == true)
        {
            e.Handled = true;
            BeginInvoke(new Action(() => activeTab?.WebView.CoreWebView2?.GoForward()));
        }
        else if (modifiers == Keys.None && key == Keys.F5)
        {
            e.Handled = true;
            BeginInvoke(new Action(() => activeTab?.WebView.CoreWebView2?.Reload()));
        }
        else if (modifiers == Keys.Control && key == Keys.R)
        {
            e.Handled = true;
            BeginInvoke(new Action(() => activeTab?.WebView.CoreWebView2?.Reload()));
        }
        else if (modifiers == Keys.None && key == Keys.F11)
        {
            e.Handled = true;
            BeginInvoke(new Action(ToggleFullscreen));
        }
        else if (modifiers == Keys.None && key == Keys.Escape && isBrowserFullscreen)
        {
            e.Handled = true;
            BeginInvoke(new Action(ToggleFullscreen));
        }
    }

    private void ToggleFullscreen()
    {
        if (!isBrowserFullscreen)
        {
            previousBorderStyle = FormBorderStyle;
            previousWindowState = WindowState;
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
            isBrowserFullscreen = true;
            return;
        }

        WindowState = FormWindowState.Normal;
        FormBorderStyle = previousBorderStyle;
        WindowState = previousWindowState;
        isBrowserFullscreen = false;
    }

    private void UpdateNavigationButtons()
    {
        backButton.Enabled = activeTab?.WebView.CoreWebView2?.CanGoBack == true;
        forwardButton.Enabled = activeTab?.WebView.CoreWebView2?.CanGoForward == true;
    }

    private void UpdateTabTitle(BrowserTab tab)
    {
        if (!tabs.Contains(tab)) return;
        var pageTitle = tab.WebView.CoreWebView2?.DocumentTitle;
        tab.TitleLabel.Text = string.IsNullOrWhiteSpace(pageTitle) ? "Tab mới" : pageTitle;
        if (ReferenceEquals(activeTab, tab))
            Text = string.IsNullOrWhiteSpace(pageTitle) ? "Orbit Browser" : $"{pageTitle} - Orbit Browser";
    }

    private void AddBookmark()
    {
        if (activeTab?.WebView.CoreWebView2 is not { } core || !Uri.TryCreate(core.Source, UriKind.Absolute, out _))
            return;
        var bookmarks = ReadBookmarks();
        if (bookmarks.Any(item => item.Url == core.Source)) return;
        bookmarks.Add(new Bookmark(string.IsNullOrWhiteSpace(core.DocumentTitle) ? core.Source : core.DocumentTitle, core.Source));
        Directory.CreateDirectory(Path.GetDirectoryName(bookmarksPath)!);
        File.WriteAllText(bookmarksPath, JsonSerializer.Serialize(bookmarks, new JsonSerializerOptions { WriteIndented = true }));
        UpdateBookmarkButton();
    }

    private List<Bookmark> ReadBookmarks()
    {
        if (!File.Exists(bookmarksPath)) return new List<Bookmark>();
        return JsonSerializer.Deserialize<List<Bookmark>>(File.ReadAllText(bookmarksPath)) ?? new List<Bookmark>();
    }

    private void LoadHistory()
    {
        if (!File.Exists(historyPath)) return;
        try
        {
            var savedHistory = JsonSerializer.Deserialize<List<HistoryEntry>>(File.ReadAllText(historyPath));
            if (savedHistory is not null) history.AddRange(savedHistory.Take(100));
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"Không thể đọc lịch sử trình duyệt.\n\n{ex.Message}",
                "Orbit Browser", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void SaveHistory()
    {
        try
        {
            File.WriteAllText(historyPath, JsonSerializer.Serialize(history));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"Không thể lưu lịch sử trình duyệt.\n\n{ex.Message}",
                "Orbit Browser", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void UpdateBookmarkButton()
    {
        var isSaved = activeTab?.WebView.CoreWebView2 is { } core &&
            ReadBookmarks().Any(item => item.Url == core.Source);
        bookmarkButton.Text = isSaved ? "★" : "☆";
        bookmarkButton.AccessibleName = isSaved ? "Đã lưu bookmark" : "Thêm bookmark";
    }

    private void ShowBrowserMenu(Control anchor)
    {
        var menu = new ContextMenuStrip();
        var bookmarks = ReadBookmarks();
        var bookmarkMenu = new ToolStripMenuItem("Bookmarks");
        if (bookmarks.Count == 0)
            bookmarkMenu.DropDownItems.Add(new ToolStripMenuItem("Chưa có bookmark") { Enabled = false });
        foreach (var bookmark in bookmarks)
        {
            var item = new ToolStripMenuItem(bookmark.Title) { ToolTipText = bookmark.Url };
            item.Click += (_, _) =>
            {
                if (activeTab is not null) Navigate(activeTab, bookmark.Url);
            };
            bookmarkMenu.DropDownItems.Add(item);
        }

        var historyMenu = new ToolStripMenuItem("Lịch sử");
        if (history.Count == 0)
            historyMenu.DropDownItems.Add(new ToolStripMenuItem("Chưa có lịch sử") { Enabled = false });
        foreach (var entry in history.Take(20))
        {
            var item = new ToolStripMenuItem(entry.Title) { ToolTipText = entry.Url };
            item.Click += (_, _) =>
            {
                if (activeTab is not null) Navigate(activeTab, entry.Url);
            };
            historyMenu.DropDownItems.Add(item);
        }

        menu.Items.Add(bookmarkMenu);
        menu.Items.Add(historyMenu);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Tab mới", null, async (_, _) => await CreateTabAsync("about:blank"));
        menu.Items.Add("Tải lại", null, (_, _) => activeTab?.WebView.CoreWebView2?.Reload());
        menu.Items.Add("Cài đặt thu phóng", null, (_, _) =>
        {
            var webView = activeTab?.WebView;
            if (webView is null) return;
            webView.ZoomFactor = Math.Clamp(webView.ZoomFactor + 0.1, 0.5, 2.0);
        });
        menu.Items.Add("DevTools", null, (_, _) => activeTab?.WebView.CoreWebView2?.OpenDevToolsWindow());
        menu.Show(anchor, new Point(anchor.Width, anchor.Height));
    }

    private sealed class BrowserTab(WebView2 webView, Panel page, string initialUrl)
    {
        public WebView2 WebView { get; } = webView;
        public Panel Page { get; } = page;
        public string InitialUrl { get; } = initialUrl;
        public Panel Header { get; set; } = null!;
        public Label TitleLabel { get; set; } = null!;
    }

    private sealed record Bookmark(string Title, string Url);
    private sealed record HistoryEntry(string Title, string Url);
}
