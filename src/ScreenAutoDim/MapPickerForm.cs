using System.Globalization;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace ScreenAutoDim;

/// <summary>A dialog with an OpenStreetMap (Leaflet) map where the user clicks to choose a location.</summary>
internal sealed class MapPickerForm : Form
{
    private const string VirtualHost = "autodim.map";

    private readonly WebView2 _web = new() { Dock = DockStyle.Fill };
    private readonly Label _status = new() { AutoSize = true, Anchor = AnchorStyles.Left, Text = "Click on the map to choose a location." };
    private readonly Button _ok = new() { Text = "Use this location", AutoSize = true, DialogResult = DialogResult.OK, Enabled = false };
    private readonly double? _startLatitude;
    private readonly double? _startLongitude;

    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }

    public MapPickerForm(double? latitude, double? longitude)
    {
        (_startLatitude, _startLongitude) = (latitude, longitude);

        Text = "Pick location on the map";
        Font = new Font("Segoe UI", 9f);
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(760, 540);
        MinimumSize = new Size(480, 360);

        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        AcceptButton = _ok;
        CancelButton = cancel;

        var bottom = new TableLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, ColumnCount = 3, Padding = new Padding(8) };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.Controls.Add(_status, 0, 0);
        bottom.Controls.Add(_ok, 1, 0);
        bottom.Controls.Add(cancel, 2, 0);

        Controls.Add(_web);
        Controls.Add(bottom);
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        try
        {
            await InitializeMapAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            _status.Text = ex is WebView2RuntimeNotFoundException
                ? "The Microsoft Edge WebView2 Runtime is not installed, so the map can't be shown."
                : $"The map could not be loaded: {ex.GetBaseException().Message}";
        }
    }

    private async Task InitializeMapAsync()
    {
        var environment = await CoreWebView2Environment.CreateAsync(null, Path.Combine(AppConfig.DataDirectory, "webview2"));
        await _web.EnsureCoreWebView2Async(environment);

        var folder = Path.Combine(AppConfig.DataDirectory, "map");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "index.html"), MapHtml.Page);

        var core = _web.CoreWebView2;
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.SetVirtualHostNameToFolderMapping(VirtualHost, folder, CoreWebView2HostResourceAccessKind.Allow);
        core.WebMessageReceived += OnWebMessage;
        core.NewWindowRequested += (_, e) => e.Handled = true;
        core.NavigationStarting += (_, e) =>
        {
            if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri) || uri.Host != VirtualHost)
                e.Cancel = true; // the page is a map, not a browser
        };
        core.NavigationCompleted += async (_, _) =>
        {
            if (_startLatitude is { } lat && _startLongitude is { } lon)
                await core.ExecuteScriptAsync(string.Create(CultureInfo.InvariantCulture, $"showAt({lat}, {lon}, 8)"));
        };
        core.Navigate($"https://{VirtualHost}/index.html");
    }

    private void OnWebMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var doc = JsonDocument.Parse(e.TryGetWebMessageAsString());
            var lat = doc.RootElement.GetProperty("lat").GetDouble();
            var lon = doc.RootElement.GetProperty("lon").GetDouble();
            if (lat is < -90 or > 90 || lon is < -180 or > 180)
                return;

            (Latitude, Longitude) = (lat, lon);
            _ok.Enabled = true;
            _status.Text = string.Create(CultureInfo.InvariantCulture, $"Selected: {lat:0.0000}, {lon:0.0000}");
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            Log.Error(ex);
        }
    }
}
