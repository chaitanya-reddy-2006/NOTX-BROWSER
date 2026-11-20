using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace NOTXBrowser;

public partial class MainWindow : Window
{
    private const string HomeUrl = "https://www.google.com";
    private bool _isUpdatingAddress;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await CreateTabAsync(HomeUrl);
    }

    private async Task CreateTabAsync(string url)
    {
        var webView = new WebView2
        {
            DefaultBackgroundColor = System.Drawing.Color.FromArgb(16, 17, 22)
        };

        var content = new Grid();
        content.Children.Add(webView);

        var tab = new TabItem
        {
            Header = MakeTabHeader("New Tab"),
            Content = content,
            Tag = webView
        };

        BrowserTabs.Items.Add(tab);
        BrowserTabs.SelectedItem = tab;

        try
        {
            await webView.EnsureCoreWebView2Async();

            // Keep browser content isolated from the desktop application's WPF process.
            webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            webView.CoreWebView2.Settings.AreDevToolsEnabled = true;
            webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            webView.CoreWebView2.Settings.IsZoomControlEnabled = true;

            webView.NavigationStarting += (_, e) =>
            {
                if (BrowserTabs.SelectedItem == tab)
                    SetAddress(e.Uri);
            };

            webView.NavigationCompleted += (_, _) =>
            {
                if (BrowserTabs.SelectedItem == tab)
                    UpdateNavigationButtons();
            };

            webView.SourceChanged += (_, _) =>
            {
                if (BrowserTabs.SelectedItem == tab && webView.Source != null)
                    SetAddress(webView.Source.ToString());
            };

            webView.CoreWebView2.DocumentTitleChanged += (_, _) =>
            {
                Dispatcher.Invoke(() =>
                {
                    var title = webView.CoreWebView2.DocumentTitle;
                    UpdateTabTitle(tab, string.IsNullOrWhiteSpace(title) ? "New Tab" : title);
                });
            };

            webView.CoreWebView2.NewWindowRequested += (_, e) =>
            {
                e.Handled = true;
                _ = CreateTabAsync(e.Uri);
            };

            webView.Source = new Uri(url);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "NOTX Browser could not initialize Microsoft Edge WebView2.\n\n" +
                "Install the Microsoft Edge WebView2 Runtime and restart the browser.\n\n" +
                ex.Message,
                "WebView2 initialization error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static StackPanel MakeTabHeader(string title)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        var label = new TextBlock
        {
            Text = Shorten(title, 22),
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 170,
            TextTrimming = System.Windows.TextTrimming.CharacterEllipsis
        };
        var close = new Button
        {
            Content = "×",
            Padding = new Thickness(5, 0, 5, 1),
            Margin = new Thickness(10, 0, 0, 0),
            Background = System.Windows.Media.Brushes.Transparent,
            Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0),
            FontSize = 15,
            Cursor = Cursors.Hand,
            ToolTip = "Close tab"
        };
        panel.Children.Add(label);
        panel.Children.Add(close);
        close.Click += (_, _) =>
        {
            var owner = FindAncestorTabItem(panel);
            if (owner?.Parent is TabControl tabs)
            {
                if (tabs.Items.Count == 1)
                {
                    if (Application.Current.MainWindow is MainWindow main)
                        _ = main.CreateTabAsync(HomeUrl);
                }
                tabs.Items.Remove(owner);
            }
        };
        return panel;
    }

    private static TabItem? FindAncestorTabItem(DependencyObject child)
    {
        var current = child;
        while (current != null)
        {
            if (current is TabItem tab) return tab;
            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private static string Shorten(string value, int max)
        => value.Length <= max ? value : value[..(max - 1)] + "…";

    private void UpdateTabTitle(TabItem tab, string title)
    {
        if (tab.Header is StackPanel panel && panel.Children.Count > 0 &&
            panel.Children[0] is TextBlock label)
            label.Text = Shorten(title, 22);
    }

    private WebView2? CurrentWebView => (BrowserTabs.SelectedItem as TabItem)?.Tag as WebView2;

    private void SetAddress(string address)
    {
        _isUpdatingAddress = true;
        AddressBox.Text = address;
        _isUpdatingAddress = false;
    }

    private void NavigateFromAddress()
    {
        var text = AddressBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;

        string destination;
        if (Uri.TryCreate(text, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            destination = uri.ToString();
        }
        else if (!text.Contains(' ') && text.Contains('.') &&
                 Uri.TryCreate("https://" + text, UriKind.Absolute, out var domainUri))
        {
            destination = domainUri.ToString();
        }
        else
        {
            destination = "https://www.google.com/search?q=" + Uri.EscapeDataString(text);
        }

        if (CurrentWebView != null)
            CurrentWebView.Source = new Uri(destination);
    }

    private void Go_Click(object sender, RoutedEventArgs e) => NavigateFromAddress();

    private void AddressBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            NavigateFromAddress();
            e.Handled = true;
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentWebView?.CanGoBack == true) CurrentWebView.GoBack();
    }

    private void Forward_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentWebView?.CanGoForward == true) CurrentWebView.GoForward();
    }

    private void Reload_Click(object sender, RoutedEventArgs e) => CurrentWebView?.Reload();

    private void Home_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentWebView != null) CurrentWebView.Source = new Uri(HomeUrl);
    }

    private async void NewTab_Click(object sender, RoutedEventArgs e) => await CreateTabAsync(HomeUrl);

    private void BrowserTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CurrentWebView?.Source != null)
            SetAddress(CurrentWebView.Source.ToString());
        UpdateNavigationButtons();
    }

    private void UpdateNavigationButtons()
    {
        BackButton.IsEnabled = CurrentWebView?.CanGoBack == true;
        ForwardButton.IsEnabled = CurrentWebView?.CanGoForward == true;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.L)
        {
            AddressBox.Focus();
            AddressBox.SelectAll();
            e.Handled = true;
        }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.T)
        {
            _ = CreateTabAsync(HomeUrl);
            e.Handled = true;
        }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.W)
        {
            if (BrowserTabs.SelectedItem is TabItem tab && BrowserTabs.Items.Count > 1)
                BrowserTabs.Items.Remove(tab);
            e.Handled = true;
        }
        else if (e.Key == Key.F5)
        {
            CurrentWebView?.Reload();
            e.Handled = true;
        }

        base.OnPreviewKeyDown(e);
    }
}
