using Commons.Music.Midi;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using UI24RController;
using UI24RController.MIDIController;
using UI24RController.Settings.Helper;

namespace MidiTest
{
    class Program
    {
        static void Main(string[] args)
        {
            //MidiConnectionTest();
            SerializationTest();
        }

        protected class SerializerTest
        {
            [JsonConverter(typeof(DictionaryTKeyEnumTValueConverter))]
            public Dictionary<ButtonsEnum, byte> ButtonsDictionary { get; set; }
        }

        private static void SerializationTest()
        {
            ButtonsID buttonsID = ButtonsID.Instance;

            var dictionary = buttonsID.GetButtonsDictionary();
            var test = new SerializerTest();
            test.ButtonsDictionary = dictionary;

            string json = JsonSerializer.Serialize(test);
            File.WriteAllText("ButtonsConfig.json", json);

            var testin = JsonSerializer.Deserialize(json, typeof(SerializerTest));
        }

        private static void MidiConnectionTest()
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
            byte modulus = 0;
            while (isrunning)
            {
                if (Console.KeyAvailable)
                {
                    modulus = (byte)((modulus + 1) % 16);
                    Console.Write($" {modulus}");
                    output.Send(new byte[] { 0xD0, modulus }, 0, 2, 0);
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
