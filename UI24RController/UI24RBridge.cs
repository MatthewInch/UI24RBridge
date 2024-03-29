﻿using System;
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

        protected BridgeSettings _settings;
        protected WebsocketClient _client;

        protected UI24RBridge _secondaryBridge; //Represent the secondary controller that can be controlled by the current instance
   
        protected int _selectedChannel = -1; //-1 = no selected channel
        //protected int _pressedFunctionButton = -1; //-1 = no pressed button
        protected SelectedLayoutEnum _selectedLayout = SelectedLayoutEnum.Channels;

        protected bool _isReconnecting = false;
 
        /// <summary>
        /// Represent the UI24R mixer state
        /// TODO: need to move every global variable that store any mixer specific state to the Mixer class (viewGroups, selectedChannel etc.) 
        /// </summary>
        protected Mixer _mixer = new Mixer();

        //0-23: Input channels
        //24-25: Linie In L/R
        //26-27: Player L/R
        //28-31: FX channels
        //32-37: Subgroups
        //38-47: AUX 1-10
        //48-53: VCA 1-6
        //54: Main

        /// <summary>
        /// Contains the channels of the mixer. the channel number like the view groups 0-23 input channels, 24-25 Line in etc.
        /// </summary>
        protected List<ChannelBase> _mixerChannels;



        /// <summary>
        ///  Represent the bridge between the UI24R and a DAW controller
        /// </summary>
        public UI24RBridge(BridgeSettings settings, UI24RBridge secondaryBridge = null)
        {
            this._settings = settings;
            SendMessage("Start initialization...", false);
            InitializeChannels();
            InitializeViewGroupsFromConfig();
            //get buttons value from settings file
            var buttonSettingsFromFile = settings.GetButtonsValues(); 
            //update initial values if in the settings file it overwrited
            foreach (KeyValuePair<ButtonsEnum, byte> button in buttonSettingsFromFile)
            {
                ButtonsID.Instance.ButtonsDictionary[button.Key] = button.Value; 
            }

            _secondaryBridge = secondaryBridge;

            SendMessage("Create controller events....", false);
            _settings.Controller.FaderEvent += _midiController_FaderEvent;
            _settings.Controller.BankUp += _midiController_BankUp;
            _settings.Controller.BankDown += _midiController_BankDown;
            _settings.Controller.LayerUp += _midiController_LayerUp;
            _settings.Controller.LayerDown += _midiController_LayerDown;
            _settings.Controller.KnobEvent += _midiController_KnobEvent;
            _settings.Controller.MuteChannelEvent += _midiController_MuteChannelEvent;
            _settings.Controller.SoloChannelEvent += _midiController_SoloChannelEvent;
            _settings.Controller.SelectChannelEvent += _midiController_SelectChannelEvent;
            _settings.Controller.RecChannelEvent += _midiController_RecChannelEvent;
            _settings.Controller.MuteGroupButtonEvent += Controller_MuteGroupButtonEvent;
            _settings.Controller.SaveEvent += _midiController_MuteAllEvent;
            _settings.Controller.UndoEvent += _midiController_MuteAllFxEvent;
            _settings.Controller.CancelEvent += _midiController_ClearMute;
            _settings.Controller.EnterEvent += _midiController_ClearSolo;
            _settings.Controller.RecEvent += _midiController_RecEvent;
            _settings.Controller.PlayEvent += _midiController_PlayEvent;
            _settings.Controller.StopEvent += _midiController_StopEvent;
            _settings.Controller.NextEvent += _midiController_NextEvent;
            _settings.Controller.PrevEvent += _midiController_PrevEvent;
            _settings.Controller.UserBtnEvent += _midiController_UserLayerEdit;
            _settings.Controller.GlobalViewEvent += _midiController_SaveEvent;
            _settings.Controller.WheelEvent += _midiController_WheelEvent;
            _settings.Controller.SmtpeBeatsBtnEvent += _midiController_TapTempoEvent;
            _settings.Controller.WriteTextToLCDSecondLine("");
            _settings.Controller.ConnectionErrorEvent += _midiController_ConnectionErrorEvent;
            _settings.Controller.AuxButtonEvent += _midiController_AuxButtonEvent;
            _settings.Controller.FxButtonEvent += Controller_FXButtonEvent;
             if (!_settings.Controller.IsConnected)
            {
                _midiController_ConnectionErrorEvent(this, null);
            }
            _settings.Controller.InitializeController(new ControllerSettings(_settings.ControllerIsExtender));
            _mixer.setBank(_settings.StartBank);
            SendMessage("Start websocket connection...", false);
            _client = new WebsocketClient(new Uri(_settings.Address));
            _client.MessageReceived.Subscribe(msg => UI24RMessageReceived(msg));
            _client.DisconnectionHappened.Subscribe(info => WebsocketDisconnectionHappened(info));
            _client.ReconnectionHappened.Subscribe(info => WebsocketReconnectionHappened(info));
            _client.ErrorReconnectTimeout = new TimeSpan(0,0,10);
            SendMessage("Connecting to UI24R....", false);
            _client.Start();
            _settings.Controller.WriteTextToAssignmentDisplay(_mixer.getCurrentLayerString());
            if (settings.ControllerStartChannel != null && settings.ControllerStartChannel == "1")
            {
                _mixer.setLayerUp();
            }
            
        }


        private void _midiController_ConnectionErrorEvent(object sender, EventArgs e)
        {
            SendMessage("Midi controller connection error.", false);
            SendMessage("Try to reconnect....", false);
            if (!_isReconnecting)
            {
                new Thread(() =>
                {
                    _isReconnecting = true;
                    while (_isReconnecting && !_settings.Controller.ReConnectDevice())
                    {
                        Thread.Sleep(100);
                    }

                    SetControllerToCurrentLayerAndSend();
                    SetStateLedsOnController();

                    SendMessage("Midi controller reconnected.", false);
                    _isReconnecting = false;
                }).Start();
            }
            else
            {
                SendMessage("Reconnection state is true...");
            }
        }
        private void WebsocketReconnectionHappened(ReconnectionInfo info)
        {
            _settings.Controller.WriteTextToLCDSecondLine("UI24R is reconnected",5);
        }
        private void WebsocketDisconnectionHappened(DisconnectionInfo info)
        {
            _settings.Controller.WriteTextToLCDSecondLine("UI24R disconnected. Try to reconnect");
        }
        private void InitializeViewGroupsFromConfig()
        {
            if (File.Exists(CONFIGFILE_VIEW_GROUP))
            {
                var jsonString = File.ReadAllText(CONFIGFILE_VIEW_GROUP);
                int[][] viewViewGroups = JsonSerializer.Deserialize<int[][]>(jsonString);
                _mixer.setUserLayerFromArray(viewViewGroups);
            }
        }

        #region Midicontroller events

        private void _midiController_RecChannelEvent(object sender, MIDIController.ChannelEventArgs e)
        {
            var ch = _mixer.getChannelNumberInCurrentLayer(e.ChannelNumber);
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
                if (_mixerChannels[ch] is IInputable)
                {
                    var currentChannel = _mixerChannels[ch] as IInputable;
                    if (currentChannel.SrcType == SrcTypeEnum.Hw)
                    {
                        currentChannel.IsPhantom = !currentChannel.IsPhantom;
                        _client.Send(currentChannel.PhantomMessage());
                        _settings.Controller.SetRecLed(e.ChannelNumber, currentChannel.IsPhantom);

                        //if multiple channels are set to same HW input
                        foreach (var singleChannel in _mixerChannels)
                        {
                            if (singleChannel is IInputable)
                            {
                                var singleInputChannel = singleChannel as IInputable;
                                if (singleInputChannel.SrcType == SrcTypeEnum.Hw && singleInputChannel.SrcNumber == currentChannel.SrcNumber)
                                {
                                    singleInputChannel.IsPhantom = currentChannel.IsPhantom;
                                    for (int i = 0; i < 8; ++i)
                                        if (singleInputChannel == _mixerChannels[_mixer.getChannelNumberInCurrentLayer(i)])
                                            _settings.Controller.SetRecLed(i, currentChannel.IsPhantom);
                                }
                            }
                        }
                    }
                }
            }

        }

        private void _midiController_SoloChannelEvent(object sender, MIDIController.ChannelEventArgs e)
        {
            var ch = _mixer.getChannelNumberInCurrentLayer(e.ChannelNumber);
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
            var ch = _mixer.getChannelNumberInCurrentLayer(e.ChannelNumber);
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
            SetControllerMuteButtonsForCurrentLayer();
        }
        private void _midiController_SelectChannelEvent(object sender, MIDIController.ChannelEventArgs e)
        {
            var ch = _mixer.getChannelNumberInCurrentLayer(e.ChannelNumber);
            if (_selectedChannel != -1)
            {
                _mixerChannels[_selectedChannel].IsSelected = false;
            }
            _mixerChannels[ch].IsSelected = true;
            _selectedChannel = ch;
            _settings.Controller.SetSelectLed(e.ChannelNumber, true);
            _client.Send(_mixerChannels[ch].SelectChannelMessage(_settings.SyncID));
        }
        private void _midiController_KnobEvent(object sender, MIDIController.KnobEventArgs e)
        {
            var ch = _mixer.getChannelNumberInCurrentLayer(e.ChannelNumber);
            if (_mixerChannels[ch] is IInputable)
            {
                var currentChannel = _mixerChannels[ch] as IInputable;
                if(currentChannel.SrcType == SrcTypeEnum.Hw)
                {
                    currentChannel.Gain = currentChannel.Gain + (1.0d / 100.0d) * e.KnobDirection;
                    if (currentChannel.Gain > 1)
                        currentChannel.Gain = 1;
                    if (currentChannel.Gain < 0)
                        currentChannel.Gain = 0;

                    _client.Send(currentChannel.GainMessage());
                    _settings.Controller.SetGainLed(e.ChannelNumber, currentChannel.Gain);
                    //if multiple channels are set to same HW input
                    foreach (var singleChannel in _mixerChannels)
                    {
                        if (singleChannel is IInputable)
                        {
                            var singleInputChannel = singleChannel as IInputable;
                            if (singleInputChannel.SrcType == SrcTypeEnum.Hw && singleInputChannel.SrcNumber == currentChannel.SrcNumber)
                            {
                                singleInputChannel.Gain = currentChannel.Gain;
                                for(int i=0;i<8; ++i)
                                    if(singleInputChannel == _mixerChannels[_mixer.getChannelNumberInCurrentLayer(i)])
                                        _settings.Controller.SetGainLed(i, currentChannel.Gain);
                            }
                        }
                    }

                }
            }
        }

        public void _midiController_LayerDown(object sender, EventArgs e)
        {
            _mixer.setLayerDown();
            SetControllerToCurrentLayerAndSend();
            if (_secondaryBridge != null)
            {
                _secondaryBridge._midiController_LayerDown(sender, e);
            }
        }
        public void _midiController_LayerUp(object sender, EventArgs e)
        {
            _mixer.setLayerUp();
            SetControllerToCurrentLayerAndSend();
            if (_secondaryBridge != null)
            {
                _secondaryBridge._midiController_LayerUp(sender, e);
            }
        }
        public void _midiController_BankDown(object sender, EventArgs e)
        {
            _mixer.setBankDown();
            SetControllerToCurrentLayerAndSend();
            if (_secondaryBridge != null)
            {
                _secondaryBridge._midiController_BankDown(sender, e);
            }
        }
        public void _midiController_BankUp(object sender, EventArgs e)
        {
            _mixer.setBankUp();
            SetControllerToCurrentLayerAndSend();
            if (_settings.ControllerStartChannel != null && _settings.ControllerStartChannel == "1")
            {
                _mixer.setLayerUp();
            }
            if (_secondaryBridge != null)
            {
                _secondaryBridge._midiController_BankUp(sender, e);
            }
        }

        private void _midiController_FaderEvent(object sender, MIDIController.FaderEventArgs e)
        {
            var ch = _mixer.getChannelNumberInCurrentLayer(e.ChannelNumber);
            //if (_pressedFunctionButton == -1) or it is a master fader
            if ((_selectedLayout == SelectedLayoutEnum.Channels) || (_mixerChannels[ch] is MainChannel))
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

        public void _midiController_AuxButtonEvent(object sender, MIDIController.FunctionEventArgs e)
        {
            if (_settings.AuxButtonBehavior == BridgeSettings.AuxButtonBehaviorEnum.Release)
            {
                if (e.IsPress)
                {
                    _selectedLayout = e.FunctionButton.ToAux();
                    _settings.Controller.SetLed(_selectedLayout.ToButtonsEnum(), true);
                    _settings.Controller.WriteTextToBarsDisplay("AX" + (_selectedLayout.AuxToInt() + 1).ToString());
                }
                else
                {
                    _settings.Controller.SetLed(_selectedLayout.ToButtonsEnum(), false);
                    _selectedLayout = SelectedLayoutEnum.Channels;
                    _settings.Controller.WriteTextToBarsDisplay("   ");
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
                        _settings.Controller.WriteTextToBarsDisplay("   ");
                    }
                    else
                    {
                        _settings.Controller.SetLed(_selectedLayout.ToButtonsEnum(), false);
                        _selectedLayout = e.FunctionButton.ToAux();
                        _settings.Controller.SetLed(_selectedLayout.ToButtonsEnum(), true);
                        _settings.Controller.WriteTextToBarsDisplay("AX" + (_selectedLayout.AuxToInt()+1).ToString());
                    }
                }
            }
            SetControllerToCurrentLayerAndSend();
            if (_secondaryBridge != null)
            {
                _secondaryBridge._midiController_AuxButtonEvent(sender, e);
            }
        }
        public void Controller_FXButtonEvent(object sender, FunctionEventArgs e)
        {
            if (_settings.AuxButtonBehavior == BridgeSettings.AuxButtonBehaviorEnum.Release)
            {
                if (e.IsPress)
                {
                    _selectedLayout = e.FunctionButton.ToFx();
                    _settings.Controller.SetLed(_selectedLayout.ToButtonsEnum(), true);
                    _settings.Controller.WriteTextToBarsDisplay("FX" + (_selectedLayout.FxToInt() + 1).ToString());
                }
                else
                {
                    _settings.Controller.SetLed(_selectedLayout.ToButtonsEnum(), false);
                    _selectedLayout = SelectedLayoutEnum.Channels;
                    _settings.Controller.WriteTextToBarsDisplay("   ");
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
                        _settings.Controller.WriteTextToBarsDisplay("   ");
                    }
                    else
                    {
                        _settings.Controller.SetLed(_selectedLayout.ToButtonsEnum(), false);
                        _selectedLayout = e.FunctionButton.ToFx();
                        _settings.Controller.SetLed(_selectedLayout.ToButtonsEnum(), true);
                        _settings.Controller.WriteTextToBarsDisplay("FX" + (_selectedLayout.FxToInt() + 1).ToString());
                    }
                }
            }
            SetControllerToCurrentLayerAndSend();
            if (_secondaryBridge != null)
            {
                _secondaryBridge.Controller_FXButtonEvent(sender, e);
            }
        }
        private void Controller_MuteGroupButtonEvent(object sender, FunctionEventArgs e)
        {
            if (e.IsPress)
            {
                _mixer.ToggleMuteGroup(e.FunctionButton);
                SetClientMute();
                SetControllerMuteButtonsForCurrentLayer();
            }
        }
        private void _midiController_MuteAllFxEvent(object sender, EventArgs e)
        {
            _mixer.ToggleMuteAllFx();
            SetClientMute();
            SetControllerMuteButtonsForCurrentLayer();
        }
        private void _midiController_MuteAllEvent(object sender, EventArgs e)
        {
            _mixer.ToggleMuteAll();
            SetClientMute();
            SetControllerMuteButtonsForCurrentLayer();
        }
        private void _midiController_ClearMute(object sender, EventArgs e)
        {
            _mixer.ClearMute();
            foreach (var ch in _mixerChannels)
            {
                if (ch.IsMute == true)
                {
                    ch.IsMute = false;
                    _client.Send(ch.MuteMessage());
                }
                ch.ForceUnMute = false;
            }
            SetClientMute();
            SetControllerMuteButtonsForCurrentLayer();
        }
        private void _midiController_ClearSolo(object sender, EventArgs e)
        {
            foreach (var ch in _mixerChannels)
            {
                if (ch.IsSolo == true)
                {
                    ch.IsSolo = false;
                    _client.Send(ch.SoloMessage());
                }
            }
            var channels = _mixer.getCurrentLayer().Select((item, i) => new { Channel = item, controllerChannelNumber = i });
            foreach (var ch in channels)
            {
                _settings.Controller.SetSoloLed(ch.controllerChannelNumber, _mixerChannels[ch.Channel].IsSolo);
            }
        }

        private void SetControllerMuteButtonsForCurrentLayer()
        {
            //why do we set back the mute of the client? I've moved that block to an independent mehod
            //SetClientMute();
            SetMuteGroupsLeds();
            var channels = _mixer.getCurrentLayer().Select((item, i) => new { Channel = item, controllerChannelNumber = i });
            foreach (var ch in channels)
            {
                _settings.Controller.SetMuteLed(ch.controllerChannelNumber, _mixerChannels[ch.Channel].IsMute);
            }
        }

        private void SetClientMute()
        {
            _client.Send(_mixer.GetMuteGroupsMessage());
            foreach (var ch in _mixerChannels)
            {
                if (ch.ForceUnMute == true)
                {
                    ch.ForceUnMute = false;
                    _client.Send(ch.ForceUnMuteMessage());
                    if (ch.IsMute == true)
                    {
                        ch.IsMute = false;
                        _client.Send(ch.MuteMessage());
                    }
                }
            }
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
        private void _midiController_UserLayerEdit(object sender, FunctionEventArgs e)
        {
            _mixer.UserLayerEdit = e.IsPress;
            _settings.Controller.SetLed(ButtonsEnum.User, e.IsPress);

            if (e.IsPress)
            {
                if (_mixer.UserLayerEdit && _selectedChannel != 54)
                {
                    //Go to user layer if necessary
                    if (_mixer.goToUserBank())
                    {
                        SetControllerToCurrentLayerAndSend();
                        _settings.Controller.WriteTextToAssignmentDisplay(_mixer.getCurrentLayerString());
                    }
                    //Set Select to First channel if necessary
                    bool isOnLayer = _mixer.getCurrentLayer().Where(x => x == _selectedChannel).Count() > 0;
                    if (!isOnLayer)
                    {
                        if (_selectedChannel != -1)
                        {
                            _mixerChannels[_selectedChannel].IsSelected = false;
                        }
                        _selectedChannel = _mixer.getCurrentLayer().FirstOrDefault();
                        _mixerChannels[_selectedChannel].IsSelected = true;
                        _settings.Controller.SetSelectLed(0, true);
                        _client.Send(_mixerChannels[_selectedChannel].SelectChannelMessage(_settings.SyncID));
                    }
                    _mixer.UserLayerEditNewChannel = _selectedChannel;
                }
            }
            else
            {
                int controllerPos = Array.IndexOf(_mixer.getCurrentLayer(), _selectedChannel);
                _mixer.setNewUserChannelInCurrentBank(controllerPos);
                _settings.Controller.WriteTextToChannelLCDSecondLine(controllerPos, "");
                SetControllerChannelToCurrentLayerAndSend(_mixer.UserLayerEditNewChannel, controllerPos);
            }


        }
        private void _midiController_WheelEvent(object sender, MIDIController.WheelEventArgs e)
        {
            if (_mixer.UserLayerEdit && _selectedChannel != 54)
            { 
                //selected layer must be at current layer
                bool isOnLayer = _mixer.getCurrentLayer().Where(x => x == _selectedChannel).Count() > 0;
                if (!isOnLayer)
                {
                    if (_selectedChannel != -1)
                    {
                        _mixerChannels[_selectedChannel].IsSelected = false;
                    }
                    _selectedChannel = _mixer.getCurrentLayer().FirstOrDefault();
                    _mixerChannels[_selectedChannel].IsSelected = true;
                    _settings.Controller.SetSelectLed(0, true);
                    _mixer.UserLayerEditNewChannel = _selectedChannel;
                    _client.Send(_mixerChannels[_selectedChannel].SelectChannelMessage(_settings.SyncID));
                }
                //find next channel not used on this layer
                int controllerPos = Array.IndexOf(_mixer.getCurrentLayer(), _selectedChannel);
                _mixer.findNextAvailableChannelForUserLayer(controllerPos, e.WheelDirection);
                _settings.Controller.WriteTextToChannelLCDSecondLine(controllerPos, _mixerChannels[_mixer.UserLayerEditNewChannel].Name);
            }
        }
        private void _midiController_SaveEvent(object sender, EventArgs e)
        {
            string jsonString;
            jsonString = JsonSerializer.Serialize(_mixer.getUserLayerToArray());
            File.WriteAllText(CONFIGFILE_VIEW_GROUP, jsonString);
        }
        private void _midiController_TapTempoEvent(object sender, EventArgs e)
        {
            int tempo = _mixer.TapTempo();
            if (tempo > 0)
            {
                for (int fxNum = 0; fxNum < 4; ++fxNum)
                    _client.Send(_mixer.GetStartMTKRecordMessage(fxNum, tempo));
                _settings.Controller.WriteTextToTicksDisplay(tempo.ToString().PadLeft(3, ' '));
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

        private void SetControllerChannelToCurrentLayerAndSend(int channelNumber, int controllerChannelNumber)
        {
            if (_selectedLayout == SelectedLayoutEnum.Channels)
            {
                _settings.Controller.SetFader(controllerChannelNumber, _mixerChannels[channelNumber].ChannelFaderValue);
            }
            else
            {
                if (!(_mixerChannels[channelNumber] is MainChannel))
                {
                    _settings.Controller.SetFader(controllerChannelNumber, _mixerChannels[channelNumber].AuxSendValues[_selectedLayout]);
                }
            }

            if (_mixerChannels[channelNumber].IsSelected)
            {
                _settings.Controller.SetSelectLed(controllerChannelNumber, true);
            }

            if (_mixerChannels[channelNumber] is IInputable)
            {
                var inputChannel = _mixerChannels[channelNumber] as IInputable;
                if (inputChannel.SrcType == SrcTypeEnum.Hw)
                    _settings.Controller.SetGainLed(controllerChannelNumber, inputChannel.Gain);
                else
                    _settings.Controller.SetGainLed(controllerChannelNumber, 0);
            }
            else
                _settings.Controller.SetGainLed(controllerChannelNumber, 0);

            _settings.Controller.WriteTextToChannelLCDFirstLine(controllerChannelNumber, _mixerChannels[channelNumber].Name);
            _settings.Controller.SetMuteLed(controllerChannelNumber, _mixerChannels[channelNumber].IsMute);
            _settings.Controller.SetSoloLed(controllerChannelNumber, _mixerChannels[channelNumber].IsSolo);

            if (_settings.ChannelRecButtonBehavior == BridgeSettings.ChannelRecButtonBehaviorEnum.Rec)
            {
                if (_mixerChannels[channelNumber] is IRecordable)
                    _settings.Controller.SetRecLed(controllerChannelNumber, (_mixerChannels[channelNumber] as IRecordable).IsRec);
                else
                    _settings.Controller.SetRecLed(controllerChannelNumber, false);
            }
            else //phantom
            {
                if (_mixerChannels[channelNumber] is IInputable)
                    _settings.Controller.SetRecLed(controllerChannelNumber, (_mixerChannels[channelNumber] as IInputable).IsPhantom);
                else
                    _settings.Controller.SetRecLed(controllerChannelNumber, false);
            }

        }
        private void SetControllerToCurrentLayerAndSend()
        {
            var channels =  _mixer.getCurrentLayer().Select((item, i) => new { Channel = item, controllerChannelNumber = i });
            _settings.Controller.SetSelectLed(0, false); //turn off all
            foreach (var ch in channels)
            {
                SetControllerChannelToCurrentLayerAndSend(ch.Channel, ch.controllerChannelNumber);
            }
            _settings.Controller.WriteTextToAssignmentDisplay(_mixer.getCurrentLayerString());
        }
        private void SetStateLedsOnController()
        {
            _settings.Controller.SetLed(ButtonsEnum.Rec, _mixer.IsMultitrackRecordingRun || _mixer.IsTwoTrackRecordingRun);
            SetMuteGroupsLeds();
            if (_selectedLayout.IsAux())
            {
                _settings.Controller.SetLed(_selectedLayout.ToButtonsEnum(), true);
                _settings.Controller.WriteTextToBarsDisplay("AX" + (_selectedLayout.AuxToInt() + 1).ToString());
            } 
            else if (_selectedLayout.IsFx())
            {
                _settings.Controller.SetLed(_selectedLayout.ToButtonsEnum(), true);
                _settings.Controller.WriteTextToBarsDisplay("FX" + (_selectedLayout.FxToInt() + 1).ToString());
            }
                
        }
        protected (int, bool) GetControllerChannel(int ch)
        {
            var controllerChannelNumber = _mixer.getCurrentLayer().Select((item, i) => new { Channel = item, controllerChannelNumber = i })
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
                                _settings.Controller.WriteTextToChannelLCDFirstLine(controllerChannelNumber, _mixerChannels[ui24Message.ChannelNumber].Name);
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
                            SetControllerMuteButtonsForCurrentLayer();
                            break;
                        case MessageTypeEnum.vca:
                            if(ui24Message.IntValue >= 0 && ui24Message.IntValue < 6)
                            {
                                _mixerChannels[ui24Message.ChannelNumber].VCA = 1 << ui24Message.IntValue;
                                break;
                            }
                            _mixerChannels[ui24Message.ChannelNumber].VCA = 0;
                            break;
                        case MessageTypeEnum.forceunmute:
                            _mixerChannels[ui24Message.ChannelNumber].ForceUnMute = ui24Message.LogicValue;
                            if (ui24Message.IsValid && isOnLayer && controllerChannelNumber < 8)
                            {
                                _settings.Controller.SetMuteLed(controllerChannelNumber, _mixerChannels[ui24Message.ChannelNumber].IsMute);
                            }
                            break;
                        case MessageTypeEnum.mgMask:
                            _mixerChannels[ui24Message.ChannelNumber].MuteGroupMask = (UInt32)ui24Message.IntValue;
                            if (ui24Message.IsValid && isOnLayer && controllerChannelNumber < 8)
                            {
                                SetControllerMuteButtonsForCurrentLayer();
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
                            if (_mixerChannels[ui24Message.ChannelNumber] is IInputable && _settings.ChannelRecButtonBehavior == BridgeSettings.ChannelRecButtonBehaviorEnum.Phantom)
                            {
                                (_mixerChannels[ui24Message.ChannelNumber] as IInputable).IsPhantom = ui24Message.LogicValue;
                                if (ui24Message.IsValid && isOnLayer && controllerChannelNumber < 8)
                                {
                                    _settings.Controller.SetRecLed(controllerChannelNumber, ui24Message.LogicValue);
                                }
                            }
                            break;
                        case MessageTypeEnum.source:
                            if(_mixerChannels[ui24Message.ChannelNumber] is IInputable)
                            {
                                var inputableChannel = _mixerChannels[ui24Message.ChannelNumber] as IInputable;
                                if (ui24Message.IntValue < 0)
                                {
                                    inputableChannel.SrcType = SrcTypeEnum.None;
                                    inputableChannel.SrcNumber = 20;
                                }
                                else if (ui24Message.IntValue > 99)
                                {
                                    inputableChannel.SrcType = SrcTypeEnum.Line;
                                    inputableChannel.SrcNumber = ui24Message.IntValue - 100;
                                }
                                else
                                {
                                    inputableChannel.SrcType = SrcTypeEnum.Hw;
                                    inputableChannel.SrcNumber = ui24Message.IntValue;
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
                        case MessageTypeEnum.bpm:
                            _settings.Controller.WriteTextToTicksDisplay(ui24Message.FaderValue.ToString().PadLeft(3, ' '));
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
                        case MessageTypeEnum.globalMGMask:
                            _mixer.MuteMask = (UInt32)ui24Message.IntValue;
                            SetControllerMuteButtonsForCurrentLayer();
                            SetMuteGroupsLeds();
                            break;
                    }
                }
                else if (m.StartsWith("SETS^vg.")) //first global view group (e.g:"SETS^vg.0^[0,1,2,3,4,5,6,17,18,19,20,22,23,38,39,40,41,42,43,44,45,48,49,21]")
                {
                    var parts = m.Split('^');
                    var newViewChannel = parts.Last().Trim('[', ']').Split(',');
                    var viewGroupString = parts[1].Split('.').Last();
                    int viewGroup;
                    if (int.TryParse(viewGroupString, out viewGroup))
                        if (newViewChannel.Length > 7)
                            for (int i = 0; i < 8; ++i)
                                _mixer.setChannelToViewLayerAndPosition(int.Parse(newViewChannel[i]), viewGroup, i);
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
                            _mixer.UserLayerEditNewChannel = -1;

                        }
                        else
                        {
                            _mixerChannels[ch].IsSelected = true;
                            _selectedChannel = ch;
                            _mixer.UserLayerEditNewChannel = ch;
                        }
                        var channelNumber = _mixer.getCurrentLayer().Select((item, i) => new { Channel = item, controllerChannelNumber = i })
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
                       var channelNumber = _mixer.getChannelNumberInCurrentLayer(i);
                        if (channelNumber < 48) //input channel
                        {
                            _settings.Controller.WriteChannelMeter(i, ui24rvumessage.VUChannels[channelNumber].GetPostValue());
                        }
                    }
                }

            }
        }
        private void SetMuteGroupsLeds()
        {
            UInt32 mask = _mixer.MuteMask;
            for (int i = 0; i < 6; ++i)
            {
                _settings.Controller.SetLed(ButtonsEnum.MuteGroup1 + i, ((mask >> i) & 1) == 1);
            }
            _settings.Controller.SetLed(ButtonsEnum.Save, ((mask >> Mixer._muteAllBit) & 1) == 1);
            _settings.Controller.SetLed(ButtonsEnum.Undo, ((mask >> Mixer._muteAllFxBit) & 1) == 1);
        }
        protected void SendMessage(string message, bool isDebug = true)
        {
            if (_settings.MessageWriter != null)
                _settings.MessageWriter(message, isDebug);
        }
        public void Dispose()
        {
            _secondaryBridge?.Dispose();
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
