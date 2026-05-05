using System;
using System.Collections.Generic;
using System.Text;
using Commons.Music.Midi;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using System.Text.Json.Serialization;
using System.Text.Json;
using UI24RController.Settings.Helper;
using System.IO;

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
        /// Keep track of last MIDI status byte
        /// </summary>
        protected byte? _lastMidiStatus;

        /// <summary>
        /// Store every fader setted value of the faders, key is the channel number (z in the message)
        /// </summary>
        protected ConcurrentDictionary<byte, FaderState> faderValues = new ConcurrentDictionary<byte, FaderState>();

        IMidiInput _input = null;
        protected string _inputDeviceNumber;
        protected string _inputDeviceName;
        IMidiOutput _output = null;
        protected string _outputDeviceNumber;
        protected string _outputDeviceName;
        protected Guid _lcdTextSyncGuid = Guid.NewGuid();
        private const int ChannelStripCount = 8;
        private readonly string[][] _lineDefault = new[] { new string[ChannelStripCount], new string[ChannelStripCount] };
        private readonly Timer[][] _lineTempTimer = new[] { new Timer[ChannelStripCount], new Timer[ChannelStripCount] };
        private readonly ChannelStripColour[] _stripColours = Enumerable.Repeat(ChannelStripColour.White, ChannelStripCount).ToArray();
        protected bool _isConnected = false;
        protected bool _isConnectionErrorOccured = false;
        protected Thread _pingThread;
        protected ConcurrentDictionary<int, DateTime> _clipLeds = new ConcurrentDictionary<int, DateTime>();
        protected byte _lcdDisplayNumber = 0x14; //with X-touch extender this is 15

        protected ButtonsID _buttonsID = new ButtonsID();
        public bool IsConnectionErrorOccured { get => _isConnectionErrorOccured; }
        public bool IsConnected { get => _isConnected; }
        public bool IsExtender { get; set; }
        public int ChannelOffset { get; set; }


        public string ButtonsFileName {
            set {
                var buttonSettingsFromFile = GetButtonsValues(value);
                //update initial values if in the settings file it overwrited
                foreach (KeyValuePair<ButtonsEnum, byte> button in buttonSettingsFromFile)
                {
                    _buttonsID.ButtonsDictionary[button.Key] = button.Value;
                }

            }
        }

        public event EventHandler<MessageEventArgs> MessageReceived;
        public event EventHandler<EventArgs> ConnectionErrorEvent;

        #region Button Events
        public event EventHandler<FaderEventArgs> FaderEvent;
        public event EventHandler<KnobEventArgs> KnobEvent;
        public event EventHandler<WheelEventArgs> WheelEvent;
        public event EventHandler<ChannelEventArgs> SelectChannelEvent;
        public event EventHandler<ChannelEventArgs> MuteChannelEvent;
        public event EventHandler<ChannelEventArgs> SoloChannelEvent;
        public event EventHandler<ChannelEventArgs> RecChannelEvent;

        public event EventHandler<EventArgs> GainModeEvent;
        public event EventHandler<EventArgs> PanEvent;

        public event EventHandler<EventArgs> TapTempoEvent;
        public event EventHandler<EventArgs> SaveUserLayerEvent;

        public event EventHandler<FunctionEventArgs> SetUserChannelEvent;

        public event EventHandler<FunctionEventArgs> AuxButtonEvent;
        public event EventHandler<FunctionEventArgs> FxButtonEvent;
        public event EventHandler<FunctionEventArgs> MuteGroupButtonEvent;
        public event EventHandler<FunctionEventArgs> ViewGroupButtonEvent;

        public event EventHandler<EventArgs> MuteAllEvent;
        public event EventHandler<EventArgs> MuteFXEvent;
        public event EventHandler<EventArgs> ClearMuteEvent;
        public event EventHandler<EventArgs> ClearSoloEvent;

        public event EventHandler<EventArgs> PrevEvent;
        public event EventHandler<EventArgs> NextEvent;
        public event EventHandler<EventArgs> StopEvent;
        public event EventHandler<EventArgs> PlayEvent;
        public event EventHandler<EventArgs> RecEvent;

        public event EventHandler<EventArgs> LayerUp;
        public event EventHandler<EventArgs> LayerDown;
        public event EventHandler<EventArgs> BankUp;
        public event EventHandler<EventArgs> BankDown;

        public event EventHandler<ButtonEventArgs> TalkbackEvent;

        #endregion

        public MC()
        {
            for (byte i=0; i<9; i++)
            {
                faderValues.TryAdd(i, new FaderState());
                _clipLeds.TryAdd(i, DateTime.MinValue);
            }
            IsExtender = false;
            ChannelOffset = 0;
            //initialize Controller - all leds off
            //TODO: Search for right SysEx command, this one not working
            //byte[] sysex = new byte[] { 0xf0, 0, 0, 0x66, 0x14, 0x61, 0xf7 };
            //Send(sysex, 0, sysex.Length, 0);
        }

        protected IEnumerable<byte[]> NormalizeAndSplitMidiMessages(byte[] data, int start, int length)
        {
            int i = start;
            int end = start + length;

            while (i < end)
            {
                byte first = data[i];

                // If the first byte is a status byte, track it, depending on type
                // See: https://studiocode.dev/kb/MIDI/midi/
                //   Channel messages can have running status. That is, if the next
                //   channel status byte is the same as the last, it may be omitted.
                //   The receiver assumes that the accompanying data is of the same
                //   status as was last sent. Receipt of any other status byte except
                //   real-time terminates running status.

                if ((first & 0x80) != 0)
                {
                    // Status byte present: full 3-byte message
                    _lastMidiStatus = first;
                    if (i + 3 > end) yield break;
                    yield return new byte[] { data[i], data[i + 1], data[i + 2] };
                    i += 3;
                }
                else
                {
                    // Running status: 2 data bytes, prepend last known status
                    if (_lastMidiStatus == null) { i++; continue; } // no known status, skip
                    if (i + 2 > end) yield break;
                    yield return new byte[] { _lastMidiStatus.Value, data[i], data[i + 1] };
                    i += 2;
                }
            }
        }

        protected void OnMessageReceived(string message)
        {
            MessageReceived?.Invoke(this, new MessageEventArgs(message));
        }

        protected void OnKnobEvent(int channelNumber, int knobDirection)
        {
            KnobEvent?.Invoke(this, new KnobEventArgs(channelNumber, knobDirection));
        }
        protected void OnWheelEvent(int wheelDirection)
        {
            WheelEvent?.Invoke(this, new WheelEventArgs(wheelDirection));
        }
        protected void OnFaderEvent(int channelNumber, double faderValue)
        {
            FaderEvent?.Invoke(this, new FaderEventArgs(channelNumber, faderValue));
        }

        protected void OnChannelRecEvent(int channelNumber)
        {
            RecChannelEvent?.Invoke(this, new ChannelEventArgs(channelNumber));
        }
        protected void OnChannelSoloEvent(int channelNumber)
        {
            SoloChannelEvent?.Invoke(this, new ChannelEventArgs(channelNumber));
        }
        protected void OnChannelMuteEvent(int channelNumber)
        {
            MuteChannelEvent?.Invoke(this, new ChannelEventArgs(channelNumber));
        }
        protected void OnChannelSelectEvent(int channelNumber)
        {
            SelectChannelEvent?.Invoke(this, new ChannelEventArgs(channelNumber));
        }

        protected void OnGainModeEvent()
        {
            GainModeEvent?.Invoke(this, new EventArgs());
        }
        protected void OnPanEvent()
        {
            PanEvent?.Invoke(this, new EventArgs());
        }
        protected void OnTapTempoEvent()
        {
            TapTempoEvent?.Invoke(this, new EventArgs());
        }


        protected void OnSaveUserLayerEvent()
        {
            SaveUserLayerEvent?.Invoke(this, new EventArgs());
        }

        protected void OnSetUserChannelEvent(int functionNumber, bool isPress)
        {
            SetUserChannelEvent?.Invoke(this, new FunctionEventArgs(functionNumber, isPress));
        }

        protected void OnAuxButtonEvent(int functionNumber, bool isPress)
        {
            AuxButtonEvent?.Invoke(this, new FunctionEventArgs(functionNumber, isPress));
        }
        protected void OnFxButtonEvent(int functionNumber, bool isPress)
        {
            FxButtonEvent?.Invoke(this, new FunctionEventArgs(functionNumber, isPress));
        }
        protected void OnMuteGroupButtonEvent(int functionNumber, bool isPress)
        {
            MuteGroupButtonEvent?.Invoke(this, new FunctionEventArgs(functionNumber, isPress));
        }
        protected void OnViewGroupButtonEvent(int functionNumber, bool isPress)
        {
            ViewGroupButtonEvent?.Invoke(this, new FunctionEventArgs(functionNumber, isPress));
        }

        protected void OnMuteAllEvent()
        {
            MuteAllEvent?.Invoke(this, new EventArgs());
        }
        protected void OnClearMuteEvent()
        {
            ClearMuteEvent?.Invoke(this, new EventArgs());
        }
        protected void OnMuteFXEvent()
        {
            MuteFXEvent?.Invoke(this, new EventArgs());
        }
        protected void OnClearSoloEvent()
        {
            ClearSoloEvent?.Invoke(this, new EventArgs());
        }

        protected void OnNextEvent()
        {
            NextEvent?.Invoke(this, new EventArgs());
        }
        protected void OnPrevEvent()
        {
            PrevEvent?.Invoke(this, new EventArgs());
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

        protected void OnLayerUp()
        {
            LayerUp?.Invoke(this, new EventArgs());
        }
        protected void OnLayerDown()
        {
            LayerDown?.Invoke(this, new EventArgs());
        }
        protected void OnBankUp()
        {
            BankUp?.Invoke(this, new EventArgs());
        }
        protected void OnBankDown()
        {
            BankDown?.Invoke(this, new EventArgs());
        }

        protected void OnWheelEvent(int channelNumber, int wheelDirection)
        {
            WheelEvent?.Invoke(this, new WheelEventArgs(wheelDirection));
        }
        protected void OnTalkbackEvent(bool isPressed=true)
        {
            TalkbackEvent?.Invoke(this, new ButtonEventArgs(isPressed));
        }


        protected void OnConnectionErrorEvent()
        {
            _isConnectionErrorOccured = true;
            _isConnected = false;
            ConnectionErrorEvent?.Invoke(this, new EventArgs());
        }

        private void DisposePorts()
        {
            // On linux, input and output ports might be the same device. The input
            // port owns the resource and should be the one to dispose it

            bool samePort = _input != null && _output != null
                    && _input.Details?.Id == _output.Details?.Id;

            if (_output != null && !samePort)
            {
                _output.Dispose();
            }

            _output = null;


            if (_input != null)
            {
                _input.Dispose();
                _input = null;
            }
        }


        public void Dispose()
        {
            _isConnected = false;

            if (_pingThread != null)
            {
                _isConnectionErrorOccured = true;
            }

            DisposePorts();
        }

        public async Task<bool> ConnectInputDevice(string deviceName)
        {
            _lastMidiStatus = null;
            try
            {
                _inputDeviceName = deviceName;
                var access = MidiAccessManager.Default;
                var deviceNumber = access.Inputs.Where(i => i.Name.ToUpper() == deviceName.ToUpper()).FirstOrDefault();
                if (deviceNumber != null)
                {
                    var input = await access.OpenInputAsync(deviceNumber.Id);

                    _input = input;
                    _inputDeviceNumber = deviceNumber.Id;
                    _input.MessageReceived += (obj, e) =>
                    {
                        if (e.Data.Length > 0)
                        {
                            ProcessMidiMessage(e);
                        }
                    };
                    return true;
                }
                else
                {
                    //_isConnectionErrorOccured = true;
                    return false;
                }
            }
            catch (Exception ex)
            {
                OnMessageReceived($"Input device connection error. ({ex.Message})");
            }
            return false;
        }
        public async Task<bool> ConnectOutputDevice(string deviceName)
        {
            try
            {
                _outputDeviceName = deviceName;
                var access = MidiAccessManager.Default;
                var deviceNumber = access.Outputs.Where(i => i.Name.ToUpper() == deviceName.ToUpper()).FirstOrDefault();
                if (deviceNumber != null)
                {
                    var output = await access.OpenOutputAsync(deviceNumber.Id);
                    _outputDeviceNumber = deviceNumber.Id;
                    _output = output;
                    _isConnected = true;
                    _isConnectionErrorOccured = false;
                    StartPingThread();
                    return true;
                }
                else
                {
                    //_isConnectionErrorOccured = true;
                }
            }
            catch (Exception ex)
            {
                OnMessageReceived($"Output device connection error. ({ex.Message})");
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
                        Thread.Sleep(1000);

                        bool deviceStillPresent = MidiAccessManager.Default.Inputs
                            .Any(i => i.Name.ToUpper() == _inputDeviceName.ToUpper());

                        if (!deviceStillPresent)
                        {
                            OnConnectionErrorEvent();
                        }
                        else
                        {
                            Send(new byte[] { 0xb0, 0x00, 0x00 }, 0, 3, 0);
                        }
                    }
                });

                _pingThread.IsBackground = true;
            }
            if (!_pingThread.IsAlive)
                _pingThread.Start();

        }
        public async Task<bool> ReConnectDevice()
        {
            DisposePorts();
            var inputOk  = await ConnectInputDevice(_inputDeviceName);
            var outputOk = await ConnectOutputDevice(_outputDeviceName);
            return inputOk && outputOk;
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
                OnConnectionErrorEvent();
            }
        }
        private void ProcessMidiMessage(MidiReceivedEventArgs e)
        {
            foreach (var message in NormalizeAndSplitMidiMessages(e.Data, e.Start, e.Length))
            {
                if (message[0] == 0x90) //button pressed, released, fader released
                {
                    OnMessageReceived($"MIDI Received: {string.Join(" ", message.Select(b => $"{b:x2}"))}");

                    if (message.MIDIEqual(0x90, 0x00, 0x00, 0xff, 0x00, 0xff) && (message[1] >= 0x68) && (message[1] <= 0x70)) //release fader (0x90 [0x68-0x70] 0x00)
                    {
                        byte channelNumber = (byte)(message[1] - 0x68);
                        if (faderValues.ContainsKey(channelNumber))
                        {
                            faderValues[channelNumber].IsTouched = false;
                            SetFader(channelNumber, faderValues[channelNumber].Value);
                        }
                    }
                    else if (message.MIDIEqual(0x90, 0x00, 0x7f, 0xff, 0x00, 0xff) && (message[1] >= 0x68) && (message[1] <= 0x70)) //touch fader (0x90 [0x68-0x70] 0x00)
                    {
                        byte channelNumber = (byte)(message[1] - 0x68);
                        if (faderValues.ContainsKey(channelNumber))
                        {
                            faderValues[channelNumber].IsTouched = true;
                        }
                    }
                    else if (message[1] >= _buttonsID[ButtonsEnum.Ch1Mute] && message[1] <= (_buttonsID[ButtonsEnum.Ch1Mute] + 7) && message[2] == 0x7f) //channel mute button
                    {
                        byte channelNumber = (byte)(message[1] - _buttonsID[ButtonsEnum.Ch1Mute]);
                        OnChannelMuteEvent(channelNumber);
                    }
                    else if (message[1] >= _buttonsID[ButtonsEnum.Ch1Solo] && message[1] <= (_buttonsID[ButtonsEnum.Ch1Solo] + 7) && message[2] == 0x7f) //channel solo button
                    {
                        byte channelNumber = (byte)(message[1] - _buttonsID[ButtonsEnum.Ch1Solo]);
                        OnChannelSoloEvent(channelNumber);
                    }
                    else if (message[1] >= _buttonsID[ButtonsEnum.Ch1Rec] && message[1] <= (_buttonsID[ButtonsEnum.Ch1Rec] + 7) && message[2] == 0x7f) //channel rec button
                    {
                        byte channelNumber = (byte)(message[1]- _buttonsID[ButtonsEnum.Ch1Rec]);
                        OnChannelRecEvent(channelNumber);
                    }
                    else if  (message[1] >= _buttonsID[ButtonsEnum.Ch1Select] && message[1] <= (_buttonsID[ButtonsEnum.Ch1Select] + 7) && message[2] == 0x7f) //channel select button
                    {
                        byte channelNumber = (byte)(message[1] - _buttonsID[ButtonsEnum.Ch1Select]);
                        OnChannelSelectEvent(channelNumber);
                    }
                    else if (message.MIDIEqual(0x90, 0x32, 0x7f)) //main select button
                    {
                        OnChannelSelectEvent(8);
                    }
                    else if (message.MIDIEqual(0x90, _buttonsID[ButtonsEnum.Gain], 0x7f))
                    {
                        OnGainModeEvent();
                    }
                    else if (message.MIDIEqual(0x90, _buttonsID[ButtonsEnum.Pan], 0x7f))
                    {
                        OnPanEvent();
                    }
                    else if (message.MIDIEqual(0x90, _buttonsID[ButtonsEnum.TapTempo], 0x7f))
                    {
                        OnTapTempoEvent();
                    }
                    else if (message.MIDIEqual(0x90, _buttonsID[ButtonsEnum.SaveUserLayer], 0x7f))
                    {
                        OnSaveUserLayerEvent();
                    }
                    else if ((message[0] == 0x90) && message[1] == _buttonsID[ButtonsEnum.SetUserChannel])
                    {
                        OnSetUserChannelEvent(0, message[2] == 0x7f);
                    }
                    else if (message.MIDIEqual(0x90, _buttonsID[ButtonsEnum.MuteAll], 0x7f))
                    {
                        OnMuteAllEvent();
                    }
                    else if (message.MIDIEqual(0x90, _buttonsID[ButtonsEnum.MuteFX], 0x7f))
                    {
                        OnMuteFXEvent();
                    }
                    else if (message.MIDIEqual(0x90, _buttonsID[ButtonsEnum.ClearMute], 0x7f))
                    {
                        OnClearMuteEvent();
                    }
                    else if (message.MIDIEqual(0x90, _buttonsID[ButtonsEnum.ClearSolo], 0x7f))
                    {
                        OnClearSoloEvent();
                    }
                    else if (message.MIDIEqual(0x90, _buttonsID[ButtonsEnum.PlayPrev], 0x7f))
                    {
                        OnPrevEvent();
                    }
                    else if (message.MIDIEqual(0x90, _buttonsID[ButtonsEnum.PlayNext], 0x7f))
                    {
                        OnNextEvent();
                    }
                    else if (message.MIDIEqual(0x90, _buttonsID[ButtonsEnum.Stop], 0x7f))
                    {
                        OnStopEvent();
                    }
                    else if (message.MIDIEqual(0x90, _buttonsID[ButtonsEnum.Play], 0x7f))
                    {
                        OnPlayEvent();
                    }
                    else if (message.MIDIEqual(0x90, _buttonsID[ButtonsEnum.Rec], 0x7f))
                    {
                        OnRecEvent();
                    }
                    else if (message.MIDIEqual(0x90, _buttonsID[ButtonsEnum.FaderBankUp], 0x7f))
                    {
                        OnBankUp();
                    }
                    else if (message.MIDIEqual(0x90, _buttonsID[ButtonsEnum.FaderBankDown], 0x7f))
                    {
                        OnBankDown();
                    }
                    else if (message.MIDIEqual(0x90, _buttonsID[ButtonsEnum.ChannelUp], 0x7f))
                    {
                        OnLayerUp();
                    }
                    else if (message.MIDIEqual(0x90, _buttonsID[ButtonsEnum.ChannelDown], 0x7f))
                    {
                        OnLayerDown();
                    }
                    else if (message.MIDIEqual(0x90, _buttonsID[ButtonsEnum.Talkback], 0x00))
                    {
                        OnTalkbackEvent(true);
                    }
                    else if (message.MIDIEqual(0x90, _buttonsID[ButtonsEnum.Talkback], 0x7f))
                    {
                        OnTalkbackEvent(false);
                    }

                    else if (message[0]== 0x90 && _buttonsID.GetAuxButton(message[1]).isAux) //F1-F8 press
                    {
                        var auxNum = _buttonsID.GetAuxButton(message[1]).auxNum;
                        OnAuxButtonEvent(auxNum, message[2] == 0x7f);
                    }
                    else if ((message[0] == 0x90) && _buttonsID.GetFxButton(message[1]).isFX)
                    {
                        var fxNum = _buttonsID.GetFxButton(message[1]).fxNum;
                        OnFxButtonEvent(fxNum, message[2] == 0x7f);
                    }
                    else if ((message[0] == 0x90) && _buttonsID.GetMuteGroupsButton(message[1]).isMuteGroupButton)
                    {
                        var muteNum = _buttonsID.GetMuteGroupsButton(message[1]).muteGroupNum;
                        OnMuteGroupButtonEvent(muteNum, message[2] == 0x7f);
                    }
                    else if ((message[0] == 0x90) && _buttonsID.GetViewGroupButton(message[1]).isViewGroup)
                    {
                        var viewGroupNum = _buttonsID.GetViewGroupButton(message[1]).viewGroupNum;
                        OnViewGroupButtonEvent(viewGroupNum, message[2] == 0x7f);
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
                else if (message[0] == 0xb0 && message[1] >= 0x10 && message[1] <= 0x17) //channel knobb turning
                {
                    byte channelNumber = (byte)(message[1] - 0x10);
                    if (message[2] >= 0x01 && message[2] <= 0x03)
                        OnKnobEvent(channelNumber, message[2]);
                    if (message[2] >= 0x41 && message[2] <= 0x43)
                        OnKnobEvent(channelNumber, -1 * (message[2] & 0x03));
                }
                else if (message[0] == 0xb0 && message[1] == 0x3c ) //jog wheel turning
                {
                    if (message[2] >= 0x01 && message[2] <= 0x03)
                        OnWheelEvent(0, message[2]);
                    if (message[2] >= 0x41 && message[2] <= 0x43)
                        OnWheelEvent(0, -1 * (message[2] & 0x03));
                }
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

        public bool SetKnobLed(int channelNumber, double gainValue)
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
                Send(new byte[] { 0x90, (byte)(i==8? 0x32 : _buttonsID[ButtonsEnum.Ch1Select] + i), (byte)((i==channelNumber && turnOn)? 0x7f : 0x00) }, 0, 3, 0);
            }
        }
        public void SetMuteLed(int channelNumber, bool turnOn)
        {
            if (channelNumber < 8)
            {
                Send(new byte[] { 0x90, (byte)(_buttonsID[ButtonsEnum.Ch1Mute] + channelNumber), (byte)(turnOn ? 0x7f : 0x00) }, 0, 3, 0);
            }
        }
        public void SetSoloLed(int channelNumber, bool turnOn)
        {
            if (channelNumber < 8)
            {
                Send(new byte[] { 0x90, (byte)(_buttonsID[ButtonsEnum.Ch1Solo] + channelNumber), (byte)(turnOn ? 0x7f : 0x00) }, 0, 3, 0);
            }
        }
        public void SetRecLed(int channelNumber, bool turnOn)
        {
            if (channelNumber < 8)
            {
                Send(new byte[] { 0x90, (byte)(_buttonsID[ButtonsEnum.Ch1Rec] + channelNumber), (byte)(turnOn ? 0x7f : 0x00) }, 0, 3, 0);
            }
        }
        public void SetLed(ButtonsEnum buttonName, bool turnOn)
        {
            Send(new byte[] { 0x90, _buttonsID[buttonName], (byte)(turnOn ? 0x7f : 0x00) }, 0, 3, 0);
        }

        public void SetChannelStripColour(int channelNumber, ChannelStripColour colour)
        {
            if (channelNumber < 0 || channelNumber >= ChannelStripCount) return;
            _stripColours[channelNumber] = colour;
            byte[] sysex = new byte[] { 0xf0, 0x00, 0x00, 0x66, _lcdDisplayNumber, 0x72,
                (byte)_stripColours[0], (byte)_stripColours[1], (byte)_stripColours[2], (byte)_stripColours[3],
                (byte)_stripColours[4], (byte)_stripColours[5], (byte)_stripColours[6], (byte)_stripColours[7],
                0xf7 };
            Send(sysex, 0, sysex.Length, 0);
        }

        public void WriteTextToChannelLCD(int channelNumber, string text, int line = 0)
        {
            if (channelNumber < 8)
            {
                text = Regex.Replace(text, @"[^\u0020-\u007E]", string.Empty);

                var position = channelNumber * 7 + line*56;
                var message = ASCIIEncoding.ASCII.GetBytes((text + "       ").Substring(0, 7));
                byte[] sysex = (new byte[] { 0xf0, 0, 0, 0x66, _lcdDisplayNumber, 0x12, (byte)position }).Concat(message).Concat(new byte[] { 0xf7 }).ToArray();
                Send(sysex, 0, sysex.Length, 0);
            }
        }
        private void WriteDefaultText(int channelNumber, int line, string text)
        {
            if (channelNumber >= 0 && channelNumber < ChannelStripCount)
            {
                _lineTempTimer[line][channelNumber]?.Dispose();
                _lineTempTimer[line][channelNumber] = null;
                _lineDefault[line][channelNumber] = text;
            }
            WriteTextToChannelLCD(channelNumber, text, line);
        }

        private void WriteTemporaryText(int channelNumber, int line, string text, int seconds)
        {
            if (channelNumber < 0 || channelNumber >= ChannelStripCount) return;
            _lineTempTimer[line][channelNumber]?.Dispose();
            WriteTextToChannelLCD(channelNumber, text, line);
            _lineTempTimer[line][channelNumber] = new Timer(_ =>
            {
                _lineTempTimer[line][channelNumber]?.Dispose();
                _lineTempTimer[line][channelNumber] = null;
                WriteTextToChannelLCD(channelNumber, _lineDefault[line][channelNumber] ?? "", line);
            }, null, seconds * 1000, Timeout.Infinite);
        }

        public void WriteDefaultTextToChannelLCDFirstLine(int channelNumber, string text) =>
            WriteDefaultText(channelNumber, 0, text);

        public void WriteTemporaryTextToChannelLCDFirstLine(int channelNumber, string text, int seconds) =>
            WriteTemporaryText(channelNumber, 0, text, seconds);

        public void WriteDefaultTextToChannelLCDSecondLine(int channelNumber, string text) =>
            WriteDefaultText(channelNumber, 1, text);

        public void WriteTemporaryTextToChannelLCDSecondLine(int channelNumber, string text, int seconds) =>
            WriteTemporaryText(channelNumber, 1, text, seconds);

        public void WriteTextToLCDSecondLine(string text)
        {
            _lcdTextSyncGuid = Guid.NewGuid();
            var padded = (text + new string(' ', ChannelStripCount * 7)).Substring(0, ChannelStripCount * 7);
            var message = ASCIIEncoding.ASCII.GetBytes(padded);
            byte[] sysex = (new byte[] { 0xf0, 0, 0, 0x66, _lcdDisplayNumber, 0x12, 0x38 }).Concat(message).Concat(new byte[] { 0xf7 }).ToArray();
            Send(sysex, 0, sysex.Length, 0);
        }

        public void WriteTextToLCDSecondLine(string text, int delay)
        {
            var guid = Guid.NewGuid();
            _lcdTextSyncGuid = guid;
            var padded = (text + new string(' ', ChannelStripCount * 7)).Substring(0, ChannelStripCount * 7);
            var message = ASCIIEncoding.ASCII.GetBytes(padded);
            byte[] sysex = (new byte[] { 0xf0, 0, 0, 0x66, _lcdDisplayNumber, 0x12, 0x38 }).Concat(message).Concat(new byte[] { 0xf7 }).ToArray();
            Send(sysex, 0, sysex.Length, 0);
            new Thread(() =>
            {
                Thread.Sleep(delay * 1000);
                if (guid == _lcdTextSyncGuid)
                {
                    for (int i = 0; i < ChannelStripCount; i++)
                        WriteTextToChannelLCD(i, _lineDefault[1][i] ?? "", 1);
                }
            }).Start();
        }

        private byte convertStringToDisplayBytes(string str, int poz = 0)
        {
            byte[] asciiBytes = ASCIIEncoding.ASCII.GetBytes(str);
            if (poz >= asciiBytes.Length)
                poz = asciiBytes.Length - 1;

            if (asciiBytes[poz] >= 0x40 && asciiBytes[poz] <= 0x60)
                return (byte)(asciiBytes[poz] - 0x40);
            else if (asciiBytes[poz] >= 0x21 && asciiBytes[poz] <= 0x3F)
                return asciiBytes[poz];

            return 0x20;
        }
        public void WriteTextToMainDisplay(string text, int position, int maxChar = 1)
        {
            if (position < 0 || position > 11) position = 0;
            if (maxChar + position > 12) maxChar = 12 - position;
            text = (text + "             ").ToUpper();
            for (int i=0; i < maxChar; ++i)
            {
                byte[] sysex = (new byte[] { 0xB0, Convert.ToByte(position+64+i), convertStringToDisplayBytes(text, maxChar-i-1) });
                Send(sysex, 0, sysex.Length, 0);
            }

        }
        public void WriteTextToAssignmentDisplay(string text)
        {
            WriteTextToMainDisplay(text, 10, 2);
        }
        public void WriteTextToBarsDisplay(string text)
        {
            WriteTextToMainDisplay(text, 7, 3);
        }
        public void WriteTextToBeatsDisplay(string text)
        {
            WriteTextToMainDisplay(text, 5, 2);

        }
        public void WriteTextToSubDivisionDisplay(string text)
        {
            WriteTextToMainDisplay(text, 3, 2);

        }
        public void WriteTextToTicksDisplay(string text)
        {
            WriteTextToMainDisplay(text, 0, 3);
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

        public void InitializeController()
        {
            if (this.IsExtender)
            {
                _lcdDisplayNumber = 0x15;
            }
            WriteTextToMainDisplay("            ", 0, 12);
            WriteTextToLCDSecondLine("");
            foreach (var btn in Enum.GetValues<ButtonsEnum>())
                SetLed(btn, false);
            for (int i = 0; i < 9; i++)
            {
                SetSelectLed(i, false);
                SetMuteLed(i, false);
                SetSoloLed(i, false);
                SetRecLed(i, false);
                WriteDefaultTextToChannelLCDFirstLine(i, "");
                WriteDefaultTextToChannelLCDSecondLine(i, "");
                SetChannelStripColour(i, ChannelStripColour.Black);
            }

        }

        protected class DictionarySerializerClass
        {
            [JsonConverter(typeof(DictionaryTKeyEnumTValueConverter))]
            public Dictionary<ButtonsEnum, byte> ButtonsDictionary { get; set; }


        }

        protected Dictionary<ButtonsEnum, byte> GetButtonsValues(string fileName)
        {
            var jsonText = File.ReadAllText(fileName);
            var options = MyClassTypeResolver<DictionarySerializerClass>.GetSerializerOptions();
            var outObject = JsonSerializer.Deserialize(jsonText, typeof(DictionarySerializerClass), options);
            Dictionary<ButtonsEnum, byte> result = (outObject as DictionarySerializerClass).ButtonsDictionary;
            return result;
        }

    }
}
