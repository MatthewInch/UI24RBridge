using System;
using System.Collections.Generic;
using System.Text;
using Commons.Music.Midi;
using System.Linq;
using System.Threading.Tasks;

namespace UI24RController.MIDIController
{
    public class MackieHUI : IMIDIController, IDisposable
    {
        protected Queue<byte[]> _messageQueue = new Queue<byte[]>();
        /// <summary>
        /// Store every fader setted value of the faders, key is the channel number (z in the message)
        /// </summary>
        protected Dictionary<byte, double> faderValues = new Dictionary<byte, double>();
            
        IMidiInput _input = null;
        IMidiOutput _output = null;

        public event EventHandler<MessageEventArgs> MessageReceived;
        public event EventHandler<FaderEventArgs> FaderEvent;
        public event EventHandler<EventArgs> PresetUp;
        public event EventHandler<EventArgs> PresetDown;
        public event EventHandler<GainEventArgs> GainEvent;
        public event EventHandler<ChannelEventArgs> SelectChannelEvent;

        protected void OnMessageReceived(string message)
        {
            if (MessageReceived != null)
            {
                var messageEventArgs = new MessageEventArgs();
                messageEventArgs.Message = message;
                MessageReceived(this, messageEventArgs);
            }
        }

        protected void OnFaderEvent(int channelNumber, double faderValue)
        {
            if (FaderEvent != null)
            {
                var faderEventArgs = new FaderEventArgs(channelNumber, faderValue);
                FaderEvent(this, faderEventArgs);
            }
        }

        protected void OnPresetUp()
        {
            if (PresetUp != null)
            {
                PresetUp(this, new EventArgs());
            }
        }
        protected void OnPresetDown()
        {
            if (PresetDown != null)
            {
                PresetDown(this, new EventArgs());
            }
        }

        public void Dispose()
        {
            if (_input != null)
            {
                _input.Dispose();
                _input = null;
            }
            if (_output != null)
            {
                _output.Dispose();
                _output = null;
            }
        }

        public  bool ConnectInputDevice(string deviceName)
        {
            var access = MidiAccessManager.Default;
            var deviceNumber = access.Inputs.Where(i => i.Name == deviceName).FirstOrDefault();
            if (deviceNumber != null)
            {
                _input = access.OpenInputAsync(deviceNumber.Id).Result;
                _input.MessageReceived += (obj, e) =>
                {
                    if (e.Data.Length>2)
                    {
                        _messageQueue.Enqueue(e.Data.Clone() as byte[]);
                        OnMessageReceived( $"{e.Data[0].ToString("x2")} - {e.Data[1].ToString("x2")} - {e.Data[2].ToString("x2")}");
                        ProcessMidiMessage();
                    }
                };
                return true;
            }
            else
                return false;            
        }

        private void ProcessMidiMessage()
        {
            //Every HUI event consist of two midi message except the answer of ping
            //ping: 90 00 00 -> 90 00 7f
            if (_messageQueue.Count > 1)
            {
                var firstMessage = _messageQueue.Dequeue();

                if (firstMessage.MIDIEqual(0x90, 0x00, 0x7f)) //ping answer -> do nothing
                {
                }
                else if(firstMessage.MIDIEqual(0xb0, 0x0f)&& (firstMessage[2] < 8)) //first message of:  release fader 
                {
                    var channelNumber = firstMessage[2];
                    var secondMessage = _messageQueue.Dequeue();
                    if (secondMessage.MIDIEqual(0xb0, 0x2f, 0x00)) //release fader
                    {
                        //TODO: Send back the last fader value to the controller 
                        if (faderValues.ContainsKey(channelNumber))
                        {
                            SetFader(channelNumber, faderValues[channelNumber]);
                        }
                    }
                }
                else if(firstMessage[0] == 0xb0 && (firstMessage[1] & 0xf8) == 0x00) //move fader, second byte is between x00 and x07
                {
                    var channelNumber = firstMessage[1];
                    var secondMessage = _messageQueue.Dequeue();
                    if (secondMessage.Length > 2 && secondMessage[0] == 0xb0 && (secondMessage[1] & 0xf8) == 0x20)
                    {
                        //int data2 = (int)Math.Round(faderValue * 1023); //10 bit
                        int upper = firstMessage[2] << 3; //upper 7 bit
                        int lower = secondMessage[2] >> 4; // lower 3 bit

                        var faderValue = (upper + lower) / 1023.0;
                        faderValues.AddOrSet(channelNumber, faderValue);
                        OnFaderEvent(channelNumber, faderValue);
                    }
                }   
                else if (firstMessage.MIDIEqual(0xb0, 0x0f, 0x0a)) //preset up, preset down
                {
                    var secondMessage = _messageQueue.Dequeue();
                    if (secondMessage.MIDIEqual(0xb0, 0x2f, 0x43)) //preset up
                    {
                        OnPresetUp();
                    }
                    else if (secondMessage.MIDIEqual(0xb0, 0x2f, 0x41)) //preset down
                    {
                        OnPresetDown();
                    }

                }
            }
        }

        public  bool ConnectOutputDevice(string deviceName)
        {
            var access = MidiAccessManager.Default;
            var deviceNumber = access.Outputs.Where(i => i.Name == deviceName).FirstOrDefault();
            if (deviceNumber != null)
            {
                _output = access.OpenOutputAsync(deviceNumber.Id).Result;
                return true;
            }
            return false;
        }



        public  string[] GetInputDeviceNames()
        {
            var access = MidiAccessManager.Default;
            return access.Inputs.Select(port => port.Name).ToArray();
        }

        public  string[] GetOutputDeviceNames()
        {
            var access = MidiAccessManager.Default;
            return access.Outputs.Select(port => port.Name).ToArray();
        }

        public  bool SetFader(int channelNumber, double faderValue)
        {
            if (_output != null && channelNumber<8)
            {
                //'touch fader': b0 0f 0z 
                //               b0 2f 40
                //'release fader': b0 0f 0z 
                //                 b0 2f 00 
                //'move fader': b0 0z hi 
                //              b0 2z lo
                //where z is the channel number 1-8
                //hi is between 0x00-0x7f
                //lo is [0x00, 0x10, 0x20, 0x30, 0x40, 0x50, 0x60, 0x70]

                byte data0 = 0xb0;
                byte z = Convert.ToByte(channelNumber);
                int data2 = (int)Math.Round(faderValue * 1023); //10 bit
                byte upper = (byte)(data2 >> 3); //upper 7 bit
                byte lower = (byte)((data2 << 4) & 0x70); // lower 3 bit

                //touch fader on channel
                //_output.Send(new byte[] {data0, 0x0f, z }, 0, 3, 0);
                //_output.Send(new byte[] { data0, 0x2f, 0x40 }, 0, 3, 0);
                //move fader 
                _output.Send(new byte[] { data0, (byte)(0x00 + z), upper }, 0, 3, 0);
                _output.Send(new byte[] { data0, (byte)(0x20 + z), lower }, 0, 3, 0);
                faderValues.AddOrSet(z, faderValue);
                //release fader
                //_output.Send(new byte[] { data0, 0x0f, z }, 0, 3, 0);
                //_output.Send(new byte[] { data0, 0x2f, 0x00 }, 0, 3, 0);

                return true;
            }
            return false;
        }

        public bool SetGainLed(int channelNumber, double gainValue)
        {
            return false;
        }

        public void SetSelectLed(int channelNumber, bool turnOn)
        {
            //throw new NotImplementedException();
        }

        public void WriteTextToChannelLCD(int channelNumber, string text)
        {
            throw new NotImplementedException();
        }
    }
}
