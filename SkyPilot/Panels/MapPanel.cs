using SkyPilot.Utils;

namespace SkyPilot.Panels;

/// <summary>
/// Interactive map panel with click-to-add waypoint planning.
/// </summary>
public class MapPanel : UserControl
{
    private Microsoft.Web.WebView2.WinForms.WebView2? _webView;
    private readonly Label _placeholder;
    private double _lastLat, _lastLon;
    private readonly List<(double Lat, double Lon)> _track = new();
    private readonly List<(double Lat, double Lon, int Index)> _waypoints = new();

    /// <summary>Fires when user clicks map to add a waypoint. Args: lat, lon, waypointIndex</summary>
    public event Action<double, double, int>? WaypointAdded;

    /// <summary>Fires when waypoint is updated (moved or settings changed). Args: index, lat, lon, alt, spd</summary>
    public event Action<int, double, double, double, double>? WaypointUpdated;

    /// <summary>Fires when user clicks Export KML on map</summary>
    public event Action? ExportKmlRequested;

    /// <summary>Fires when user clicks Sync Mission on map</summary>
    public event Action? SyncMissionRequested;

    /// <summary>Fires when user right-clicks to remove a waypoint. Args: waypointIndex</summary>
    public event Action<int>? WaypointRemoved;

    public MapPanel()
    {
        Dock = DockStyle.Fill;
        BackColor = ModernTheme.Background;

        _placeholder = new Label
        {
            Text = "Loading map...\n\nClick on map to add waypoints\nRight-click waypoint to remove",
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

            _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
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

    private void OnWebMessageReceived(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            string msg = e.WebMessageAsJson;
            // Format: {"type":"addWaypoint","lat":51.5,"lon":-0.1}
            // or: {"type":"removeWaypoint","index":2}
            if (msg.Contains("addWaypoint"))
            {
                var lat = double.Parse(msg.Split("\"lat\":")[1].Split(",")[0]);
                var lon = double.Parse(msg.Split("\"lon\":")[1].Split("}")[0]);
                int idx = _waypoints.Count + 1;
                _waypoints.Add((lat, lon, idx));
                WaypointAdded?.Invoke(lat, lon, idx);
            }
            else if (msg.Contains("removeWaypoint"))
            {
                var idx = int.Parse(msg.Split("\"index\":")[1].Split("}")[0]);
                WaypointRemoved?.Invoke(idx);
            }
        }
        catch { }
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

    public void SyncWaypoints(List<(double Lat, double Lon)> waypoints)
    {
        _waypoints.Clear();
        try { _webView?.CoreWebView2.ExecuteScriptAsync("clearWaypoints()"); } catch { }
        for (int i = 0; i < waypoints.Count; i++)
        {
            _waypoints.Add((waypoints[i].Lat, waypoints[i].Lon, i + 1));
            try { _webView?.CoreWebView2.ExecuteScriptAsync($"addWaypoint({waypoints[i].Lat},{waypoints[i].Lon},{i + 1})"); } catch { }
        }
    }

    public void ClearWaypoints()
    {
        _waypoints.Clear();
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
  body { background: #0D1117; overflow: hidden; font-family: 'Segoe UI', sans-serif; }
  #map { width: 100vw; height: 100vh; }
  .vehicle-icon { background: none; border: none; }
  .vehicle-marker { width: 30px; height: 30px; }
  .vehicle-marker svg { filter: drop-shadow(0 0 8px rgba(0,212,255,0.9)); }
  .waypoint-icon { background: none; border: none; }
  .waypoint-marker {
    width: 26px; height: 26px; background: rgba(0,212,255,0.95);
    border: 2px solid #fff; border-radius: 50%;
    display: flex; align-items: center; justify-content: center;
    font-size: 11px; font-weight: bold; color: #fff;
    font-family: 'Cascadia Code', monospace;
    box-shadow: 0 0 10px rgba(0,212,255,0.7);
    cursor: pointer; transition: transform 0.2s;
  }
  .waypoint-marker:hover { transform: scale(1.2); background: rgba(0,212,255,1); }
  .home-marker {
    width: 22px; height: 22px; background: rgba(255,184,0,0.95);
    border: 2px solid #fff; border-radius: 50%;
    display: flex; align-items: center; justify-content: center;
    font-size: 11px; font-weight: bold; color: #fff;
    box-shadow: 0 0 10px rgba(255,184,0,0.7);
  }
  #toolbar {
    position: absolute; top: 10px; right: 10px; z-index: 1000;
    background: rgba(22,27,34,0.95); border-radius: 8px; padding: 8px;
    border: 1px solid rgba(0,212,255,0.3);
  }
  #toolbar button {
    background: rgba(0,212,255,0.2); border: 1px solid rgba(0,212,255,0.5);
    color: #00D4FF; padding: 6px 12px; border-radius: 4px; cursor: pointer;
    font-size: 11px; margin: 2px; font-family: 'Segoe UI', sans-serif;
  }
  #toolbar button:hover { background: rgba(0,212,255,0.4); }
  #toolbar button.danger { background: rgba(255,51,102,0.2); border-color: rgba(255,51,102,0.5); color: #FF3366; }
  #toolbar button.danger:hover { background: rgba(255,51,102,0.4); }
  #wpcount { color: #8B949E; font-size: 11px; padding: 4px; }
</style>
</head>
<body>
<div id=""map""></div>
<div id=""toolbar"">
  <div id=""wpcount"">Waypoints: 0</div>
  <button onclick=""clearAllWaypoints()"">Clear All</button>
  <button class=""danger"" onclick=""toggleAddMode()"">+ Add Mode</button>
  <button onclick=""exportKML()"">Export KML</button>
  <button onclick=""syncFromMission()"">Sync Mission</button>
</div>
<script>
var map = L.map('map', { center: [51.5074, -0.1278], zoom: 16, zoomControl: false });
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
var trackLine = L.polyline([], { color: '#00D4FF', weight: 2, opacity: 0.6 }).addTo(map);
var routeLine = L.polyline([], { color: '#FF3366', weight: 2, opacity: 0.8, dashArray: '8,8' }).addTo(map);
var waypointMarkers = [];
var homeMarker = null;
var addMode = false;
var waypointIndex = 0;

// Click to add waypoint
map.on('click', function(e) {
  if (!addMode) return;
  waypointIndex++;
  var lat = e.latlng.lat;
  var lon = e.latlng.lng;
  var marker = L.marker([lat, lon], {icon: L.divIcon({
    className: 'waypoint-icon',
    html: '<div class=""waypoint-marker"" data-idx=""' + waypointIndex + '"" onclick=""removeWaypoint(' + waypointIndex + ')"">' + waypointIndex + '</div>',
    iconSize: [26, 26], iconAnchor: [13, 13]
  })}).addTo(map);
  marker.bindPopup('WP' + waypointIndex + '<br><small>Click marker to remove</small>');
  waypointMarkers.push({marker: marker, index: waypointIndex, lat: lat, lon: lon});
  updateRoute();
  updateWpCount();
  // Send to C#
  window.chrome && window.chrome.webview && window.chrome.webview.postMessage(JSON.stringify({type:'addWaypoint', lat:lat, lon:lon}));
});

function removeWaypoint(idx) {
  var i = waypointMarkers.findIndex(function(w) { return w.index === idx; });
  if (i >= 0) {
    map.removeLayer(waypointMarkers[i].marker);
    waypointMarkers.splice(i, 1);
    updateRoute();
    updateWpCount();
    window.chrome && window.chrome.webview && window.chrome.webview.postMessage(JSON.stringify({type:'removeWaypoint', index:idx}));
  }
}

function clearAllWaypoints() {
  waypointMarkers.forEach(function(w) { map.removeLayer(w.marker); });
  waypointMarkers = [];
  routeLine.setLatLngs([]);
  waypointIndex = 0;
  updateWpCount();
}

function toggleAddMode() {
  addMode = !addMode;
  var btn = document.querySelector('#toolbar button.danger');
  btn.textContent = addMode ? '■ Stop Adding' : '+ Add Mode';
  btn.style.background = addMode ? 'rgba(255,51,102,0.5)' : 'rgba(255,51,102,0.2)';
  map.getContainer().style.cursor = addMode ? 'crosshair' : '';
}

function updateRoute() {
  var coords = waypointMarkers.map(function(w) { return [w.lat, w.lon]; });
  routeLine.setLatLngs(coords);
}

function updateWpCount() {
  document.getElementById('wpcount').textContent = 'Waypoints: ' + waypointMarkers.length;
}

function updateVehicle(lat, lon, heading) {
  vehicleMarker.setLatLng([lat, lon]);
  var el = vehicleMarker.getElement();
  if (el) { var svg = el.querySelector('svg'); if (svg) svg.style.transform = 'rotate(' + heading + 'deg)'; }
  map.panTo([lat, lon], {animate: true, duration: 0.3});
}

function updateTrack(points) { trackLine.setLatLngs(points); }

function setHome(lat, lon) {
  if (homeMarker) map.removeLayer(homeMarker);
  homeMarker = L.marker([lat, lon], {icon: L.divIcon({
    className: 'waypoint-icon', html: '<div class=""home-marker"">H</div>',
    iconSize: [22, 22], iconAnchor: [11, 11]
  })}).addTo(map);
}

function addWaypoint(lat, lon, index, alt, spd) {
  alt = alt || 50;
  spd = spd || 10;
  var m = L.marker([lat, lon], {
    draggable: true,
    icon: L.divIcon({
      className: 'waypoint-icon',
      html: '<div class=""waypoint-marker"" data-idx=""' + index + '"">' + index + '</div>',
      iconSize: [26, 26], iconAnchor: [13, 13]
    })
  }).addTo(map);
  
  var popupHtml = '<div style=""font-family:Segoe UI;font-size:12px;min-width:150px"">' +
    '<b style=""color:#00D4FF"">WP' + index + '</b><br>' +
    '<div style=""margin:6px 0"">' +
    '<label style=""color:#8B949E"">Altitude (m):</label><br>' +
    '<input id=""wpAlt' + index + '"" type=""number"" value=""' + alt + '"" style=""width:100%;padding:4px;background:#21262D;color:#E6EDF3;border:1px solid #30363D;border-radius:4px"">' +
    '</div>' +
    '<div style=""margin:6px 0"">' +
    '<label style=""color:#8B949E"">Speed (m/s):</label><br>' +
    '<input id=""wpSpd' + index + '"" type=""number"" value=""' + spd + '"" step=""0.1"" style=""width:100%;padding:4px;background:#21262D;color:#E6EDF3;border:1px solid #30363D;border-radius:4px"">' +
    '</div>' +
    '<div style=""margin:6px 0;display:flex;gap:4px"">' +
    '<button onclick=""saveWpSettings(' + index + ')"" style=""flex:1;padding:4px;background:#00D4FF;color:#fff;border:none;border-radius:4px;cursor:pointer"">Save</button>' +
    '<button onclick=""removeWaypoint(' + index + ')"" style=""flex:1;padding:4px;background:#FF3366;color:#fff;border:none;border-radius:4px;cursor:pointer"">Delete</button>' +
    '</div></div>';
  
  m.bindPopup(popupHtml, {maxWidth: 200});
  m.on('dragend', function(e) {
    var pos = e.target.getLatLng();
    var wi = waypointMarkers.findIndex(function(w) { return w.index === index; });
    if (wi >= 0) {
      waypointMarkers[wi].lat = pos.lat;
      waypointMarkers[wi].lon = pos.lng;
      updateRoute();
      sendWpUpdate(waypointMarkers[wi]);
    }
  });
  waypointMarkers.push({marker: m, index: index, lat: lat, lon: lon, alt: alt, spd: spd});
  updateRoute();
  updateWpCount();
}

function saveWpSettings(idx) {
  var altInput = document.getElementById('wpAlt' + idx);
  var spdInput = document.getElementById('wpSpd' + idx);
  var wi = waypointMarkers.findIndex(function(w) { return w.index === idx; });
  if (wi >= 0) {
    waypointMarkers[wi].alt = parseFloat(altInput.value) || 50;
    waypointMarkers[wi].spd = parseFloat(spdInput.value) || 10;
    sendWpUpdate(waypointMarkers[wi]);
    map.closePopup();
  }
}

function sendWpUpdate(wp) {
  window.chrome && window.chrome.webview && window.chrome.webview.postMessage(JSON.stringify({
    type:'waypointUpdated', index:wp.index, lat:wp.lat, lon:wp.lon, alt:wp.alt, spd:wp.spd
  }));
}

function clearWaypoints() { clearAllWaypoints(); }
function exportKML() {
  if (waypointMarkers.length === 0) { alert('No waypoints to export'); return; }
  window.chrome && window.chrome.webview && window.chrome.webview.postMessage(JSON.stringify({type:'exportKML'}));
}

function syncFromMission() {
  window.chrome && window.chrome.webview && window.chrome.webview.postMessage(JSON.stringify({type:'syncMission'}));
}

function clearTrack() { trackLine.setLatLngs([]); }
</script>
</body>
</html>";
}
