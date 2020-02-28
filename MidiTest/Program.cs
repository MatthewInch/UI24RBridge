using Commons.Music.Midi;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Linq;

namespace MidiTest
{
    class Program
    {
        static void Main(string[] args)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            var configuration = builder.Build();
            var address = configuration["UI24R-Url"];
            var midiInputDevice = configuration["MIDI-Input-Name"];
            var midiOutputDevice = configuration["MIDI-Output-Name"];
            var protocol = configuration["Protocol"];
            var syncID = configuration["SyncID"];
            var viewDebugMessage = configuration["DebugMessages"] == "true";

            var access = MidiAccessManager.Default;
            var deviceNumber = access.Outputs.Where(i => i.Name.ToUpper() == midiOutputDevice.ToUpper()).FirstOrDefault();
           
            var output = access.OpenOutputAsync(deviceNumber.Id).Result;

            deviceNumber = access.Inputs.Where(i => i.Name.ToUpper() == midiInputDevice.ToUpper()).FirstOrDefault();
            var input = access.OpenInputAsync(deviceNumber.Id).Result;
            input.MessageReceived += Input_MessageReceived;
            

            var ch = Console.ReadKey();
            bool lastValue = false;
            bool isrunning = true;
            while (isrunning)
            {
                if (Console.KeyAvailable)
                {
                    output.Send(new byte[] { 0x90, 0x00, (byte)(lastValue ? 0x7f : 0x00) }, 0, 3, 0);
                    lastValue = !lastValue;
                    ch = Console.ReadKey();
                    isrunning = ch.KeyChar != ' ';
                }
            }

        }

        private static void Input_MessageReceived(object sender, MidiReceivedEventArgs e)
        {
            if (e.Data.Length > 2)
            {
                //Console.WriteLine($"{e.Data[0].ToString("x2")} - {e.Data[1].ToString("x2")} - {e.Data[2].ToString("x2")}");
            }
        }
    }
}
