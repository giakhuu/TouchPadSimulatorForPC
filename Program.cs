using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

class Program
{
    // P/Invoke cho SendInput
    [DllImport("user32.dll", SetLastError = true)]
    static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    struct INPUT
    {
        public uint type;
        public MOUSEINPUT mi;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
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

    static void Main()
    {
        const int PORT = 5000;
        using var udp = new UdpClient(PORT);
        Console.WriteLine($"Listening for UDP on port {PORT}...");

        var remoteEP = new IPEndPoint(IPAddress.Any, 0);
        while (true)
        {
            byte[] buf;
            try
            {
                buf = udp.Receive(ref remoteEP);
            }
            catch (SocketException se)
            {
                Console.WriteLine("Socket closed: " + se.Message);
                break;
            }

            if (buf.Length < 1) continue;
            byte type = buf[0];
            Console.WriteLine($"Received type={type:X2}, data={BitConverter.ToString(buf)}");

            if (type == 0x00 && buf.Length >= 3)
            {
                // Chuyển sang signed
                sbyte dx = (sbyte)buf[1];
                sbyte dy = (sbyte)buf[2];
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
        }
    }
}
