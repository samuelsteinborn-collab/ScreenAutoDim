namespace ScreenAutoDim;

internal sealed class SettingsForm : Form
{
    private readonly TextBox _query = new() { Dock = DockStyle.Fill };
    private readonly Button _search = new() { Text = "Search", AutoSize = true };
    private readonly Button _map = new() { Text = "Map...", AutoSize = true };
    private readonly ListBox _results = new() { Dock = DockStyle.Fill, Height = 90, Visible = false };
    private readonly Label _selected = new() { AutoSize = true, MaximumSize = new Size(380, 0) };
    private readonly TrackBar _day = CreateSlider();
    private readonly TrackBar _night = CreateSlider();
    private readonly Label _dayValue = new() { AutoSize = true, Anchor = AnchorStyles.Right };
    private readonly Label _nightValue = new() { AutoSize = true, Anchor = AnchorStyles.Right };

    private readonly CheckBox _autostart = new() { Text = "Start ScreenAutoDim with Windows", AutoSize = true, Margin = new Padding(3, 10, 3, 3) };
    private readonly CheckBox _showTray = new() { Text = "Show icon in the system tray", AutoSize = true };
    private readonly NumericUpDown _sunriseFade = CreateMinutes(AppConfig.MinFadeMinutes, AppConfig.MaxFadeMinutes);
    private readonly NumericUpDown _dimBeforeSunset = CreateMinutes(0, AppConfig.MaxFadeMinutes);
    private readonly NumericUpDown _darkAfterSunset = CreateMinutes(0, AppConfig.MaxFadeMinutes);
    private readonly NumericUpDown _clouds = CreateMinutes(0, AppConfig.MaxCloudDimmingPercent);
    private readonly ToolTip _tips = new() { AutoPopDelay = 20000 };

    private string _place;
    private double? _latitude;
    private double? _longitude;

    /// <summary>The settings chosen by the user; only meaningful when <see cref="Form.DialogResult"/> is OK.</summary>
    public AppConfig Result { get; private set; }

    public bool StartWithWindows => _autostart.Checked;

    /// <summary>Raised when the user asks to quit the whole app, which matters when the tray icon is hidden.</summary>
    public event Action? QuitRequested;

    public SettingsForm(AppConfig config)
    {
        Result = config;
        _place = config.Place;
        _latitude = config.Latitude;
        _longitude = config.Longitude;

        Text = "ScreenAutoDim settings";
        Font = new Font("Segoe UI", 9f);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        _day.Value = config.DayBrightness;
        _night.Value = config.NightBrightness;
        _query.Text = config.Place;
        _autostart.Checked = Autostart.IsEnabled;
        _showTray.Checked = config.ShowTrayIcon;
        _sunriseFade.Value = config.SunriseFadeMinutes;
        _dimBeforeSunset.Value = config.DimBeforeSunsetMinutes;
        _darkAfterSunset.Value = config.DarkAfterSunsetMinutes;
        _clouds.Value = config.CloudDimmingPercent;
        UpdateLocationLabel();
        UpdateValueLabels();

        var save = new Button { Text = "Save", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        AcceptButton = save;
        CancelButton = cancel;
        // The window is shown modelessly, where a button's DialogResult alone does not close it.
        save.Click += (_, _) =>
        {
            Result = BuildConfig();
            DialogResult = DialogResult.OK;
            Close();
        };
        cancel.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        _search.Click += async (_, _) => await SearchAsync();
        _map.Click += (_, _) => PickOnMap();
        _query.KeyDown += async (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            await SearchAsync();
        };
        _results.SelectedIndexChanged += (_, _) =>
        {
            if (_results.SelectedItem is not Place place) return;
            (_place, _latitude, _longitude) = (place.Label, place.Latitude, place.Longitude);
            UpdateLocationLabel();
        };
        _day.ValueChanged += (_, _) => UpdateValueLabels();
        _night.ValueChanged += (_, _) => UpdateValueLabels();

        var hint = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(380, 0),
            ForeColor = SystemColors.GrayText,
            Text = "100% means your monitor's own brightness, with no dimming. " +
                   "Set the monitor's brightness in its menu to the level you like in daylight " +
                   "and ScreenAutoDim will only make it darker in the evening."
        };

        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill };
        var quit = new Button { Text = "Quit ScreenAutoDim", AutoSize = true, Margin = new Padding(20, 3, 3, 3) };
        quit.Click += (_, _) => QuitRequested?.Invoke();
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(save);
        buttons.Controls.Add(quit);

        var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Padding = new Padding(14), Width = 420 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        AddRow(layout, new Label { Text = "Location (city or place name)", AutoSize = true }, span: 2);
        var locationButtons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        locationButtons.Controls.Add(_search);
        locationButtons.Controls.Add(_map);
        layout.Controls.Add(_query, 0, layout.RowCount++);
        layout.Controls.Add(locationButtons, 1, layout.RowCount - 1);
        AddRow(layout, _results, span: 2);
        AddRow(layout, _selected, span: 2);
        AddRow(layout, new Label { Text = "Daytime brightness (clear sky)", AutoSize = true, Margin = new Padding(3, 14, 3, 0) }, _dayValue);
        AddRow(layout, _day, span: 2);
        AddRow(layout, new Label { Text = "Night brightness", AutoSize = true }, _nightValue);
        AddRow(layout, _night, span: 2);
        AddRow(layout, hint, span: 2);
        AddRow(layout, CreateCurveGroup(), span: 2);
        AddRow(layout, _autostart, span: 2);
        AddRow(layout, _showTray, span: 2);
        AddRow(layout, buttons, span: 2);
        Controls.Add(layout);
    }

    private GroupBox CreateCurveGroup()
    {
        var table = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Dock = DockStyle.Fill, Padding = new Padding(6) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        AddCurveRow(table, "Sunrise: brighten up over (minutes)", _sunriseFade,
            "Brightness starts rising at sunrise and reaches the daytime level after this many minutes.");
        AddCurveRow(table, "Sunset: start dimming before (minutes)", _dimBeforeSunset,
            "Dimming starts this many minutes before sunset, because it is already getting dark by then.");
        AddCurveRow(table, "Sunset: fully dark after (minutes)", _darkAfterSunset,
            "The night brightness is reached this many minutes after sunset.");
        AddCurveRow(table, "Cloudy weather: dim by up to (%)", _clouds,
            "On a fully overcast day the daytime brightness is lowered by this many percent. Set 0 to ignore the weather.");

        var group = new GroupBox { Text = "Sunrise, sunset and weather", AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(3, 10, 3, 3) };
        group.Controls.Add(table);
        return group;
    }

    private void AddCurveRow(TableLayoutPanel table, string label, NumericUpDown input, string tip)
    {
        var text = new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left };
        _tips.SetToolTip(text, tip);
        _tips.SetToolTip(input, tip);

        var row = table.RowCount++;
        table.Controls.Add(text, 0, row);
        table.Controls.Add(input, 1, row);
    }

    private static NumericUpDown CreateMinutes(int minimum, int maximum) =>
        new() { Minimum = minimum, Maximum = maximum, Width = 64 };

    private static TrackBar CreateSlider() => new()
    {
        Minimum = AppConfig.MinBrightness,
        Maximum = 100,
        TickFrequency = 10,
        SmallChange = 1,
        LargeChange = 10,
        Dock = DockStyle.Fill
    };

    private static void AddRow(TableLayoutPanel table, Control first, Control? second = null, int span = 1)
    {
        var row = table.RowCount++;
        table.Controls.Add(first, 0, row);
        if (second is not null)
            table.Controls.Add(second, 1, row);
        else if (span > 1)
            table.SetColumnSpan(first, span);
    }

    private AppConfig BuildConfig() => new()
    {
        Place = _place,
        Latitude = _latitude,
        Longitude = _longitude,
        DayBrightness = _day.Value,
        NightBrightness = _night.Value,
        SunriseFadeMinutes = (int)_sunriseFade.Value,
        DimBeforeSunsetMinutes = (int)_dimBeforeSunset.Value,
        DarkAfterSunsetMinutes = (int)_darkAfterSunset.Value,
        CloudDimmingPercent = (int)_clouds.Value,
        ShowTrayIcon = _showTray.Checked
    };

    private void UpdateValueLabels()
    {
        _dayValue.Text = $"{_day.Value}%";
        _nightValue.Text = $"{_night.Value}%";
    }

    private void UpdateLocationLabel() => _selected.Text = _latitude is null
        ? "No location set. A fixed 07:00-19:00 schedule is used until you pick one."
        : $"Selected: {_place}";

    private void PickOnMap()
    {
        using var picker = new MapPickerForm(_latitude, _longitude);
        if (picker.ShowDialog(this) != DialogResult.OK || picker.Latitude is not { } lat || picker.Longitude is not { } lon)
            return;

        (_latitude, _longitude) = (lat, lon);
        _place = FormatCoordinates(lat, lon);
        _results.Visible = false;
        UpdateLocationLabel();
    }

    private static string FormatCoordinates(double latitude, double longitude) => string.Create(
        System.Globalization.CultureInfo.InvariantCulture,
        $"{Math.Abs(latitude):0.00}°{(latitude >= 0 ? 'N' : 'S')}, {Math.Abs(longitude):0.00}°{(longitude >= 0 ? 'E' : 'W')}");

    private async Task SearchAsync()
    {
        // The geocoder matches a single place name, so "Gdansk, Poland" is searched as "Gdansk".
        var query = _query.Text.Split(',')[0].Trim();
        if (query.Length == 0)
        {
            _selected.Text = "Type a city or place name first.";
            return;
        }

        _search.Enabled = false;
        _selected.Text = "Searching...";
        _results.Visible = false;
        try
        {
            var places = await WeatherService.SearchPlacesAsync(query);
            _results.Items.Clear();
            foreach (var place in places)
                _results.Items.Add(place);
            _results.Visible = places.Count > 0;
            _selected.Text = places.Count > 0
                ? $"Found {places.Count}. Pick one from the list."
                : $"No matches for \"{query}\". Try another spelling, e.g. the English name.";
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            _selected.Text = ex is HttpRequestException or TaskCanceledException
                ? $"Search failed, check your internet connection ({ex.GetBaseException().Message})."
                : $"Search failed: {ex.GetBaseException().Message}";
        }
        finally
        {
            _search.Enabled = true;
        }
    }
}
