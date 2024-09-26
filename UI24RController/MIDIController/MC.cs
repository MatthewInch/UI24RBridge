using System;
using System.Collections.Generic;
using System.Text;
using Commons.Music.Midi;
using System.Linq;
using System.Threading.Tasks;
using Commons.Music.Midi.RtMidi;
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

        protected string _buttonsValuesFileName;
        public string ButtonsValuesFileName { get { return _buttonsValuesFileName; }
            set {
                _buttonsValuesFileName = value;
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

        public event EventHandler<EventArgs> TrackEvent;
        public event EventHandler<EventArgs> PanEvent;
        public event EventHandler<EventArgs> EqEvent;
        public event EventHandler<EventArgs> SendEvent;
        public event EventHandler<EventArgs> PlugInEvent;
        public event EventHandler<EventArgs> InstrEvent;

        public event EventHandler<EventArgs> DisplayBtnEvent;
        public event EventHandler<EventArgs> SmtpeBeatsBtnEvent;

        public event EventHandler<EventArgs> GlobalViewEvent;

        public event EventHandler<EventArgs> MidiTracksEvent;
        public event EventHandler<EventArgs> InputsEvent;
        public event EventHandler<EventArgs> AudioTracksEvent;
        public event EventHandler<EventArgs> AudioInstEvent;
        public event EventHandler<EventArgs> AuxBtnEvent;
        public event EventHandler<EventArgs> BusesBtnEvent;
        public event EventHandler<EventArgs> OutputsEvent;
        public event EventHandler<FunctionEventArgs> UserBtnEvent;

        public event EventHandler<FunctionEventArgs> AuxButtonEvent;       //F1-F8
        public event EventHandler<FunctionEventArgs> FxButtonEvent;        //Modify buttons
        public event EventHandler<FunctionEventArgs> MuteGroupButtonEvent; //Automation buttons

        public event EventHandler<EventArgs> SaveEvent;
        public event EventHandler<EventArgs> UndoEvent;
        public event EventHandler<EventArgs> CancelEvent;
        public event EventHandler<EventArgs> EnterEvent;

        public event EventHandler<EventArgs> MarkerEvent;
        public event EventHandler<EventArgs> NudgeEvent;
        public event EventHandler<EventArgs> CycleEvent;
        public event EventHandler<EventArgs> DropEvent;
        public event EventHandler<EventArgs> ReplaceEvent;
        public event EventHandler<EventArgs> ClickEvent;
        public event EventHandler<EventArgs> SoloEvent;

        public event EventHandler<EventArgs> PrevEvent;
        public event EventHandler<EventArgs> NextEvent;
        public event EventHandler<EventArgs> StopEvent;
        public event EventHandler<EventArgs> PlayEvent;
        public event EventHandler<EventArgs> RecEvent;

        public event EventHandler<EventArgs> LayerUp;
        public event EventHandler<EventArgs> LayerDown;
        public event EventHandler<EventArgs> BankUp;
        public event EventHandler<EventArgs> BankDown;

        public event EventHandler<EventArgs> UpEvent;
        public event EventHandler<EventArgs> DownEvent;
        public event EventHandler<EventArgs> LeftEvent;
        public event EventHandler<EventArgs> RightEvent;
        public event EventHandler<EventArgs> CenterEvent;

        public event EventHandler<ButtonEventArgs> ScrubEvent;

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

        protected void OnTrackEvent()
        {
            TrackEvent?.Invoke(this, new EventArgs());
        }
        protected void OnPanEvent()
        {
            PanEvent?.Invoke(this, new EventArgs());
        }
        protected void OnEqEvent()
        {
            EqEvent?.Invoke(this, new EventArgs());
        }
        protected void OnSendEvent()
        {
            SendEvent?.Invoke(this, new EventArgs());
        }
        protected void OnPlugInEvent()
        {
            PlugInEvent?.Invoke(this, new EventArgs());
        }
        protected void OnInstrEvent()
        {
            InstrEvent?.Invoke(this, new EventArgs());
        }

        protected void OnDisplayBtnEvent()
        {
            DisplayBtnEvent?.Invoke(this, new EventArgs());
        }
        protected void OnSmtpeBeatsBtnEvent()
        {
            SmtpeBeatsBtnEvent?.Invoke(this, new EventArgs());
        }


        protected void OnGlobalViewEvent()
        {
            GlobalViewEvent?.Invoke(this, new EventArgs());
        }

        protected void OnMidiTracksEvent()
        {
            MidiTracksEvent?.Invoke(this, new EventArgs());
        }
        protected void OnInputsEvent()
        {
            InputsEvent?.Invoke(this, new EventArgs());
        }
        protected void OnAudioTracksEvent()
        {
            AudioTracksEvent?.Invoke(this, new EventArgs());
        }
        protected void OnAudioInstEvent()
        {
            AudioInstEvent?.Invoke(this, new EventArgs());
        }
        protected void OnAuxBtnEvent()
        {
            AuxBtnEvent?.Invoke(this, new EventArgs());
        }
        protected void OnBusesBtnEvent()
        {
            BusesBtnEvent?.Invoke(this, new EventArgs());
        }
        protected void OnOutputsEvent()
        {
            OutputsEvent?.Invoke(this, new EventArgs());
        }
        protected void OnUserEvent(int functionNumber, bool isPress)
        {
            UserBtnEvent?.Invoke(this, new FunctionEventArgs(functionNumber, isPress));
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

        protected void OnSaveEvent()
        {
            SaveEvent?.Invoke(this, new EventArgs());
        }
        protected void OnCancelEvent()
        {
            CancelEvent?.Invoke(this, new EventArgs());
        }
        protected void OnUndoEvent()
        {
            UndoEvent?.Invoke(this, new EventArgs());
        }
        protected void OnEnterEvent()
        {
            EnterEvent?.Invoke(this, new EventArgs());
        }

        protected void OnMarkerEvent()
        {
            MarkerEvent?.Invoke(this, new EventArgs());
        }
        protected void OnNudgeEvent()
        {
            NudgeEvent?.Invoke(this, new EventArgs());
        }
        protected void OnCycleEvent()
        {
            CycleEvent?.Invoke(this, new EventArgs());
        }
        protected void OnDropEvent()
        {
            DropEvent?.Invoke(this, new EventArgs());
        }
        protected void OnReplaceEvent()
        {
            ReplaceEvent?.Invoke(this, new EventArgs());
        }
        protected void OnClickEvent()
        {
            ClickEvent?.Invoke(this, new EventArgs());
        }
        protected void OnSoloEvent()
        {
            SoloEvent?.Invoke(this, new EventArgs());
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

        protected void OnUpEvent()
        {
            UpEvent?.Invoke(this, new EventArgs());
        }
        protected void OnDownEvent()
        {
            DownEvent?.Invoke(this, new EventArgs());
        }
        protected void OnLeftEvent()
        {
            LeftEvent?.Invoke(this, new EventArgs());
        }
        protected void OnRightEvent()
        {
            RightEvent?.Invoke(this, new EventArgs());
        }
        protected void OnCenterEvent()
        {
            CenterEvent?.Invoke(this, new EventArgs());
        }

        protected void OnWheelEvent(int channelNumber, int wheelDirection)
        {
            WheelEvent?.Invoke(this, new WheelEventArgs(wheelDirection));
        }
        protected void OnScrubEvent(bool isPressed=true)
        {
            ScrubEvent?.Invoke(this, new ButtonEventArgs(isPressed));
        }


        protected void OnConnectionErrorEvent()
        {
            if (ConnectionErrorEvent != null )
            {
                _isConnectionErrorOccured = true;
                _isConnected = false;
                ConnectionErrorEvent(this, new EventArgs());
            }
        }

        public void Dispose()
        {
            _isConnected = false;
            if (_pingThread != null)
            {
                //stop the pinging thread
                _isConnectionErrorOccured = true;
            }
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
            try
            {
                _inputDeviceName = deviceName;
                var access = MidiAccessManager.Default;
                var deviceNumber = access.Inputs.Where(i => i.Name.ToUpper() == deviceName.ToUpper()).FirstOrDefault();
                if (deviceNumber != null)
                {
                    
                    var input = access.OpenInputAsync(deviceNumber.Id).Result;
                    _input = input;
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
        public bool ConnectOutputDevice(string deviceName)
        {
            try
            {
                _outputDeviceName = deviceName;
                var access = MidiAccessManager.Default;
                var deviceNumber = access.Outputs.Where(i => i.Name.ToUpper() == deviceName.ToUpper()).FirstOrDefault();
                if (deviceNumber != null)
                {
                    var output = access.OpenOutputAsync(deviceNumber.Id).Result;
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
                OnConnectionErrorEvent();
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

                else if (message.MIDIEqual(0x90, _buttonsID[ButtonsEnum.Track], 0x7f))
                {
                    OnTrackEvent();
                }
                else if (message.MIDIEqual(0x90, _buttonsID[ButtonsEnum.Pan], 0x7f))
                {
                    OnPanEvent();
                }
                else if (message.MIDIEqual(0x90, _buttonsID[ButtonsEnum.Eq], 0x7f))
                {
                    OnEqEvent();
                }
                else if (message.MIDIEqual(0x90, _buttonsID[ButtonsEnum.Send], 0x7f))
                {
                    OnSendEvent();
                }
                else if (message.MIDIEqual(0x90, _buttonsID[ButtonsEnum.PlugIn], 0x7f))
                {
                    OnPlugInEvent();
                }
                else if (message.MIDIEqual(0x90, _buttonsID[ButtonsEnum.Instr], 0x7f))
                {
                    OnInstrEvent();
                }

                else if (message.MIDIEqual(0x90, _buttonsID[ButtonsEnum.Display], 0x7f))
                {
                    OnDisplayBtnEvent();
                }
                else if (message.MIDIEqual(0x90, _buttonsID[ButtonsEnum.Smtpe], 0x7f))
                {
                    OnSmtpeBeatsBtnEvent();
                }

                else if (message.MIDIEqual(0x90, _buttonsID[ButtonsEnum.GlobalView], 0x7f))
                {
                    OnGlobalViewEvent();
                }

                else if (message.MIDIEqual(0x90, _buttonsID[ButtonsEnum.MidiTracks], 0x7f))
                {
                    OnMidiTracksEvent();
                }
                else if (message.MIDIEqual(0x90, _buttonsID[ButtonsEnum.Inputs], 0x7f))
                {
                    OnInputsEvent();
                }
                else if (message.MIDIEqual(0x90, _buttonsID[ButtonsEnum.AudioTracks], 0x7f))
                {
                    OnAudioTracksEvent();
                }
                else if (message.MIDIEqual(0x90, _buttonsID[ButtonsEnum.AudioInst], 0x7f))
                {
                    OnAudioInstEvent();
                }
                else if (message.MIDIEqual(0x90, _buttonsID[ButtonsEnum.AuxBtn], 0x7f))
                {
                    OnAuxBtnEvent();
                }
                else if (message.MIDIEqual(0x90, _buttonsID[ButtonsEnum.BusesBtn], 0x7f))
                {
                    OnBusesBtnEvent();
                }
                else if (message.MIDIEqual(0x90, _buttonsID[ButtonsEnum.Outputs], 0x7f))
                {
                    OnOutputsEvent();
                }
                else if ((message[0] == 0x90) && message[1] == _buttonsID[ButtonsEnum.User])
                {
                    OnUserEvent(0, message[2] == 0x7f);
                }

                else if (message.MIDIEqual(0x90, _buttonsID[ButtonsEnum.Save], 0x7f))
                {
                    OnSaveEvent();
                }
                else if (message.MIDIEqual(0x90, _buttonsID[ButtonsEnum.Undo], 0x7f))
                {
                    OnUndoEvent();
                }
                else if (message.MIDIEqual(0x90, _buttonsID[ButtonsEnum.Cancel], 0x7f))
                {
                    OnCancelEvent();
                }
                else if (message.MIDIEqual(0x90, _buttonsID[ButtonsEnum.Enter], 0x7f))
                {
                    OnEnterEvent();
                }

                else if (message.MIDIEqual(0x90, _buttonsID[ButtonsEnum.Marker], 0x7f))
                {
                    OnMarkerEvent();
                }
                else if (message.MIDIEqual(0x90, _buttonsID[ButtonsEnum.Nudge], 0x7f))
                {
                    OnNudgeEvent();
                }
                else if (message.MIDIEqual(0x90, _buttonsID[ButtonsEnum.Cycle], 0x7f))
                {
                    OnCycleEvent();
                }
                else if (message.MIDIEqual(0x90, _buttonsID[ButtonsEnum.Drop], 0x7f))
                {
                    OnDropEvent();
                }
                else if (message.MIDIEqual(0x90, _buttonsID[ButtonsEnum.Replace], 0x7f))
                {
                    OnReplaceEvent();
                }
                else if (message.MIDIEqual(0x90, _buttonsID[ButtonsEnum.Click], 0x7f))
                {
                    OnClickEvent();
                }
                else if (message.MIDIEqual(0x90, _buttonsID[ButtonsEnum.Solo], 0x7f))
                {
                    OnSoloEvent();
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

                else if (message.MIDIEqual(0x90, _buttonsID[ButtonsEnum.Up], 0x7f))
                {
                    OnUpEvent();
                }
                else if (message.MIDIEqual(0x90, _buttonsID[ButtonsEnum.Down], 0x7f))
                {
                    OnDownEvent();
                }
                else if (message.MIDIEqual(0x90, _buttonsID[ButtonsEnum.Left], 0x7f))
                {
                    OnLeftEvent();
                }
                else if (message.MIDIEqual(0x90, _buttonsID[ButtonsEnum.Right], 0x7f))
                {
                    OnRightEvent();
                }
                else if (message.MIDIEqual(0x90, _buttonsID[ButtonsEnum.Center], 0x7f))
                {
                    OnCenterEvent();
                }
                else if (message.MIDIEqual(0x90, _buttonsID[ButtonsEnum.Scrub], 0x00))
                {
                    OnScrubEvent(true);
                }
                else if (message.MIDIEqual(0x90, _buttonsID[ButtonsEnum.Scrub], 0x7f))
                {
                    OnScrubEvent(false);
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
            else if (message[0] == 0xb0 && (message[1] >= 0x10 && message[1] <= 0x17)) //channel knobb turning
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
        public void WriteTextToChannelLCDFirstLine(int channelNumber, string text)
        {
            WriteTextToChannelLCD(channelNumber, text, 0);
        }
        public void WriteTextToChannelLCDSecondLine(int channelNumber, string text)
        {
            WriteTextToChannelLCD(channelNumber, text, 1);
        }
        public void WriteTextToLCDSecondLine( string text)
        {
                var message = ASCIIEncoding.ASCII.GetBytes((text + "                                                        ").Substring(0, 50));
                byte[] sysex = (new byte[] { 0xf0, 0, 0, 0x66, _lcdDisplayNumber, 0x12, 0x38 }).Concat(message).Concat(new byte[] { 0xf7 }).ToArray();
                Send(sysex, 0, sysex.Length, 0);
        }
        public void WriteTextToLCDSecondLine(string text, int delay)
        {
            WriteTextToLCDSecondLine(text);
            var guid = Guid.NewGuid();
            _lcdTextSyncGuid = guid;
            new Thread(() =>
            {
                Thread.Sleep(delay * 1000);
                //if value change the other thread write back the last fader value
                if (guid == _lcdTextSyncGuid)
                {
                    WriteTextToLCDSecondLine("");
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
