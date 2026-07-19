using System.Reflection;
using SkyPilot.Utils;

namespace SkyPilot.Panels;

/// <summary>
/// Interactive map panel using WebView2 + Leaflet/OpenStreetMap.
/// </summary>
public class MapPanel : UserControl
{
    private Microsoft.Web.WebView2.WinForms.WebView2? _webView;
    private readonly Label _placeholder;
    private double _lastLat, _lastLon;
    private readonly List<(double Lat, double Lon)> _track = new();

    public MapPanel()
    {
        Dock = DockStyle.Fill;
        BackColor = ModernTheme.Background;

        _placeholder = new Label
        {
            Text = "Loading map...\n\nIf map doesn't appear, install WebView2 Runtime:\nhttps://developer.microsoft.com/en-us/microsoft-edge/webview2/",
            ForeColor = ModernTheme.TextMuted,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 11f),
            Visible = true
        };
        Controls.Add(_placeholder);
        Load += OnLoad;
    }

    private async void OnLoad(object? sender, EventArgs e)
    {
        try
        {
            _webView = new Microsoft.Web.WebView2.WinForms.WebView2
            {
                Dock = DockStyle.Fill,
                DefaultBackgroundColor = System.Drawing.Color.FromArgb(13, 17, 23)
            };

            var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(
                null, Path.Combine(Path.GetTempPath(), "SkyPilotWebView2"));
            await _webView.EnsureCoreWebView2Async(env);
            _webView.NavigateToString(GetMapHtml());
            Controls.Add(_webView);
            _webView.BringToFront();
            _placeholder.Visible = false;
        }
        catch (Exception ex)
        {
            _placeholder.Text = $"Map unavailable\n\n{ex.Message}\n\nInstall WebView2 Runtime";
        }
    }

    public void UpdatePosition(double lat, double lon, float heading)
    {
        _lastLat = lat;
        _lastLon = lon;
        _track.Add((lat, lon));
        if (_track.Count > 5000) _track.RemoveAt(0);
        try
        {
            _webView?.CoreWebView2.ExecuteScriptAsync($"updateVehicle({lat},{lon},{heading})");
            if (_track.Count % 10 == 0)
            {
                string trackJson = string.Join(",", _track.Select(t => $"[{t.Lat},{t.Lon}]"));
                _webView?.CoreWebView2.ExecuteScriptAsync($"updateTrack([{trackJson}])");
            }
        }
        catch { }
    }

    public void SetHome(double lat, double lon)
    {
        try { _webView?.CoreWebView2.ExecuteScriptAsync($"setHome({lat},{lon})"); } catch { }
    }

    public void AddWaypointMarker(double lat, double lon, int index)
    {
        try { _webView?.CoreWebView2.ExecuteScriptAsync($"addWaypoint({lat},{lon},{index})"); } catch { }
    }

    public void ClearWaypoints()
    {
        try { _webView?.CoreWebView2.ExecuteScriptAsync("clearWaypoints()"); } catch { }
    }

    public void ClearTrack()
    {
        _track.Clear();
        try { _webView?.CoreWebView2.ExecuteScriptAsync("clearTrack()"); } catch { }
    }

    private static string GetMapHtml() => @"<!DOCTYPE html>
<html>
<head>
<meta charset=""utf-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
<link rel=""stylesheet"" href=""https://unpkg.com/leaflet@1.9.4/dist/leaflet.css"" />
<script src=""https://unpkg.com/leaflet@1.9.4/dist/leaflet.js""></script>
<style>
  * { margin: 0; padding: 0; box-sizing: border-box; }
  body { background: #0D1117; overflow: hidden; }
  #map { width: 100vw; height: 100vh; }
  .vehicle-icon { background: none; border: none; }
  .vehicle-marker { width: 30px; height: 30px; }
  .vehicle-marker svg { filter: drop-shadow(0 0 6px rgba(0,212,255,0.8)); }
  .waypoint-icon { background: none; border: none; }
  .waypoint-marker {
    width: 24px; height: 24px; background: rgba(0,212,255,0.9);
    border: 2px solid #fff; border-radius: 50%;
    display: flex; align-items: center; justify-content: center;
    font-size: 11px; font-weight: bold; color: #fff;
    font-family: monospace; box-shadow: 0 0 8px rgba(0,212,255,0.6);
  }
  .home-marker {
    width: 20px; height: 20px; background: rgba(255,184,0,0.9);
    border: 2px solid #fff; border-radius: 50%;
    display: flex; align-items: center; justify-content: center;
    font-size: 12px; box-shadow: 0 0 8px rgba(255,184,0,0.6);
  }
</style>
</head>
<body>
<div id=""map""></div>
<script>
var map = L.map('map', { center: [51.5074, -0.1278], zoom: 15, zoomControl: false });
L.control.zoom({ position: 'bottomright' }).addTo(map);
L.tileLayer('https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png', {
  attribution: '&copy; OpenStreetMap &copy; CARTO', subdomains: 'abcd', maxZoom: 19
}).addTo(map);
var vehicleIcon = L.divIcon({
  className: 'vehicle-icon',
  html: '<div class=""vehicle-marker""><svg viewBox=""0 0 24 24"" width=""30"" height=""30""><path fill=""#00D4FF"" d=""M12 2L4 20h3l5-14 5 14h3L12 2z""/></svg></div>',
  iconSize: [30, 30], iconAnchor: [15, 15]
});
var vehicleMarker = L.marker([51.5074, -0.1278], {icon: vehicleIcon}).addTo(map);
var trackLine = L.polyline([], { color: '#00D4FF', weight: 2, opacity: 0.7 }).addTo(map);
var waypointMarkers = [];
var homeMarker = null;
function updateVehicle(lat, lon, heading) {
  vehicleMarker.setLatLng([lat, lon]);
  var el = vehicleMarker.getElement();
  if (el) { var svg = el.querySelector('svg'); if (svg) svg.style.transform = 'rotate(' + heading + 'deg)'; }
  map.panTo([lat, lon], {animate: true, duration: 0.5});
}
function updateTrack(points) { trackLine.setLatLngs(points); }
function setHome(lat, lon) {
  if (homeMarker) map.removeLayer(homeMarker);
  homeMarker = L.marker([lat, lon], {icon: L.divIcon({
    className: 'waypoint-icon', html: '<div class=""home-marker"">H</div>',
    iconSize: [20, 20], iconAnchor: [10, 10]
  })}).addTo(map);
}
function addWaypoint(lat, lon, index) {
  var m = L.marker([lat, lon], {icon: L.divIcon({
    className: 'waypoint-icon', html: '<div class=""waypoint-marker"">' + index + '</div>',
    iconSize: [24, 24], iconAnchor: [12, 12]
  })}).addTo(map);
  m.bindPopup('WP' + index);
  waypointMarkers.push(m);
}
function clearWaypoints() { waypointMarkers.forEach(function(m) { map.removeLayer(m); }); waypointMarkers = []; }
function clearTrack() { trackLine.setLatLngs([]); }
</script>
</body>
</html>";
}
