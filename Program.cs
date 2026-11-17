using System.Text.Json;
namespace BatteryDisplay;

class Preference { public bool Show { get; set; } = true; public string Kind { get; set; } = "Другое"; }
class Settings { public Dictionary<string, Preference> Devices { get; set; } = new(); public List<string> Monitors { get; set; } = new(); public int RightOffset { get; set; } = 220; public bool Above { get; set; } = false; public int PollSeconds { get; set; } = 30; public bool AnchorToTray { get; set; } = true; }
static class Program
{
    [STAThread] static void Main(string[] args) { ApplicationConfiguration.Initialize(); using var mutex = new Mutex(true, "BatteryDisplay.App", out bool first); if (!first) return; Application.Run(new Tray()); }
}
class Tray : ApplicationContext
{
    readonly TrayMenu popup = new(); readonly Control dispatcher = new(); readonly NotifyIcon tray; readonly System.Windows.Forms.Timer timer = new() { Interval = 30000 }; readonly System.Windows.Forms.Timer layout = new() { Interval = 250 }; readonly List<Badge> badges = new();
    readonly string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BatteryDisplay", "settings.json");
    public Settings Config = new(); public List<Reading> Readings = new(); bool busy; Form? settings;
    public Tray()
    {
        try { if (File.Exists(path)) Config = JsonSerializer.Deserialize<Settings>(File.ReadAllText(path)) ?? new(); } catch { }
        if (Config.Monitors.Count == 0) Config.Monitors.Add(Screen.PrimaryScreen!.DeviceName);
        _ = dispatcher.Handle; SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext()); tray = new() { Icon = SystemIcons.Information, Text = "BatteryDisplay", Visible = true }; tray.MouseUp += (_, e) => { if (e.Button == MouseButtons.Right) ShowMenu(); }; tray.DoubleClick += (_, _) => dispatcher.BeginInvoke(OpenSettings); timer.Tick += async (_, _) => await Refresh(); layout.Tick += (_, _) => Position(); timer.Interval = Math.Clamp(Config.PollSeconds, 5, 3600) * 1000; timer.Start(); layout.Start(); dispatcher.BeginInvoke(async () => { await Refresh(); if (Environment.GetCommandLineArgs().Contains("--settings")) OpenSettings(); });
    }
    public void ShowMenu() { var command = popup.ShowAtCursor(); if (command == 1) dispatcher.BeginInvoke(OpenSettings); else if (command == 2) _ = Refresh(); else if (command == 3) ExitThread(); }
    public async Task Refresh() { if (busy) return; busy = true; try { Readings = await Task.Run(Native.Scan); foreach (var r in Readings) if (!Config.Devices.ContainsKey(r.Id)) Config.Devices[r.Id] = new() { Kind = r.Kind }; foreach (var b in badges.ToList()) if (!Screen.AllScreens.Any(s => s.DeviceName == b.Monitor && Config.Monitors.Contains(s.DeviceName))) { b.Close(); badges.Remove(b); } foreach (var s in Screen.AllScreens.Where(s => Config.Monitors.Contains(s.DeviceName))) if (!badges.Any(b => b.Monitor == s.DeviceName)) { var b = new Badge(this, s); badges.Add(b); b.Show(); } foreach (var b in badges) b.UpdateTooltip(); Position(); } catch (Exception e) { tray.Text = "BatteryDisplay: ошибка чтения"; Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(Path.Combine(Path.GetDirectoryName(path)!, "error.log"), e.ToString()); } finally { busy = false; } }
    void Position() { foreach (var b in badges) b.Place(); }
    public void Save() { Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(Config, new JsonSerializerOptions { WriteIndented = true })); File.Move(path + ".tmp", path, true); timer.Interval = Math.Clamp(Config.PollSeconds, 5, 3600) * 1000; _ = Refresh(); }
    public void OpenSettings() { if (dispatcher.InvokeRequired) { dispatcher.BeginInvoke(OpenSettings); return; } try { if (settings is not { IsDisposed: false }) settings = new Options(this); settings.Show(); if (settings.WindowState == FormWindowState.Minimized) settings.WindowState = FormWindowState.Normal; TaskbarAnchor.ShowWindow(settings.Handle, 9); settings.BringToFront(); settings.Activate(); TaskbarAnchor.SetForegroundWindow(settings.Handle); } catch (Exception ex) { MessageBox.Show(ex.ToString(), "BatteryDisplay — ошибка настроек"); } }
    protected override void ExitThreadCore() { timer.Stop(); layout.Stop(); foreach (var b in badges) b.Close(); settings?.Close(); tray.Visible = false; tray.Dispose(); popup.Dispose(); dispatcher.Dispose(); timer.Dispose(); layout.Dispose(); base.ExitThreadCore(); }
}
class Badge : Form
{
    public string Monitor => monitor; readonly Tray owner; readonly string monitor; readonly ToolTip tooltip = new();
    public Badge(Tray owner, Screen screen) { this.owner = owner; monitor = screen.DeviceName; FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true; BackColor = Color.FromArgb(30, 32, 38); ForeColor = Color.White; AutoScaleMode = AutoScaleMode.Dpi; Font = new Font("Segoe UI", 10); DoubleBuffered = true; Cursor = Cursors.Hand; MouseUp += (_, e) => { if (e.Button == MouseButtons.Right) owner.ShowMenu(); else if (e.Button == MouseButtons.Left) owner.OpenSettings(); }; UpdateTooltip(); }
    public void UpdateTooltip() { tooltip.SetToolTip(this, string.Join("\n", owner.Readings.Where(r => owner.Config.Devices[r.Id].Show).Select(r => $"{r.Name}: {(r.Percent.HasValue ? r.Percent + "%" : "—")} • {r.Status}"))); }
    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams { get { var p = base.CreateParams; p.ExStyle |= 0x08000080; return p; } }
    public void Place() { var s = Screen.AllScreens.FirstOrDefault(s => s.DeviceName == monitor); if (s == null) { Hide(); return; } var rows = owner.Readings.Where(r => owner.Config.Devices[r.Id].Show).ToList(); if (rows.Count == 0) { Hide(); return; } int unit = (int)(72 * DeviceDpi / 96f); Width = Math.Min(rows.Sum(r => r.Percent.HasValue ? unit : (int)(36 * DeviceDpi / 96f)), s.Bounds.Width / 2); Height = (int)(30 * DeviceDpi / 96f); int bottom = s.Bounds.Bottom; bool taskbar = s.WorkingArea.Bottom < s.Bounds.Bottom; int y = owner.Config.Above ? s.WorkingArea.Bottom - Height - 4 : taskbar ? s.WorkingArea.Bottom + (bottom - s.WorkingArea.Bottom - Height) / 2 : bottom - Height - 4; var anchor = owner.Config.AnchorToTray ? TaskbarAnchor.TrayBounds(s) : null; int right = anchor?.Left - 6 ?? s.Bounds.Right - owner.Config.RightOffset; if (anchor.HasValue && !owner.Config.Above) y = anchor.Value.Top + (anchor.Value.Height - Height) / 2; Location = new(Math.Max(s.Bounds.Left, right - Width), y); if (!Visible) Show(); TaskbarAnchor.RaiseWithoutFocus(this); Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        float scale = DeviceDpi / 96f; int left = 0;
        foreach (var r in owner.Readings.Where(r => owner.Config.Devices[r.Id].Show))
        {
            int cell = (int)((r.Percent.HasValue ? 72 : 36) * scale);
            var color = DeviceVisuals.ColorFor(r);
            DeviceVisuals.DrawIcon(e.Graphics, owner.Config.Devices[r.Id].Kind, new RectangleF(left + 7 * scale, (Height - 20 * scale) / 2, 20 * scale, 20 * scale), color);
            if (r.Percent.HasValue) TextRenderer.DrawText(e.Graphics, r.Percent + "%", Font, new Rectangle(left + (int)(30 * scale), 0, cell - (int)(30 * scale), Height), color, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);
            left += cell;
        }
    }
    protected override void Dispose(bool disposing) { if (disposing) tooltip.Dispose(); base.Dispose(disposing); }
}
class Options : Form
{
    public Options(Tray app) { Text = "BatteryDisplay — настройки"; AutoScaleMode = AutoScaleMode.Dpi; Size = new(960, 640); MinimumSize = new(700, 450); StartPosition = FormStartPosition.CenterScreen; Font = new("Segoe UI", 10); var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 1, RowCount = 5 }; root.RowStyles.Add(new(SizeType.Absolute, 55)); root.RowStyles.Add(new(SizeType.Percent, 100)); root.RowStyles.Add(new(SizeType.Absolute, 80)); root.RowStyles.Add(new(SizeType.Absolute, 110)); root.RowStyles.Add(new(SizeType.Absolute, 45)); Controls.Add(root); root.Controls.Add(new Label { Text = "Устройства и заряд\n— означает отсутствие данных, а не нулевой заряд.", AutoSize = true }); var grid = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Color.White }; grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Показать", FillWeight = 45 }); grid.Columns.Add("name", "Устройство"); grid.Columns.Add(new DataGridViewComboBoxColumn { HeaderText = "Тип", DataSource = new[] { "Мышь", "Клавиатура", "Наушники", "Геймпад", "Другое" } }); grid.Columns.Add("charge", "Заряд"); grid.Columns.Add("status", "Источник / состояние"); grid.Columns[1].ReadOnly = grid.Columns[3].ReadOnly = grid.Columns[4].ReadOnly = true; grid.Columns[4].FillWeight = 170; foreach (var r in app.Readings) { int n = grid.Rows.Add(app.Config.Devices[r.Id].Show, r.Name, app.Config.Devices[r.Id].Kind, r.Percent.HasValue ? r.Percent + "%" : "—", r.Status); grid.Rows[n].Tag = r.Id; } root.Controls.Add(grid); var monitors = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true }; foreach (var s in Screen.AllScreens) monitors.Items.Add(s.DeviceName, app.Config.Monitors.Contains(s.DeviceName)); root.Controls.Add(monitors); var display = new FlowLayoutPanel { Dock = DockStyle.Fill }; var offset = new NumericUpDown { Minimum = 0, Maximum = 5000, Value = Math.Clamp(app.Config.RightOffset, 0, 5000), Width = 80 }; var above = new CheckBox { Text = "Над панелью задач", Checked = app.Config.Above, AutoSize = true }; display.Controls.Add(new Label { Text = "Отступ справа (px):", AutoSize = true, Padding = new Padding(0, 5, 0, 0) }); display.Controls.Add(offset); display.Controls.Add(above); var anchorBox = new CheckBox { Text = "Привязать к области трея", AutoSize = true, Checked = app.Config.AnchorToTray }; display.Controls.Add(anchorBox); var poll = new NumericUpDown { Minimum = 5, Maximum = 3600, Value = Math.Clamp(app.Config.PollSeconds, 5, 3600), Width = 90 }; display.SetFlowBreak(anchorBox, true); display.Controls.Add(new Label { Text = "Опрос каждые (сек.):", AutoSize = true, Padding = new Padding(0, 5, 0, 0) }); display.Controls.Add(poll); root.Controls.Add(display); var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft }; var save = new Button { Text = "Сохранить", AutoSize = true }; save.Click += (_, _) => { grid.EndEdit(); foreach (DataGridViewRow row in grid.Rows) { var p = app.Config.Devices[(string)row.Tag!]; p.Show = Convert.ToBoolean(row.Cells[0].Value); p.Kind = row.Cells[2].Value?.ToString() ?? "Другое"; } app.Config.Monitors = monitors.CheckedItems.Cast<string>().ToList(); app.Config.RightOffset = (int)offset.Value; app.Config.Above = above.Checked; app.Config.AnchorToTray = anchorBox.Checked; app.Config.PollSeconds = (int)poll.Value; try { app.Save(); Close(); } catch (Exception ex) { MessageBox.Show(ex.Message, "Ошибка сохранения"); } }; buttons.Controls.Add(save); root.Controls.Add(buttons); }
}








