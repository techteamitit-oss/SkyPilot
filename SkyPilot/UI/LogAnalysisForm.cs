using SkyPilot.Log;
using SkyPilot.Panels;
using SkyPilot.Utils;

namespace SkyPilot.UI;

/// <summary>
/// Log analysis form with playback and sensor visualization.
/// </summary>
public class LogAnalysisForm : Form
{
    private readonly string _filePath;
    private readonly SensorDashboardPanel _sensorsPanel;
    private readonly OverviewPanel _overviewPanel;
    private readonly ToolStripProgressBar progressBar;
    private readonly TrackBar trackBar;
    private readonly Label lblPosition;
    private readonly System.Windows.Forms.Timer playTimer;
    private List<LogMessage> _messages = new();
    private int _currentIndex = 0;
    private bool _isPlaying = false;

    public LogAnalysisForm(string filePath, AppSettings settings)
    {
        _filePath = filePath;
        Text = $"Log Analysis - {Path.GetFileName(filePath)}";
        Size = new Size(1200, 750);
        BackColor = Color.FromArgb(30, 30, 30);
        ForeColor = Color.White;

        // Top toolbar
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(5) };

        var btnPlay = new Button { Text = "Play", Width = 60 };
        btnPlay.Click += (s, e) => { _isPlaying = !_isPlaying; btnPlay.Text = _isPlaying ? "Pause" : "Play"; };
        toolbar.Controls.Add(btnPlay);

        var btnSlower = new Button { Text = "<<", Width = 40 };
        btnSlower.Click += (s, e) => playTimer.Interval = Math.Min(playTimer.Interval * 2, 5000);
        toolbar.Controls.Add(btnSlower);

        var btnFaster = new Button { Text = ">>", Width = 40 };
        btnFaster.Click += (s, e) => playTimer.Interval = Math.Max(playTimer.Interval / 2, 10);
        toolbar.Controls.Add(btnFaster);

        trackBar = new TrackBar { Dock = DockStyle.Bottom, Minimum = 0, Maximum = 100, TickFrequency = 1 };
        trackBar.Scroll += (s, e) => { _currentIndex = trackBar.Value; UpdateDisplay(); };
        Controls.Add(trackBar);

        lblPosition = new Label { Dock = DockStyle.Bottom, Height = 20, TextAlign = ContentAlignment.MiddleCenter };
        Controls.Add(lblPosition);

        progressBar = new ToolStripProgressBar { Width = 200 };

        // Panels
        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 400 };
        _overviewPanel = new OverviewPanel();
        _sensorsPanel = new SensorDashboardPanel();
        split.Panel1.Controls.Add(_overviewPanel);
        split.Panel2.Controls.Add(_sensorsPanel);
        Controls.Add(split);
        Controls.Add(toolbar);

        // Playback timer
        playTimer = new System.Windows.Forms.Timer { Interval = 100 };
        playTimer.Tick += PlayTimer_Tick;
        playTimer.Start();

        // Load log async
        Task.Run(() => LoadLog());
    }

    private void LoadLog()
    {
        BeginInvoke(() => Text = $"Loading: {Path.GetFileName(_filePath)}...");

        var ext = Path.GetExtension(_filePath).ToLower();
        if (ext == ".bin")
        {
            var parser = new BinaryLogParser();
            parser.Parse(_filePath, p => BeginInvoke(() => progressBar.Value = (int)p));
            _messages = parser.Messages.ToList();
        }
        else
        {
            var parser = new TextLogParser();
            parser.Parse(_filePath, p => BeginInvoke(() => progressBar.Value = (int)p));
            _messages = parser.Messages.ToList();
        }

        BeginInvoke(() =>
        {
            Text = $"Log Analysis - {Path.GetFileName(_filePath)} ({_messages.Count} messages)";
            trackBar.Maximum = Math.Max(0, _messages.Count - 1);
            progressBar.Value = 0;
            if (_messages.Count > 0) UpdateDisplay();
        });
    }

    private void PlayTimer_Tick(object? sender, EventArgs e)
    {
        if (!_isPlaying || _messages.Count == 0) return;

        _currentIndex = Math.Min(_currentIndex + 1, _messages.Count - 1);
        trackBar.Value = _currentIndex;
        UpdateDisplay();

        if (_currentIndex >= _messages.Count - 1)
            _isPlaying = false;
    }

    private void UpdateDisplay()
    {
        if (_currentIndex < 0 || _currentIndex >= _messages.Count) return;
        var msg = _messages[_currentIndex];

        lblPosition.Text = $"{_currentIndex}/{_messages.Count - 1} | {msg.TypeName} | T={msg.TimeSeconds:F3}s";

        _sensorsPanel?.UpdateFromLogMessage(msg);
    }
}
