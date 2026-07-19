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
        _grid.Columns.Add("Command", "Command");
        _grid.Columns.Add("Lat", "Latitude");
        _grid.Columns.Add("Lon", "Longitude");
        _grid.Columns.Add("Alt", "Alt (m)");
        _grid.Columns.Add("P1", "Param1");
        _grid.Columns.Add("P2", "Param2");
        _grid.Columns.Add("P3", "Param3");
        _grid.Columns.Add("P4", "Param4");

        var cmdCol = (DataGridViewComboBoxColumn)_grid.Columns["Command"]!;
        cmdCol.Items.AddRange(new object[] {
            "WAYPOINT", "TAKEOFF", "LAND", "LOITER_UNLIM",
            "LOITER_TURNS", "LOITER_TIME", "RTL", "DO_CHANGE_SPEED",
            "DO_SET_ROI", "CONDITION_DELAY"
        });

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
        using var sfd = new SaveFileDialog { Filter = "Mission files|*.mission|All files|*.*", FileName = "mission.mission" };
        if (sfd.ShowDialog() != DialogResult.OK) return;

        var lines = _waypoints.Select(wp =>
            $"{wp.Seq}\t{wp.Command}\t{wp.Lat}\t{wp.Lon}\t{wp.Alt}\t{wp.Param1}\t{wp.Param2}\t{wp.Param3}\t{wp.Param4}");
        File.WriteAllLines(sfd.FileName, lines);
    }

    private void LoadMission()
    {
        using var ofd = new OpenFileDialog { Filter = "Mission files|*.mission|All files|*.*" };
        if (ofd.ShowDialog() != DialogResult.OK) return;

        _waypoints.Clear();
        foreach (var line in File.ReadLines(ofd.FileName))
        {
            var parts = line.Split('\t');
            if (parts.Length < 5) continue;
            _waypoints.Add(new Waypoint
            {
                Seq = int.TryParse(parts[0], out var s) ? s : 0,
                Command = parts[1],
                Lat = double.TryParse(parts[2], out var la) ? la : 0,
                Lon = double.TryParse(parts[3], out var lo) ? lo : 0,
                Alt = double.TryParse(parts[4], out var al) ? al : 50,
                Param1 = parts.Length > 5 && double.TryParse(parts[5], out var p1) ? p1 : 0,
                Param2 = parts.Length > 6 && double.TryParse(parts[6], out var p2) ? p2 : 0,
                Param3 = parts.Length > 7 && double.TryParse(parts[7], out var p3) ? p3 : 0,
                Param4 = parts.Length > 8 && double.TryParse(parts[8], out var p4) ? p4 : 0
            });
        }
        RefreshGrid();
    }

    public void SetWaypoints(List<Waypoint> waypoints)
    {
        _waypoints.Clear();
        _waypoints.AddRange(waypoints);
        RefreshGrid();
    }

    public List<Waypoint> GetWaypoints() => new(_waypoints);
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
