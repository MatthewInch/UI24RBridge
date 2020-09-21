using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.WebSockets;
using System.Threading;
using UI24RController.UI24RChannels;
using UI24RController.UI24RChannels.Interfaces;
using Websocket.Client;
using System.IO;
using Websocket.Client.Models;
using UI24RController.MIDIController;

namespace UI24RController
{
    public class UI24RBridge : IDisposable
    {



        const string CONFIGFILE_VIEW_GROUP = "ViewGroups.json";
        protected bool _hasViewGroupConfig = false;

        protected BridgeSettings _settings;
        protected WebsocketClient _client;
   
        protected int _selectedChannel = -1; //-1 = no selected channel
        //protected int _pressedFunctionButton = -1; //-1 = no pressed button
        protected SelectedLayoutEnum _selectedLayout = SelectedLayoutEnum.Channels;
 
        /// <summary>
        /// Represent the UI24R mixer state
        /// TODO: need to move every global variable that store any mixer specific state to the Mixer class (viewGroups, selectedChannel etc.) 
        /// </summary>
        protected Mixer _mixer = new Mixer();

        //0-23: input channels
        //24-25: Linie In L/R
        //26-27: Player L/R
        //28-31: FX channels
        //32-37: Subgroups
        //38-47: AUX 1-10
        //48-53: VCA 1-6

        protected int[][] _viewViewGroups;
        protected int _selectedViewGroup = 0;
        /// <summary>
        /// Contains the channels of the mixer. the channel number like the view groups 0-23 input channels, 24-25 Line in etc.
        /// </summary>
        protected List<ChannelBase> _mixerChannels;


        /// <summary>
        ///  Represent the bridge between the UI24R and a DAW controller
        /// </summary>
        public UI24RBridge(BridgeSettings settings)
        {
            this._settings = settings;
            SendMessage("Start initialization...", false);
            InitializeChannels();
            InitializeViewGroups();
            SendMessage("Create controller events....", false);
            _settings.Controller.FaderEvent += _midiController_FaderEvent;
            _settings.Controller.PresetUp += _midiController_PresetUp;
            _settings.Controller.PresetDown += _midiController_PresetDown;
            _settings.Controller.GainEvent += _midiController_GainEvent;
            _settings.Controller.MuteChannelEvent += _midiController_MuteChannelEvent;
            _settings.Controller.SoloChannelEvent += _midiController_SoloChannelEvent;
            _settings.Controller.SelectChannelEvent += _midiController_SelectChannelEvent;
            _settings.Controller.RecChannelEvent += _midiController_RecChannelEvent;
            _settings.Controller.SaveEvent += _midiController_SaveEvent;
            _settings.Controller.RecEvent += _midiController_RecEvent;
            _settings.Controller.PlayEvent += _midiController_PlayEvent;
            _settings.Controller.StopEvent += _midiController_StopEvent;
            _settings.Controller.NextEvent += _midiController_NextEvent;
            _settings.Controller.PrevEvent += _midiController_PrevEvent;
            _settings.Controller.WriteTextToLCD("");
            _settings.Controller.ConnectionErrorEvent += _midiController_ConnectionErrorEvent;
            _settings.Controller.AuxButtonEvent += _midiController_AuxButtonEvent;
            _settings.Controller.FxButtonEvent += Controller_FXButtonEvent;
            if (_settings.Controller.IsConnectionErrorOccured)
            {
                _midiController_ConnectionErrorEvent(this, null);
            }
            SendMessage("Start websocket connection...", false);
            _client = new WebsocketClient(new Uri(_settings.Address));
            _client.MessageReceived.Subscribe(msg => UI24RMessageReceived(msg));
            _client.DisconnectionHappened.Subscribe(info => WebsocketDisconnectionHappened(info));
            _client.ReconnectionHappened.Subscribe(info => WebsocketReconnectionHappened(info));
            _client.ErrorReconnectTimeout = new TimeSpan(0,0,10);
            SendMessage("Connecting to UI24R....", false);
            _client.Start();
        }


        private void _midiController_PrevEvent(object sender, EventArgs e)
        {
            _client.Send(_mixer.Get2TrackPrevMessage());
        }

        private void _midiController_NextEvent(object sender, EventArgs e)
        {
            _client.Send(_mixer.Get2TrackNextMessage());
        }

        private void _midiController_StopEvent(object sender, EventArgs e)
        {
            _client.Send(_mixer.Get2TrackStopMessage());
        }

        private void _midiController_PlayEvent(object sender, EventArgs e)
        {
            _client.Send(_mixer.Get2TrackPlayMessage());
        }

        private void _midiController_AuxButtonEvent(object sender, MIDIController.FunctionEventArgs e)
        {
            if (_settings.AuxButtonBehavior == BridgeSettings.AuxButtonBehaviorEnum.Release)
            {
                if (e.IsPress)
                {
                    _selectedLayout = e.FunctionButton.ToAux();
                    _settings.Controller.SetLed(_selectedLayout.ToButtonsEnum(), true);
                }
                else
                {
                    _settings.Controller.SetLed(_selectedLayout.ToButtonsEnum(), false);
                    _selectedLayout = SelectedLayoutEnum.Channels;
                }
            }
            else
            {
                if (e.IsPress)
                {
                    if (_selectedLayout == e.FunctionButton.ToAux())
                    {
                        _settings.Controller.SetLed(_selectedLayout.ToButtonsEnum(), false);
                        _selectedLayout = SelectedLayoutEnum.Channels;
                    }
                    else
                    {
                        _settings.Controller.SetLed(_selectedLayout.ToButtonsEnum(), false);
                        _selectedLayout = e.FunctionButton.ToAux();
                        _settings.Controller.SetLed(_selectedLayout.ToButtonsEnum(), true);
                    }
                }
            }
            SetControllerToCurrentViewGroup();
        }

        private void Controller_FXButtonEvent(object sender, FunctionEventArgs e)
        {
            if (_settings.AuxButtonBehavior == BridgeSettings.AuxButtonBehaviorEnum.Release)
            {
                if (e.IsPress)
                {
                    _selectedLayout = e.FunctionButton.ToFx();
                    _settings.Controller.SetLed(_selectedLayout.ToButtonsEnum(), true);
                }
                else
                {
                    _settings.Controller.SetLed(_selectedLayout.ToButtonsEnum(), false);
                    _selectedLayout = SelectedLayoutEnum.Channels;
                }
            }
            else
            {
                if (e.IsPress)
                {
                    if (_selectedLayout == e.FunctionButton.ToFx())
                    {
                        _settings.Controller.SetLed(_selectedLayout.ToButtonsEnum(), false);
                        _selectedLayout = SelectedLayoutEnum.Channels;
                    }
                    else
                    {
                        _settings.Controller.SetLed(_selectedLayout.ToButtonsEnum(), false);
                        _selectedLayout = e.FunctionButton.ToFx();
                        _settings.Controller.SetLed(_selectedLayout.ToButtonsEnum(), true);
                    }
                }
            }
            SetControllerToCurrentViewGroup();
        }

        private void _midiController_ConnectionErrorEvent(object sender, EventArgs e)
        {
            SendMessage("Midi controller connection error.", false);
            SendMessage("Try to reconnect....", false);
            new Thread(() =>
            {
                while (!_settings.Controller.ReConnectDevice())
                {
                    Thread.Sleep(100);
                }

                SetControllerToCurrentViewGroup();
                SetStateLedsOnController();
                SendMessage("Midi controller reconnected.", false);
            }).Start();
        }

        private void WebsocketReconnectionHappened(ReconnectionInfo info)
        {
            _settings.Controller.WriteTextToLCD("UI24R is reconnected",5);
        }

        private void WebsocketDisconnectionHappened(DisconnectionInfo info)
        {
            _settings.Controller.WriteTextToLCD("UI24R disconnected. Try to reconnect");
        }

        private void _midiController_RecEvent(object sender, EventArgs e)
        {
            if (_settings.RecButtonBehavior == BridgeSettings.RecButtonBehaviorEnum.OnlyMTK ||
                _settings.RecButtonBehavior == BridgeSettings.RecButtonBehaviorEnum.TwoTrackAndMTK)
            {
                if (!_mixer.IsMultitrackRecordingRun)
                {
                    _client.Send(_mixer.GetStartMTKRecordMessage());
                    _mixer.IsMultitrackRecordingRun = true;
                }
                else
                {
                    _client.Send(_mixer.GetStopMTKRecordMessage());
                    _mixer.IsMultitrackRecordingRun = false;
                }
            }
            if (_settings.RecButtonBehavior == BridgeSettings.RecButtonBehaviorEnum.OnlyTwoTrack ||
              _settings.RecButtonBehavior == BridgeSettings.RecButtonBehaviorEnum.TwoTrackAndMTK)
            {
                if (!_mixer.IsTwoTrackRecordingRun)
                {
                    _client.Send(_mixer.GetStartRecordMessage());
                    _mixer.IsTwoTrackRecordingRun = true;
                }
                else
                {
                    _client.Send(_mixer.GetStopRecordMessage());
                    _mixer.IsTwoTrackRecordingRun = false;
                }
            }
        }

        private void InitializeViewGroups()
        {
            if (File.Exists(CONFIGFILE_VIEW_GROUP))
            {
                var jsonString = File.ReadAllText(CONFIGFILE_VIEW_GROUP);
                _viewViewGroups = JsonSerializer.Deserialize<int[][]>(jsonString);
                _hasViewGroupConfig = true;
            }
            else
            {
                _viewViewGroups = new int[][]{
                    new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 54 },
                    new int[] { 8, 9, 10, 11, 12, 13, 14, 15, 54 },
                    new int[] { 16, 17, 18, 19, 20, 21, 22, 23, 54 },
                    new int[] { 24, 25, 26, 27, 28, 29, 30, 31, 54 },
                    new int[] { 32, 33, 34, 35, 36, 37, 38, 39, 54 },
                    new int[] { 40, 41, 42, 43, 44, 45, 46, 47, 54 }
                };
            }
        }

        #region Midicontroller events

        private void _midiController_SaveEvent(object sender, EventArgs e)
        {
            string jsonString;
            jsonString = JsonSerializer.Serialize(_viewViewGroups);
            File.WriteAllText(CONFIGFILE_VIEW_GROUP, jsonString);
        }
        private void _midiController_RecChannelEvent(object sender, MIDIController.ChannelEventArgs e)
        {
            var ch = _viewViewGroups[_selectedViewGroup][e.ChannelNumber];
            if (_settings.ChannelRecButtonBehavior == BridgeSettings.ChannelRecButtonBehaviorEnum.Rec )
            {
                if (_mixerChannels[ch] is IRecordable)
                {
                    var channel = _mixerChannels[ch] as IRecordable;
                    channel.IsRec = !channel.IsRec;
                    _client.Send(channel.RecMessage());
                    _settings.Controller.SetRecLed(e.ChannelNumber, channel.IsRec);
                }
            }
            else //phantom
            {
                if (_mixerChannels[ch] is IPhantomable)
                {
                    var channel = _mixerChannels[ch] as IPhantomable;
                    channel.IsPhantom = !channel.IsPhantom;
                    _client.Send(channel.PhantomMessage());
                    _settings.Controller.SetRecLed(e.ChannelNumber, channel.IsPhantom);
                }
            }

        }

        private void _midiController_SoloChannelEvent(object sender, MIDIController.ChannelEventArgs e)
        {
            var ch = _viewViewGroups[_selectedViewGroup][e.ChannelNumber];
            _mixerChannels[ch].IsSolo = !_mixerChannels[ch].IsSolo;
            _client.Send(_mixerChannels[ch].SoloMessage());
            _settings.Controller.SetSoloLed(e.ChannelNumber, _mixerChannels[ch].IsSolo);
            //if the channel is linked we have to set the other channel to the same value
            if (_mixerChannels[ch] is IStereoLinkable)
            {
                var stereoChannel = _mixerChannels[ch] as IStereoLinkable;
                if (stereoChannel.LinkedWith != -1)
                {
                    var otherCh = stereoChannel.LinkedWith == 0 ? ch + 1 : ch - 1;
                    _mixerChannels[otherCh].IsSolo = !_mixerChannels[otherCh].IsSolo;
                    _client.Send(_mixerChannels[otherCh].SoloMessage());
                    //If the other chanel is on the current layout we have to set too
                    (var otherChOnLayer, var isOnLayer) = GetControllerChannel(otherCh);
                    if (isOnLayer)
                    {
                        _settings.Controller.SetSoloLed(otherChOnLayer, _mixerChannels[otherCh].IsSolo);
                    }
                }
            }
        }

        private void _midiController_MuteChannelEvent(object sender, MIDIController.ChannelEventArgs e)
        {
            var ch = _viewViewGroups[_selectedViewGroup][e.ChannelNumber];
            _mixerChannels[ch].IsMute = !_mixerChannels[ch].IsMute;
            _client.Send(_mixerChannels[ch].MuteMessage());
            _settings.Controller.SetMuteLed(e.ChannelNumber, _mixerChannels[ch].IsMute);
            //if the channel is linked we have to set the other channel to the same value
            if (_mixerChannels[ch] is IStereoLinkable)
            {
                var stereoChannel = _mixerChannels[ch] as IStereoLinkable;
                if (stereoChannel.LinkedWith != -1)
                {
                    var otherCh = stereoChannel.LinkedWith == 0 ? ch + 1 : ch - 1;
                    _mixerChannels[otherCh].IsMute = !_mixerChannels[otherCh].IsMute;
                    _client.Send(_mixerChannels[otherCh].MuteMessage());
                    //If the other chanel is on the current layout we have to set too
                    (var otherChOnLayer, var isOnLayer) = GetControllerChannel(otherCh);
                    if (isOnLayer)
                    {
                        _settings.Controller.SetMuteLed(otherChOnLayer, _mixerChannels[otherCh].IsMute);
                    }
                }
            }
        }

        private void _midiController_SelectChannelEvent(object sender, MIDIController.ChannelEventArgs e)
        {
            var ch = _viewViewGroups[_selectedViewGroup][e.ChannelNumber];
            if (_selectedChannel != -1)
            {
                _mixerChannels[_selectedChannel].IsSelected = false;
            }
            _mixerChannels[ch].IsSelected = true;
            _selectedChannel = ch;
            _settings.Controller.SetSelectLed(e.ChannelNumber, true);
            _client.Send(_mixerChannels[ch].SelectChannelMessage(_settings.SyncID));
        }

        private void _midiController_GainEvent(object sender, MIDIController.GainEventArgs e)
        {
            var ch = _viewViewGroups[_selectedViewGroup][e.ChannelNumber];
            if (_mixerChannels[ch] is InputChannel)
            {
                var inputChannel = _mixerChannels[ch] as InputChannel;
                inputChannel.Gain = inputChannel.Gain + (1.0d / 100.0d) * e.GainDirection;
                if (inputChannel.Gain > 1)
                    inputChannel.Gain = 1;
                if (inputChannel.Gain < 0)
                    inputChannel.Gain = 0;
                _client.Send(inputChannel.GainMessage());
                _settings.Controller.SetGainLed(e.ChannelNumber, inputChannel.Gain);
            }
        }
        private void _midiController_PresetDown(object sender, EventArgs e)
        {
            if (_selectedViewGroup > 0)
                _selectedViewGroup = (_selectedViewGroup - 1) % _viewViewGroups.Length;
            SetControllerToCurrentViewGroup();
        }


        private void _midiController_PresetUp(object sender, EventArgs e)
        {
            if (_selectedViewGroup < _viewViewGroups.Length-1)
                _selectedViewGroup = (_selectedViewGroup + 1) % _viewViewGroups.Length;
            SetControllerToCurrentViewGroup();
        }

        private void _midiController_FaderEvent(object sender, MIDIController.FaderEventArgs e)
        {
            var ch = _viewViewGroups[_selectedViewGroup][e.ChannelNumber];
            //if (_pressedFunctionButton == -1)
            if (_selectedLayout == SelectedLayoutEnum.Channels)
                {
                    _mixerChannels[ch].ChannelFaderValue = e.FaderValue;
                _client.Send(_mixerChannels[ch].MixFaderMessage());
                //if the channel is linked we have to set the other channel to the same value
                if (_mixerChannels[ch] is IStereoLinkable)
                {
                    var stereoChannel = _mixerChannels[ch] as IStereoLinkable;
                    if (stereoChannel.LinkedWith != -1)
                    {
                        var otherCh = stereoChannel.LinkedWith == 0 ? ch + 1 : ch - 1;
                        _mixerChannels[otherCh].ChannelFaderValue = e.FaderValue;
                        _client.Send(_mixerChannels[otherCh].MixFaderMessage());
                        //If the other chanel is on the current layout we have to set too
                        (var otherChOnLayer, var isOnLayer) = GetControllerChannel(otherCh);
                        if (isOnLayer)
                        {
                            _settings.Controller.SetFader(otherChOnLayer, e.FaderValue);
                        }
                    }
                }
            }
            else
            {
                //_mixerChannels[ch].AuxSendValues[_pressedFunctionButton] = e.FaderValue;
                _mixerChannels[ch].AuxSendValues[_selectedLayout] = e.FaderValue;
                if (_selectedLayout.IsAux())
                {
                    _client.Send(_mixerChannels[ch].SetAuxValueMessage(_selectedLayout));
                }
                else if (_selectedLayout.IsFx())
                {
                    _client.Send(_mixerChannels[ch].SetFxValueMessage(_selectedLayout));
                }
            }
        }

        #endregion

        private void InitializeChannels()
        { 
            _mixerChannels = new List<ChannelBase>();
            for (int i=0; i<24; i++)
            {
                _mixerChannels.Add(new InputChannel(i));
            }
            _mixerChannels.Add(new LineInChannel(0));
            _mixerChannels.Add(new LineInChannel(1));
            _mixerChannels.Add(new PlayerChannel(0));
            _mixerChannels.Add(new PlayerChannel(1));
            _mixerChannels.Add(new FXChannel(0));
            _mixerChannels.Add(new FXChannel(1));
            _mixerChannels.Add(new FXChannel(2));
            _mixerChannels.Add(new FXChannel(3));
            for (int i=0; i<6; i++)
            {
                _mixerChannels.Add(new SubgroupChannel(i));
            }
            for (int i = 0; i < 10; i++)
            {
                _mixerChannels.Add(new AuxChannel(i));
            }
            for (int i = 0; i < 6; i++)
            {
                _mixerChannels.Add(new VCAChannel(i));
            }
            _mixerChannels.Add(new MainChannel());
        }



        protected void UI24RMessageReceived(ResponseMessage msg)
        {
            if (msg.Text.Length > 3)
            {
                SendMessage(msg.Text);
                ProcessUI24Message(msg.Text);
            }
            else
            {
                _client.Send(msg.Text);
                _client.Send("3:::ALIVE");
            }
        }

        private void SetControllerToCurrentViewGroup()
        {
            var channels =  _viewViewGroups[_selectedViewGroup].Select((item, i) => new { Channel = item, controllerChannelNumber = i });
            _settings.Controller.SetSelectLed(0, false); //turn off all
            foreach (var ch in channels)
            {
                var channelNumber = ch.Channel;

                if (_selectedLayout == SelectedLayoutEnum.Channels  )
                {
                    _settings.Controller.SetFader(ch.controllerChannelNumber, _mixerChannels[channelNumber].ChannelFaderValue);
                }
                else
                {
                    if (!(_mixerChannels[channelNumber] is MainChannel))
                    {
                        _settings.Controller.SetFader(ch.controllerChannelNumber, _mixerChannels[channelNumber].AuxSendValues[_selectedLayout]);
                    }
                }

                if (_mixerChannels[channelNumber].IsSelected)
                {
                    _settings.Controller.SetSelectLed(ch.controllerChannelNumber, true);
                }

                _settings.Controller.SetGainLed(ch.controllerChannelNumber, _mixerChannels[channelNumber].Gain);
                _settings.Controller.WriteTextToChannelLCD(ch.controllerChannelNumber, _mixerChannels[channelNumber].Name);
                _settings.Controller.SetMuteLed(ch.controllerChannelNumber, _mixerChannels[channelNumber].IsMute);
                _settings.Controller.SetSoloLed(ch.controllerChannelNumber, _mixerChannels[channelNumber].IsSolo);

                if (_settings.ChannelRecButtonBehavior == BridgeSettings.ChannelRecButtonBehaviorEnum.Rec)
                {
                    if (_mixerChannels[channelNumber] is IRecordable)
                        _settings.Controller.SetRecLed(ch.controllerChannelNumber, (_mixerChannels[channelNumber] as IRecordable).IsRec);
                    else
                        _settings.Controller.SetRecLed(ch.controllerChannelNumber, false);
                }
                else //phantom
                {
                    if (_mixerChannels[channelNumber] is IPhantomable)
                        _settings.Controller.SetRecLed(ch.controllerChannelNumber, (_mixerChannels[channelNumber] as IPhantomable).IsPhantom);
                    else
                        _settings.Controller.SetRecLed(ch.controllerChannelNumber, false);
                }

            }
        }

        private void SetStateLedsOnController()
        {
            _settings.Controller.SetLed(ButtonsEnum.Rec, _mixer.IsMultitrackRecordingRun || _mixer.IsTwoTrackRecordingRun);
        }

        protected (int, bool) GetControllerChannel(int ch)
        {
            var controllerChannelNumber = _viewViewGroups[_selectedViewGroup].Select((item, i) => new { Channel = item, controllerChannelNumber = i })
                .Where(c => c.Channel == ch).FirstOrDefault();
            if (controllerChannelNumber != null)
                return (controllerChannelNumber.controllerChannelNumber, true);
            else
                return (0,false);
        }

        private void ProcessUI24Message(string text)
        {
            var messageparts = text.Replace("3:::","").Split('\n');
            foreach (var m in messageparts)
            {
                var ui24Message = new UI24Message(m);
                if (ui24Message.IsValid)
                {
                    (var controllerChannelNumber, var isOnLayer) = GetControllerChannel(ui24Message.ChannelNumber);

                    switch (ui24Message.MessageType)
                    {
                        case MessageTypeEnum.mix:
                            _mixerChannels[ui24Message.ChannelNumber].ChannelFaderValue = ui24Message.FaderValue;
                            if (ui24Message.IsValid && isOnLayer && controllerChannelNumber <= 8)
                            {
                                _settings.Controller.SetFader(controllerChannelNumber, ui24Message.FaderValue);
                            }
                            break;
                        case MessageTypeEnum.name:
                            _mixerChannels[ui24Message.ChannelNumber].Name = ui24Message.ChannelName;
                            if (ui24Message.IsValid && isOnLayer && controllerChannelNumber < 8)
                            {
                                _settings.Controller.WriteTextToChannelLCD(controllerChannelNumber, _mixerChannels[ui24Message.ChannelNumber].Name);
                            }
                            break;
                        case MessageTypeEnum.gain:
                            if (_mixerChannels[ui24Message.ChannelNumber] is InputChannel && ui24Message.ChannelType == ChannelTypeEnum.HW)
                            {
                                (_mixerChannels[ui24Message.ChannelNumber] as InputChannel).Gain = ui24Message.Gain;
                                if (ui24Message.IsValid && isOnLayer && controllerChannelNumber < 8)
                                {
                                    _settings.Controller.SetGainLed(controllerChannelNumber, ui24Message.Gain);
                                }
                            }
                            break;
                        case MessageTypeEnum.mute:
                            _mixerChannels[ui24Message.ChannelNumber].IsMute = ui24Message.LogicValue;
                            if (ui24Message.IsValid && isOnLayer && controllerChannelNumber < 8)
                            {
                                _settings.Controller.SetMuteLed(controllerChannelNumber, ui24Message.LogicValue);
                            }
                            break;
                        case MessageTypeEnum.solo:
                            _mixerChannels[ui24Message.ChannelNumber].IsSolo = ui24Message.LogicValue;
                            if (ui24Message.IsValid && isOnLayer && controllerChannelNumber < 8)
                            {
                                _settings.Controller.SetSoloLed(controllerChannelNumber, ui24Message.LogicValue);
                            }
                            break;
                        case MessageTypeEnum.mtkrec:
                            if (_mixerChannels[ui24Message.ChannelNumber] is IRecordable && _settings.ChannelRecButtonBehavior == BridgeSettings.ChannelRecButtonBehaviorEnum.Rec)
                            {
                                (_mixerChannels[ui24Message.ChannelNumber] as IRecordable).IsRec = ui24Message.LogicValue;
                                if (ui24Message.IsValid && isOnLayer && controllerChannelNumber < 8)
                                {
                                    _settings.Controller.SetRecLed(controllerChannelNumber, ui24Message.LogicValue);
                                }
                            }
                            break;
                        case MessageTypeEnum.phantom:
                            if (_mixerChannels[ui24Message.ChannelNumber] is IPhantomable && _settings.ChannelRecButtonBehavior == BridgeSettings.ChannelRecButtonBehaviorEnum.Phantom)
                            {
                                (_mixerChannels[ui24Message.ChannelNumber] as IPhantomable).IsPhantom = ui24Message.LogicValue;
                                if (ui24Message.IsValid && isOnLayer && controllerChannelNumber < 8)
                                {
                                    _settings.Controller.SetRecLed(controllerChannelNumber, ui24Message.LogicValue);
                                }
                            }
                            break;
                        case MessageTypeEnum.stereoIndex:
                            if (_mixerChannels[ui24Message.ChannelNumber] is IStereoLinkable)
                            {
                                var stereoChannel = _mixerChannels[ui24Message.ChannelNumber] as IStereoLinkable;
                                stereoChannel.LinkedWith = ui24Message.IntValue;
                            }
                            break;
                        case MessageTypeEnum.mtk:
                            if (ui24Message.SystemVarType == SystemVarTypeEnum.MtkRecCurrentState)
                            {
                                if (_settings.RecButtonBehavior == BridgeSettings.RecButtonBehaviorEnum.OnlyMTK || _settings.RecButtonBehavior == BridgeSettings.RecButtonBehaviorEnum.TwoTrackAndMTK)
                                {
                                    _settings.Controller.SetLed(ButtonsEnum.Rec, ui24Message.LogicValue || _mixer.IsTwoTrackRecordingRun);
                                    _mixer.IsMultitrackRecordingRun = ui24Message.LogicValue;
                                }
                            }
                            break;
                        case MessageTypeEnum.isRecording:
                            if (_settings.RecButtonBehavior == BridgeSettings.RecButtonBehaviorEnum.OnlyTwoTrack || _settings.RecButtonBehavior == BridgeSettings.RecButtonBehaviorEnum.TwoTrackAndMTK)
                            {
                                _settings.Controller.SetLed(ButtonsEnum.Rec, ui24Message.LogicValue || _mixer.IsMultitrackRecordingRun);
                                _mixer.IsTwoTrackRecordingRun = ui24Message.LogicValue;
                            }
                            break;
                        case MessageTypeEnum.currentState:
                                _settings.Controller.SetLed(ButtonsEnum.Play, ui24Message.LogicValue);
                            break;
                        case MessageTypeEnum.auxFaderValue:
                            _mixerChannels[ui24Message.ChannelNumber].AuxSendValues[ui24Message.IntValue.ToAux()] = ui24Message.FaderValue;
                            if (isOnLayer && controllerChannelNumber < 8 && _selectedLayout == ui24Message.IntValue.ToAux())
                            {
                                _settings.Controller.SetFader(controllerChannelNumber, ui24Message.FaderValue);
                            }
                            break;
                        case MessageTypeEnum.fxFaderValue:
                            _mixerChannels[ui24Message.ChannelNumber].AuxSendValues[ui24Message.IntValue.ToFx()] = ui24Message.FaderValue;
                            if (isOnLayer && controllerChannelNumber < 8 && _selectedLayout == ui24Message.IntValue.ToFx())
                            {
                                _settings.Controller.SetFader(controllerChannelNumber, ui24Message.FaderValue);
                            }
                            break;
                    }
                }
                else if (m.StartsWith("SETS^vg.")) //first global view group (e.g:"SETS^vg.0^[0,1,2,3,4,5,6,17,18,19,20,22,23,38,39,40,41,42,43,44,45,48,49,21]")
                {
                    if (!_hasViewGroupConfig)
                    {
                        var parts = m.Split('^');
                        var newViewChannel = parts.Last().Trim('[', ']').Split(',');
                        var viewGroupString = parts[1].Split('.').Last();
                        int viewGroup;
                        if (int.TryParse(viewGroupString, out viewGroup))
                        {
                            if (newViewChannel.Length > 7)
                            {
                                var newViewChannelWithMain = newViewChannel.ToList().Take(8).ToList();
                                newViewChannelWithMain.Add("54");
                                _viewViewGroups[viewGroup] = newViewChannelWithMain.Select(i => int.Parse(i)).ToArray();
                            }
                        }
                    }
                }
                else if (m.StartsWith($"BMSG^SYNC^{_settings.SyncID}"))
                {
                    int ch;
                    var chString = m.Split("^").Last();
                    if (int.TryParse(chString, out ch))
                    {
                        if (_selectedChannel != -1)
                            _mixerChannels[_selectedChannel].IsSelected = false;
                        if (ch == -1) //main channel
                        {
                            _mixerChannels[54].IsSelected = true;
                            _selectedChannel = 54;
                        }
                        else
                        {
                            _mixerChannels[ch].IsSelected = true;
                            _selectedChannel = ch;
                        }
                        var channelNumber = _viewViewGroups[_selectedViewGroup].Select((item, i) => new { Channel = item, controllerChannelNumber = i })
                           .Where(c => c.Channel == _mixerChannels[_selectedChannel].ChannelNumberInMixer).FirstOrDefault();
                        if (channelNumber != null)
                        {
                            _settings.Controller.SetSelectLed(channelNumber.controllerChannelNumber, true);
                        }
                        else
                            _settings.Controller.SetSelectLed(0, false);
                    }
                }
                else if (m.StartsWith("VU2^"))
                {
                    var ui24rvumessage = new UI24RVUMessage(m);
                    for (int i = 0; i < 8; i++)
                    {
                       var channelNumber =  _viewViewGroups[_selectedViewGroup][i];
                        if (channelNumber < 48) //input channel
                        {
                            _settings.Controller.WriteChannelMeter(i, ui24rvumessage.VUChannels[channelNumber].GetPostValue());
                        }
                    }
                }

            }
        }

        protected void SendMessage(string message, bool isDebug = true)
        {
            if (_settings.MessageWriter != null)
                _settings.MessageWriter(message, isDebug);
        }

        public void Dispose()
        {
            SendMessage("Disconnecting UI24R....", false);
            if (_client != null)
            {
                _client.Dispose();
                _client = null;
            }
            if ((_settings.Controller is IDisposable) && _settings.Controller != null)
            {
                (_settings.Controller as IDisposable).Dispose();
            }
        }
    }
}
