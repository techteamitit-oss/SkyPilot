using System.IO.Ports;
using System.Net;
using System.Net.Sockets;

namespace SkyPilot.Core.Mavlink;

/// <summary>
/// Manages serial/UDP/TCP connection and sends/receives MAVLink packets.
/// Also supports simulation mode with a virtual vehicle.
/// </summary>
public class MavlinkStream : IDisposable
{
    private SerialPort? _serial;
    private UdpClient? _udp;
    private TcpClient? _tcp;
    private NetworkStream? _tcpStream;
    private CancellationTokenSource? _cts;
    private Sim.VirtualVehicle? _sim;

    public bool IsOpen =>
        (_serial?.IsOpen ?? false) ||
        (_udp != null) ||
        (_tcp?.Connected ?? false) ||
        (_sim != null);

    public bool IsSimulation => _sim != null;

    public event Action<MavlinkPacket>? PacketReceived;
    public event Action<byte[], int>? RawDataSent;

    public void OpenSerial(string portName, int baudRate)
    {
        Close();
        _serial = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One);
        _serial.Open();
        StartReadingAsync();
    }

    public void OpenUdp(string host, int port)
    {
        Close();
        _udp = new UdpClient();
        _udp.Connect(host, port);
        StartReadingAsync();
    }

    public void OpenTcp(string host, int port)
    {
        Close();
        _tcp = new TcpClient();
        _tcp.Connect(host, port);
        _tcpStream = _tcp.GetStream();
        StartReadingAsync();
    }

    public void OpenSimulation(Sim.VirtualVehicle sim)
    {
        Close();
        _sim = sim;
        _sim.PacketGenerated += OnSimPacket;
    }

    private void OnSimPacket(MavlinkPacket packet)
    {
        PacketReceived?.Invoke(packet);
    }

    public void Send(byte[] data)
    {
        RawDataSent?.Invoke(data, data.Length);

        // In simulation mode, feed sent commands back to the simulator
        if (_sim != null)
        {
            try
            {
                var pkt = MavlinkCodec.Decode(data, 0, data.Length);
                if (pkt != null) HandleSimCommand(pkt);
            }
            catch { }
            return;
        }

        try
        {
            if (_serial?.IsOpen == true)
                _serial.Write(data, 0, data.Length);
            else if (_udp != null)
                _udp.Send(data, data.Length);
            else if (_tcpStream?.CanWrite == true)
                _tcpStream.Write(data, 0, data.Length);
        }
        catch { }
    }

    private void HandleSimCommand(MavlinkPacket pkt)
    {
        if (_sim == null) return;

        if (pkt.MessageId == MavlinkDefinitions.COMMAND_LONG)
        {
            ushort cmd = pkt.GetUInt16(0);
            float p1 = pkt.GetFloat(4);

            if (cmd == MavlinkDefinitions.MAV_CMD_COMPONENT_ARM_DISARM)
                _sim.SetArmed(p1 > 0.5f);
            else if (cmd == MavlinkDefinitions.MAV_CMD_NAV_TAKEOFF)
            {
                _sim.SetArmed(true);
                _sim.SendStatustext("Takeoff");
            }
            else if (cmd == MavlinkDefinitions.MAV_CMD_NAV_RETURN_TO_LAUNCH)
                _sim.SendStatustext("RTL");
            else if (cmd == MavlinkDefinitions.MAV_CMD_NAV_LAND)
                _sim.SendStatustext("Land");

            // Send COMMAND_ACK
            var ackPayload = new byte[3];
            ackPayload[0] = (byte)(cmd & 0xFF);
            ackPayload[1] = MavlinkDefinitions.MAV_RESULT_ACCEPTED;
            ackPayload[2] = 0;
            var ackBytes = MavlinkCodec.Encode(1, 1, MavlinkDefinitions.COMMAND_ACK, ackPayload);
            var ackPkt = MavlinkCodec.Decode(ackBytes, 0, ackBytes.Length);
            if (ackPkt != null) PacketReceived?.Invoke(ackPkt);
        }
        else if (pkt.MessageId == MavlinkDefinitions.SET_MODE)
        {
            uint mode = pkt.GetUInt32(4);
            _sim.SetMode((int)mode);

            var ackPayload = new byte[3];
            ackPayload[0] = MavlinkDefinitions.SET_MODE;
            ackPayload[1] = MavlinkDefinitions.MAV_RESULT_ACCEPTED;
            var ackBytes = MavlinkCodec.Encode(1, 1, MavlinkDefinitions.COMMAND_ACK, ackPayload);
            var ackPkt = MavlinkCodec.Decode(ackBytes, 0, ackBytes.Length);
            if (ackPkt != null) PacketReceived?.Invoke(ackPkt);
        }
    }

    public void Close()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        if (_sim != null)
        {
            _sim.PacketGenerated -= OnSimPacket;
            _sim.Dispose();
            _sim = null;
        }

        try { _serial?.Close(); } catch { }
        _serial = null;

        try { _udp?.Close(); } catch { }
        _udp = null;

        try { _tcp?.Close(); } catch { }
        _tcp = null;
        _tcpStream = null;
    }

    private void StartReadingAsync()
    {
        _cts = new CancellationTokenSource();
        Task.Run(() => ReadLoop(_cts.Token));
    }

    private void ReadLoop(CancellationToken ct)
    {
        var buffer = new byte[4096];
        var accumulator = new List<byte>(4096);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                int bytesRead = 0;

                if (_serial?.IsOpen == true)
                {
                    if (_serial.BytesToRead > 0)
                        bytesRead = _serial.Read(buffer, 0, Math.Min(buffer.Length, _serial.BytesToRead));
                    else
                    {
                        Thread.Sleep(5);
                        continue;
                    }
                }
                else if (_udp != null)
                {
                    var ep = new IPEndPoint(IPAddress.Any, 0);
                    var data = _udp.Receive(ref ep);
                    bytesRead = data.Length;
                    Array.Copy(data, buffer, bytesRead);
                }
                else if (_tcpStream?.CanRead == true)
                {
                    bytesRead = _tcpStream.Read(buffer, 0, buffer.Length);
                }
                else
                {
                    Thread.Sleep(100);
                    continue;
                }

                if (bytesRead == 0)
                {
                    Thread.Sleep(10);
                    continue;
                }

                // Accumulate bytes and try to parse packets
                for (int i = 0; i < bytesRead; i++)
                {
                    if (buffer[i] == MavlinkCodec.StartByte)
                    {
                        accumulator.Add(buffer[i]);
                    }
                    else if (accumulator.Count > 0)
                    {
                        accumulator.Add(buffer[i]);
                    }

                    if (accumulator.Count >= MavlinkCodec.HeaderLength)
                    {
                        int payloadLen = accumulator[1];
                        int totalLen = MavlinkCodec.HeaderLength + payloadLen + MavlinkCodec.CrcLength;

                        if (accumulator.Count >= totalLen)
                        {
                            var packet = MavlinkCodec.Decode(
                                accumulator.ToArray(), 0, accumulator.Count);
                            if (packet != null)
                                PacketReceived?.Invoke(packet);
                            accumulator.Clear();
                        }
                    }
                }
            }
            catch (OperationCanceledException) { break; }
            catch { Thread.Sleep(10); }
        }
    }

    public void Dispose()
    {
        Close();
    }
}
