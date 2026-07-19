using SkyPilot.Core.Mavlink;

namespace SkyPilot.Panels;

/// <summary>
/// Mission planning panel with waypoint editor and upload/download.
/// </summary>
public class MissionPanel : UserControl
{
    private readonly DataGridView _grid;
    private readonly List<Waypoint> _waypoints = new();
    private int _wpSeq;

    public event Action<List<Waypoint>>? MissionUploadRequested;
    public event Action? MissionDownloadRequested;

    public MissionPanel()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(30, 30, 30);

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 36,
            BackColor = Color.FromArgb(40, 40, 40),
            Padding = new Padding(4)
        };

        toolbar.Controls.Add(MakeButton("Add WP", (s, e) => AddWaypoint(0, 0, 50)));
        toolbar.Controls.Add(MakeButton("Insert", (s, e) => InsertWaypoint()));
        toolbar.Controls.Add(MakeButton("Remove", (s, e) => RemoveWaypoint()));
        toolbar.Controls.Add(MakeButton("Clear All", (s, e) => { _waypoints.Clear(); RefreshGrid(); }));
        toolbar.Controls.Add(new Label { Text = "|", ForeColor = Color.Gray, AutoSize = false, Width = 10, Dock = DockStyle.None });
        toolbar.Controls.Add(MakeButton("Upload", (s, e) => MissionUploadRequested?.Invoke(_waypoints)));
        toolbar.Controls.Add(MakeButton("Download", (s, e) => MissionDownloadRequested?.Invoke()));
        toolbar.Controls.Add(new Label { Text = "|", ForeColor = Color.Gray, AutoSize = false, Width = 10, Dock = DockStyle.None });
        toolbar.Controls.Add(MakeButton("Save", (s, e) => SaveMission()));
        toolbar.Controls.Add(MakeButton("Load", (s, e) => LoadMission()));
        toolbar.Controls.Add(new Label { Text = "|", ForeColor = Color.Gray, AutoSize = false, Width = 10, Dock = DockStyle.None });
        toolbar.Controls.Add(MakeButton("Export KML", (s, e) => ExportKml()));

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.White,
            GridColor = Color.FromArgb(60, 60, 60),
            BorderStyle = BorderStyle.None,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            Font = new Font("Segoe UI", 9f)
        };

        _grid.Columns.Add("Seq", "#");

        var cmdCol = new DataGridViewComboBoxColumn
        {
            Name = "Command",
            HeaderText = "Command",
            Items = { "WAYPOINT", "TAKEOFF", "LAND", "LOITER_UNLIM", "LOITER_TURNS", "LOITER_TIME", "RTL", "DO_CHANGE_SPEED", "DO_SET_ROI", "CONDITION_DELAY" }
        };
        _grid.Columns.Add(cmdCol);

        _grid.Columns.Add("Lat", "Latitude");
        _grid.Columns.Add("Lon", "Longitude");
        _grid.Columns.Add("Alt", "Alt (m)");
        _grid.Columns.Add("P1", "Param1");
        _grid.Columns.Add("P2", "Param2");
        _grid.Columns.Add("P3", "Param3");
        _grid.Columns.Add("P4", "Param4");

        Controls.Add(_grid);
        Controls.Add(toolbar);

        // Add default takeoff + waypoint
        AddWaypointCommand("TAKEOFF", 0, 0, 50);
        AddWaypointCommand("WAYPOINT", 51.508, -0.13, 50);
    }

    private Button MakeButton(string text, EventHandler onClick)
    {
        var btn = new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(60, 60, 60),
            ForeColor = Color.White,
            Height = 28,
            AutoSize = true,
            Margin = new Padding(2)
        };
        btn.Click += onClick;
        return btn;
    }

    private void AddWaypoint(double lat, double lon, double alt)
    {
        AddWaypointCommand("WAYPOINT", lat, lon, alt);
    }

    private void AddWaypointCommand(string cmd, double lat, double lon, double alt)
    {
        _waypoints.Add(new Waypoint
        {
            Seq = _waypoints.Count,
            Command = cmd,
            Lat = lat,
            Lon = lon,
            Alt = alt
        });
        RefreshGrid();
    }

    private void InsertWaypoint()
    {
        int idx = _grid.CurrentRow?.Index ?? _waypoints.Count;
        var wp = new Waypoint
        {
            Seq = idx,
            Command = "WAYPOINT",
            Lat = 51.508,
            Lon = -0.13,
            Alt = 50
        };
        _waypoints.Insert(idx, wp);
        for (int i = 0; i < _waypoints.Count; i++) _waypoints[i].Seq = i;
        RefreshGrid();
    }

    private void RemoveWaypoint()
    {
        int idx = _grid.CurrentRow?.Index ?? -1;
        if (idx < 0 || idx >= _waypoints.Count) return;
        _waypoints.RemoveAt(idx);
        for (int i = 0; i < _waypoints.Count; i++) _waypoints[i].Seq = i;
        RefreshGrid();
    }

    private void RefreshGrid()
    {
        _grid.Rows.Clear();
        foreach (var wp in _waypoints)
        {
            _grid.Rows.Add(wp.Seq, wp.Command, $"{wp.Lat:F6}", $"{wp.Lon:F6}", $"{wp.Alt:F1}",
                wp.Param1, wp.Param2, wp.Param3, wp.Param4);
        }
    }

    private void SaveMission()
    {
        using var sfd = new SaveFileDialog
        {
            Filter = "Waypoint files|*.waypoints|Mission files|*.mission|All files|*.*",
            FileName = "mission.waypoints"
        };
        if (sfd.ShowDialog() != DialogResult.OK) return;

        if (sfd.FileName.EndsWith(".waypoints"))
            SaveWaypointsFormat(sfd.FileName);
        else
            SaveMissionFormat(sfd.FileName);
    }

    private void SaveWaypointsFormat(string path)
    {
        // Standard ArduPilot QGC WPL 110 format
        var lines = new List<string> { "QGC WPL 110" };
        foreach (var wp in _waypoints)
        {
            int frame = 3; // MAV_FRAME_GLOBAL_RELATIVE_ALT
            int cmd = CommandToMavCmd(wp.Command);
            lines.Add($"{wp.Seq}\t0\t{frame}\t{cmd}\t{wp.Param1}\t{wp.Param2}\t{wp.Param3}\t{wp.Param4}\t{wp.Lat}\t{wp.Lon}\t{wp.Alt}\t1");
        }
        File.WriteAllLines(path, lines);
    }

    private void SaveMissionFormat(string path)
    {
        var lines = _waypoints.Select(wp =>
            $"{wp.Seq}\t{wp.Command}\t{wp.Lat}\t{wp.Lon}\t{wp.Alt}\t{wp.Param1}\t{wp.Param2}\t{wp.Param3}\t{wp.Param4}");
        File.WriteAllLines(path, lines);
    }

    private void LoadMission()
    {
        using var ofd = new OpenFileDialog
        {
            Filter = "Waypoint files|*.waypoints|Mission files|*.mission|All files|*.*"
        };
        if (ofd.ShowDialog() != DialogResult.OK) return;

        var firstLine = File.ReadLines(ofd.FileName).FirstOrDefault() ?? "";
        if (firstLine.StartsWith("QGC WPL"))
            LoadWaypointsFormat(ofd.FileName);
        else
            LoadMissionFormat(ofd.FileName);

        RefreshGrid();
    }

    private void LoadWaypointsFormat(string path)
    {
        // Standard ArduPilot QGC WPL 110 format:
        // index, current, frame, command, param1, param2, param3, param4, lat, lon, alt, auto_continue
        _waypoints.Clear();
        bool headerSkipped = false;
        foreach (var line in File.ReadLines(path))
        {
            if (!headerSkipped) { headerSkipped = true; continue; }
            var parts = line.Split('\t');
            if (parts.Length < 12) continue;

            int.TryParse(parts[0], out int seq);
            int.TryParse(parts[3], out int cmdId);
            double.TryParse(parts[8], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double lat);
            double.TryParse(parts[9], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double lon);
            double.TryParse(parts[10], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double alt);
            double.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double p1);
            double.TryParse(parts[2], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double p2);
            double.TryParse(parts[4], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double p3);
            double.TryParse(parts[5], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double p4);

            _waypoints.Add(new Waypoint
            {
                Seq = seq,
                Command = MavCmdToCommand(cmdId),
                Lat = lat,
                Lon = lon,
                Alt = alt,
                Param1 = p1,
                Param2 = p2,
                Param3 = p3,
                Param4 = p4
            });
        }
    }

    private void LoadMissionFormat(string path)
    {
        _waypoints.Clear();
        foreach (var line in File.ReadLines(path))
        {
            var parts = line.Split('\t');
            if (parts.Length < 5) continue;
            _waypoints.Add(new Waypoint
            {
                Seq = int.TryParse(parts[0], out var s) ? s : 0,
                Command = parts[1],
                Lat = double.TryParse(parts[2], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var la) ? la : 0,
                Lon = double.TryParse(parts[3], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var lo) ? lo : 0,
                Alt = double.TryParse(parts[4], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var al) ? al : 50,
                Param1 = parts.Length > 5 && double.TryParse(parts[5], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var p1) ? p1 : 0,
                Param2 = parts.Length > 6 && double.TryParse(parts[6], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var p2) ? p2 : 0,
                Param3 = parts.Length > 7 && double.TryParse(parts[7], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var p3) ? p3 : 0,
                Param4 = parts.Length > 8 && double.TryParse(parts[8], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var p4) ? p4 : 0
            });
        }
    }

    private static int CommandToMavCmd(string cmd) => cmd switch
    {
        "WAYPOINT" => 16,
        "LOITER_UNLIM" => 17,
        "LOITER_TURNS" => 18,
        "LOITER_TIME" => 19,
        "RTL" => 20,
        "LAND" => 21,
        "TAKEOFF" => 22,
        "DO_CHANGE_SPEED" => 178,
        "DO_SET_ROI" => 201,
        "CONDITION_DELAY" => 112,
        _ => 16
    };

    private static string MavCmdToCommand(int cmd) => cmd switch
    {
        16 => "WAYPOINT",
        17 => "LOITER_UNLIM",
        18 => "LOITER_TURNS",
        19 => "LOITER_TIME",
        20 => "RTL",
        21 => "LAND",
        22 => "TAKEOFF",
        178 => "DO_CHANGE_SPEED",
        201 => "DO_SET_ROI",
        112 => "CONDITION_DELAY",
        _ => $"CMD_{cmd}"
    };

    private void ExportKml()
    {
        if (_waypoints.Count == 0)
        {
            MessageBox.Show("No waypoints to export.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var sfd = new SaveFileDialog
        {
            Filter = "KML files|*.kml|All files|*.*",
            FileName = "mission.kml"
        };
        if (sfd.ShowDialog() != DialogResult.OK) return;

        var coords = new List<string>();
        var placemarks = new List<string>();

        foreach (var wp in _waypoints)
        {
            // KML coordinates are lon,lat,alt
            coords.Add($"{wp.Lon:F7},{wp.Lat:F7},{wp.Alt:F1}");

            placemarks.Add($@"
    <Placemark>
      <name>WP{wp.Seq} - {wp.Command}</name>
      <description>Altitude: {wp.Alt:F1}m | {wp.Command}</description>
      <Point>
        <coordinates>{wp.Lon:F7},{wp.Lat:F7},{wp.Alt:F1}</coordinates>
      </Point>
    </Placemark>");
        }

        string coordsString = string.Join(" ", coords);
        string placemarkString = string.Join("", placemarks);

        string kml = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<kml xmlns=""http://www.opengis.net/kml/2.2"">
  <Document>
    <name>SkyPilot Mission</name>
    <description>Exported from SkyPilot GCS</description>
    <Style id=""waypointStyle"">
      <IconStyle>
        <color>ff00aaff</color>
        <scale>0.8</scale>
        <Icon>
          <href>http://maps.google.com/mapfiles/kml/shapes/placemark_square.png</href>
        </Icon>
      </IconStyle>
      <LineStyle>
        <color>ff00aaff</color>
        <width>3</width>
      </LineStyle>
    </Style>
    <Style id=""routeStyle"">
      <LineStyle>
        <color>ff00aaff</color>
        <width>3</width>
      </LineStyle>
    </Style>
    <Placemark>
      <name>Mission Route</name>
      <styleUrl>#routeStyle</styleUrl>
      <LineString>
        <tessellate>1</tessellate>
        <altitudeMode>relativeToGround</altitudeMode>
        <coordinates>
          {coordsString}
        </coordinates>
      </LineString>
    </Placemark>
    {placemarkString}
  </Document>
</kml>";

        File.WriteAllText(sfd.FileName, kml);
        MessageBox.Show($"KML exported: {_waypoints.Count} waypoints\n{sfd.FileName}",
            "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    public void SetWaypoints(List<Waypoint> waypoints)
    {
        _waypoints.Clear();
        _waypoints.AddRange(waypoints);
        RefreshGrid();
    }

    public List<Waypoint> GetWaypoints() => new(_waypoints);

    public void AddWaypointFromMap(double lat, double lon)
    {
        AddWaypointCommand("WAYPOINT", lat, lon, 50);
    }

    public void UpdateWaypointFromMap(int index, double lat, double lon, double alt, double spd)
    {
        var wp = _waypoints.FirstOrDefault(w => w.Seq == index);
        if (wp != null)
        {
            wp.Lat = lat;
            wp.Lon = lon;
            wp.Alt = alt;
            wp.Param2 = spd; // Speed stored in Param2
            RefreshGrid();
        }
    }
}

public class Waypoint
{
    public int Seq { get; set; }
    public string Command { get; set; } = "WAYPOINT";
    public double Lat { get; set; }
    public double Lon { get; set; }
    public double Alt { get; set; }
    public double Param1 { get; set; }
    public double Param2 { get; set; }
    public double Param3 { get; set; }
    public double Param4 { get; set; }
}
