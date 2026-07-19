using System.IO.Ports;
using SkyPilot.Utils;

namespace SkyPilot.UI;

public class ConnectSerialDialog : Form
{
    public string SelectedPort => cmbPorts.SelectedItem?.ToString() ?? "";
    public int SelectedBaud => int.Parse(cmbBaud.SelectedItem?.ToString() ?? "115200");

    private readonly ComboBox cmbPorts;
    private readonly ComboBox cmbBaud;
    private readonly Button btnConnect;

    public ConnectSerialDialog(AppSettings settings)
    {
        Text = "Connect Serial Port";
        Size = new Size(350, 200);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(35, 35, 35);
        ForeColor = Color.White;

        Controls.Add(new Label { Text = "Port:", Location = new Point(20, 25), AutoSize = true });
        cmbPorts = new ComboBox
        {
            Location = new Point(80, 22),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 200
        };
        cmbPorts.Items.AddRange(SerialPort.GetPortNames());
        if (cmbPorts.Items.Contains(settings.LastSerialPort))
            cmbPorts.SelectedItem = settings.LastSerialPort;
        else if (cmbPorts.Items.Count > 0)
            cmbPorts.SelectedIndex = 0;
        Controls.Add(cmbPorts);

        Controls.Add(new Label { Text = "Baud:", Location = new Point(20, 60), AutoSize = true });
        cmbBaud = new ComboBox
        {
            Location = new Point(80, 57),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 200
        };
        cmbBaud.Items.AddRange(new object[] { "57600", "115200", "230400", "460800", "921600" });
        cmbBaud.SelectedItem = settings.LastBaudRate.ToString();
        Controls.Add(cmbBaud);

        btnConnect = new Button
        {
            Text = "Connect",
            Location = new Point(120, 110),
            Size = new Size(100, 30),
            DialogResult = DialogResult.OK
        };
        Controls.Add(btnConnect);
        AcceptButton = btnConnect;
    }
}

public class ConnectUdpDialog : Form
{
    public string SelectedHost => txtHost.Text;
    public int SelectedPort => int.Parse(txtPort.Text);

    private readonly TextBox txtHost;
    private readonly TextBox txtPort;
    private readonly Button btnConnect;

    public ConnectUdpDialog(AppSettings settings)
    {
        Text = "Connect UDP";
        Size = new Size(350, 180);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(35, 35, 35);
        ForeColor = Color.White;

        Controls.Add(new Label { Text = "Host:", Location = new Point(20, 25), AutoSize = true });
        txtHost = new TextBox { Location = new Point(80, 22), Width = 200, Text = settings.LastUdpHost };
        Controls.Add(txtHost);

        Controls.Add(new Label { Text = "Port:", Location = new Point(20, 60), AutoSize = true });
        txtPort = new TextBox { Location = new Point(80, 57), Width = 200, Text = settings.LastUdpPort.ToString() };
        Controls.Add(txtPort);

        btnConnect = new Button
        {
            Text = "Connect",
            Location = new Point(120, 100),
            Size = new Size(100, 30),
            DialogResult = DialogResult.OK
        };
        Controls.Add(btnConnect);
        AcceptButton = btnConnect;
    }
}

public class ConnectTcpDialog : Form
{
    public string SelectedHost => txtHost.Text;
    public int SelectedPort => int.Parse(txtPort.Text);

    private readonly TextBox txtHost;
    private readonly TextBox txtPort;
    private readonly Button btnConnect;

    public ConnectTcpDialog(AppSettings settings)
    {
        Text = "Connect TCP";
        Size = new Size(350, 180);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(35, 35, 35);
        ForeColor = Color.White;

        Controls.Add(new Label { Text = "Host:", Location = new Point(20, 25), AutoSize = true });
        txtHost = new TextBox { Location = new Point(80, 22), Width = 200, Text = settings.LastUdpHost };
        Controls.Add(txtHost);

        Controls.Add(new Label { Text = "Port:", Location = new Point(20, 60), AutoSize = true });
        txtPort = new TextBox { Location = new Point(80, 57), Width = 200, Text = "5762" };
        Controls.Add(txtPort);

        btnConnect = new Button
        {
            Text = "Connect",
            Location = new Point(120, 100),
            Size = new Size(100, 30),
            DialogResult = DialogResult.OK
        };
        Controls.Add(btnConnect);
        AcceptButton = btnConnect;
    }
}
