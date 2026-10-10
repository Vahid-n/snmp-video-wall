using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using SnmpVideoWall.Controls;
using SnmpVideoWall.Models;
using SnmpVideoWall.Services;
using System.Text.Json;

namespace SnmpVideoWall;

public partial class MainWindow : Window
{
    private readonly SnmpPoller _poller = new();
    private List<PortMonitor> _monitors;
    private AppSettings _settings = new();
    private readonly string _configPath = Path.Combine(AppContext.BaseDirectory, "monitors.json");
    private readonly string _settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
    private bool _polling;
    private readonly DispatcherTimer _timer;
    private readonly Dictionary<PortMonitor, Border> _cards = new();
    private bool _customLayout;
    private double _cardWidth = 520;
    private double _cardHeight = 330;

    public MainWindow()
    {
        InitializeComponent();
        _monitors = LoadMonitors();
        _settings = LoadSettings();
        _poller.TimeoutMilliseconds = _settings.SnmpTimeoutMilliseconds;
        SettingsButton.Click += async (_, _) => await ShowSettingsDialog();
        PortsButton.Click += async (_, _) => await ShowPortsDialog();
        SaveWorkspaceButton.Click += async (_, _) => await SaveWorkspaceAs();
        LoadWorkspaceButton.Click += async (_, _) => await LoadWorkspaceFromFile();
        AboutButton.Click += async (_, _) => await ShowAboutDialog();
        TimeWindowCombo.SelectionChanged += (_, _) => RefreshGraphs();
        Opened += (_, _) => { LayoutCards(); _ = PollAll(); };
        SizeChanged += (_, _) => { if (!_customLayout) LayoutCards(); };
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(Math.Clamp(_settings.PollIntervalSeconds, 1, 60)) };
        _timer.Tick += async (_, _) => await PollAll();
        _timer.Start();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.F11) WindowState = WindowState == WindowState.FullScreen ? WindowState.Maximized : WindowState.FullScreen;
        };
    }

    private List<PortMonitor> LoadMonitors()
    {
        var path = _configPath;
        if (!File.Exists(path))
        {
            FooterText.Text = $"Configuration not found: {path}";
            return new List<PortMonitor>();
        }
        try
        {
            var json = File.ReadAllText(path)
                .Replace("capacitybps", "capacityBitsPerSecond", StringComparison.OrdinalIgnoreCase)
                .Replace("capacityBps", "capacityBitsPerSecond", StringComparison.OrdinalIgnoreCase)
                .Replace("rxLowBps", "rxLowBitsPerSecond", StringComparison.OrdinalIgnoreCase)
                .Replace("rxHighBps", "rxHighBitsPerSecond", StringComparison.OrdinalIgnoreCase)
                .Replace("txLowBps", "txLowBitsPerSecond", StringComparison.OrdinalIgnoreCase)
                .Replace("txHighBps", "txHighBitsPerSecond", StringComparison.OrdinalIgnoreCase)
                .Replace("rxLowThresholdbps", "rxLowBitsPerSecond", StringComparison.OrdinalIgnoreCase)
                .Replace("rxLowThresholdBps", "rxLowBitsPerSecond", StringComparison.OrdinalIgnoreCase)
                .Replace("rxHighThresholdbps", "rxHighBitsPerSecond", StringComparison.OrdinalIgnoreCase)
                .Replace("rxHighThresholdBps", "rxHighBitsPerSecond", StringComparison.OrdinalIgnoreCase)
                .Replace("txLowThresholdbps", "txLowBitsPerSecond", StringComparison.OrdinalIgnoreCase)
                .Replace("txLowThresholdBps", "txLowBitsPerSecond", StringComparison.OrdinalIgnoreCase)
                .Replace("txHighThresholdbps", "txHighBitsPerSecond", StringComparison.OrdinalIgnoreCase)
                .Replace("txHighThresholdBps", "txHighBitsPerSecond", StringComparison.OrdinalIgnoreCase);
            var loaded = JsonSerializer.Deserialize<List<PortMonitor>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            foreach (var m in loaded)
            {
                // Migrate older per-port thresholds into both schedules, preserving prior behavior.
                if (!m.UseCustomThresholds && (m.RxLowThresholdBps > 0 || m.RxHighThresholdBps > 0 || m.TxLowThresholdBps > 0 || m.TxHighThresholdBps > 0))
                {
                    m.UseCustomThresholds = true;
                    m.DayThresholds = new ThresholdSet { RxLowBps=m.RxLowThresholdBps, RxHighBps=m.RxHighThresholdBps, TxLowBps=m.TxLowThresholdBps, TxHighBps=m.TxHighThresholdBps };
                    m.NightThresholds = new ThresholdSet { RxLowBps=m.RxLowThresholdBps, RxHighBps=m.RxHighThresholdBps, TxLowBps=m.TxLowThresholdBps, TxHighBps=m.TxHighThresholdBps };
                }
            }
            return loaded;
        }
        catch (Exception ex)
        {
            FooterText.Text = $"Configuration error: {ex.Message}";
            return new List<PortMonitor>();
        }
    }

    private void LayoutCards()
    {
        if (!IsVisible || _customLayout) return;
        var availableWidth = Math.Max(800, Bounds.Width - 55);
        var columns = Math.Max(1, (int)(availableWidth / _cardWidth));
        var rows = (int)Math.Ceiling(_monitors.Count / (double)columns);
        CardsCanvas.Width = Math.Max(availableWidth, columns * _cardWidth);
        CardsCanvas.Height = Math.Max(400, rows * _cardHeight);
        if (_cards.Count != _monitors.Count) BuildCards();
        for (var i = 0; i < _monitors.Count; i++)
        {
            var card = _cards[_monitors[i]];
            Canvas.SetLeft(card, (i % columns) * _cardWidth + 5);
            Canvas.SetTop(card, (i / columns) * _cardHeight + 5);
            card.Width = _cardWidth - 12;
            card.Height = _cardHeight - 12;
        }
    }

    private Border BuildCard(PortMonitor m)
    {
        var graph = new TrafficGraph { MinHeight = 0, MinWidth = 0, VerticalAlignment = VerticalAlignment.Stretch, ClipToBounds = true,
            RxLow = m.RxLowThresholdBps, RxHigh = m.RxHighThresholdBps,
            TxLow = m.TxLowThresholdBps, TxHigh = m.TxHighThresholdBps, WindowSeconds = SelectedWindowSeconds() };
        var status = new TextBlock { Text = "● WAITING", Foreground = Brushes.Gray, FontSize = 10, FontWeight = FontWeight.Bold, VerticalAlignment = VerticalAlignment.Center };
        var rxValue = new TextBlock { Text = "RX --", FontSize = 13, Foreground = Brushes.DeepSkyBlue, FontWeight = FontWeight.Bold };
        var txValue = new TextBlock { Text = "TX --", FontSize = 13, Foreground = Brushes.LimeGreen, FontWeight = FontWeight.Bold };
        var alert = new Border { Background = new SolidColorBrush(Color.Parse("#59250A")), CornerRadius = new CornerRadius(4), Padding = new Thickness(5,3), IsVisible = false };
        var alertText = new TextBlock { Text = "", FontSize = 10, Foreground = Brushes.White, FontWeight = FontWeight.Bold, TextWrapping = Avalonia.Media.TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        alert.Child = alertText;
        m.Tag = (graph, status, rxValue, txValue, alert, alertText);

        var panel = new Grid { RowDefinitions = new RowDefinitions("38,*,22,8"), ClipToBounds = true };
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0,0,0,3), Cursor = new Cursor(StandardCursorType.SizeAll) };
        var labels = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        labels.Children.Add(new TextBlock { Text = m.Name, Foreground = Brushes.White, FontSize = 14, FontWeight = FontWeight.Bold, TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 1 });
        labels.Children.Add(new TextBlock { Text = $"{m.Interface} • {m.Ip}", Foreground = Brushes.LightGray, FontSize = 10, TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 1 });
        Grid.SetColumn(labels, 0); header.Children.Add(labels);
        var values = new StackPanel { Orientation = Orientation.Vertical, Spacing = 1, Margin = new Thickness(4,0,0,0), VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Right };
        values.Children.Add(rxValue); values.Children.Add(txValue); Grid.SetColumn(values,1); header.Children.Add(values);
        Grid.SetRow(header,0); panel.Children.Add(header);
        Grid.SetRow(graph,1); panel.Children.Add(graph);
        var statusLine = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), VerticalAlignment = VerticalAlignment.Center, ClipToBounds = true };
        status.Margin = new Thickness(0,0,7,0);
        statusLine.Children.Add(status);
        alert.Padding = new Thickness(4,1); alert.Margin = new Thickness(0); alert.VerticalAlignment = VerticalAlignment.Center; alert.Height = 20;
        alert.Child = alertText; alert.HorizontalAlignment = HorizontalAlignment.Stretch;
        Grid.SetColumn(alert,1); statusLine.Children.Add(alert); Grid.SetRow(statusLine,2); panel.Children.Add(statusLine);
        var resizeGrip = new TextBlock { Text = "◢", FontSize = 13, Foreground = new SolidColorBrush(Color.Parse("#718096")), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Cursor = new Cursor(StandardCursorType.BottomRightCorner), Margin = new Thickness(0,0,1,0) };
        Grid.SetRow(resizeGrip,3); panel.Children.Add(resizeGrip);
        var card = new Border { Width = _cardWidth - 12, Height = _cardHeight - 12, MinWidth = 155, MinHeight = 145, Background = Brushes.Black, BorderBrush = new SolidColorBrush(Color.Parse("#29313D")), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(9), Padding = new Thickness(10), Child = panel, ClipToBounds = true };

        AttachDrag(header, card);
        AttachResize(resizeGrip, card, graph);
        return card;
    }

    private void BuildCards()
    {
        CardsCanvas.Children.Clear(); _cards.Clear();
        foreach (var m in _monitors)
        {
            var card = BuildCard(m); _cards[m] = card; CardsCanvas.Children.Add(card);
        }
        if (!_customLayout) LayoutCards();
    }

    private void AttachDrag(Control dragHandle, Border card)
    {
        Point startPointer = default; double startLeft = 0, startTop = 0; bool dragging = false;
        dragHandle.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(dragHandle).Properties.IsLeftButtonPressed) return;
            startPointer = e.GetPosition(CardsCanvas); startLeft = Canvas.GetLeft(card); startTop = Canvas.GetTop(card);
            dragging = true; dragHandle.PointerCaptureLost += (_, _) => dragging = false; e.Pointer.Capture(dragHandle); e.Handled = true;
        };
        dragHandle.PointerMoved += (_, e) =>
        {
            if (!dragging || !e.GetCurrentPoint(dragHandle).Properties.IsLeftButtonPressed) return;
            var p = e.GetPosition(CardsCanvas); Canvas.SetLeft(card, Math.Max(0, startLeft + p.X - startPointer.X)); Canvas.SetTop(card, Math.Max(0, startTop + p.Y - startPointer.Y));
            _customLayout = true; UpdateCanvasExtent();
        };
        dragHandle.PointerReleased += (_, e) => { dragging = false; e.Pointer.Capture(null); };
    }

    private void AttachResize(Control grip, Border card, TrafficGraph graph)
    {
        Point start = default; double width = 0, height = 0; bool resizing = false;
        grip.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(grip).Properties.IsLeftButtonPressed) return;
            start = e.GetPosition(CardsCanvas); width = card.Width; height = card.Height; resizing = true; e.Pointer.Capture(grip); e.Handled = true;
        };
        grip.PointerMoved += (_, e) =>
        {
            if (!resizing || !e.GetCurrentPoint(grip).Properties.IsLeftButtonPressed) return;
            var p = e.GetPosition(CardsCanvas); card.Width = Math.Clamp(width + p.X - start.X, 155, 1200); card.Height = Math.Clamp(height + p.Y - start.Y, 145, 900);
            _customLayout = true; UpdateCanvasExtent();
        };
        grip.PointerReleased += (_, e) => { resizing = false; e.Pointer.Capture(null); };
    }

    private void UpdateCanvasExtent()
    {
        var right = CardsCanvas.Children.OfType<Control>().Select(c => Canvas.GetLeft(c) + c.Bounds.Width).DefaultIfEmpty(1200).Max();
        var bottom = CardsCanvas.Children.OfType<Control>().Select(c => Canvas.GetTop(c) + c.Bounds.Height).DefaultIfEmpty(700).Max();
        CardsCanvas.Width = Math.Max(Bounds.Width - 35, right + 20); CardsCanvas.Height = Math.Max(Bounds.Height - 170, bottom + 20);
    }

    private async Task PollAll()
    {
        if (_polling) return;
        _polling = true;
        try { await Task.WhenAll(_monitors.Select(m => _poller.PollAsync(m, CancellationToken.None))); }
        finally { _polling = false; }
        var now = DateTime.Now;
        foreach (var m in _monitors) m.ActiveThresholds = ResolveThresholds(m, now);
        foreach (var m in _monitors)
        {
            if (m.Tag is not ValueTuple<TrafficGraph,TextBlock,TextBlock,TextBlock,Border,TextBlock> tag) continue;
            var (graph,status,rxv,txv,alert,alertText)=tag;
            graph.RxValues=m.RxHistory.ToArray(); graph.TxValues=m.TxHistory.ToArray(); graph.Times=m.HistoryTimes.ToArray(); graph.WindowSeconds=SelectedWindowSeconds(); graph.RxLow=m.ActiveThresholds.RxLowBps; graph.RxHigh=m.ActiveThresholds.RxHighBps; graph.TxLow=m.ActiveThresholds.TxLowBps; graph.TxHigh=m.ActiveThresholds.TxHighBps; graph.InvalidateVisual();
            rxv.Text=$"RX  {Format(m.RxBps)}"; txv.Text=$"TX  {Format(m.TxBps)}";
            var activeAlerts = m.Online ? m.ActiveAlerts : new List<string>();
            status.Text = !m.Online ? "● SNMP ERROR" : activeAlerts.Count > 0 ? "● ALARM" : "● ONLINE";
            status.Foreground = !m.Online ? Brushes.OrangeRed : activeAlerts.Count > 0 ? Brushes.Orange : Brushes.LimeGreen;
            // Keep this row reserved even when hidden, so showing an alarm never moves the graph.
            alert.IsVisible = activeAlerts.Count > 0 || !m.Online;
            alert.Background = activeAlerts.Count > 0 ? new SolidColorBrush(Color.Parse("#59250A")) : new SolidColorBrush(Color.Parse("#111820"));
            alertText.Text = activeAlerts.Count > 0 ? "⚠ " + string.Join(" • ", activeAlerts) : (!m.Online ? "⚠ SNMP error: " + m.Error : string.Empty);
            alertText.Foreground = Brushes.White;
            alert.MinWidth = 0;
            var border = _cards.GetValueOrDefault(m);
            if (border != null) border.BorderBrush = activeAlerts.Count > 0 ? Brushes.OrangeRed : new SolidColorBrush(Color.Parse("#29313D"));
        }
        FooterText.Text=$"Ports: {_monitors.Count}  •  Online: {_monitors.Count(x=>x.Online)}  •  SNMP: {_settings.PollIntervalSeconds}s  •  Window: {WindowLabel()}";
    }

    private async Task SaveWorkspaceAs()
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save SNMP Video Wall workspace",
            SuggestedFileName = $"SNMPVideoWall-workspace-{DateTime.Now:yyyyMMdd-HHmm}.json",
            DefaultExtension = "json",
            FileTypeChoices = new[] { new FilePickerFileType("SNMP Video Wall workspace") { Patterns = new[] { "*.json" } } }
        });
        if (file is null) return;
        try
        {
            var snapshot = new WorkspaceSnapshot
            {
                Version = "0.3.13",
                Monitors = _monitors,
                Settings = _settings,
                TimeWindowSeconds = SelectedWindowSeconds(),
                CustomLayout = _customLayout,
                Cards = _monitors.Select((m, index) =>
                {
                    var card = _cards.GetValueOrDefault(m);
                    return new CardLayout
                    {
                        Index = index,
                        Name = m.Name,
                        Ip = m.Ip,
                        Interface = m.Interface,
                        Left = card is null ? 5 + (index % 2) * (_cardWidth + 5) : Canvas.GetLeft(card),
                        Top = card is null ? 5 + (index / 2) * (_cardHeight + 5) : Canvas.GetTop(card),
                        Width = card?.Width ?? (_cardWidth - 12),
                        Height = card?.Height ?? (_cardHeight - 12)
                    };
                }).ToList()
            };
            await using var stream = await file.OpenWriteAsync();
            stream.SetLength(0);
            await JsonSerializer.SerializeAsync(stream, snapshot, new JsonSerializerOptions { WriteIndented = true });
            FooterText.Text = $"Workspace saved: {Path.GetFileName(file.Path.LocalPath)}";
        }
        catch (Exception ex) { FooterText.Text = $"Could not save workspace: {ex.Message}"; }
    }

    private async Task LoadWorkspaceFromFile()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Load SNMP Video Wall workspace",
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("SNMP Video Wall workspace") { Patterns = new[] { "*.json" } } }
        });
        var file = files.FirstOrDefault();
        if (file is null) return;
        try
        {
            await using var stream = await file.OpenReadAsync();
            var snapshot = await JsonSerializer.DeserializeAsync<WorkspaceSnapshot>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (snapshot?.Monitors is null || snapshot.Settings is null) throw new InvalidDataException("The selected file is not a valid workspace file.");
            _monitors = snapshot.Monitors;
            _settings = snapshot.Settings;
            SaveMonitors();
            SaveSettings();
            _poller.TimeoutMilliseconds = _settings.SnmpTimeoutMilliseconds;
            _timer.Stop();
            _timer.Interval = TimeSpan.FromSeconds(Math.Clamp(_settings.PollIntervalSeconds, 1, 60));
            _timer.Start();
            var seconds = snapshot.TimeWindowSeconds;
            var match = TimeWindowCombo.Items.OfType<ComboBoxItem>().FirstOrDefault(x => int.TryParse(x.Tag?.ToString(), out var value) && value == seconds);
            if (match is not null) TimeWindowCombo.SelectedItem = match;
            _customLayout = snapshot.CustomLayout || snapshot.Cards.Count > 0;
            RebuildCards();
            foreach (var layout in snapshot.Cards)
            {
                if (layout.Index < 0 || layout.Index >= _monitors.Count) continue;
                var monitor = _monitors[layout.Index];
                if (!_cards.TryGetValue(monitor, out var card)) continue;
                Canvas.SetLeft(card, Math.Max(0, layout.Left));
                Canvas.SetTop(card, Math.Max(0, layout.Top));
                card.Width = Math.Clamp(layout.Width, 155, 1200);
                card.Height = Math.Clamp(layout.Height, 105, 900);
            }
            UpdateCanvasExtent();
            RefreshGraphs();
            FooterText.Text = $"Workspace loaded: {Path.GetFileName(file.Path.LocalPath)}";
            _ = PollAll();
        }
        catch (Exception ex) { FooterText.Text = $"Could not load workspace: {ex.Message}"; }
    }

    private async Task ShowAboutDialog()
    {
        var content = new StackPanel { Spacing = 10, Margin = new Thickness(22), Width = 430, Background = Brushes.White };
        content.Children.Add(new TextBlock { Text = "SNMP Video Wall", FontSize = 24, FontWeight = FontWeight.Bold, Foreground = new SolidColorBrush(Color.Parse("#172033")) });
        content.Children.Add(new TextBlock { Text = "Version 0.3.13", FontSize = 13, Foreground = new SolidColorBrush(Color.Parse("#245A8D")) });
        content.Children.Add(new TextBlock { Text = "A live SNMP interface traffic dashboard for network operations teams.", TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.Parse("#334155")) });
        content.Children.Add(new TextBlock { Text = "Project author: [Your name]", Foreground = new SolidColorBrush(Color.Parse("#172033")) });
        content.Children.Add(new TextBlock { Text = "Role / organization: [Your title or organization]", Foreground = new SolidColorBrush(Color.Parse("#172033")) });
        content.Children.Add(new TextBlock { Text = "Contact: [Optional email or website]", Foreground = new SolidColorBrush(Color.Parse("#172033")) });
        content.Children.Add(new TextBlock { Text = "GitHub: [Your GitHub profile or repository URL]", TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.Parse("#172033")) });
        content.Children.Add(new TextBlock { Text = "SNMP v2c • RX/TX rates in bps • Day/night thresholds • Workspace save/load", TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.Parse("#475569")) });
        var close = new Button { Content = "Close", HorizontalAlignment = HorizontalAlignment.Right, Padding = new Thickness(16, 6) };
        content.Children.Add(close);
        StyleDialogTree(content);
        var win = new Window { Title = "About SNMP Video Wall", Width = 520, Height = 390, SizeToContent = SizeToContent.Height, Background = Brushes.White, WindowStartupLocation = WindowStartupLocation.CenterOwner, RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light, Content = content };
        close.Click += (_, _) => win.Close();
        await win.ShowDialog(this);
    }

    private sealed class WorkspaceSnapshot
    {
        public string Version { get; set; } = "0.3.13";
        public List<PortMonitor> Monitors { get; set; } = new();
        public AppSettings Settings { get; set; } = new();
        public int TimeWindowSeconds { get; set; } = 60;
        public bool CustomLayout { get; set; }
        public List<CardLayout> Cards { get; set; } = new();
    }

    private sealed class CardLayout
    {
        public int Index { get; set; }
        public string Name { get; set; } = "";
        public string Ip { get; set; } = "";
        public string Interface { get; set; } = "";
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }

    private AppSettings LoadSettings()
    {
        try { return File.Exists(_settingsPath) ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_settingsPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new AppSettings() : new AppSettings(); }
        catch { return new AppSettings(); }
    }

    private void SaveSettings() => File.WriteAllText(_settingsPath, JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true }));
    private void SaveMonitors() => File.WriteAllText(_configPath, JsonSerializer.Serialize(_monitors, new JsonSerializerOptions { WriteIndented = true }));

    private ThresholdSet ResolveThresholds(PortMonitor m, DateTime now)
    {
        var t = m.UseCustomThresholds ? m.DayThresholds : _settings.Day;
        if (TimeSpan.TryParse(_settings.DayStart, out var dayStart) && TimeSpan.TryParse(_settings.NightStart, out var nightStart))
        {
            var tod = now.TimeOfDay;
            bool day = dayStart < nightStart ? tod >= dayStart && tod < nightStart : tod >= dayStart || tod < nightStart;
            t = day ? (m.UseCustomThresholds ? m.DayThresholds : _settings.Day) : (m.UseCustomThresholds ? m.NightThresholds : _settings.Night);
        }
        return new ThresholdSet { RxLowBps=t.RxLowBps, RxHighBps=t.RxHighBps, TxLowBps=t.TxLowBps, TxHighBps=t.TxHighBps };
    }

    private async Task ShowSettingsDialog()
    {
        var poll = new ComboBox { Width = 140, Foreground=new SolidColorBrush(Color.Parse("#172033")), Background=Brushes.White };
        foreach (var n in new[] {1,2,5,10,30,60}) poll.Items.Add($"{n} second(s)");
        poll.SelectedIndex = new[] {1,2,5,10,30,60}.ToList().IndexOf(_settings.PollIntervalSeconds);
        if (poll.SelectedIndex < 0) poll.SelectedIndex = 0;
        var timeout = new ComboBox { Width=140, Foreground=new SolidColorBrush(Color.Parse("#172033")), Background=Brushes.White };
        foreach (var n in new[] {500,1000,1500,2000,3000,5000}) timeout.Items.Add($"{n} ms");
        timeout.SelectedIndex = new[] {500,1000,1500,2000,3000,5000}.ToList().IndexOf(_settings.SnmpTimeoutMilliseconds);
        if(timeout.SelectedIndex<0) timeout.SelectedIndex=2;
        var dayStart = new TextBox { Text = _settings.DayStart, Width=90, Foreground=new SolidColorBrush(Color.Parse("#172033")), Background=Brushes.White, CaretBrush=new SolidColorBrush(Color.Parse("#172033")), BorderBrush=new SolidColorBrush(Color.Parse("#94A3B8")) };
        var nightStart = new TextBox { Text = _settings.NightStart, Width=90, Foreground=new SolidColorBrush(Color.Parse("#172033")), Background=Brushes.White, CaretBrush=new SolidColorBrush(Color.Parse("#172033")), BorderBrush=new SolidColorBrush(Color.Parse("#94A3B8")) };
        var day = ThresholdEditors(_settings.Day); var night = ThresholdEditors(_settings.Night);
        var globalAll = new CheckBox { Content="Use global day/night thresholds for ALL ports", IsChecked=_monitors.All(m => !m.UseCustomThresholds), Foreground=new SolidColorBrush(Color.Parse("#172033")) };
        var content = new StackPanel { Spacing=8, Margin=new Thickness(16), Width=460 };
        content.Children.Add(new TextBlock { Text="Application settings", FontSize=20, FontWeight=FontWeight.Bold, Foreground=new SolidColorBrush(Color.Parse("#172033")) });
        content.Children.Add(Labeled("SNMP polling interval", poll)); content.Children.Add(Labeled("SNMP timeout", timeout));
        content.Children.Add(new TextBlock { Text="Polling interval applies to all ports (1–60 seconds); shorter intervals increase SNMP load. Schedule uses local PC time. Thresholds and graph values are bit/s (bps), not bytes/s (B/s). SNMP counter OIDs return octets; rate = counter delta × 8 / elapsed seconds. Enter 0 to disable a threshold.", TextWrapping=TextWrapping.Wrap, Foreground=new SolidColorBrush(Color.Parse("#334155")) });
        var times = new StackPanel { Orientation=Orientation.Horizontal, Spacing=12 }; times.Children.Add(Labeled("Day starts",dayStart)); times.Children.Add(Labeled("Night starts",nightStart)); content.Children.Add(times);
        content.Children.Add(new TextBlock { Text="DAY threshold defaults (global)", FontWeight=FontWeight.Bold, Foreground=new SolidColorBrush(Color.Parse("#172033")) }); content.Children.Add(day.Panel);
        content.Children.Add(new TextBlock { Text="NIGHT threshold defaults (global)", FontWeight=FontWeight.Bold, Foreground=new SolidColorBrush(Color.Parse("#172033")) }); content.Children.Add(night.Panel);
        content.Children.Add(globalAll);
        var buttons = new StackPanel { Orientation=Orientation.Horizontal, HorizontalAlignment=HorizontalAlignment.Right, Spacing=8 };
        var save = new Button { Content="Save settings" }; var cancel = new Button { Content="Cancel" }; buttons.Children.Add(save); buttons.Children.Add(cancel); content.Children.Add(buttons);
        StyleDialogTree(content);
        var win = new Window { Title="SNMP Video Wall Settings", Width=600, Height=760, Background=new SolidColorBrush(Color.Parse("#F1F5F9")), WindowStartupLocation=WindowStartupLocation.CenterOwner, RequestedThemeVariant=Avalonia.Styling.ThemeVariant.Light, Content=new ScrollViewer { Background=new SolidColorBrush(Color.Parse("#F1F5F9")), Content=content } };
        save.Click += (_,_) => { if (!TimeSpan.TryParse(dayStart.Text, out _) || !TimeSpan.TryParse(nightStart.Text, out _)) { FooterText.Text="Please enter schedule times as HH:mm."; return; }
            _settings.PollIntervalSeconds = new[] {1,2,5,10,30,60}[Math.Max(0,poll.SelectedIndex)]; _settings.SnmpTimeoutMilliseconds = new[] {500,1000,1500,2000,3000,5000}[Math.Max(0,timeout.SelectedIndex)];
            _settings.DayStart=dayStart.Text!; _settings.NightStart=nightStart.Text!; ReadThresholdEditors(day, _settings.Day); ReadThresholdEditors(night, _settings.Night);
            if (globalAll.IsChecked == true) { foreach (var monitor in _monitors) monitor.UseCustomThresholds = false; SaveMonitors(); }
            SaveSettings(); _poller.TimeoutMilliseconds=_settings.SnmpTimeoutMilliseconds; _timer.Stop(); _timer.Interval=TimeSpan.FromSeconds(_settings.PollIntervalSeconds); _timer.Start(); RefreshGraphs(); win.Close(); };
        cancel.Click += (_,_) => win.Close(); await win.ShowDialog(this);
    }

    private async Task ShowPortsDialog()
    {
        var list = new ListBox { Width=500, Height=280, ItemsSource=_monitors, Foreground=new SolidColorBrush(Color.Parse("#172033")), Background=Brushes.White };
        list.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<PortMonitor>((m, _) => new TextBlock { Text=$"{m.Name} | {m.Ip} | {m.Interface}", Foreground=new SolidColorBrush(Color.Parse("#172033")), Background=Brushes.White, Margin=new Thickness(4) }, true);
        var content = new StackPanel { Spacing=8, Margin=new Thickness(16), Background=new SolidColorBrush(Color.Parse("#F1F5F9")) }; content.Children.Add(new TextBlock { Text="Manage monitored ports", FontSize=20, FontWeight=FontWeight.Bold, Foreground=new SolidColorBrush(Color.Parse("#172033")) }); content.Children.Add(list);
        var buttons = new StackPanel { Orientation=Orientation.Horizontal, Spacing=8 }; var add=new Button{Content="Add"}; var duplicate=new Button{Content="Duplicate selected"}; var edit=new Button{Content="Edit selected"}; var remove=new Button{Content="Remove"}; var done=new Button{Content="Close"}; buttons.Children.Add(add); buttons.Children.Add(duplicate); buttons.Children.Add(edit); buttons.Children.Add(remove); buttons.Children.Add(done); content.Children.Add(buttons);
        StyleDialogTree(content);
        var win = new Window { Title="Manage ports", Width=640, Height=500, Background=new SolidColorBrush(Color.Parse("#F1F5F9")), WindowStartupLocation=WindowStartupLocation.CenterOwner, RequestedThemeVariant=Avalonia.Styling.ThemeVariant.Light, Content=new ScrollViewer { Background=new SolidColorBrush(Color.Parse("#F1F5F9")), Content=content } };
        add.Click += async (_,_) => { var m = new PortMonitor(); if (await EditPortDialog(m, true)) { _monitors.Add(m); SaveMonitors(); RebuildCards(); list.ItemsSource=null; list.ItemsSource=_monitors; } };
        duplicate.Click += async (_,_) => { if (list.SelectedItem is PortMonitor source) { var copy = CloneMonitor(source); copy.Name = source.Name + "-COPY"; if (await EditPortDialog(copy, true)) { _monitors.Add(copy); SaveMonitors(); RebuildCards(); list.ItemsSource=null; list.ItemsSource=_monitors; list.SelectedItem=copy; } } };
        edit.Click += async (_,_) => { if (list.SelectedItem is PortMonitor m && await EditPortDialog(m, false)) { SaveMonitors(); RebuildCards(); list.ItemsSource=null; list.ItemsSource=_monitors; } };
        remove.Click += (_,_) => { if(list.SelectedItem is PortMonitor m) { _monitors.Remove(m); SaveMonitors(); RebuildCards(); list.ItemsSource=null; list.ItemsSource=_monitors; } };
        done.Click += (_,_) => win.Close(); await win.ShowDialog(this);
    }

    private static PortMonitor CloneMonitor(PortMonitor source) => new()
    {
        Name=source.Name, Ip=source.Ip, Community=source.Community, Interface=source.Interface,
        RxOid=source.RxOid, TxOid=source.TxOid, CapacityBps=source.CapacityBps,
        UseCustomThresholds=source.UseCustomThresholds,
        DayThresholds=new ThresholdSet { RxLowBps=source.DayThresholds.RxLowBps, RxHighBps=source.DayThresholds.RxHighBps, TxLowBps=source.DayThresholds.TxLowBps, TxHighBps=source.DayThresholds.TxHighBps },
        NightThresholds=new ThresholdSet { RxLowBps=source.NightThresholds.RxLowBps, RxHighBps=source.NightThresholds.RxHighBps, TxLowBps=source.NightThresholds.TxLowBps, TxHighBps=source.NightThresholds.TxHighBps }
    };

    private async Task<bool> EditPortDialog(PortMonitor m, bool isNew)
    {
        var name=Field(m.Name); var ip=Field(m.Ip); var community=Field(m.Community); community.PasswordChar='●'; var iface=Field(m.Interface); var rxoid=Field(m.RxOid); var txoid=Field(m.TxOid); var capacity=Field(m.CapacityBps.ToString());
        var custom=new CheckBox { Content="Use separate day/night thresholds for this port (otherwise use global settings)", IsChecked=m.UseCustomThresholds, Foreground=new SolidColorBrush(Color.Parse("#172033")) };
        var day=ThresholdEditors(m.DayThresholds); var night=ThresholdEditors(m.NightThresholds);
        var content=new StackPanel { Spacing=6, Margin=new Thickness(14), Width=540, Background=new SolidColorBrush(Color.Parse("#F1F5F9")) };
        content.Children.Add(new TextBlock{Text=isNew?"Add port":"Edit port",FontSize=20,FontWeight=FontWeight.Bold,Foreground=new SolidColorBrush(Color.Parse("#172033"))});
        foreach(var pair in new[]{("Card name",name),("Device IP",ip),("SNMP v2c community",community),("Interface label",iface),("RX counter OID (octets; SNMP counter)",rxoid),("TX counter OID (octets; SNMP counter)",txoid),("Interface capacity (bit/s)",capacity)}) content.Children.Add(Labeled(pair.Item1,pair.Item2));
        content.Children.Add(custom); content.Children.Add(new TextBlock{Text="DAY thresholds for this port (used when separate thresholds are enabled)",FontWeight=FontWeight.Bold,Foreground=new SolidColorBrush(Color.Parse("#172033"))}); content.Children.Add(day.Panel); content.Children.Add(new TextBlock{Text="NIGHT thresholds for this port",FontWeight=FontWeight.Bold,Foreground=new SolidColorBrush(Color.Parse("#172033"))}); content.Children.Add(night.Panel);
        var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Spacing=8}; var save=new Button{Content="Save port"}; var cancel=new Button{Content="Cancel"}; buttons.Children.Add(save); buttons.Children.Add(cancel); content.Children.Add(buttons);
        StyleDialogTree(content);
        var win=new Window{Title="Port configuration",Width=700,Height=860,Background=new SolidColorBrush(Color.Parse("#F1F5F9")),WindowStartupLocation=WindowStartupLocation.CenterOwner,RequestedThemeVariant=Avalonia.Styling.ThemeVariant.Light,Content=new ScrollViewer{Background=new SolidColorBrush(Color.Parse("#F1F5F9")),Content=content}}; bool saved=false;
        save.Click += (_,_) => { if(!System.Net.IPAddress.TryParse(ip.Text,out _) || !long.TryParse(capacity.Text,out var cap) || cap<=0) { FooterText.Text="Invalid IP address or interface capacity."; return; }
            m.Name=name.Text??"Port"; m.Ip=ip.Text!; m.Community=community.Text??"public"; m.Interface=iface.Text??"Port"; m.RxOid=rxoid.Text??""; m.TxOid=txoid.Text??""; m.CapacityBps=cap; m.UseCustomThresholds=custom.IsChecked==true; ReadThresholdEditors(day,m.DayThresholds); ReadThresholdEditors(night,m.NightThresholds); m.PreviousRx=null; m.PreviousTx=null; m.HasRateSample=false; saved=true; win.Close(); };
        cancel.Click += (_,_)=>win.Close(); await win.ShowDialog(this); return saved;
    }

    private static void StyleDialogTree(Control root)
    {
        // Dialogs intentionally use a light input surface regardless of the main window's dark theme.
        var white = Brushes.White;
        var dark = new SolidColorBrush(Color.Parse("#172033"));
        var borderBrush = new SolidColorBrush(Color.Parse("#94A3B8"));
        if (root is Panel panel)
            foreach (var child in panel.Children.OfType<Control>()) StyleDialogTree(child);
        if (root is ContentControl cc && cc.Content is Control childControl) StyleDialogTree(childControl);
        if (root is ItemsControl items)
            foreach (var child in items.Items.OfType<Control>()) StyleDialogTree(child);
        if (root is ScrollViewer sv && sv.Content is Control content) StyleDialogTree(content);
        if (root is TextBlock tb) tb.Foreground = dark;
        if (root is TextBox textBox) { textBox.Foreground = dark; textBox.Background = white; textBox.CaretBrush = dark; textBox.BorderBrush = borderBrush; textBox.SelectionBrush = new SolidColorBrush(Color.Parse("#93C5FD")); }
        if (root is ComboBox combo) { combo.Foreground = dark; combo.Background = white; combo.BorderBrush = borderBrush; }
        if (root is ComboBoxItem comboItem) { comboItem.Foreground = dark; comboItem.Background = white; }
        if (root is ListBoxItem listItem) { listItem.Foreground = dark; listItem.Background = white; }
        if (root is ListBox list) { list.Foreground = dark; list.Background = white; }
        if (root is CheckBox check) { check.Foreground = dark; }
        if (root is Button button) { button.Foreground = white; button.Background = new SolidColorBrush(Color.Parse("#245A8D")); button.BorderBrush = new SolidColorBrush(Color.Parse("#1D4D78")); }
        if (root is Border border && border.Background == null) border.Background = white;
    }

    private static TextBox Field(string value) => new() { Text=value, Width=360, Foreground=new SolidColorBrush(Color.Parse("#172033")), Background=Brushes.White, BorderBrush=new SolidColorBrush(Color.Parse("#94A3B8")), CaretBrush=new SolidColorBrush(Color.Parse("#172033")), SelectionBrush=new SolidColorBrush(Color.Parse("#93C5FD")), Padding=new Thickness(6,4), Watermark=null };
    private static Control Labeled(string label, Control control) { var p=new StackPanel{Orientation=Orientation.Horizontal,Spacing=10,VerticalAlignment=VerticalAlignment.Center}; p.Children.Add(new TextBlock{Text=label,Width=170,VerticalAlignment=VerticalAlignment.Center,Foreground=new SolidColorBrush(Color.Parse("#334155"))}); p.Children.Add(control); return p; }
    private static (StackPanel Panel, TextBox RxLow, TextBox RxHigh, TextBox TxLow, TextBox TxHigh) ThresholdEditors(ThresholdSet t)
    {
        var rl=Field(t.RxLowBps.ToString(System.Globalization.CultureInfo.InvariantCulture)); var rh=Field(t.RxHighBps.ToString(System.Globalization.CultureInfo.InvariantCulture)); var tl=Field(t.TxLowBps.ToString(System.Globalization.CultureInfo.InvariantCulture)); var th=Field(t.TxHighBps.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var p=new StackPanel{Spacing=4}; p.Children.Add(Labeled("RX low (bps)",rl)); p.Children.Add(Labeled("RX high (bps)",rh)); p.Children.Add(Labeled("TX low (bps)",tl)); p.Children.Add(Labeled("TX high (bps)",th)); return(p,rl,rh,tl,th);
    }
    private static void ReadThresholdEditors((StackPanel Panel, TextBox RxLow, TextBox RxHigh, TextBox TxLow, TextBox TxHigh) e, ThresholdSet t)
    {
        static double Val(TextBox b) => double.TryParse(b.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v) ? Math.Max(0,v) : 0;
        t.RxLowBps=Val(e.RxLow); t.RxHighBps=Val(e.RxHigh); t.TxLowBps=Val(e.TxLow); t.TxHighBps=Val(e.TxHigh);
    }

    private void RebuildCards()
    {
        var previous = _cards.ToDictionary(
            pair => pair.Key,
            pair => (Left: Canvas.GetLeft(pair.Value), Top: Canvas.GetTop(pair.Value), Width: pair.Value.Width, Height: pair.Value.Height));
        foreach (var m in _monitors) m.Tag = null;
        _cards.Clear();
        CardsCanvas.Children.Clear();
        // Keep the user's manually arranged layout when editing settings or port details.
        _customLayout = previous.Count > 0;
        foreach (var m in _monitors)
        {
            var card = BuildCard(m);
            _cards[m] = card;
            CardsCanvas.Children.Add(card);
            if (previous.TryGetValue(m, out var box))
            {
                Canvas.SetLeft(card, double.IsFinite(box.Left) ? box.Left : 5);
                Canvas.SetTop(card, double.IsFinite(box.Top) ? box.Top : 5);
                card.Width = box.Width > 0 ? box.Width : _cardWidth - 12;
                card.Height = box.Height > 0 ? box.Height : _cardHeight - 12;
            }
            else
            {
                var index = _cards.Count - 1;
                Canvas.SetLeft(card, 5 + (index % 2) * (_cardWidth + 5));
                Canvas.SetTop(card, 5 + (int)(index / 2) * (_cardHeight + 5));
                card.Width = _cardWidth - 12;
                card.Height = _cardHeight - 12;
            }
        }
        UpdateCanvasExtent();
    }


    private int SelectedWindowSeconds()
    {
        if (TimeWindowCombo.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out var seconds)) return seconds;
        return 60;
    }
    private string WindowLabel() => SelectedWindowSeconds() switch { 60=>"1m",300=>"5m",900=>"15m",1800=>"30m",3600=>"1h",7200=>"2h",14400=>"4h",_=>"1m" };
    private void RefreshGraphs()
    {
        foreach (var m in _monitors) if (m.Tag is ValueTuple<TrafficGraph,TextBlock,TextBlock,TextBlock,Border,TextBlock> tag) { tag.Item1.WindowSeconds=SelectedWindowSeconds(); tag.Item1.InvalidateVisual(); }
    }
    private static string Format(double bps)
    {
        string[] units={"bps","kbps","Mbps","Gbps","Tbps"}; int i=0; while(Math.Abs(bps)>=1000 && i<units.Length-1){bps/=1000;i++;} return $"{bps:0.##} {units[i]}";
    }
}
