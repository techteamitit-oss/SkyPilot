namespace SkyPilot.Panels;

/// <summary>
/// Parameter management panel for reading/writing vehicle parameters.
/// </summary>
public class ParameterPanel : UserControl
{
    private readonly DataGridView _grid;
    private readonly TextBox _txtSearch;
    private readonly Dictionary<string, double> _params = new();
    private readonly HashSet<string> _changed = new();

    public event Action? RequestAllParams;
    public event Action<string, float>? WriteParam;

    public ParameterPanel()
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

        toolbar.Controls.Add(MakeButton("Refresh", (s, e) => RequestAllParams?.Invoke()));
        toolbar.Controls.Add(MakeButton("Write Changed", (s, e) => WriteChanged()));
        toolbar.Controls.Add(MakeButton("Save File", (s, e) => SaveParams()));
        toolbar.Controls.Add(MakeButton("Load File", (s, e) => LoadParams()));

        _txtSearch = new TextBox
        {
            Width = 200,
            PlaceholderText = "Search parameters...",
            BackColor = Color.FromArgb(50, 50, 50),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        _txtSearch.TextChanged += (s, e) => FilterGrid();
        toolbar.Controls.Add(_txtSearch);

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

        _grid.Columns.Add("Name", "Parameter");
        _grid.Columns.Add("Value", "Value");
        _grid.Columns.Add("Status", "Status");

        _grid.CellValueChanged += (s, e) =>
        {
            if (e.ColumnIndex == 1 && e.RowIndex >= 0)
            {
                string name = _grid.Rows[e.RowIndex].Cells[0].Value?.ToString() ?? "";
                if (_params.ContainsKey(name))
                {
                    _changed.Add(name);
                    _grid.Rows[e.RowIndex].Cells[2].Value = "Modified";
                    _grid.Rows[e.RowIndex].Cells[2].Style.ForeColor = Color.Orange;
                }
            }
        };

        Controls.Add(_grid);
        Controls.Add(toolbar);
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

    public void SetParameter(string name, float value)
    {
        _params[name] = value;
        RefreshGrid();
    }

    public void ClearParameters()
    {
        _params.Clear();
        _changed.Clear();
        _grid.Rows.Clear();
    }

    private void RefreshGrid()
    {
        string filter = _txtSearch.Text.ToLower();
        _grid.Rows.Clear();
        foreach (var kvp in _params.OrderBy(x => x.Key))
        {
            if (!string.IsNullOrEmpty(filter) && !kvp.Key.ToLower().Contains(filter))
                continue;

            string status = _changed.Contains(kvp.Key) ? "Modified" : "OK";
            var row = _grid.Rows.Add(kvp.Key, $"{kvp.Value:F4}", status);
            if (_changed.Contains(kvp.Key))
                _grid.Rows[row].Cells[2].Style.ForeColor = Color.Orange;
            else
                _grid.Rows[row].Cells[2].Style.ForeColor = Color.LimeGreen;
        }
    }

    private void FilterGrid() => RefreshGrid();

    private void WriteChanged()
    {
        foreach (var name in _changed)
        {
            if (_params.TryGetValue(name, out var val))
                WriteParam?.Invoke(name, (float)val);
        }
        _changed.Clear();
        RefreshGrid();
    }

    private void SaveParams()
    {
        using var sfd = new SaveFileDialog { Filter = "Param files|*.param|All files|*.*", FileName = "params.param" };
        if (sfd.ShowDialog() != DialogResult.OK) return;

        var lines = _params.Select(kvp => $"{kvp.Key}\t{kvp.Value}");
        File.WriteAllLines(sfd.FileName, lines);
    }

    private void LoadParams()
    {
        using var ofd = new OpenFileDialog { Filter = "Param files|*.param|All files|*.*" };
        if (ofd.ShowDialog() != DialogResult.OK) return;

        foreach (var line in File.ReadLines(ofd.FileName))
        {
            var parts = line.Split('\t');
            if (parts.Length >= 2 && double.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var val))
            {
                _params[parts[0]] = val;
            }
        }
        RefreshGrid();
    }
}
