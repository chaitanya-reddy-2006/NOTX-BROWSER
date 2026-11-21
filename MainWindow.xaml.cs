using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace NOTXBrowser;

public partial class MainWindow : Window
{
    private const string HomeUrl = "https://www.google.com";
    private readonly string _dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NOTXBrowser");
    private readonly ObservableCollection<string> _bookmarks = new();
    private readonly ObservableCollection<string> _history = new();
    private readonly ObservableCollection<string> _downloads = new();
    private int _adsBlocked, _popupsBlocked;
    private static readonly string[] AdHostFragments = { "doubleclick.net", "googlesyndication.com", "googleadservices.com", "adservice.google.com", "adnxs.com", "adform.net", "taboola.com", "outbrain.com", "criteo.com", "scorecardresearch.com", "zedo.com", "adsrvr.org", "popads.net", "popcash.net", "adsterra.com", "propellerads.com" };
    private static readonly string[] AdUrlFragments = { "/pagead/", "/adsystem/", "/adserver/", "/advertising/", "doubleclick", "googlesyndication", "adservice", "popunder" };

    public MainWindow()
    {
        InitializeComponent();
        Directory.CreateDirectory(_dataDir);
        LoadCollection("bookmarks.json", _bookmarks);
        LoadCollection("history.json", _history);
        LoadCollection("downloads.json", _downloads);
        Loaded += async (_, _) => await CreateTabAsync(HomeUrl);
    }

    private void LoadCollection(string file, ObservableCollection<string> target)
    {
        try
        {
            var path = Path.Combine(_dataDir, file);
            if (File.Exists(path)) foreach (var item in JsonSerializer.Deserialize<List<string>>(File.ReadAllText(path)) ?? new()) target.Add(item);
        }
        catch { StatusText.Text = $"Could not load {file}"; }
    }

    private void SaveCollection(string file, ObservableCollection<string> source)
    {
        try { File.WriteAllText(Path.Combine(_dataDir, file), JsonSerializer.Serialize(source.Distinct().ToList())); }
        catch { StatusText.Text = "Could not save browser data"; }
    }

    private async Task CreateTabAsync(string url)
    {
        var webView = new WebView2 { DefaultBackgroundColor = System.Drawing.Color.FromArgb(11, 13, 19) };
        var content = new Grid(); content.Children.Add(webView);
        var tab = new TabItem { Header = MakeTabHeader("New Tab"), Content = content, Tag = webView };
        BrowserTabs.Items.Add(tab); BrowserTabs.SelectedItem = tab;
        try
        {
            await webView.EnsureCoreWebView2Async();
            var core = webView.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled = true;
            core.Settings.AreDevToolsEnabled = true;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsZoomControlEnabled = true;
            core.Settings.AreBrowserAcceleratorKeysEnabled = true;
            core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            core.WebResourceRequested += (_, e) =>
            {
                if (AdBlockToggle.IsChecked != true || !ShouldBlockAd(e.Request.Uri)) return;
                e.Response = core.Environment.CreateWebResourceResponse(new MemoryStream(), 204, "Blocked by NOTX", "Content-Type: text/plain");
                Interlocked.Increment(ref _adsBlocked);
                Dispatcher.Invoke(UpdateProtectionStatus);
            };
            webView.NavigationStarting += (_, e) =>
            {
                if (BrowserTabs.SelectedItem == tab) SetAddress(e.Uri);
                if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var historyUri) && (historyUri.Scheme == "http" || historyUri.Scheme == "https"))
                {
                    Dispatcher.Invoke(() => { _history.Insert(0, e.Uri); while (_history.Count > 1000) _history.RemoveAt(_history.Count - 1); SaveCollection("history.json", _history); });
                }
            };
            webView.NavigationCompleted += (_, e) =>
            {
                if (BrowserTabs.SelectedItem == tab) { UpdateNavigationButtons(); StatusText.Text = e.IsSuccess ? "Page loaded" : "Page load issue"; }
            };
            webView.SourceChanged += (_, _) => { if (BrowserTabs.SelectedItem == tab && webView.Source != null) SetAddress(webView.Source.ToString()); };
            core.DocumentTitleChanged += (_, _) => Dispatcher.Invoke(() => UpdateTabTitle(tab, string.IsNullOrWhiteSpace(core.DocumentTitle) ? "New Tab" : core.DocumentTitle));
            core.NewWindowRequested += (_, e) =>
            {
                e.Handled = true;
                if (PopupToggle.IsChecked == true)
                {
                    Interlocked.Increment(ref _popupsBlocked); Dispatcher.Invoke(() => { UpdateProtectionStatus(); StatusText.Text = "NOTX blocked a pop-up window"; });
                }
                else if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var popupUri) && (popupUri.Scheme == "https" || popupUri.Scheme == "http"))
                    Dispatcher.InvokeAsync(() => _ = CreateTabAsync(e.Uri));
            };
            core.DownloadStarting += (_, e) =>
            {
                var name = Path.GetFileName(e.ResultFilePath);
                Dispatcher.Invoke(() => { _downloads.Insert(0, $"{e.ResultFilePath} — {e.DownloadOperation.State}"); SaveCollection("downloads.json", _downloads); StatusText.Text = $"Downloading: {name}"; });
                e.DownloadOperation.StateChanged += (_, _) => Dispatcher.Invoke(() =>
                {
                    var item = $"{e.DownloadOperation.ResultFilePath} — {e.DownloadOperation.State}";
                    if (_downloads.Count > 0) _downloads[0] = item; SaveCollection("downloads.json", _downloads);
                });
            };
            webView.Source = new Uri(url);
        }
        catch (Exception ex)
        {
            MessageBox.Show("NOTX Browser could not initialize Microsoft Edge WebView2.\n\nInstall the Microsoft Edge WebView2 Runtime and restart the browser.\n\n" + ex.Message, "WebView2 initialization error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static bool ShouldBlockAd(string address)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri)) return false;
        var host = uri.Host.ToLowerInvariant(); var full = address.ToLowerInvariant();
        return AdHostFragments.Any(host.Contains) || AdUrlFragments.Any(full.Contains);
    }

    private static StackPanel MakeTabHeader(string title)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        var label = new TextBlock { Text = Shorten(title, 22), VerticalAlignment = VerticalAlignment.Center, MaxWidth = 170, TextTrimming = TextTrimming.CharacterEllipsis };
        var close = new Button { Content = "×", Padding = new Thickness(5, 0, 5, 1), Margin = new Thickness(10, 0, 0, 0), Background = Brushes.Transparent, Foreground = Brushes.White, BorderThickness = new Thickness(0), FontSize = 15, Cursor = Cursors.Hand, ToolTip = "Close tab" };
        panel.Children.Add(label); panel.Children.Add(close);
        close.Click += (_, _) => { var owner = FindAncestorTabItem(panel); if (owner?.Parent is TabControl tabs) { if (tabs.Items.Count == 1 && Application.Current.MainWindow is MainWindow main) _ = main.CreateTabAsync(HomeUrl); tabs.Items.Remove(owner); } };
        return panel;
    }

    private static TabItem? FindAncestorTabItem(DependencyObject child)
    {
        var current = child; while (current != null) { if (current is TabItem tab) return tab; current = VisualTreeHelper.GetParent(current); } return null;
    }
    private static string Shorten(string value, int max) => value.Length <= max ? value : value[..(max - 1)] + "…";
    private void UpdateTabTitle(TabItem tab, string title) { if (tab.Header is StackPanel p && p.Children[0] is TextBlock label) label.Text = Shorten(title, 22); }
    private WebView2? CurrentWebView => (BrowserTabs.SelectedItem as TabItem)?.Tag as WebView2;

    private void SetAddress(string address)
{
    AddressBox.Text = address;
}
    private void NavigateFromAddress()
    {
        var text = AddressBox.Text.Trim(); if (string.IsNullOrWhiteSpace(text)) return;
        string destination;
        if (Uri.TryCreate(text, UriKind.Absolute, out var uri) && (uri.Scheme == "http" || uri.Scheme == "https")) destination = uri.ToString();
        else if (!text.Contains(' ') && text.Contains('.') && Uri.TryCreate("https://" + text, UriKind.Absolute, out var domainUri)) destination = domainUri.ToString();
        else destination = "https://www.google.com/search?q=" + Uri.EscapeDataString(text);
        if (CurrentWebView != null) CurrentWebView.Source = new Uri(destination);
    }
    private void Go_Click(object sender, RoutedEventArgs e) => NavigateFromAddress();
    private void AddressBox_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { NavigateFromAddress(); e.Handled = true; } }
    private void Back_Click(object sender, RoutedEventArgs e) { if (CurrentWebView?.CanGoBack == true) CurrentWebView.GoBack(); }
    private void Forward_Click(object sender, RoutedEventArgs e) { if (CurrentWebView?.CanGoForward == true) CurrentWebView.GoForward(); }
    private void Reload_Click(object sender, RoutedEventArgs e) => CurrentWebView?.Reload();
    private void Home_Click(object sender, RoutedEventArgs e) { if (CurrentWebView != null) CurrentWebView.Source = new Uri(HomeUrl); }
    private async void NewTab_Click(object sender, RoutedEventArgs e) => await CreateTabAsync(HomeUrl);
    private void BrowserTabs_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (CurrentWebView?.Source != null) SetAddress(CurrentWebView.Source.ToString()); UpdateNavigationButtons(); }
    private void UpdateNavigationButtons() { BackButton.IsEnabled = CurrentWebView?.CanGoBack == true; ForwardButton.IsEnabled = CurrentWebView?.CanGoForward == true; }
    private void UpdateProtectionStatus() { ProtectionText.Text = $"🛡 {_adsBlocked:N0} ads blocked  •  {_popupsBlocked:N0} pop-ups blocked"; }
    private void AdBlockToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (AdBlockToggle.IsChecked == true) { AdBlockToggle.Content = "🛡 Ad blocker ON"; AdBlockToggle.Background = new SolidColorBrush(Color.FromRgb(32,55,47)); AdBlockToggle.Foreground = new SolidColorBrush(Color.FromRgb(143,224,179)); }
        else { AdBlockToggle.Content = "Ad blocker OFF"; AdBlockToggle.Background = new SolidColorBrush(Color.FromRgb(65,43,42)); AdBlockToggle.Foreground = Brushes.White; }
    }
    private void PopupToggle_Changed(object sender, RoutedEventArgs e) { PopupToggle.Content = PopupToggle.IsChecked == true ? "Pop-ups blocked" : "Pop-ups allowed"; }

    private void Bookmark_Click(object sender, RoutedEventArgs e)
    {
        var url = CurrentWebView?.Source?.ToString(); if (string.IsNullOrWhiteSpace(url)) return;
        if (!_bookmarks.Contains(url)) { _bookmarks.Insert(0, url); SaveCollection("bookmarks.json", _bookmarks); StatusText.Text = "Bookmark saved"; } else StatusText.Text = "Already bookmarked";
    }
    private void ShowBookmarks_Click(object sender, RoutedEventArgs e) => ShowListMenu(_bookmarks, "Bookmarks", "bookmarks.json", sender);
    private void ShowHistory_Click(object sender, RoutedEventArgs e) => ShowListMenu(_history, "History", "history.json", sender);
    private void ShowDownloads_Click(object sender, RoutedEventArgs e) => ShowListMenu(_downloads, "Downloads", "downloads.json", sender);
    private void ShowListMenu(ObservableCollection<string> items, string title, string file, object sender)
    {
        var menu = new ContextMenu(); menu.Items.Add(new MenuItem { Header = title, IsEnabled = false }); menu.Items.Add(new Separator());
        foreach (var item in items.Take(30)) { var entry = new MenuItem { Header = Shorten(item, 75), ToolTip = item }; entry.Click += (_, _) => { if (title == "Downloads") { var path = item.Split(" — ")[0]; if (File.Exists(path)) Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); } else if (title == "History") NavigateTo(item); else NavigateTo(item); }; menu.Items.Add(entry); }
        if (items.Count == 0) menu.Items.Add(new MenuItem { Header = "Nothing here yet", IsEnabled = false });
        menu.Items.Add(new Separator()); var clear = new MenuItem { Header = "Clear " + title.ToLowerInvariant() }; clear.Click += (_, _) => { items.Clear(); SaveCollection(file, items); }; menu.Items.Add(clear);
        menu.PlacementTarget = sender as UIElement ?? this; menu.IsOpen = true;
    }
    private void NavigateTo(string url) { if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == "http" || uri.Scheme == "https") && CurrentWebView != null) CurrentWebView.Source = uri; }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.L) { AddressBox.Focus(); AddressBox.SelectAll(); e.Handled = true; }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.T) { _ = CreateTabAsync(HomeUrl); e.Handled = true; }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.W) { if (BrowserTabs.SelectedItem is TabItem tab && BrowserTabs.Items.Count > 1) BrowserTabs.Items.Remove(tab); e.Handled = true; }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.D) { Bookmark_Click(this, new RoutedEventArgs()); e.Handled = true; }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.H) { ShowHistory_Click(this, new RoutedEventArgs()); e.Handled = true; }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.J) { ShowDownloads_Click(this, new RoutedEventArgs()); e.Handled = true; }
        else if (e.Key == Key.F5) { CurrentWebView?.Reload(); e.Handled = true; }
        else if (e.Key == Key.Escape) { /* Reserved for stopping navigation in a future update. */ }
    }
}
