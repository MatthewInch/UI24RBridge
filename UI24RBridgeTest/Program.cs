using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using UI24RController;
using UI24RController.MIDIController;

namespace UI24RBridgeTest
{
    class Program
    {
        private static readonly object balanceLock = new object();
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
            var defaultRecButtonIsMtk = configuration["DefaultRecButton"] == "mtk";
            //var controller = new BehringerUniversalMIDI();
            var controller = MIDIControllerFactory.GetMidiController(protocol);
            if (args.Length > 0)
                WriteMIDIDeviceNames(controller);
            else
            {
                Console.WriteLine("Create Bridge Object");
                if (midiInputDevice == null || midiOutputDevice == null)
                {
                    if (midiInputDevice == null)
                        Console.WriteLine("The input device name is mandantory in the config file. (MIDI-Input-Name)");
                    if (midiOutputDevice == null)
                        Console.WriteLine("The ouput device name is mandantory in the config file. (MIDI-Output-Name)");
                    WriteMIDIDeviceNames(controller);
                }
                else
                {
                    Console.WriteLine("Set controller message event....");
                    controller.MessageReceived += (obj, e) =>
                    {
                        lock (balanceLock)
                        {
                            Console.WriteLine(e.Message);
                        }
                    };

                    Console.WriteLine("Connect input device...");
                    controller.ConnectInputDevice(midiInputDevice);
                    Console.WriteLine("Connect output device...");
                    controller.ConnectOutputDevice(midiOutputDevice);

                }

                Action<string, bool> messageWriter = (string messages, bool isDebugMessage) =>
                 {
                     if (!isDebugMessage || (isDebugMessage && viewDebugMessage))
                     {
                         var m = messages.Split('\n');
                         foreach (var message in m)
                         {
                             if (!message.StartsWith("3:::RTA^") && !message.StartsWith("RTA^") &&
                                 !message.StartsWith("3:::VU2^") && !message.StartsWith("VU2^")
                             )
                             {
                                 lock (balanceLock)
                                 {
                                     Console.WriteLine(message);
                                 }
                             }
                         }
                     }
                 };
                Console.WriteLine("Start bridge...");
                using (UI24RBridge bridge = new UI24RBridge(address, controller, messageWriter, syncID, defaultRecButtonIsMtk))
                {
                    while (!Console.KeyAvailable)
                    {
                    }
                }
            }
        }

        /// <summary>
        /// Write the MIDI device names to the console. It help to set the config file.
        /// </summary>
        /// <param name="controller"></param>
        private static void WriteMIDIDeviceNames(IMIDIController controller)
        {
            var inputDevicenames = controller.GetInputDeviceNames();
            foreach (var inputDevice in inputDevicenames)
            {
                Console.WriteLine($"Input device name: {inputDevice}");
            }

            var outputDevicenames = controller.GetOutputDeviceNames();
            foreach (var outputDevice in outputDevicenames)
            {
                Console.WriteLine($"Output device name: {outputDevice}");
            }
            Console.WriteLine();
        }
    }
}
