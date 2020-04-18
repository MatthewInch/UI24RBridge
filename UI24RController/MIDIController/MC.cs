using System;
using System.Collections.Generic;
using System.Text;
using Commons.Music.Midi;
using System.Linq;
using System.Threading.Tasks;
using Commons.Music.Midi.RtMidi;
using System.Threading;
using System.Collections.Concurrent;

namespace UI24RController.MIDIController
{
    public class MC : IMIDIController, IDisposable
    {
        protected class FaderState
        {
            public double Value { get; set; }
            public bool IsTouched { get; set; }

            public FaderState()
            {
                Value = 0;
                IsTouched = false;
            }

            public FaderState(double value)
            {
                Value = value;
                IsTouched = false;
            }
        }
        /// <summary>
        /// Store every fader setted value of the faders, key is the channel number (z in the message)
        /// </summary>
        protected ConcurrentDictionary<byte, FaderState> faderValues = new ConcurrentDictionary<byte, FaderState>();
            
        IMidiInput _input = null;
        protected string _inputDeviceNumber;
        protected string _inputDeviceName;
        IMidiOutput _output = null;
        protected int _outputDeviceNumber;
        protected string _outputDeviceName;
        protected Guid _lcdTextSyncGuid = Guid.NewGuid();
        protected bool _isConnected = false;
        protected bool _isConnectionErrorOccured = false;
        protected Thread _pingThread;
        protected ConcurrentDictionary<int, DateTime> _clipLeds = new ConcurrentDictionary<int, DateTime>();

        public Dictionary<string, byte> ButtonsID { get ; set; }
        public bool IsConnectionErrorOccured { get => _isConnectionErrorOccured; }

        public event EventHandler<MessageEventArgs> MessageReceived;
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
        public event EventHandler<EventArgs> ConnectionErrorEvent;
        public event EventHandler<FunctionEventArgs> FunctionButtonEvent;
        public event EventHandler<EventArgs> PrevEvent;
        public event EventHandler<EventArgs> NextEvent;

        public MC()
        {
            ButtonsID = new Dictionary<string, byte>();
            //It will be configurable 
            ButtonsID.Add("PlayPrev", 0x5b);
            ButtonsID.Add("PlayNext", 0x5c);
            ButtonsID.Add("Play", 0x5e);
            ButtonsID.Add("Rec", 0x5f);
            ButtonsID.Add("Stop", 0x5d);
            ButtonsID.Add("F1", 0x36);
            ButtonsID.Add("F2", 0x37);
            ButtonsID.Add("F3", 0x38);
            ButtonsID.Add("F4", 0x39);
            ButtonsID.Add("F5", 0x3a);
            ButtonsID.Add("F6", 0x3b);
            ButtonsID.Add("F7", 0x3c);
            ButtonsID.Add("F8", 0x3d);
            for (byte i=0; i<9; i++)
            {
                faderValues.TryAdd(i, new FaderState());
                _clipLeds.TryAdd(i, DateTime.MinValue);
            }
        }
        protected void OnMessageReceived(string message)
        {
            MessageReceived?.Invoke(this, new MessageEventArgs(message));
        }

        protected void OnFaderEvent(int channelNumber, double faderValue)
        {
            FaderEvent?.Invoke(this, new FaderEventArgs(channelNumber, faderValue));
        }

        protected void OnGainEvent(int channelNumber, int gainDirection)
        {
            GainEvent?.Invoke(this, new GainEventArgs(channelNumber, gainDirection));
        }

        protected void OnSelectEvent(int channelNumber)
        {
            SelectChannelEvent?.Invoke(this, new ChannelEventArgs(channelNumber));
        }
        protected void OnMuteEvent(int channelNumber)
        {
            MuteChannelEvent?.Invoke(this, new ChannelEventArgs(channelNumber));
        }

        protected void OnSoloEvent(int channelNumber)
        {
            SoloChannelEvent?.Invoke(this, new ChannelEventArgs(channelNumber));
        }
        protected void OnRecEvent(int channelNumber)
        {
            RecChannelEvent?.Invoke(this, new ChannelEventArgs(channelNumber));
        }

        protected void OnSaveEvent()
        {
            SaveEvent?.Invoke(this, new EventArgs());
        }

        protected void OnRecEvent()
        {
            RecEvent?.Invoke(this, new EventArgs());
        }

        protected void OnStopEvent()
        {
            StopEvent?.Invoke(this, new EventArgs());
        }

        protected void OnPlayEvent()
        {
            PlayEvent?.Invoke(this, new EventArgs());
        }
        protected void OnNextEvent()
        {
            NextEvent?.Invoke(this, new EventArgs());
        }
        protected void OnPrevEvent()
        {
            PrevEvent?.Invoke(this, new EventArgs());
        }

        protected void OnFunctionButtonEvent(int functionNumber,bool isPress)
        {
            FunctionButtonEvent?.Invoke(this, new FunctionEventArgs(functionNumber, isPress));
        }

        protected void OnPresetUp()
        {
            PresetUp?.Invoke(this, new EventArgs());
        }
        protected void OnPresetDown()
        {
            PresetDown?.Invoke(this, new EventArgs());
        }

        public void Dispose()
        {
            _isConnected = false;
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
            if (_pingThread != null)
            {
                //stop the pinging thread
                _isConnectionErrorOccured = true;
            }
        }

        public  bool ConnectInputDevice(string deviceName)
        {
            try
            {
                _inputDeviceName = deviceName;
                var access = MidiAccessManager.Default;
                var deviceNumber = access.Inputs.Where(i => i.Name.ToUpper() == deviceName.ToUpper()).FirstOrDefault();
                if (deviceNumber != null)
                {
                    _input = access.OpenInputAsync(deviceNumber.Id).Result;
                    _inputDeviceNumber = deviceNumber.Id;
                    _input.MessageReceived += (obj, e) =>
                    {
                        if (e.Data.Length > 2)
                        {
                            OnMessageReceived($"{e.Data[0].ToString("x2")} - {e.Data[1].ToString("x2")} - {e.Data[2].ToString("x2")}");
                            ProcessMidiMessage(e);
                        }
                    };
                    return true;
                }
                else
                {
                    _isConnectionErrorOccured = true;
                    return false;
                }
            }
            catch (Exception ex)
            {
                OnMessageReceived($"Input device connection error. ({ex.Message})");
            }
            return false;
        }



        public bool ConnectOutputDevice(string deviceName)
        {
            try
            {
                _outputDeviceName = deviceName;
                var access = MidiAccessManager.Default;
                var deviceNumber = access.Outputs.Where(i => i.Name.ToUpper() == deviceName.ToUpper()).FirstOrDefault();
                if (deviceNumber != null)
                {
                    _output = access.OpenOutputAsync(deviceNumber.Id).Result;
                    _outputDeviceNumber = Convert.ToInt32(deviceNumber.Id);
                    _isConnected = true;
                    _isConnectionErrorOccured = false;
                    StartPingThread();
                    return true;
                }
                else
                {
                    _isConnectionErrorOccured = true;
                }
            }
            catch (Exception ex)
            {
                OnMessageReceived($"Output device connection error. ({ex.Message})");
                if (ConnectionErrorEvent != null && !_isConnectionErrorOccured)
                {
                    _isConnectionErrorOccured = true;
                    ConnectionErrorEvent(this, new EventArgs());
                }
            }
            return false;
        }

        private void StartPingThread()
        {
            if (_pingThread == null || !_pingThread.IsAlive)
            {
                _pingThread = new Thread(() =>
                {
                    while (!_isConnectionErrorOccured)
                    {
                        Thread.Sleep(5000);
                        Send(new byte[] { 0xb0, 0x00, 0x00 }, 0, 3, 0);
                    }
                });
            }
            if (!_pingThread.IsAlive)
                _pingThread.Start();

        }

        public bool ReConnectDevice()
        {
           return ConnectInputDevice(_inputDeviceName) &&
            ConnectOutputDevice(_outputDeviceName);
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

        protected void Send(byte[] mevent, int offset, int length, long timestamp)
        {
            try
            {
                if (_isConnected && _output != null)
                {
                    _output.Send(mevent, offset, length, timestamp);

                }
            }
            catch 
            { 
                if (ConnectionErrorEvent != null && !_isConnectionErrorOccured)
                {
                    _isConnectionErrorOccured = true;
                    ConnectionErrorEvent(this, new EventArgs());
                }
            }
        }

        private void ProcessMidiMessage(MidiReceivedEventArgs e)
        {
            var message = e.Data;

            if (message[0] == 0x90) //button pressed, released, fader released 
            {
                if (message.MIDIEqual(0x90, 0x00, 0x00, 0xff, 0x00, 0xff) && (message[1] >= 0x68) && (message[1] <= 0x70)) //release fader (0x90 [0x68-0x70] 0x00)
                {
                    byte channelNumber = (byte)(message[1] - 0x68);
                    if (faderValues.ContainsKey(channelNumber))
                    {
                        faderValues[channelNumber].IsTouched = false;
                        SetFader(channelNumber, faderValues[channelNumber].Value);
                    }
                }
                if (message.MIDIEqual(0x90, 0x00, 0x7f, 0xff, 0x00, 0xff) && (message[1] >= 0x68) && (message[1] <= 0x70)) //touch fader (0x90 [0x68-0x70] 0x00)
                {
                    byte channelNumber = (byte)(message[1] - 0x68);
                    if (faderValues.ContainsKey(channelNumber))
                    {
                        faderValues[channelNumber].IsTouched = true;
                    }
                }
                else if (message.MIDIEqual(0x90, 0x2f, 0x7f)) //fader bank right press
                {
                    OnPresetUp();
                }
                else if (message.MIDIEqual(0x90, 0x2e, 0x7f)) //fader bank left press
                {
                    OnPresetDown();
                }
                else if (message[1] >= 0x10 && message[1] <= 0x17 && message[2] == 0x7f) //channel mute button
                {
                    byte channelNumber = (byte)(message[1] - 0x10);
                    OnMuteEvent(channelNumber);
                }
                else if (message[1] >= 0x08 && message[1] <= 0x0f && message[2] == 0x7f) //channel solo button
                {
                    byte channelNumber = (byte)(message[1] - 0x08);
                    OnSoloEvent(channelNumber);
                }
                else if (message[1] >= 0x00 && message[1] <= 0x07 && message[2] == 0x7f) //channel rec button
                {
                    byte channelNumber = (byte)(message[1]);
                    OnRecEvent(channelNumber);
                }
                else if  (message[1] >= 0x18 && message[1] <= 0x1f && message[2] == 0x7f) //channel select button
                {
                    byte channelNumber = (byte)(message[1] - 0x18);
                    OnSelectEvent(channelNumber);
                }
                else if (message.MIDIEqual(0x90, 0x32, 0x7f)) //main select button
                {
                    byte channelNumber = (byte)(message[1] - 0x18);
                    OnSelectEvent(8);
                }
                else if (message.MIDIEqual(0x90, 0x50, 0x7f)) //Save button
                {
                    OnSaveEvent();
                }
                else if (message.MIDIEqual(0x90, ButtonsID["Rec"], 0x7f)) //Rec button
                {
                    OnRecEvent();
                }
                else if (message.MIDIEqual(0x90, ButtonsID["Stop"], 0x7f)) //Stop button
                {
                    OnStopEvent();
                }
                else if (message.MIDIEqual(0x90, ButtonsID["Play"], 0x7f)) //Stop button
                {
                    OnPlayEvent();
                }
                else if (message.MIDIEqual(0x90, ButtonsID["PlayPrev"], 0x7f)) //Stop button
                {
                    OnPrevEvent();
                }
                else if (message.MIDIEqual(0x90, ButtonsID["PlayNext"], 0x7f)) //Stop button
                {
                    OnNextEvent();
                }
                else if (message[0]== 0x90 && message[1]>=ButtonsID["F1"] && message[1] <= ButtonsID["F8"]) //F1-F8 press
                {
                    OnFunctionButtonEvent(message[1] - ButtonsID["F1"], message[2] == 0x7f);
                }

            }
            else if (message[0] >= 0xe0 && (message[0] <= 0xe8)) //move fader
            {
                byte channelNumber = (byte)(message[0] - 0xe0);
                //int data2 = (int)Math.Round(faderValue * 1023); //10 bit
                int upper = message[2] << 7; //upper 7 bit
                int lower = message[1]; // lower 7 bit

                var faderValue = (upper + lower) / 16383.0;
                
                faderValues[channelNumber].Value = faderValue;
                OnFaderEvent(channelNumber, faderValue);
                if (!faderValues[channelNumber].IsTouched)
                {
                    new Thread(() =>
                    {
                        Thread.Sleep(100);
                        //if value change the other thread write back the last fader value
                        if (faderValue == faderValues[channelNumber].Value)
                        {
                            SetFader(channelNumber, faderValue);
                        }
                    }).Start();
                }

            } 
            else if(message[0] == 0xb0 && (message[1] >= 0x10 && message[1] <= 0x17)) //channel knobb turning
            {
                byte channelNumber = (byte)(message[1] - 0x10);
                if (message[2] >= 0x01 && message[2] <= 0x03)
                    OnGainEvent(channelNumber, message[2]);
                if (message[2] >= 0x41 && message[2] <= 0x43)
                    OnGainEvent(channelNumber, -1*(message[2]&0x03));
            }
        }

        public  bool SetFader(int channelNumber, double faderValue)
        {
            if (_output != null && channelNumber < 9)
            {

                byte z = Convert.ToByte(channelNumber);
                int data2 = (int)Math.Round(faderValue * 16383); //14 bit
                byte upper = (byte)(data2 >> 7); //upper 7 bit
                byte lower = (byte)((data2) & 0x7f); // lower 7 bit

                Send(new byte[] { (byte)(0xe0 + channelNumber), lower, upper }, 0, 3, 0);


                faderValues[z].Value = faderValue;

                return true;
            }
            return false;
        }

        public bool SetGainLed(int channelNumber, double gainValue)
        {
            if (_output != null && channelNumber < 9)
            {
                var segment = Convert.ToByte( Math.Round(gainValue * 12));
                if (segment == 0)
                    segment = 1;
                if (segment == 0x0c)
                    segment = 0x0b;
                Send(new byte[] {0xb0, (byte)(0x30 + channelNumber), segment }, 0, 3, 0);
                return true;
            }
            return false;
        }

        public void SetSelectLed(int channelNumber, bool turnOn)
        {
            //turn off all select led and on in the current channel (channel 8 is 0x32 the main channel)
            for (byte i = 0; i < 9; i++)
            {
                Send(new byte[] { 0x90, (byte)(i==8? 0x32 : 0x18 + i), (byte)((i==channelNumber && turnOn)? 0x7f : 0x00) }, 0, 3, 0);
            }
        }

        public void SetMuteLed(int channelNumber, bool turnOn)
        {
            if (channelNumber < 8)
            {
                Send(new byte[] { 0x90, (byte)(0x10 + channelNumber), (byte)(turnOn ? 0x7f : 0x00) }, 0, 3, 0);
            }
        }
        public void SetSoloLed(int channelNumber, bool turnOn)
        {
            if (channelNumber < 8)
            {
                Send(new byte[] { 0x90, (byte)(0x08 + channelNumber), (byte)(turnOn ? 0x7f : 0x00) }, 0, 3, 0);
            }
        }

        public void SetRecLed(int channelNumber, bool turnOn)
        {
            if (channelNumber < 8)
            {
                Send(new byte[] { 0x90, (byte)(0x00 + channelNumber), (byte)(turnOn ? 0x7f : 0x00) }, 0, 3, 0);
            }
        }

        public void WriteTextToChannelLCD(int channelNumber, string text)
        {
            if (channelNumber < 8)
            {
                var position = channelNumber * 7;
                var message = ASCIIEncoding.ASCII.GetBytes((text + "       ").Substring(0, 7));
                byte[] sysex = (new byte[] { 0xf0, 0, 0, 0x66, 0x14, 0x12, (byte)position }).Concat(message).Concat(new byte[] { 0xf7 }).ToArray();
                Send(sysex, 0, sysex.Length, 0);
            }
        }

        public void WriteTextToLCD( string text)
        {
                var message = ASCIIEncoding.ASCII.GetBytes((text + "                                                        ").Substring(0, 50));
                byte[] sysex = (new byte[] { 0xf0, 0, 0, 0x66, 0x14, 0x12, 0x38 }).Concat(message).Concat(new byte[] { 0xf7 }).ToArray();
                Send(sysex, 0, sysex.Length, 0);
        }

        public void SetLed(string buttonName, bool turnOn)
        {
            Send(new byte[] { 0x90, ButtonsID[buttonName], (byte)(turnOn ? 0x7f : 0x00) }, 0, 3, 0);
        }

        public void WriteTextToLCD(string text, int delay)
        {
            WriteTextToLCD(text);
            var guid = Guid.NewGuid();
            _lcdTextSyncGuid = guid;
            new Thread(() =>
            {
                Thread.Sleep(delay*1000);
                //if value change the other thread write back the last fader value
                if (guid == _lcdTextSyncGuid)
                {
                    WriteTextToLCD("");
                }
            }).Start();

        }

        public void WriteChannelMeter(int channelNumber, byte value)
        {
            if (channelNumber < 8)
            {
                byte calculatedValue = 0;
                if (value >= 240) //clip
                {
                    calculatedValue = 14;
                    if (_clipLeds[channelNumber] == DateTime.MinValue)
                    {
                        _clipLeds[channelNumber] = DateTime.Now;
                    }
                }
                else
                {
                    calculatedValue = Convert.ToByte(value / 18);
                    if (_clipLeds[channelNumber] != DateTime.MinValue)
                    {
                        var ticksNumber = DateTime.Now - _clipLeds[channelNumber];
                        if (ticksNumber.TotalMilliseconds > 1000)
                        {
                            TurnOffClipLed(channelNumber);
                            _clipLeds[channelNumber] = DateTime.MinValue;
                        }
                    }
                }
                Send(new byte[] { 0xd0, (byte)(channelNumber * 16 + calculatedValue) }, 0, 2, 0);
            }
        }

        public void TurnOffClipLed(int channelNumber)
        {
            Send(new byte[] { 0xd0, (byte)(channelNumber * 16 + 0x0f) }, 0, 2, 0);
        }
    }
}
