using System;
using System.Threading;
using UDP;

namespace XtclTest
{
    class Program
    {


        static void Main(string[] args)
        {
            UDPSocket s = new UDPSocket();
            s.Server("192.168.3.50", 10111);
            s.Client("192.168.3.91", 10111);
            
            bool isAlive = true;
            var thread = new Thread(() =>
            {
                byte i = 0;
                while (isAlive)
                {
                    s.Send(new byte[] { 0xf0, 0x00, 0x00, 0x66, 0x14, 0x00, 0xf7 });
                    //c.Send(new byte[] { 0x90, (byte)(63 + i), 0x7f });
                    //i =(byte)((i + 1) % 8);
                    Thread.Sleep(6000);
                }

            });
            thread.Start();
            Console.ReadKey();
            isAlive = false;
            Thread.Sleep(4500);
        }
    }
}
