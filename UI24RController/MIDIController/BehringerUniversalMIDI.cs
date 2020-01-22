using System;
using System.Collections.Generic;
using System.Text;
using Commons.Music.Midi;
using System.Linq;
using System.Threading.Tasks;

namespace UI24RController.MIDIController
{
    public class BehringerUniversalMIDI : IMIDIController,IDisposable
    {
        IMidiInput _input = null;
        IMidiOutput _output = null;

        public Dictionary<string, byte> ButtonsID { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public event EventHandler<MessageEventArgs> _messageReceived;
        public event EventHandler<FaderEventArgs> FaderEvent;
        public event EventHandler<EventArgs> PresetUp;
        public event EventHandler<EventArgs> PresetDown;
        public event EventHandler<GainEventArgs> GainEvent;
        public event EventHandler<ChannelEventArgs> SelectChannelEvent;
        public event EventHandler<EventArgs> SaveEvent;
        public event EventHandler<EventArgs> UndoEvent;
        public event EventHandler<EventArgs> CancelEvent;
        public event EventHandler<EventArgs> EnterEvent;
        public event EventHandler<EventArgs> UpEvent;
        public event EventHandler<EventArgs> DownEvent;
        public event EventHandler<EventArgs> LeftEvent;
        public event EventHandler<EventArgs> RightEvent;
        public event EventHandler<EventArgs> CenterEvent;
        public event EventHandler<ChannelEventArgs> MuteChannelEvent;
        public event EventHandler<ChannelEventArgs> SoloChannelEvent;
        public event EventHandler<ChannelEventArgs> RecChannelEvent;
        public event EventHandler<EventArgs> StopEvent;
        public event EventHandler<EventArgs> PlayEvent;
        public event EventHandler<EventArgs> RecEvent;

        event EventHandler<MessageEventArgs> IMIDIController.MessageReceived
        {
            add
            {
                _messageReceived += value;
            }

            remove
            {
                _messageReceived -= value;
            }
        }

        protected void OnMessageReceived(string message)
        {
            if (_messageReceived != null)
            {
                var messageEventArgs = new MessageEventArgs();
                messageEventArgs.Message = message;
                _messageReceived(this, messageEventArgs);
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

        public bool ConnectInputDevice(string deviceName)
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
                        OnMessageReceived( $"{e.Data[0].ToString("x2")} - {e.Data[1].ToString("x2")} - {e.Data[2].ToString("x2")}");
                        ProcessMidiMessage(e);
                    }
                };
                return true;
            }
            else
                return false;            
        }

        private void ProcessMidiMessage(MidiReceivedEventArgs e)
        {
            if (e.Data.Length > 2)
            {
                //My bcf2000 controller first fader in the preset P-1  is 0x51 the last is 0x58 (the second byte in the midi message)
                //the 3rd byte is the value of the fader
                if (e.Data[1]>= 0x51 && e.Data[1] <= 0x58)
                {
                    var channelNumber = e.Data[1] - 0x51;
                    var faderValue = e.Data[2] / 127.0;
                    OnFaderEvent(channelNumber, faderValue);
                }
            }
        }

        public bool ConnectOutputDevice(string deviceName)
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



        public string[] GetInputDeviceNames()
        {
            var access = MidiAccessManager.Default;
            return access.Inputs.Select(port => port.Name).ToArray();
        }

        public string[] GetOutputDeviceNames()
        {
            var access = MidiAccessManager.Default;
            return access.Outputs.Select(port => port.Name).ToArray();
        }

        public bool SetFader(int channelNumber, double faderValue)
        {
            if (_output != null)
            {
                byte data0 = 0xb0;
                byte data1 = Convert.ToByte(channelNumber + 0x51);
                byte data2 = (byte)Math.Round(faderValue * 127);
                _output.Send(new byte[] {data0, data1, data2 }, 0, 3, 0);
                return true;
            }
            return false;
        }

        public bool SetGainLed(int channelNumber, double gainValue)
        {
            //throw new NotImplementedException();
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

        public void WriteTextToLCD(string text)
        {
            throw new NotImplementedException();
        }

        public void SetMuteLed(int channelNumber, bool turnOn)
        {
            throw new NotImplementedException();
        }

        public void SetSoloLed(int channelNumber, bool turnOn)
        {
            throw new NotImplementedException();
        }

        public void SetRecLed(int channelNumber, bool turnOn)
        {
            throw new NotImplementedException();
        }

        public void SetLed(string buttonName, bool turnOn)
        {
            throw new NotImplementedException();
        }
    }
}
