using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

class Program
{
    // P/Invoke cho SendInput (giữ nguyên)
    [DllImport("user32.dll", SetLastError = true)]
    static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    struct INPUT { public uint type; public MOUSEINPUT mi; }
    [StructLayout(LayoutKind.Sequential)]
    struct MOUSEINPUT
    {
        public int dx; public int dy; public uint mouseData;
        public uint dwFlags; public uint time; public IntPtr dwExtraInfo;
    }

    const uint INPUT_MOUSE = 0;
    const uint MOUSEEVENTF_MOVE = 0x0001;
    const uint MOUSEEVENTF_MOVE_NOCOALESCE = 0x2000;
    const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    const uint MOUSEEVENTF_LEFTUP = 0x0004;
    const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    const uint MOUSEEVENTF_RIGHTUP = 0x0010;

    static void MouseMove(int dx, int dy)
    {
        var inputs = new INPUT[1];
        inputs[0].type = INPUT_MOUSE;
        inputs[0].mi.dx = dx;
        inputs[0].mi.dy = dy;
        inputs[0].mi.mouseData = 0;
        inputs[0].mi.dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_MOVE_NOCOALESCE;
        inputs[0].mi.time = 0;
        inputs[0].mi.dwExtraInfo = IntPtr.Zero;
        if (SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT))) == 0)
            Console.WriteLine("SendInput(MouseMove) failed: " + Marshal.GetLastWin32Error());
    }

    static void MouseLeftClick()
    {
        var inputs = new INPUT[2];
        inputs[0].type = INPUT_MOUSE;
        inputs[0].mi.dwFlags = MOUSEEVENTF_LEFTDOWN;
        inputs[1].type = INPUT_MOUSE;
        inputs[1].mi.dwFlags = MOUSEEVENTF_LEFTUP;
        SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
    }

    static void MouseRightClick()
    {
        var inputs = new INPUT[2];
        inputs[0].type = INPUT_MOUSE;
        inputs[0].mi.dwFlags = MOUSEEVENTF_RIGHTDOWN;
        inputs[1].type = INPUT_MOUSE;
        inputs[1].mi.dwFlags = MOUSEEVENTF_RIGHTUP;
        SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
    }

    // Lấy IP địa phương phù hợp để reply (connect tạm UDP đến remote để biết local endpoint)
    static IPAddress GetLocalAddressForRemote(IPAddress remote)
    {
        try
        {
            using var sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            sock.Connect(remote, 65530); // port arbitrary
            if (sock.LocalEndPoint is IPEndPoint lep)
                return lep.Address;
        }
        catch
        {
            // fallback to first IPv4 non-loopback
            foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                    return ip;
            }
        }
        return IPAddress.Loopback;
    }

    static void Main()
    {
        const int PORT = 5000;
        using var udp = new UdpClient(PORT);
        Console.WriteLine($"Responder & Controller listening on UDP port {PORT}...");

        IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);

        while (true)
        {
            byte[] data;
            try
            {
                data = udp.Receive(ref remote); // blocking
            }
            catch (SocketException se)
            {
                Console.WriteLine("Socket closed: " + se.Message);
                break;
            }

            if (data == null || data.Length == 0) continue;

            // Nếu first byte tương ứng control (0x00/0x01/0x02) và độ dài phù hợp => control
            bool maybeControl = (data.Length >= 1) &&
                (data[0] == 0x00 || data[0] == 0x01 || data[0] == 0x02);

            if (maybeControl)
            {
                byte type = data[0];
                Console.WriteLine($"Control packet from {remote.Address}:{remote.Port} - type=0x{type:X2}, raw={BitConverter.ToString(data)}");
                if (type == 0x00 && data.Length >= 3)
                {
                    sbyte dx = (sbyte)data[1];
                    sbyte dy = (sbyte)data[2];
                    MouseMove(dx, dy);
                    Console.WriteLine($"Move dx={dx}, dy={dy}");
                }
                else if (type == 0x01)
                {
                    MouseLeftClick();
                    Console.WriteLine("Left click");
                }
                else if (type == 0x02)
                {
                    MouseRightClick();
                    Console.WriteLine("Right click");
                }
                continue;
            }

            // Không phải control -> thử decode là discovery (text)
            string txt = null;
            try { txt = Encoding.UTF8.GetString(data).Trim(); } catch { txt = null; }

            if (!string.IsNullOrEmpty(txt) && txt.StartsWith("DISCOVER_TOUCHPAD", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"Discovery from {remote.Address}:{remote.Port} payload='{txt}'");

                // Lấy IP local phù hợp để gửi về phone (giúp phone biết IP PC)
                IPAddress local = GetLocalAddressForRemote(remote.Address);
                string reply = $"TOUCHPAD_OK:{local}"; // e.g. "TOUCHPAD_OK:192.168.244.149"
                byte[] respBytes = Encoding.UTF8.GetBytes(reply);
                udp.Send(respBytes, respBytes.Length, remote); // gửi unicast tới sender
                Console.WriteLine($"Replied '{reply}' to {remote.Address}");
                continue;
            }

            // Nếu tới đây mà không phải control hay discovery -> log và ignore
            Console.WriteLine($"Unknown packet from {remote.Address}:{remote.Port} - raw={BitConverter.ToString(data)}");
        }
    }
}
