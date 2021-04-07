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
            var recButtonBahavior = configuration["DefaultRecButton"];
            var channelRecButtonBahavior = configuration["DefaultChannelRecButton"];
            var auxButtonBehavior = configuration["AuxButtonBehavior"];
            var buttonsValues = configuration["PrimaryButtons"];
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

                BridgeSettings settings = new BridgeSettings(address, controller, messageWriter);
                if (syncID != null)
                {
                    settings.SyncID = syncID;
                }
                if (recButtonBahavior != null)
                {
                    switch (recButtonBahavior.ToLower())
                    {
                        case "onlymtk":
                            settings.RecButtonBehavior = BridgeSettings.RecButtonBehaviorEnum.OnlyMTK;
                            break;
                        case "only2track":
                            settings.RecButtonBehavior = BridgeSettings.RecButtonBehaviorEnum.OnlyTwoTrack;
                            break;
                        default:
                            settings.RecButtonBehavior = BridgeSettings.RecButtonBehaviorEnum.TwoTrackAndMTK;
                            break;
                    }
                }
                if (channelRecButtonBahavior != null)
                {
                    switch (channelRecButtonBahavior.ToLower())
                    {
                        case "phantom":
                            settings.ChannelRecButtonBehavior = BridgeSettings.ChannelRecButtonBehaviorEnum.Phantom;
                            break;
                        default:
                            settings.ChannelRecButtonBehavior = BridgeSettings.ChannelRecButtonBehaviorEnum.Rec;
                            break;
                    }
                }
                if (auxButtonBehavior != null)
                {
                    switch(auxButtonBehavior.ToLower() )
                    {
                        case "lock":
                            settings.AuxButtonBehavior = BridgeSettings.AuxButtonBehaviorEnum.Lock;
                            break;
                        default:
                            settings.AuxButtonBehavior = BridgeSettings.AuxButtonBehaviorEnum.Release;
                            break;
                    }
                }
                if (buttonsValues != null)
                {
                    settings.ButtonsValuesFileName = buttonsValues;
                }
                using (UI24RBridge bridge = new UI24RBridge(settings))
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
