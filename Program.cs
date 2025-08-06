using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

class Program
{
    // P/Invoke cho SendInput
    [DllImport("user32.dll")]
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
    const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    const uint MOUSEEVENTF_LEFTUP = 0x0004;
    const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    const uint MOUSEEVENTF_RIGHTUP = 0x0010;

    static void MouseMove(int dx, int dy)
    {
        INPUT[] inputs = new INPUT[1];
        inputs[0].type = INPUT_MOUSE;
        inputs[0].mi.dx = dx;
        inputs[0].mi.dy = dy;
        inputs[0].mi.mouseData = 0;
        inputs[0].mi.dwFlags = MOUSEEVENTF_MOVE;
        inputs[0].mi.time = 0;
        inputs[0].mi.dwExtraInfo = IntPtr.Zero;
        SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
    }

    static void MouseLeftClick()
    {
        INPUT[] inputs = new INPUT[2];
        inputs[0].type = INPUT_MOUSE;
        inputs[0].mi.dwFlags = MOUSEEVENTF_LEFTDOWN;
        inputs[1].type = INPUT_MOUSE;
        inputs[1].mi.dwFlags = MOUSEEVENTF_LEFTUP;
        SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
    }

    static void MouseRightClick()
    {
        INPUT[] inputs = new INPUT[2];
        inputs[0].type = INPUT_MOUSE;
        inputs[0].mi.dwFlags = MOUSEEVENTF_RIGHTDOWN;
        inputs[1].type = INPUT_MOUSE;
        inputs[1].mi.dwFlags = MOUSEEVENTF_RIGHTUP;
        SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
    }

    static void Main()
    {
        const int PORT = 5000;
        var listener = new TcpListener(IPAddress.Any, PORT);
        listener.Start();
        Console.WriteLine($"Listening on port {PORT}...");

        using var client = listener.AcceptTcpClient();
        Console.WriteLine("Client connected!");

        using var stream = client.GetStream();
        using var reader = new BinaryReader(stream);

        while (true)
        {
            byte type;
            Console.WriteLine(reader.ReadByte());
            try { type = reader.ReadByte(); }
            catch { Console.WriteLine("Client disconnected."); break; }

            if (type == 0x00)
            {
                byte dx = reader.ReadByte();
                byte dy = reader.ReadByte();
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

        listener.Stop();
    }
}