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
using System.Reflection.Emit;

namespace UI24RController
{
    public class UI24RBridge : IDisposable
    {



        const string CONFIGFILE_VIEW_GROUP = "ViewGroups.json";

        protected BridgeSettings _settings;
        protected List<IMIDIController> _controllers;
        protected WebsocketClient _client;


        private int selectedChannel = -1; //-1 = no selected channel
        public int SelectedChannel { get => selectedChannel; set => selectedChannel = value; }
        public KnobsFunctionEnum KnobsFunction { get; set; }
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
        public UI24RBridge(BridgeSettings settings, List<IMIDIController> controllers)
        {
            this._settings = settings;
            this._controllers = controllers;
            SendMessage("Start initialization...", false);
            InitializeChannels();
            InitializeViewGroupsFromConfig();


            SendMessage("Create controller events....", false);
            foreach (var controller in controllers)
            {
                controller.FaderEvent += (sender, args) => _midiController_FaderEvent(controller, args);
                controller.BankUp += _midiController_BankUp;
                controller.BankDown += _midiController_BankDown;
                controller.LayerUp += _midiController_LayerUp;
                controller.LayerDown += _midiController_LayerDown;
                controller.KnobEvent += (sender, args) => _midiController_KnobEvent(controller, args);
                controller.MuteChannelEvent += (sender, args) => _midiController_MuteChannelEvent(controller, args);
                controller.SoloChannelEvent += (sender, args) => _midiController_SoloChannelEvent(controller, args);
                controller.SelectChannelEvent += (sender, args) => _midiController_SelectChannelEvent(controller, args);
                controller.RecChannelEvent += (sender, args) => _midiController_RecChannelEvent(controller, args);
                controller.MuteGroupButtonEvent += (sender, args) => Controller_MuteGroupButtonEvent(controller, args);
                controller.SaveEvent += (sender, args) => _midiController_MuteAllEvent(controller, args);
                controller.UndoEvent += (sender, args) => _midiController_MuteAllFxEvent(controller, args);
                controller.CancelEvent += (sender, args) => _midiController_ClearMute(controller, args);
                controller.EnterEvent += (sender, args) => _midiController_ClearSolo(controller, args);
                controller.RecEvent += (sender, args) => _midiController_RecEvent(controller, args);
                controller.PlayEvent += (sender, args) => _midiController_PlayEvent(controller, args);
                controller.StopEvent += (sender, args) => _midiController_StopEvent(controller, args);
                controller.NextEvent += (sender, args) => _midiController_NextEvent(controller, args);
                controller.PrevEvent += (sender, args) => _midiController_PrevEvent(controller, args);
                controller.UserBtnEvent += (sender, args) => _midiController_UserLayerEdit(controller, args);
                controller.GlobalViewEvent += (sender, args) => _midiController_SaveEvent(controller, args);
                controller.WheelEvent += (sender, args) => _midiController_WheelEvent(controller, args);
                controller.SmtpeBeatsBtnEvent += (sender, args) => _midiController_TapTempoEvent(controller, args);
                controller.WriteTextToLCDSecondLine("");
                controller.ConnectionErrorEvent += (sender, args)=> _midiController_ConnectionErrorEvent(controller, args);
                controller.AuxButtonEvent += (sender, args) => _midiController_AuxButtonEvent(controller, args);
                controller.FxButtonEvent += (sender, args) => Controller_FXButtonEvent(controller, args);
                controller.ScrubEvent += (sender, args) => Controller_ScrubEvent(controller, args);
                controller.TrackEvent += (sender, args) => Controller_TrackEvent(controller, args); //HPF: functionality
                controller.PanEvent += (sender, args) => Controller_PanEvent(controller, args);     //Pan: functionality
                controller.InitializeController();

            }
            this.KnobsFunction = KnobsFunctionEnum.Gain;
            
            //if (!_settings.Controller.IsConnected)
            //{
            //    _midiController_ConnectionErrorEvent(this, null);
            //}
            _mixer.setBank(_settings.StartBank);
            SendMessage("Start websocket connection...", false);
            _client = new WebsocketClient(new Uri(_settings.Address));
            _client.MessageReceived.Subscribe(msg => UI24RMessageReceived(msg));
            _client.DisconnectionHappened.Subscribe(info => WebsocketDisconnectionHappened(info));
            _client.ReconnectionHappened.Subscribe(info => WebsocketReconnectionHappened(info));
            _client.ErrorReconnectTimeout = new TimeSpan(0,0,10);
            SendMessage("Connecting to UI24R....", false);
            _client.Start();
            controllers.ForEach(c=> c.WriteTextToAssignmentDisplay(_mixer.getCurrentLayerString()));
            //if (settings.ControllerStartChannel != null && settings.ControllerStartChannel == "1")
            //{
            //    _mixer.IsChannelOffset = true;
            //}
            SetStateLedsOnController();
        }



        private void _midiController_ConnectionErrorEvent(IMIDIController controller, EventArgs e)
        {
            SendMessage("Midi controller connection error.", false);
            SendMessage("Try to reconnect....", false);
            if (!_isReconnecting)
            {
                new Thread(() =>
                {
                    _isReconnecting = true;
                    while (_isReconnecting && !controller.ReConnectDevice())
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
            _controllers.ForEach(c=> c.WriteTextToLCDSecondLine("UI24R is reconnected",5));
        }
        private void WebsocketDisconnectionHappened(DisconnectionInfo info)
        {
            _controllers.ForEach(c => c.WriteTextToLCDSecondLine("UI24R disconnected. Try to reconnect"));
        }
        private void InitializeViewGroupsFromConfig()
        {
            if (File.Exists(CONFIGFILE_VIEW_GROUP))
            {
                var jsonString = File.ReadAllText(CONFIGFILE_VIEW_GROUP);
                int[][] viewViewGroups = JsonSerializer.Deserialize<int[][]>(jsonString, MyClassTypeResolver<int[][]>.GetSerializerOptions());
                _mixer.setUserLayerFromArray(viewViewGroups);
            }
        }

        #region Midicontroller events

        private void Controller_TrackEvent(IMIDIController controller, EventArgs e)
        {
            
        }

        private void Controller_PanEvent(IMIDIController controller, EventArgs e)
        {
            //if current function is panorama, turn off and set to gain
            if (this.KnobsFunction == KnobsFunctionEnum.Pan)
            {
                controller.SetLed(ButtonsEnum.Pan, false);
                this.KnobsFunction = KnobsFunctionEnum.Gain;
                //if (_secondaryBridge != null)
                //{
                //    _secondaryBridge.KnobsFunction = KnobsFunctionEnum.Gain;
                //}
            }
            else
            {
                controller.SetLed(ButtonsEnum.Pan, true);
                this.KnobsFunction = KnobsFunctionEnum.Pan;
                //if (_secondaryBridge != null)
                //{
                //    _secondaryBridge.KnobsFunction = KnobsFunctionEnum.Pan;
                //}
            }
            SetControllerToCurrentLayerAndSend();
            //if (_secondaryBridge != null)
            //{
            //    _secondaryBridge.SetControllerToCurrentLayerAndSend();
            //}
        }

        private void Controller_ScrubEvent(IMIDIController controller, ButtonEventArgs e)
        {
            if (_settings.TalkBack > 0)
            {
                var channelNumber = _settings.TalkBack - 1;
                var channelTypeID = "i";
                if (e.IsPress)
                {
                    _client.Send($"3:::SETD^{channelTypeID}.{channelNumber}.mute^1");

                    _mixerChannels[channelNumber].IsMute = true;
                    SetControllerMuteButtonsForCurrentLayer();


                }
                else
                {
                    _mixerChannels[channelNumber].IsMute = false;
                    SetControllerMuteButtonsForCurrentLayer();

                    _client.Send($"3:::SETD^{channelTypeID}.{channelNumber}.mute^0");
                }
            }
        }

        private void _midiController_RecChannelEvent(IMIDIController controller, MIDIController.ChannelEventArgs e)
        {
            var ch = _mixer.getChannelNumberInCurrentLayer(e.ChannelNumber, controller.ChannelOffset);
            if (ch > -1)
            {
                if (_settings.ChannelRecButtonBehavior == BridgeSettings.ChannelRecButtonBehaviorEnum.Rec)
                {
                    if (_mixerChannels[ch] is IRecordable)
                    {
                        var channel = _mixerChannels[ch] as IRecordable;
                        channel.IsRec = !channel.IsRec;
                        _client.Send(channel.RecMessage());
                        controller.SetRecLed(e.ChannelNumber, channel.IsRec);
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
                            controller.SetRecLed(e.ChannelNumber, currentChannel.IsPhantom);

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
                                            if (singleInputChannel == _mixerChannels[_mixer.getChannelNumberInCurrentLayer(i, controller.ChannelOffset)])
                                                controller.SetRecLed(i, currentChannel.IsPhantom);
                                    }
                                }
                            }
                        }
                    }
                }
            }

        }

        private void _midiController_SoloChannelEvent(IMIDIController controller, MIDIController.ChannelEventArgs e)
        {
            var ch = _mixer.getChannelNumberInCurrentLayer(e.ChannelNumber, controller.ChannelOffset);
            if (ch > -1)
            {
                _mixerChannels[ch].IsSolo = !_mixerChannels[ch].IsSolo;
                _client.Send(_mixerChannels[ch].SoloMessage());
                controller.SetSoloLed(e.ChannelNumber, _mixerChannels[ch].IsSolo);
                //if the channel is linked we have to set the other channel to the same value
                if (_mixerChannels[ch] is IStereoLinkable)
                {
                    var stereoChannel = _mixerChannels[ch] as IStereoLinkable;
                    if (stereoChannel.LinkedWith != -1)
                    {
                        var otherCh = stereoChannel.LinkedWith == 0 ? ch + 1 : ch - 1;
                        _mixerChannels[otherCh].IsSolo = !_mixerChannels[otherCh].IsSolo;
                        _client.Send(_mixerChannels[otherCh].SoloMessage());
                        //If the other chanel is on the current layout we have to set too in all controller
                        var otherChOnLayers = GetControllerChannel(otherCh);
                        otherChOnLayers.ForEach(otherChOnLayer =>
                            {
                                if (otherChOnLayer.isOnLayer)
                                {
                                    otherChOnLayer.controller.SetSoloLed(otherChOnLayer.controllerChannelNumber, _mixerChannels[otherCh].IsSolo);
                                }
                            }
                        );
                    }
                }
            }
        }
        private void _midiController_MuteChannelEvent(IMIDIController controller, MIDIController.ChannelEventArgs e)
        {
            var ch = _mixer.getChannelNumberInCurrentLayer(e.ChannelNumber, controller.ChannelOffset);
            if (ch > -1)
            {
                if (_mixerChannels[ch].IsMuteByMuteGroup)
                {
                    _mixerChannels[ch].ForceUnMute = !_mixerChannels[ch].ForceUnMute;
                    _client.Send(_mixerChannels[ch].ForceUnMuteMessage());
                }
                else
                {
                    _mixerChannels[ch].IsMute = !_mixerChannels[ch].IsMute;
                    _client.Send(_mixerChannels[ch].MuteMessage());
                }
                controller.SetMuteLed(e.ChannelNumber, _mixerChannels[ch].IsMute);
                //if the channel is linked we have to set the other channel to the same value
                if (_mixerChannels[ch] is IStereoLinkable)
                {
                    var stereoChannel = _mixerChannels[ch] as IStereoLinkable;
                    if (stereoChannel.LinkedWith != -1)
                    {
                        var otherCh = stereoChannel.LinkedWith == 0 ? ch + 1 : ch - 1;
                        if (_mixerChannels[ch].IsMuteByMuteGroup)
                        {
                            _mixerChannels[otherCh].ForceUnMute = !_mixerChannels[otherCh].ForceUnMute;
                            _client.Send(_mixerChannels[otherCh].ForceUnMuteMessage());
                        }
                        else
                        {
                            _mixerChannels[otherCh].IsMute = !_mixerChannels[otherCh].IsMute;
                            _client.Send(_mixerChannels[otherCh].MuteMessage());
                        }
                        //If the other chanel is on the current layout we have to set too
                        var otherChOnLayers = GetControllerChannel(otherCh);
                        otherChOnLayers.ForEach(otherChOnLayer =>
                        {
                            if (otherChOnLayer.isOnLayer)
                            {
                                otherChOnLayer.controller.SetMuteLed(otherChOnLayer.controllerChannelNumber, _mixerChannels[otherCh].IsMute);
                            }
                        });
                    }
                }
                SetControllerMuteButtonsForCurrentLayer();
            }
        }
       
        private void _midiController_SelectChannelEvent(IMIDIController controller, MIDIController.ChannelEventArgs e)
        {
            var ch = _mixer.getChannelNumberInCurrentLayer(e.ChannelNumber, controller.ChannelOffset);
            if (ch > -1)
            {
                if (SelectedChannel != -1)
                {
                    _mixerChannels[SelectedChannel].IsSelected = false;
                    //Set current led off on all controller
                    _controllers.ForEach(c =>
                    {
                        if (SelectedChannelIsOnCurrentLayer(c.ChannelOffset))
                        {
                            var chNumber = _mixer.getChannelNumberInCurrentLayer(SelectedChannel, c.ChannelOffset);
                            c.SetSelectLed(chNumber, false);
                        }
                    });
                }
                _mixerChannels[ch].IsSelected = true;
                SelectedChannel = ch;
                controller.SetSelectLed(e.ChannelNumber, true);
                _client.Send(_mixerChannels[ch].SelectChannelMessage(_settings.SyncID));
                //turnOn RTA
                if (_settings.RtaOnWhenSelect)
                {
                    _client.Send(_mixerChannels[ch].TurnOnRTAMessage());
                }
            }
        }
        private void _midiController_KnobEvent(IMIDIController controller, MIDIController.KnobEventArgs e)
        {
            var ch = _mixer.getChannelNumberInCurrentLayer(e.ChannelNumber, controller.ChannelOffset);
            if (ch > -1)
            {
                switch (this.KnobsFunction)
                {
                    case KnobsFunctionEnum.Gain:
                        if (_mixerChannels[ch] is IInputable)
                        {
                            var currentGainChannel = _mixerChannels[ch] as IInputable;
                            if (currentGainChannel.SrcType == SrcTypeEnum.Hw)
                            {
                                currentGainChannel.Gain = currentGainChannel.Gain + (1.0d / 100.0d) * e.KnobDirection;
                                if (currentGainChannel.Gain > 1)
                                    currentGainChannel.Gain = 1;
                                if (currentGainChannel.Gain < 0)
                                    currentGainChannel.Gain = 0;

                                _client.Send(currentGainChannel.GainMessage());
                                controller.SetKnobLed(e.ChannelNumber, currentGainChannel.Gain);
                                //if multiple channels are set to same HW input
                                foreach (var singleChannel in _mixerChannels)
                                {
                                    if (singleChannel is IInputable)
                                    {
                                        var singleInputChannel = singleChannel as IInputable;
                                        if (singleInputChannel.SrcType == SrcTypeEnum.Hw && singleInputChannel.SrcNumber == currentGainChannel.SrcNumber)
                                        {
                                            singleInputChannel.Gain = currentGainChannel.Gain;
                                            for (int i = 0; i < 8; ++i)
                                                if (singleInputChannel == _mixerChannels[_mixer.getChannelNumberInCurrentLayer(i, controller.ChannelOffset)])
                                                    controller.SetKnobLed(i, currentGainChannel.Gain);
                                        }
                                    }
                                }

                            }
                        }
                        break;
                    case KnobsFunctionEnum.Pan:
                        var currentChannel = _mixerChannels[ch];
                        currentChannel.Panorama = currentChannel.Panorama + (1.0d / 100.0d) * e.KnobDirection;
                        if (currentChannel.Panorama > 1)
                            currentChannel.Panorama = 1;
                        if (currentChannel.Panorama < 0)
                            currentChannel.Panorama = 0;

                        _client.Send(currentChannel.PanoramaMessage());
                        controller.SetKnobLed(e.ChannelNumber, currentChannel.Panorama);
                        break;
                }
            }
        }

        public void _midiController_LayerDown(object sender, EventArgs e)
        {
            _mixer.setLayerDown();
            SetControllerToCurrentLayerAndSend();
            //if (_secondaryBridge != null)
            //{
            //    _secondaryBridge._midiController_LayerDown(sender, e);
            //}
        }
        public void _midiController_LayerUp(object sender, EventArgs e)
        {
            _mixer.setLayerUp();
            SetControllerToCurrentLayerAndSend();
            //if (_secondaryBridge != null)
            //{
            //    _secondaryBridge._midiController_LayerUp(sender, e);
            //}
        }
        public void _midiController_BankDown(object sender, EventArgs e)
        {
            _mixer.setBankDown();
            SetControllerToCurrentLayerAndSend();
            //if (_secondaryBridge != null)
            //{
            //    _secondaryBridge._midiController_BankDown(sender, e);
            //}
        }
        public void _midiController_BankUp(object sender, EventArgs e)
        {
            _mixer.setBankUp();
            SetControllerToCurrentLayerAndSend();
            //if (_settings.ControllerStartChannel != null && _settings.ControllerStartChannel == "1")
            //{
            //    _mixer.setLayerUp();
            //}
            //if (_secondaryBridge != null)
            //{
            //    _secondaryBridge._midiController_BankUp(sender, e);
            //}
        }

        private void _midiController_FaderEvent(IMIDIController controller, MIDIController.FaderEventArgs e)
        {
            var ch = _mixer.getChannelNumberInCurrentLayer(e.ChannelNumber, controller.ChannelOffset);
            if (ch > -1)
            {
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
                            var otherChOnLayers = GetControllerChannel(otherCh);
                            otherChOnLayers.ForEach(otherChOnLayer =>
                            {
                                if (otherChOnLayer.isOnLayer)
                                {
                                    otherChOnLayer.controller.SetFader(otherChOnLayer.controllerChannelNumber, e.FaderValue);
                                }
                            });
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
        }

        public void _midiController_AuxButtonEvent(IMIDIController controller, MIDIController.FunctionEventArgs e)
        {
            if (_settings.AuxButtonBehavior == BridgeSettings.AuxButtonBehaviorEnum.Release)
            {
                if (e.IsPress)
                {
                    _selectedLayout = e.FunctionButton.ToAux();
                    controller.SetLed(_selectedLayout.ToButtonsEnum(), true);
                    controller.WriteTextToBarsDisplay("AX" + (_selectedLayout.AuxToInt() + 1).ToString());
                }
                else
                {
                    controller.SetLed(_selectedLayout.ToButtonsEnum(), false);
                    _selectedLayout = SelectedLayoutEnum.Channels;
                    controller.WriteTextToBarsDisplay("   ");
                }
            }
            else
            {
                if (e.IsPress)
                {
                    if (_selectedLayout == e.FunctionButton.ToAux())
                    {
                        controller.SetLed(_selectedLayout.ToButtonsEnum(), false);
                        _selectedLayout = SelectedLayoutEnum.Channels;
                        controller.WriteTextToBarsDisplay("   ");
                    }
                    else
                    {
                        controller.SetLed(_selectedLayout.ToButtonsEnum(), false);
                        _selectedLayout = e.FunctionButton.ToAux();
                        controller.SetLed(_selectedLayout.ToButtonsEnum(), true);
                        controller.WriteTextToBarsDisplay("AX" + (_selectedLayout.AuxToInt()+1).ToString());
                    }
                }
            }
            SetControllerToCurrentLayerAndSend();
            //if (_secondaryBridge != null)
            //{
            //    _secondaryBridge._midiController_AuxButtonEvent(sender, e);
            //}
        }
        public void Controller_FXButtonEvent(IMIDIController controller, FunctionEventArgs e)
        {
            if (_settings.AuxButtonBehavior == BridgeSettings.AuxButtonBehaviorEnum.Release)
            {
                if (e.IsPress)
                {
                    _selectedLayout = e.FunctionButton.ToFx();
                    controller.SetLed(_selectedLayout.ToButtonsEnum(), true);
                    controller.WriteTextToBarsDisplay("FX" + (_selectedLayout.FxToInt() + 1).ToString());
                }
                else
                {
                    controller.SetLed(_selectedLayout.ToButtonsEnum(), false);
                    _selectedLayout = SelectedLayoutEnum.Channels;
                    controller.WriteTextToBarsDisplay("   ");
                }
            }
            else
            {
                if (e.IsPress)
                {
                    if (_selectedLayout == e.FunctionButton.ToFx())
                    {
                        controller.SetLed(_selectedLayout.ToButtonsEnum(), false);
                        _selectedLayout = SelectedLayoutEnum.Channels;
                        controller.WriteTextToBarsDisplay("   ");
                    }
                    else
                    {
                        controller.SetLed(_selectedLayout.ToButtonsEnum(), false);
                        _selectedLayout = e.FunctionButton.ToFx();
                        controller.SetLed(_selectedLayout.ToButtonsEnum(), true);
                        controller.WriteTextToBarsDisplay("FX" + (_selectedLayout.FxToInt() + 1).ToString());
                    }
                }
            }
            SetControllerToCurrentLayerAndSend();
            //if (_secondaryBridge != null)
            //{
            //    _secondaryBridge.Controller_FXButtonEvent(sender, e);
            //}
        }
        private void Controller_MuteGroupButtonEvent(IMIDIController controller, FunctionEventArgs e)
        {
            if (e.IsPress)
            {
                _mixer.ToggleMuteGroup(e.FunctionButton);
                SetClientMute();
                SetControllerMuteButtonsForCurrentLayer();
            }
        }
        private void _midiController_MuteAllFxEvent(IMIDIController controller, EventArgs e)
        {
            _mixer.ToggleMuteAllFx();
            SetClientMute();
            SetControllerMuteButtonsForCurrentLayer();
        }
        private void _midiController_MuteAllEvent(IMIDIController controller, EventArgs e)
        {
            _mixer.ToggleMuteAll();
            SetClientMute();
            SetControllerMuteButtonsForCurrentLayer();
        }
        private void _midiController_ClearMute(IMIDIController controller, EventArgs e)
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
        private void _midiController_ClearSolo(IMIDIController controller, EventArgs e)
        {
            foreach (var ch in _mixerChannels)
            {
                if (ch.IsSolo == true)
                {
                    ch.IsSolo = false;
                    _client.Send(ch.SoloMessage());
                }
            }
            var channels = _mixer.getCurrentLayer(controller.ChannelOffset).Select((item, i) => new { Channel = item, controllerChannelNumber = i });
            foreach (var ch in channels)
            {
                if (ch.Channel > -1)
                    controller.SetSoloLed(ch.controllerChannelNumber, _mixerChannels[ch.Channel].IsSolo);
            }
        }

        private void SetControllerMuteButtonsForCurrentLayer()
        {
            //why do we set back the mute of the client? I've moved that block to an independent mehod
            //SetClientMute();
            SetMuteGroupsLeds();
            _controllers.ForEach(c =>
            {
                var channels = _mixer.getCurrentLayer(c.ChannelOffset).Select((item, i) => new { Channel = item, controllerChannelNumber = i });
                foreach (var ch in channels)
                {
                    if (ch.Channel > -1)
                        c.SetMuteLed(ch.controllerChannelNumber, _mixerChannels[ch.Channel].IsMute);
                }
            });
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

        private void _midiController_PrevEvent(IMIDIController controller, EventArgs e)
        {
            _client.Send(_mixer.Get2TrackPrevMessage());
        }
        private void _midiController_NextEvent(IMIDIController controller, EventArgs e)
        {
            _client.Send(_mixer.Get2TrackNextMessage());
        }
        private void _midiController_StopEvent(IMIDIController controller, EventArgs e)
        {
            _client.Send(_mixer.Get2TrackStopMessage());
        }
        private void _midiController_PlayEvent(IMIDIController controller, EventArgs e)
        {
            _client.Send(_mixer.Get2TrackPlayMessage());
        }
        private void _midiController_RecEvent(IMIDIController controller, EventArgs e)
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

        #region user layer events
        //these envent are public and provided to the secondary controller
        //if the selected channel is on the secondary controller the modified value has to be send back to the primary controller

        public bool SelectedChannelIsOnCurrentLayer(int channelOffset) 
        { 
               return _mixer.getCurrentLayer(channelOffset).Where(x => x == SelectedChannel).Count() > 0;
        }
        public int UserLayerEditNewChannel
        {
            get
            {
                return _mixer.UserLayerEditNewChannel;
            }
            set
            {
                _mixer.UserLayerEditNewChannel = value;
            }
        }
        public bool UserLayerEdit
        {
            get { return _mixer.UserLayerEdit; }
            set { _mixer.UserLayerEdit = value; }
        }



        public void _midiController_UserLayerEdit(IMIDIController controller, FunctionEventArgs e)
        {
            _mixer.UserLayerEdit = e.IsPress;
            //if (_secondaryBridge != null) _secondaryBridge.UserLayerEdit = e.IsPress;

            controller.SetLed(ButtonsEnum.User, e.IsPress);

            if (e.IsPress)
            {
                if (_mixer.UserLayerEdit && SelectedChannel != 54)
                {
                    //Go to user layer if necessary
                    if (_mixer.goToUserBank())
                    {
                        SetControllerToCurrentLayerAndSend();
                        controller.WriteTextToAssignmentDisplay(_mixer.getCurrentLayerString());
                    }
                    //Set Select to First channel if necessary (selected channel is not in primary and secondary controller controller)
                    if (_controllers.Where(c => this.SelectedChannelIsOnCurrentLayer(c.ChannelOffset)).Count()==0)
                    {
                        if (SelectedChannel != -1)
                        {
                            _mixerChannels[SelectedChannel].IsSelected = false;
                        }
                        SelectedChannel = _mixer.getCurrentLayer(controller.ChannelOffset).FirstOrDefault();
                        _mixerChannels[SelectedChannel].IsSelected = true;
                        controller.SetSelectLed(0, true);
                        _client.Send(_mixerChannels[SelectedChannel].SelectChannelMessage(_settings.SyncID));
                    }
                    //if current layer contains the selected channel
                    this.UserLayerEditNewChannel = SelectedChannel;
                }
            }
            else
            {
                     UpdateChannelOnUserLayer();
            }
        }

        public void SetChannelOnUserLayer(int bank, int layer, int position, int channel)
        {
            _mixer.setChannelInLayerAndPosition(bank, layer, position, channel);
        }

        public void UpdateChannelOnUserLayer()
        {
            foreach (var controller in _controllers.Where(c => SelectedChannelIsOnCurrentLayer(c.ChannelOffset)))
            {
                int controllerPos = Array.IndexOf(_mixer.getCurrentLayer(controller.ChannelOffset), SelectedChannel);
                _mixer.setNewUserChannelInCurrentBank(controllerPos);
                controller.WriteTextToChannelLCDSecondLine(controllerPos, "");
                SetControllerChannelToCurrentLayerAndSend(controller, _mixer.UserLayerEditNewChannel, controllerPos);
                
            }
        }


        public void _midiController_WheelEvent(IMIDIController controller, MIDIController.WheelEventArgs e)
        {
            foreach (var otherController in _controllers.Where(c => SelectedChannelIsOnCurrentLayer(c.ChannelOffset)))
            {
                if (_mixer.UserLayerEdit && SelectedChannel != 54 )
                {
                    //find next channel not used on this layer
                    int controllerPos = Array.IndexOf(_mixer.getCurrentLayer(otherController.ChannelOffset), SelectedChannel);
                    _mixer.findNextAvailableChannelForUserLayer(controllerPos, e.WheelDirection, otherController.ChannelOffset);
                    controller.WriteTextToChannelLCDSecondLine(controllerPos, _mixerChannels[_mixer.UserLayerEditNewChannel].Name);
                }
                
            }
            //if (_secondaryBridge != null) _secondaryBridge._midiController_WheelEvent(sender, e);
        }
        public void _midiController_SaveEvent(IMIDIController controller, EventArgs e)
        {
            string jsonString;
            jsonString = JsonSerializer.Serialize(_mixer.getUserLayerToArray(), MyClassTypeResolver<int[][]>.GetSerializerOptions());
            File.WriteAllText(CONFIGFILE_VIEW_GROUP, jsonString);
        }
        #endregion
        private void _midiController_TapTempoEvent(IMIDIController controller, EventArgs e)
        {
            int tempo = _mixer.TapTempo();
            if (tempo > 0)
            {
                for (int fxNum = 0; fxNum < 4; ++fxNum)
                    _client.Send(_mixer.GetStartMTKRecordMessage(fxNum, tempo));
                controller.WriteTextToTicksDisplay(tempo.ToString().PadLeft(3, ' '));
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

        private void SetControllerChannelToCurrentLayerAndSend(IMIDIController controller, int channelNumber, int controllerChannelNumber)
        {
            if (channelNumber > -1)
            {
                if (_selectedLayout == SelectedLayoutEnum.Channels)
                {
                    controller.SetFader(controllerChannelNumber, _mixerChannels[channelNumber].ChannelFaderValue);
                }
                else
                {
                    if (!(_mixerChannels[channelNumber] is MainChannel))
                    {
                        controller.SetFader(controllerChannelNumber, _mixerChannels[channelNumber].AuxSendValues[_selectedLayout]);
                    }
                }


                if (_mixerChannels[channelNumber].IsSelected)
                {
                    controller.SetSelectLed(controllerChannelNumber, true);
                }

                SetControllerChannelKnobb(controller, channelNumber, controllerChannelNumber);

                controller.WriteTextToChannelLCDFirstLine(controllerChannelNumber, _mixerChannels[channelNumber].Name);
                controller.SetMuteLed(controllerChannelNumber, _mixerChannels[channelNumber].IsMute);
                controller.SetSoloLed(controllerChannelNumber, _mixerChannels[channelNumber].IsSolo);

                if (_settings.ChannelRecButtonBehavior == BridgeSettings.ChannelRecButtonBehaviorEnum.Rec)
                {
                    if (_mixerChannels[channelNumber] is IRecordable)
                        controller.SetRecLed(controllerChannelNumber, (_mixerChannels[channelNumber] as IRecordable).IsRec);
                    else
                        controller.SetRecLed(controllerChannelNumber, false);
                }
                else //phantom
                {
                    if (_mixerChannels[channelNumber] is IInputable)
                        controller.SetRecLed(controllerChannelNumber, (_mixerChannels[channelNumber] as IInputable).IsPhantom);
                    else
                        controller.SetRecLed(controllerChannelNumber, false);
                }
            }
            else //empty channel
            {
                controller.SetFader(controllerChannelNumber, 0);

                controller.SetSelectLed(controllerChannelNumber, false);
                controller.SetKnobLed(controllerChannelNumber, 0);
                                
                controller.WriteTextToChannelLCDFirstLine(controllerChannelNumber, "");
                controller.SetMuteLed(controllerChannelNumber, false);
                controller.SetSoloLed(controllerChannelNumber, false);
                controller.SetRecLed(controllerChannelNumber, false);
            }

            }

        private void SetControllerChannelKnobb(IMIDIController controller, int channelNumber, int controllerChannelNumber)
        {
            if (channelNumber > -1)
            {
                switch (this.KnobsFunction)
                {
                    case KnobsFunctionEnum.Gain:
                        if (this.KnobsFunction == KnobsFunctionEnum.Gain)
                        {
                            if (_mixerChannels[channelNumber] is IInputable)
                            {
                                var inputChannel = _mixerChannels[channelNumber] as IInputable;
                                if (inputChannel.SrcType == SrcTypeEnum.Hw)
                                    controller.SetKnobLed(controllerChannelNumber, inputChannel.Gain);
                                else
                                    controller.SetKnobLed(controllerChannelNumber, 0);
                            }
                            else
                                controller.SetKnobLed(controllerChannelNumber, 0);
                        }
                        break;
                    case KnobsFunctionEnum.Pan:
                        controller.SetKnobLed(controllerChannelNumber, _mixerChannels[channelNumber].Panorama);
                        break;
                    default:
                        controller.SetKnobLed(controllerChannelNumber, 0);
                        break;
                }
            }
        }

        private void SetControllerToCurrentLayerAndSend()
        {
            _controllers.ForEach(controller =>
            {
                var channels =  _mixer.getCurrentLayer(controller.ChannelOffset).Select((item, i) => new { Channel = item, controllerChannelNumber = i });
                controller.SetSelectLed(0, false); //turn off all
                foreach (var ch in channels)
                {
                    SetControllerChannelToCurrentLayerAndSend(controller, ch.Channel, ch.controllerChannelNumber);
                }
                controller.WriteTextToAssignmentDisplay(_mixer.getCurrentLayerString());

            });
        }
        private void SetStateLedsOnController()
        {
            _controllers.ForEach(controller =>
            {
                controller.SetLed(ButtonsEnum.Rec, _mixer.IsMultitrackRecordingRun || _mixer.IsTwoTrackRecordingRun);
                SetMuteGroupsLeds();
                SetKnobsFunctionLedOnController();
                if (_selectedLayout.IsAux())
                {
                    controller.SetLed(_selectedLayout.ToButtonsEnum(), true);
                    controller.WriteTextToBarsDisplay("AX" + (_selectedLayout.AuxToInt() + 1).ToString());
                } 
                else if (_selectedLayout.IsFx())
                {
                    controller.SetLed(_selectedLayout.ToButtonsEnum(), true);
                    controller.WriteTextToBarsDisplay("FX" + (_selectedLayout.FxToInt() + 1).ToString());
                }

            });
                
        }

        private void SetKnobsFunctionLedOnController()
        {
            _controllers.ForEach(controller =>
            {
                controller.SetLed(ButtonsEnum.Pan, this.KnobsFunction == KnobsFunctionEnum.Pan);
                controller.SetLed(ButtonsEnum.Track, this.KnobsFunction == KnobsFunctionEnum.Hpf);
            });
        }
        protected List<(int controllerChannelNumber, IMIDIController controller, bool isOnLayer)> GetControllerChannel(int ch)
        {
            var controllerChannelNumbers = _controllers.Select(c =>
                    {
                        var chController = _mixer.getCurrentLayer(c.ChannelOffset).Select((item, i) => new { Channel = item, controllerChannelNumber = i })
                        .Where(c => c.Channel == ch).FirstOrDefault();
                        if (chController != null)
                        {
                            return (chController.controllerChannelNumber, c, true);
                        }
                        else
                        {
                            return (-1, null, false);
                        }
                    }
                );
            return controllerChannelNumbers.ToList();
        }
        private void ProcessUI24Message(string text)
        {
            var messageparts = text.Replace("3:::","").Split('\n');
            foreach (var m in messageparts)
            {
                var ui24Message = new UI24Message(m);
                if (ui24Message.IsValid)
                {
                   var chOnControllerLayers = GetControllerChannel(ui24Message.ChannelNumber);

                    switch (ui24Message.MessageType)
                    {
                        case MessageTypeEnum.mix:
                            _mixerChannels[ui24Message.ChannelNumber].ChannelFaderValue = ui24Message.FaderValue;
                            foreach (var chOnLayer in chOnControllerLayers.Where(chOnlayer => chOnlayer.isOnLayer))
                            {
                                if (ui24Message.IsValid && chOnLayer.controllerChannelNumber <= 8)
                                {
                                    _controllers.ForEach(controller =>
                                    {
                                        chOnLayer.controller.SetFader(chOnLayer.controllerChannelNumber, ui24Message.FaderValue);
                                    });
                                }
                                
                            }
                            break;
                        case MessageTypeEnum.name:
                            _mixerChannels[ui24Message.ChannelNumber].Name = ui24Message.ChannelName;
                            foreach (var chOnLayer in chOnControllerLayers.Where(chOnlayer => chOnlayer.isOnLayer))
                            {

                                if (ui24Message.IsValid && chOnLayer.controllerChannelNumber < 8)
                                {
                                    chOnLayer.controller.WriteTextToChannelLCDFirstLine(chOnLayer.controllerChannelNumber, _mixerChannels[ui24Message.ChannelNumber].Name);
                                }
                            }
                            break;
                        case MessageTypeEnum.gain:
                            if (_mixerChannels[ui24Message.ChannelNumber] is InputChannel && ui24Message.ChannelType == ChannelTypeEnum.HW)
                            {
                                (_mixerChannels[ui24Message.ChannelNumber] as InputChannel).Gain = ui24Message.Gain;
                                foreach (var chOnLayer in chOnControllerLayers.Where(chOnlayer => chOnlayer.isOnLayer))
                                {

                                    if (ui24Message.IsValid && chOnLayer.controllerChannelNumber < 8 && this.KnobsFunction == KnobsFunctionEnum.Gain)
                                    {
                                        chOnLayer.controller.SetKnobLed(chOnLayer.controllerChannelNumber, ui24Message.Gain);
                                    }
                                }
                            }
                            break;
                        case MessageTypeEnum.pan:
                            _mixerChannels[ui24Message.ChannelNumber].Panorama = ui24Message.Panorama;
                            foreach (var chOnLayer in chOnControllerLayers.Where(chOnlayer => chOnlayer.isOnLayer))
                            {

                                if (ui24Message.IsValid && chOnLayer.controllerChannelNumber < 8 && this.KnobsFunction == KnobsFunctionEnum.Pan)
                                {
                                    chOnLayer.controller.SetKnobLed(chOnLayer.controllerChannelNumber, ui24Message.Panorama);
                                }
                            }
                            break;

                        case MessageTypeEnum.mute:
                            _mixerChannels[ui24Message.ChannelNumber].IsMute = ui24Message.LogicValue;
                            foreach (var chOnLayer in chOnControllerLayers.Where(chOnlayer => chOnlayer.isOnLayer))
                            {

                                if (ui24Message.IsValid && chOnLayer.controllerChannelNumber < 8)
                                {
                                    chOnLayer.controller.SetMuteLed(chOnLayer.controllerChannelNumber, ui24Message.LogicValue);
                                }
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
                            foreach (var chOnLayer in chOnControllerLayers.Where(chOnlayer => chOnlayer.isOnLayer))
                            {

                                if (ui24Message.IsValid && chOnLayer.controllerChannelNumber < 8)
                                {

                                    chOnLayer.controller.SetMuteLed(chOnLayer.controllerChannelNumber, _mixerChannels[ui24Message.ChannelNumber].IsMute);
                                }
                            }
                            break;
                        case MessageTypeEnum.mgMask:
                            _mixerChannels[ui24Message.ChannelNumber].MuteGroupMask = (UInt32)ui24Message.IntValue;
                            SetControllerMuteButtonsForCurrentLayer();
                            break;
                        case MessageTypeEnum.solo:
                            _mixerChannels[ui24Message.ChannelNumber].IsSolo = ui24Message.LogicValue;
                            foreach (var chOnLayer in chOnControllerLayers.Where(chOnlayer => chOnlayer.isOnLayer))
                            {

                                if (ui24Message.IsValid && chOnLayer.controllerChannelNumber < 8)
                                {
                                    chOnLayer.controller.SetSoloLed(chOnLayer.controllerChannelNumber, ui24Message.LogicValue);
                                }
                            }
                            break;
                        case MessageTypeEnum.mtkrec:
                            if (_mixerChannels[ui24Message.ChannelNumber] is IRecordable && _settings.ChannelRecButtonBehavior == BridgeSettings.ChannelRecButtonBehaviorEnum.Rec)
                            {
                                (_mixerChannels[ui24Message.ChannelNumber] as IRecordable).IsRec = ui24Message.LogicValue;
                                foreach (var chOnLayer in chOnControllerLayers.Where(chOnlayer => chOnlayer.isOnLayer))
                                {
                                    if (ui24Message.IsValid && chOnLayer.controllerChannelNumber < 8)
                                    {
                                        chOnLayer.controller.SetRecLed(chOnLayer.controllerChannelNumber, ui24Message.LogicValue);
                                    }
                                }
                            }
                            break;
                        case MessageTypeEnum.phantom:
                            if (_mixerChannels[ui24Message.ChannelNumber] is IInputable && _settings.ChannelRecButtonBehavior == BridgeSettings.ChannelRecButtonBehaviorEnum.Phantom)
                            {
                                (_mixerChannels[ui24Message.ChannelNumber] as IInputable).IsPhantom = ui24Message.LogicValue;
                                foreach (var chOnLayer in chOnControllerLayers.Where(chOnlayer => chOnlayer.isOnLayer))
                                {

                                    if (ui24Message.IsValid && chOnLayer.controllerChannelNumber < 8)
                                    {
                                        chOnLayer.controller.SetRecLed(chOnLayer.controllerChannelNumber, ui24Message.LogicValue);
                                    }
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
                                    _controllers.ForEach(controller => controller.SetLed(ButtonsEnum.Rec, ui24Message.LogicValue || _mixer.IsTwoTrackRecordingRun));
                                    _mixer.IsMultitrackRecordingRun = ui24Message.LogicValue;
                                }
                            }
                            break;
                        case MessageTypeEnum.isRecording:
                            if (_settings.RecButtonBehavior == BridgeSettings.RecButtonBehaviorEnum.OnlyTwoTrack || _settings.RecButtonBehavior == BridgeSettings.RecButtonBehaviorEnum.TwoTrackAndMTK)
                            {
                                _controllers.ForEach(controller => controller.SetLed(ButtonsEnum.Rec, ui24Message.LogicValue || _mixer.IsMultitrackRecordingRun));
                                _mixer.IsTwoTrackRecordingRun = ui24Message.LogicValue;
                            }
                            break;
                        case MessageTypeEnum.currentState:
                            _controllers.ForEach(controller => controller.SetLed(ButtonsEnum.Play, ui24Message.LogicValue));
                            break;
                        case MessageTypeEnum.bpm:
                            _controllers.ForEach(controller => controller.WriteTextToTicksDisplay(ui24Message.FaderValue.ToString().PadLeft(3, ' ')));
                            break;
                        case MessageTypeEnum.auxFaderValue:
                            _mixerChannels[ui24Message.ChannelNumber].AuxSendValues[ui24Message.IntValue.ToAux()] = ui24Message.FaderValue;
                            foreach (var chOnLayer in chOnControllerLayers.Where(chOnlayer => chOnlayer.isOnLayer))
                            {

                                if (chOnLayer.controllerChannelNumber < 8 && _selectedLayout == ui24Message.IntValue.ToAux())
                                {
                                    chOnLayer.controller.SetFader(chOnLayer.controllerChannelNumber, ui24Message.FaderValue);
                                }
                            }
                            break;
                        case MessageTypeEnum.fxFaderValue:
                            _mixerChannels[ui24Message.ChannelNumber].AuxSendValues[ui24Message.IntValue.ToFx()] = ui24Message.FaderValue;
                            foreach (var chOnLayer in chOnControllerLayers.Where(chOnlayer => chOnlayer.isOnLayer))
                            {

                                if (chOnLayer.controllerChannelNumber < 8 && _selectedLayout == ui24Message.IntValue.ToFx())
                                {
                                    chOnLayer.controller.SetFader(chOnLayer.controllerChannelNumber, ui24Message.FaderValue);
                                }
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
                    //var newViewChannel = parts.Last().Trim('[', ']').Split(',');
                    if (parts.Last().StartsWith('[')) //managed only the view values settings
                    {
                        JsonSerializerOptions options = new JsonSerializerOptions();
                        options.TypeInfoResolver = new MyClassTypeResolver<int[] >();
                        var newViewChannels = JsonSerializer.Deserialize<int[]>(parts.Last(),options);
                        var viewGroupString = parts[1].Split('.').Last();
                        int viewGroup;
                        if (int.TryParse(viewGroupString, out viewGroup))
                        {
                            _mixer.setChannelsToViewLayerAndPosition(newViewChannels, viewGroup);

                        }
                        SetControllerToCurrentLayerAndSend();
                    }
                }
                else if (m.StartsWith($"BMSG^SYNC^{_settings.SyncID}"))
                {
                    int ch;
                    var chString = m.Split("^").Last();
                    if (int.TryParse(chString, out ch))
                    {
                        if (SelectedChannel != -1)
                            _mixerChannels[SelectedChannel].IsSelected = false;
                        if (ch == -1) //main channel
                        {
                            _mixerChannels[54].IsSelected = true;
                            SelectedChannel = 54;
                            _mixer.UserLayerEditNewChannel = -1;

                        }
                        else
                        {
                            _mixerChannels[ch].IsSelected = true;
                            SelectedChannel = ch;
                            _mixer.UserLayerEditNewChannel = ch;
                        }
                        _controllers.ForEach(controller =>
                        {
                            var channelNumber = _mixer.getCurrentLayer(controller.ChannelOffset).Select((item, i) => new { Channel = item, controllerChannelNumber = i })
                               .Where(c => c.Channel == _mixerChannels[SelectedChannel].ChannelNumberInMixer).FirstOrDefault();
                            if (channelNumber != null)
                            {
                                controller.SetSelectLed(channelNumber.controllerChannelNumber, true);
                            }
                            else
                                controller.SetSelectLed(0, false);
                        });
                    }
                }
                else if (m.StartsWith("VU2^"))
                {
                    var ui24rvumessage = new UI24RVUMessage(m);
                    _controllers.ForEach(controller =>
                    {
                        for (int i = 0; i < 8; i++)
                        {
                           var channelNumber = _mixer.getChannelNumberInCurrentLayer(i,controller.ChannelOffset);
                            if (channelNumber < 48 && channelNumber >= 0) //input channel
                            {
                                controller.WriteChannelMeter(i, ui24rvumessage.VUChannels[channelNumber].GetPostValue());
                            }
                        }
                    });
                }

            }
        }
        private void SetMuteGroupsLeds()
        {
            UInt32 mask = _mixer.MuteMask;
            _controllers.ForEach(controller => { 
                for (int i = 0; i < 6; ++i)
                {
                    controller.SetLed(ButtonsEnum.MuteGroup1 + i, ((mask >> i) & 1) == 1);
                }
                controller.SetLed(ButtonsEnum.Save, ((mask >> Mixer._muteAllBit) & 1) == 1);
                controller.SetLed(ButtonsEnum.Undo, ((mask >> Mixer._muteAllFxBit) & 1) == 1);
            });
        }
        protected void SendMessage(string message, bool isDebug = true)
        {
            if (_settings.MessageWriter != null)
                _settings.MessageWriter(message, isDebug);
        }
        public void Dispose()
        {
            //_secondaryBridge?.Dispose();
            SendMessage("Disconnecting UI24R....", false);
            if (_client != null)
            {
                _client.Dispose();
                _client = null;
            }
            _controllers.ForEach(controller =>
            {
                if ((controller is IDisposable) && controller != null)
                {
                    (controller as IDisposable).Dispose();
                }
            });

        }
    }
}
