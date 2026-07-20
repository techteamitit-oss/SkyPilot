using SkyPilot.Utils;
using System.Text.Json;

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
    private bool _mapLoaded;

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

    /// <summary>Fires when user sets simulator start on map. Args: lat, lon</summary>
    public event Action<double, double>? SimStartPosReceived;

    /// <summary>Fires when user sets simulator target on map. Args: lat, lon</summary>
    public event Action<double, double>? SimTargetPosReceived;

    /// <summary>Fires when user clicks FPV toggle on map toolbar</summary>
    public event Action? FPVToggleRequested;

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
            // Clear stale WebView2 cache to prevent blank rendering
            string cachePath = Path.Combine(Path.GetTempPath(), "SkyPilotWebView2");
            try { if (Directory.Exists(cachePath)) Directory.Delete(cachePath, true); } catch { }

            _webView = new Microsoft.Web.WebView2.WinForms.WebView2
            {
                Dock = DockStyle.Fill,
                DefaultBackgroundColor = System.Drawing.Color.FromArgb(13, 17, 23)
            };

            var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(
                null, cachePath);
            await _webView.EnsureCoreWebView2Async(env);

            _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
            _webView.CoreWebView2.NavigationCompleted += (s, e2) =>
            {
                if (!e2.IsSuccess)
                {
                    // Retry once on navigation failure
                    BeginInvoke(() => { try { _webView?.NavigateToString(GetMapHtml()); } catch { } });
                }
            };
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
            // WebMessageAsJson wraps string messages in outer quotes and escapes inner quotes
            // e.g. "{\"type\":\"addWaypoint\",\"lat\":51.5}" — we need to parse this as a string first
            string json = e.WebMessageAsJson;
            // Deserialize the outer JSON string to get the inner JSON
            var outerDoc = JsonDocument.Parse(json);
            string innerJson = outerDoc.RootElement.GetString() ?? "";
            var doc = JsonDocument.Parse(innerJson);
            var root = doc.RootElement;
            var type = root.GetProperty("type").GetString() ?? "";

            if (type == "addWaypoint")
            {
                double lat = root.GetProperty("lat").GetDouble();
                double lon = root.GetProperty("lon").GetDouble();
                int idx = _waypoints.Count + 1;
                _waypoints.Add((lat, lon, idx));
                WaypointAdded?.Invoke(lat, lon, idx);
            }
            else if (type == "removeWaypoint")
            {
                int idx = root.GetProperty("index").GetInt32();
                WaypointRemoved?.Invoke(idx);
            }
            else if (type == "setSimStart")
            {
                SimStartPosReceived?.Invoke(
                    root.GetProperty("lat").GetDouble(),
                    root.GetProperty("lon").GetDouble());
            }
            else if (type == "setSimTarget")
            {
                SimTargetPosReceived?.Invoke(
                    root.GetProperty("lat").GetDouble(),
                    root.GetProperty("lon").GetDouble());
            }
            else if (type == "moveStart")
            {
                SimStartPosReceived?.Invoke(
                    root.GetProperty("lat").GetDouble(),
                    root.GetProperty("lon").GetDouble());
            }
            else if (type == "moveTarget")
            {
                SimTargetPosReceived?.Invoke(
                    root.GetProperty("lat").GetDouble(),
                    root.GetProperty("lon").GetDouble());
            }
            else if (type == "toggleFPV")
            {
                FPVToggleRequested?.Invoke();
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
        try { _webView?.CoreWebView2.ExecuteScriptAsync("clearWaypoints();firstPosition=true"); } catch { }
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

    public void SetVehicleType(string vehicleType)
    {
        try { _webView?.CoreWebView2.ExecuteScriptAsync($"setVehicleType('{vehicleType}')"); } catch { }
    }

    public void ShowFlightPath(double startLat, double startLon, double targetLat, double targetLon, string pattern, List<(double Lat, double Lon)>? intermediateWaypoints = null)
    {
        try
        {
            var wpJson = "null";
            if (intermediateWaypoints != null && intermediateWaypoints.Count > 0)
            {
                var wpList = string.Join(",", intermediateWaypoints.Select(w => $"{{lat:{w.Lat},lon:{w.Lon}}}"));
                wpJson = $"[{wpList}]";
            }
            var json = $"{{startLat:{startLat},startLon:{startLon},targetLat:{targetLat},targetLon:{targetLon},pattern:'{pattern}',intermediateWaypoints:{wpJson}}}";
            _webView?.CoreWebView2.ExecuteScriptAsync($"showFlightPath({json})");
        } catch { }
    }

    public void HideFlightPath()
    {
        try { _webView?.CoreWebView2.ExecuteScriptAsync("hideFlightPath()"); } catch { }
    }

    public List<(double Lat, double Lon)> GetWaypoints()
    {
        return _waypoints.Select(w => (w.Lat, w.Lon)).ToList();
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
  <button class=""danger"" onclick=""toggleAddMode()"">- Stop Adding</button>
  <button id=""btnSetStart"" onclick=""setStartClick()"">Set Start</button>
  <button id=""btnSetTarget"" onclick=""setTargetClick()"">Set Target</button>
  <button onclick=""exportKML()"">Export KML</button>
  <button onclick=""syncFromMission()"">Sync Mission</button>
  <button onclick=""toggleFPV()"">FPV</button>
</div>
<script>
var map = L.map('map', { center: [51.5074, -0.1278], zoom: 16, zoomControl: false, attributionControl: false });
L.control.zoom({ position: 'bottomright' }).addTo(map);
L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
  attribution: '&copy; OpenStreetMap &copy; CARTO', subdomains: 'abcd', maxZoom: 19
}).addTo(map);

var vehicleIcons = {
  plane: '<div class=""vehicle-marker""><svg viewBox=""0 0 24 24"" width=""30"" height=""30""><path fill=""#00D4FF"" d=""M12 2L4 20h3l5-14 5 14h3L12 2z""/></svg></div>',
  drone: '<div class=""vehicle-marker""><svg viewBox=""0 0 32 32"" width=""30"" height=""30""><circle cx=""16"" cy=""16"" r=""4"" fill=""#00D4FF""/><line x1=""16"" y1=""16"" x2=""6"" y2=""6"" stroke=""#00D4FF"" stroke-width=""2""/><line x1=""16"" y1=""16"" x2=""26"" y2=""6"" stroke=""#00D4FF"" stroke-width=""2""/><line x1=""16"" y1=""16"" x2=""6"" y2=""26"" stroke=""#00D4FF"" stroke-width=""2""/><line x1=""16"" y1=""16"" x2=""26"" y2=""26"" stroke=""#00D4FF"" stroke-width=""2""/><circle cx=""6"" cy=""6"" r=""3"" fill=""none"" stroke=""#00D4FF"" stroke-width=""1.5""/><circle cx=""26"" cy=""6"" r=""3"" fill=""none"" stroke=""#00D4FF"" stroke-width=""1.5""/><circle cx=""6"" cy=""26"" r=""3"" fill=""none"" stroke=""#00D4FF"" stroke-width=""1.5""/><circle cx=""26"" cy=""26"" r=""3"" fill=""none"" stroke=""#00D4FF"" stroke-width=""1.5""/></svg></div>',
  helicopter: '<div class=""vehicle-marker""><svg viewBox=""0 0 32 32"" width=""30"" height=""30""><ellipse cx=""16"" cy=""18"" rx=""6"" ry=""10"" fill=""#00D4FF""/><line x1=""16"" y1=""8"" x2=""16"" y2=""4"" stroke=""#00D4FF"" stroke-width=""1.5""/><line x1=""4"" y1=""4"" x2=""28"" y2=""4"" stroke=""#00D4FF"" stroke-width=""1.5""/><line x1=""16"" y1=""28"" x2=""16"" y2=""30"" stroke=""#00D4FF"" stroke-width=""1""/><line x1=""12"" y1=""30"" x2=""20"" y2=""30"" stroke=""#00D4FF"" stroke-width=""1.5""/></svg></div>'
};
var currentVehicleType = 'plane';
var vehicleIcon = L.divIcon({
  className: 'vehicle-icon',
  html: vehicleIcons['plane'],
  iconSize: [30, 30], iconAnchor: [15, 15]
});
var vehicleMarker = L.marker([51.5074, -0.1278], {icon: vehicleIcon}).addTo(map);
var trackLine = L.polyline([], { color: '#00D4FF', weight: 2, opacity: 0.6 }).addTo(map);
var routeLine = L.polyline([], { color: '#FF3366', weight: 2, opacity: 0.8, dashArray: '8,8' }).addTo(map);
var waypointMarkers = [];
var homeMarker = null;
var addMode = true;
var waypointIndex = 0;
var firstPosition = true;
var setStartActive = false;
var setTargetActive = false;
var simStartLat = null, simStartLon = null;
var simTargetLat = null, simTargetLon = null;
var simStartMarker = null, simTargetMarker = null;
var fpOpts = null;

// Stop toolbar clicks from bubbling to map
document.getElementById('toolbar').addEventListener('click', function(e) { e.stopPropagation(); });

// Click to add waypoint
map.on('click', function(e) {
  if (setStartActive) {
    setStartActive = false;
    document.getElementById('btnSetStart').textContent = 'Set Start';
    document.getElementById('btnSetStart').style.background = '';
    document.getElementById('btnSetStart').style.color = '';
    simStartLat = e.latlng.lat;
    simStartLon = e.latlng.lng;
    if (simStartMarker) map.removeLayer(simStartMarker);
    simStartMarker = L.marker([simStartLat, simStartLon], {icon: L.divIcon({
      className: 'waypoint-icon',
      html: '<div style=""width:28px;height:28px;background:rgba(0,200,80,0.95);border:2px solid #fff;border-radius:50%;display:flex;align-items:center;justify-content:center;font-size:13px;font-weight:bold;color:#fff;box-shadow:0 0 10px rgba(0,200,80,0.7)"">S</div>',
      iconSize: [28, 28], iconAnchor: [14, 14]
    })}).addTo(map);
    simStartMarker.bindPopup('<b>Start Position</b>');
    window.chrome && window.chrome.webview && window.chrome.webview.postMessage(JSON.stringify({type:'setSimStart', lat:simStartLat, lon:simStartLon}));
    return;
  }
  if (setTargetActive) {
    setTargetActive = false;
    document.getElementById('btnSetTarget').textContent = 'Set Target';
    document.getElementById('btnSetTarget').style.background = '';
    document.getElementById('btnSetTarget').style.color = '';
    simTargetLat = e.latlng.lat;
    simTargetLon = e.latlng.lng;
    if (simTargetMarker) map.removeLayer(simTargetMarker);
    simTargetMarker = L.marker([simTargetLat, simTargetLon], {icon: L.divIcon({
      className: 'waypoint-icon',
      html: '<div style=""width:28px;height:28px;background:rgba(255,51,102,0.95);border:2px solid #fff;border-radius:50%;display:flex;align-items:center;justify-content:center;font-size:13px;font-weight:bold;color:#fff;box-shadow:0 0 10px rgba(255,51,102,0.7)"">T</div>',
      iconSize: [28, 28], iconAnchor: [14, 14]
    })}).addTo(map);
    simTargetMarker.bindPopup('<b>Target Position</b>');
    // Draw route line between S and T
    if (simStartLat !== null) {
      window.chrome && window.chrome.webview && window.chrome.webview.postMessage(JSON.stringify({type:'setSimTarget', lat:simTargetLat, lon:simTargetLon}));
    }
    return;
  }
  if (!addMode) return;
  waypointIndex++;
  var lat = e.latlng.lat;
  var lon = e.latlng.lng;
  var marker = L.marker([lat, lon], {icon: L.divIcon({
    className: 'waypoint-icon',
    html: '<div class=""waypoint-marker"" data-idx=""' + waypointIndex + '"" onclick=""event.stopPropagation();removeWaypoint(' + waypointIndex + ')"">' + waypointIndex + '</div>',
    iconSize: [26, 26], iconAnchor: [13, 13]
  })}).addTo(map);
  marker.bindPopup('WP' + waypointIndex + '<br><small>Click marker to remove</small>');
  waypointMarkers.push({marker: marker, index: waypointIndex, lat: lat, lon: lon});
  updateRoute();
  updateWpCount();
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

function onWpMarkerClick(idx) {
  return function(e) {
    L.DomEvent.stopPropagation(e);
    removeWaypoint(idx);
  };
}

function clearAllWaypoints() {
  waypointMarkers.forEach(function(w) { map.removeLayer(w.marker); });
  waypointMarkers = [];
  routeLine.setLatLngs([]);
  waypointIndex = 0;
  // Also clear S/T markers and sim markers
  clearFlightPath();
  if (simStartMarker) { map.removeLayer(simStartMarker); simStartMarker = null; simStartLat = null; simStartLon = null; }
  if (simTargetMarker) { map.removeLayer(simTargetMarker); simTargetMarker = null; simTargetLat = null; simTargetLon = null; }
  updateWpCount();
}

function toggleAddMode() {
  addMode = !addMode;
  var btn = document.querySelector('#toolbar button.danger');
  if (btn) {
    btn.textContent = addMode ? '- Stop Adding' : '+ Add Waypoint';
    btn.style.background = addMode ? 'rgba(255,51,102,0.2)' : 'rgba(0,212,255,0.2)';
    btn.style.borderColor = addMode ? 'rgba(255,51,102,0.5)' : 'rgba(0,212,255,0.5)';
    btn.style.color = addMode ? '#FF3366' : '#00D4FF';
  }
}

function setStartClick() {
  setStartActive = !setStartActive;
  setTargetActive = false;
  var s = document.getElementById('btnSetStart');
  var t = document.getElementById('btnSetTarget');
  s.textContent = setStartActive ? 'Click map...' : 'Set Start';
  s.style.background = setStartActive ? 'rgba(0,200,80,0.4)' : '';
  s.style.color = setStartActive ? '#00C850' : '';
  t.textContent = 'Set Target';
  t.style.background = '';
  t.style.color = '';
}

function setTargetClick() {
  setTargetActive = !setTargetActive;
  setStartActive = false;
  var s = document.getElementById('btnSetStart');
  var t = document.getElementById('btnSetTarget');
  t.textContent = setTargetActive ? 'Click map...' : 'Set Target';
  t.style.background = setTargetActive ? 'rgba(255,51,102,0.4)' : '';
  t.style.color = setTargetActive ? '#FF3366' : '';
  s.textContent = 'Set Start';
  s.style.background = '';
  s.style.color = '';
}

function toggleFPV() {
  window.chrome && window.chrome.webview && window.chrome.webview.postMessage(JSON.stringify({type:'toggleFPV'}));
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
  if (firstPosition) {
    map.setView([lat, lon], map.getZoom());
    firstPosition = false;
  }
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
      html: '<div class=""waypoint-marker"" data-idx=""' + index + '"" onclick=""event.stopPropagation()"">' + index + '</div>',
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
    '<button onclick=""event.stopPropagation();saveWpSettings(' + index + ')"" style=""flex:1;padding:4px;background:#00D4FF;color:#fff;border:none;border-radius:4px;cursor:pointer"">Save</button>' +
    '<button onclick=""event.stopPropagation();removeWaypoint(' + index + ')"" style=""flex:1;padding:4px;background:#FF3366;color:#fff;border:none;border-radius:4px;cursor:pointer"">Delete</button>' +
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

function clearTrack() { trackLine.setLatLngs([]); firstPosition = true; }

function setVehicleType(type) {
  if (vehicleIcons[type]) {
    currentVehicleType = type;
    var newIcon = L.divIcon({
      className: 'vehicle-icon',
      html: vehicleIcons[type],
      iconSize: [30, 30], iconAnchor: [15, 15]
    });
    vehicleMarker.setIcon(newIcon);
  }
}

// Flight path visualization
var flightPathLayers = [];
var startMarkerFP = null;
var targetMarkerFP = null;
var routeLineFP = null;
var circleOverlay = null;

function clearFlightPath() {
  flightPathLayers.forEach(function(l) { map.removeLayer(l); });
  flightPathLayers = [];
  startMarkerFP = null;
  targetMarkerFP = null;
  routeLineFP = null;
  circleOverlay = null;
}

function showFlightPath(opts) {
  clearFlightPath();
  fpOpts = opts;

  // Start marker (green S) — draggable
  startMarkerFP = L.marker([opts.startLat, opts.startLon], {
    draggable: true,
    icon: L.divIcon({
      className: 'waypoint-icon',
      html: '<div style=""width:28px;height:28px;background:rgba(0,200,80,0.95);border:2px solid #fff;border-radius:50%;display:flex;align-items:center;justify-content:center;font-size:13px;font-weight:bold;color:#fff;box-shadow:0 0 10px rgba(0,200,80,0.7);cursor:move"">S</div>',
      iconSize: [28, 28], iconAnchor: [14, 14]
    })
  }).addTo(map);
  startMarkerFP.bindPopup('<b>Start</b> (drag to move)');
  startMarkerFP.on('dragend', function(e) {
    var pos = e.target.getLatLng();
    window.chrome && window.chrome.webview && window.chrome.webview.postMessage(JSON.stringify({type:'moveStart', lat:pos.lat, lon:pos.lng}));
    updateFpRoute();
  });
  flightPathLayers.push(startMarkerFP);

  // Target marker (red T) — draggable
  targetMarkerFP = L.marker([opts.targetLat, opts.targetLon], {
    draggable: true,
    icon: L.divIcon({
      className: 'waypoint-icon',
      html: '<div style=""width:28px;height:28px;background:rgba(255,51,102,0.95);border:2px solid #fff;border-radius:50%;display:flex;align-items:center;justify-content:center;font-size:13px;font-weight:bold;color:#fff;box-shadow:0 0 10px rgba(255,51,102,0.7);cursor:move"">T</div>',
      iconSize: [28, 28], iconAnchor: [14, 14]
    })
  }).addTo(map);
  targetMarkerFP.bindPopup('<b>Target</b> (drag to move)');
  targetMarkerFP.on('dragend', function(e) {
    var pos = e.target.getLatLng();
    window.chrome && window.chrome.webview && window.chrome.webview.postMessage(JSON.stringify({type:'moveTarget', lat:pos.lat, lon:pos.lng}));
    updateFpRoute();
  });
  flightPathLayers.push(targetMarkerFP);

  if (opts.pattern === 'circle') {
    circleOverlay = L.circle([opts.startLat, opts.startLon], {
      radius: 200, color: '#00D4FF', fillColor: 'rgba(0,212,255,0.1)',
      fillOpacity: 0.15, weight: 2, dashArray: '8,8'
    }).addTo(map);
    flightPathLayers.push(circleOverlay);
  } else {
    drawFpRoute(opts);
  }

  // Fit map to show full path
  var allCoords = [[opts.startLat, opts.startLon], [opts.targetLat, opts.targetLon]];
  if (opts.intermediateWaypoints) {
    for (var j = 0; j < opts.intermediateWaypoints.length; j++) {
      allCoords.push([opts.intermediateWaypoints[j].lat, opts.intermediateWaypoints[j].lon]);
    }
  }
  var bounds = L.latLngBounds(allCoords);
  map.fitBounds(bounds, { padding: [60, 60] });
}

function drawFpRoute(opts) {
  // Remove old route line if exists
  if (routeLineFP) { map.removeLayer(routeLineFP); routeLineFP = null; }
  var routeCoords = [[opts.startLat, opts.startLon]];
  if (opts.intermediateWaypoints) {
    for (var i = 0; i < opts.intermediateWaypoints.length; i++) {
      var wp = opts.intermediateWaypoints[i];
      routeCoords.push([wp.lat, wp.lon]);
    }
  }
  routeCoords.push([opts.targetLat, opts.targetLon]);
  routeLineFP = L.polyline(routeCoords, { color: '#00C850', weight: 2, opacity: 0.8, dashArray: '10,6' }).addTo(map);
  flightPathLayers.push(routeLineFP);
}

function updateFpRoute() {
  if (!fpOpts || !startMarkerFP || !targetMarkerFP) return;
  fpOpts.startLat = startMarkerFP.getLatLng().lat;
  fpOpts.startLon = startMarkerFP.getLatLng().lng;
  fpOpts.targetLat = targetMarkerFP.getLatLng().lat;
  fpOpts.targetLon = targetMarkerFP.getLatLng().lng;
  // Remove old route line from flightPathLayers
  if (routeLineFP) {
    var idx = flightPathLayers.indexOf(routeLineFP);
    if (idx >= 0) flightPathLayers.splice(idx, 1);
    map.removeLayer(routeLineFP);
    routeLineFP = null;
  }
  if (fpOpts.pattern !== 'circle') drawFpRoute(fpOpts);
}

function hideFlightPath() { clearFlightPath(); }

function getSimStartPos() {
  // Send last known position back to C#, or current map center
  var center = map.getCenter();
  window.chrome && window.chrome.webview && window.chrome.webview.postMessage(JSON.stringify({
    type:'simStartPos', lat:center.lat, lon:center.lng
  }));
}
</script>
</body>
</html>";
}
