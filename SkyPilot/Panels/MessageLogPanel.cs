namespace SkyPilot.Panels;

/// <summary>
/// MAVLink message log panel displaying STATUSTEXT messages.
/// </summary>
public class MessageLogPanel : UserControl
{
    private readonly ListBox _listBox;
    private readonly int _maxMessages = 500;

    public MessageLogPanel()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(30, 30, 30);

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 30,
            BackColor = Color.FromArgb(40, 40, 40),
            Padding = new Padding(4)
        };

        var btnClear = new Button { Text = "Clear", Width = 60, FlatStyle = FlatStyle.Flat };
        btnClear.BackColor = Color.FromArgb(60, 60, 60);
        btnClear.ForeColor = Color.White;
        btnClear.Click += (s, e) => _listBox.Items.Clear();
        toolbar.Controls.Add(btnClear);

        var chkAutoScroll = new CheckBox
        {
            Text = "Auto-scroll",
            Checked = true,
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            AutoSize = true,
            Padding = new Padding(10, 0, 0, 0)
        };
        toolbar.Controls.Add(chkAutoScroll);

        _listBox = new ListBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(25, 25, 25),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.None,
            Font = new Font("Consolas", 9f),
            SelectionMode = SelectionMode.None
        };

        Controls.Add(_listBox);
        Controls.Add(toolbar);
    }

    public void AddMessage(string text, byte severity = 6)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        string prefix = severity switch
        {
            <= 2 => "[EMERG]",
            <= 4 => "[ALERT]",
            <= 6 => "[CRIT]",
            <= 8 => "[ERROR]",
            <= 10 => "[WARN]",
            <= 12 => "[NOTICE]",
            <= 14 => "[INFO]",
            _ => "[DEBUG]"
        };

        var item = new MessageItem($"{timestamp} {prefix} {text}", severity);

        if (_listBox.InvokeRequired)
            _listBox.Invoke(() => AddToList(item));
        else
            AddToList(item);
    }

    private void AddToList(MessageItem item)
    {
        _listBox.Items.Add(item);
        if (_listBox.Items.Count > _maxMessages)
            _listBox.Items.RemoveAt(0);

        // Auto-scroll
        if (_listBox.TopIndex >= _listBox.Items.Count - 5)
            _listBox.TopIndex = _listBox.Items.Count - 1;
    }

    private class MessageItem
    {
        public string Text { get; }
        public byte Severity { get; }

        public MessageItem(string text, byte severity)
        {
            Text = text;
            Severity = severity;
        }

        public override string ToString() => Text;
    }
}
