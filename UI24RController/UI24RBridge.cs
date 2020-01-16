using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using UI24RController.UI24RChannels;
using Websocket.Client;

namespace UI24RController
{
    public class UI24RBridge : IDisposable
    {

        protected WebsocketClient _client;
        protected Action<string> _sendMessageAction;
        protected IMIDIController _midiController;
        protected string _syncID;
        protected int _selectedChannel = -1; //-1 = no selected channel

        //0-23: input channels
        //24-25: Linie In L/R
        //26-27: Player L/R
        //28-31: FX channels
        //32-37: Subgroups
        //38-47: AUX 1-10
        //48-53: VCA 1-6

        protected int[][] _viewViewGroups = {
            new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 54},
            new int[] { 8, 9, 10, 11, 12, 13, 14, 15, 54},
            new int[] { 16, 17, 18, 19, 20, 21, 22, 23, 54},
            new int[] { 24, 25, 26, 27, 28, 29, 30, 31, 54},
            new int[] { 32, 33, 34, 35, 36, 37, 38, 39, 54},
            new int[] { 40, 41, 42, 43, 44, 45, 46, 47, 54 }
        };
        protected int _selectedViewGroup = 0;
        /// <summary>
        /// Contains the channels of the mixer. the channel number like the view groups 0-23 input channels, 24-25 Line in etc.
        /// </summary>
        protected List<ChannelBase> _mixerChannels;


        public UI24RBridge(string address, IMIDIController midiController):this(address, midiController, null, "SyncID")
        {
        }

        /// <summary>
        ///  Represent the bridge between the UI24R and a DAW controller
        /// </summary>
        /// <param name="address">address of the mixer (eg: 'ws://192.168.5.2')</param>
        /// <param name="midiController">the daw controller connection object</param>
        /// <param name="sendMessageAction">the logging function (implemented in the host app)</param>
        public UI24RBridge(string address, IMIDIController midiController, Action<string> sendMessageAction, string syncID)
        {
            InitializeChannels();
            _syncID = syncID;
            _sendMessageAction = sendMessageAction;
            _midiController = midiController;
            _midiController.FaderEvent += MidiController_FaderEvent;
            _midiController.PresetUp += _midiController_PresetUp;
            _midiController.PresetDown += _midiController_PresetDown;
            _midiController.GainEvent += _midiController_GainEvent;
            _midiController.MuteChannelEvent += _midiController_MuteChannelEvent;
            _midiController.SoloChannelEvent += _midiController_SoloChannelEvent;
            _midiController.SelectChannelEvent += _midiController_SelectChannelEvent;
            _midiController.RecChannelEvent += _midiController_RecChannelEvent;
            _client = new WebsocketClient(new Uri(address));
            _client.MessageReceived.Subscribe(msg => UI24RMessageReceived(msg));
            _midiController.WriteTextToLCD("");
            SendMessage("Connecting to UI24R....");
            _client.Start();
        }

        private void _midiController_RecChannelEvent(object sender, MIDIController.ChannelEventArgs e)
        {
            var ch = _viewViewGroups[_selectedViewGroup][e.ChannelNumber];
            if (_mixerChannels[ch] is InputChannel || _mixerChannels[ch] is LineInChannel)
            {
                var channel = _mixerChannels[ch];
                channel.IsRec = !channel.IsRec;
                _client.Send(channel.RecMessage());
                _midiController.SetRecLed(e.ChannelNumber, channel.IsRec);
            }
        }

        private void _midiController_SoloChannelEvent(object sender, MIDIController.ChannelEventArgs e)
        {
            var ch = _viewViewGroups[_selectedViewGroup][e.ChannelNumber];
            _mixerChannels[ch].IsSolo = !_mixerChannels[ch].IsSolo;
            _client.Send(_mixerChannels[ch].SoloMessage());
            _midiController.SetSoloLed(ch, _mixerChannels[ch].IsSolo);
        }

        private void _midiController_MuteChannelEvent(object sender, MIDIController.ChannelEventArgs e)
        {
            var ch = _viewViewGroups[_selectedViewGroup][e.ChannelNumber];
            _mixerChannels[ch].IsMute = !_mixerChannels[ch].IsMute;
            _client.Send(_mixerChannels[ch].MuteMessage());
            _midiController.SetMuteLed(ch, _mixerChannels[ch].IsMute);
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
            _midiController.SetSelectLed(e.ChannelNumber, true);
            _client.Send(_mixerChannels[ch].SelectChannelMessage(_syncID));
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
                _midiController.SetGainLed(e.ChannelNumber, inputChannel.Gain);
            }
        }

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

        private void _midiController_PresetDown(object sender, EventArgs e)
        {
            if (_selectedViewGroup > 0)
                _selectedViewGroup = (_selectedViewGroup - 1) % 6;
            SetControllerToCurrentViewGroup();
        }


        private void _midiController_PresetUp(object sender, EventArgs e)
        {
            if (_selectedViewGroup <5)
                _selectedViewGroup = (_selectedViewGroup+1) % 6;
            SetControllerToCurrentViewGroup();
        }

        private void MidiController_FaderEvent(object sender, MIDIController.FaderEventArgs e)
        {
            var ch = _viewViewGroups[_selectedViewGroup][e.ChannelNumber];
            _mixerChannels[ch].ChannelFaderValue = e.FaderValue;
            _client.Send(_mixerChannels[ch].MixFaderMessage());
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
            _midiController.SetSelectLed(0, false); //turn off all selecetd led;
            foreach (var ch in channels)
            {
                var channelNumber = ch.Channel;
                _midiController.SetFader(ch.controllerChannelNumber, _mixerChannels[channelNumber].ChannelFaderValue);
                if (_mixerChannels[channelNumber].IsSelected)
                {
                    _midiController.SetSelectLed(ch.controllerChannelNumber, true);
                }

                _midiController.SetGainLed(ch.controllerChannelNumber, _mixerChannels[channelNumber].Gain);
                _midiController.WriteTextToChannelLCD(ch.controllerChannelNumber, _mixerChannels[channelNumber].Name);
                _midiController.SetMuteLed(ch.controllerChannelNumber, _mixerChannels[channelNumber].IsMute);
                _midiController.SetSoloLed(ch.controllerChannelNumber, _mixerChannels[channelNumber].IsSolo);
                _midiController.SetRecLed(ch.controllerChannelNumber, _mixerChannels[channelNumber].IsRec);
            }
        }

        private void ProcessUI24Message(string text)
        {
            var messageparts = text.Replace("3:::","").Split('\n');
            foreach (var m in messageparts)
            {
                var ui24Message = new UI24Message(m);
                if (ui24Message.IsValid)
                {
                    var controllerChannelNumber = _viewViewGroups[_selectedViewGroup].Select((item, i) => new { Channel = item, controllerChannelNumber = i })
                        .Where(c => c.Channel == ui24Message.ChannelNumber).FirstOrDefault();

                    switch (ui24Message.MessageType)
                    {
                        case MessageTypeEnum.mix:
                            _mixerChannels[ui24Message.ChannelNumber].ChannelFaderValue = ui24Message.FaderValue;
                            if (ui24Message.IsValid && controllerChannelNumber != null && controllerChannelNumber.controllerChannelNumber <= 8)
                            {
                                _midiController.SetFader(controllerChannelNumber.controllerChannelNumber, ui24Message.FaderValue);
                            }
                            break;
                        case MessageTypeEnum.name:
                            if (ui24Message.ChannelName != "")
                            {
                                _mixerChannels[ui24Message.ChannelNumber].Name = ui24Message.ChannelName;
                            }
                            if (ui24Message.IsValid && controllerChannelNumber != null && controllerChannelNumber.controllerChannelNumber < 8)
                            {
                                _midiController.WriteTextToChannelLCD(controllerChannelNumber.controllerChannelNumber, ui24Message.ChannelName);
                            }
                            break;
                        case MessageTypeEnum.gain:
                            if (_mixerChannels[ui24Message.ChannelNumber] is InputChannel)
                            {
                                (_mixerChannels[ui24Message.ChannelNumber] as InputChannel).Gain = ui24Message.Gain;
                                if (ui24Message.IsValid && controllerChannelNumber != null && controllerChannelNumber.controllerChannelNumber < 8)
                                {
                                    _midiController.SetGainLed(controllerChannelNumber.controllerChannelNumber, ui24Message.Gain);
                                }
                            }
                            break;
                        case MessageTypeEnum.mute:
                            _mixerChannels[ui24Message.ChannelNumber].IsMute = ui24Message.LogicValue;
                            if (ui24Message.IsValid && controllerChannelNumber != null && controllerChannelNumber.controllerChannelNumber < 8)
                            {
                                _midiController.SetMuteLed(controllerChannelNumber.controllerChannelNumber, ui24Message.LogicValue);
                            }
                            break;
                        case MessageTypeEnum.solo:
                            _mixerChannels[ui24Message.ChannelNumber].IsSolo = ui24Message.LogicValue;
                            if (ui24Message.IsValid && controllerChannelNumber != null && controllerChannelNumber.controllerChannelNumber < 8)
                            {
                                _midiController.SetSoloLed(controllerChannelNumber.controllerChannelNumber, ui24Message.LogicValue);
                            }
                            break;
                        case MessageTypeEnum.mtkrec:
                            _mixerChannels[ui24Message.ChannelNumber].IsRec = ui24Message.LogicValue;
                            if (ui24Message.IsValid && controllerChannelNumber != null && controllerChannelNumber.controllerChannelNumber < 8)
                            {
                                _midiController.SetRecLed(controllerChannelNumber.controllerChannelNumber, ui24Message.LogicValue);
                            }
                            break;
                    }
                }
                else if (m.StartsWith("SETS^vg.")) //first global view group (e.g:"SETS^vg.0^[0,1,2,3,4,5,6,17,18,19,20,22,23,38,39,40,41,42,43,44,45,48,49,21]")
                {
                    var parts = m.Split('^');
                    var newViewChannel = parts.Last().Trim('[',']').Split(',');
                    var viewGroupString = parts[1].Split('.').Last();
                    int viewGroup;
                    if (int.TryParse(viewGroupString, out viewGroup))
                    {
                        if (newViewChannel.Length > 7)
                        {
                            var newViewChannelWithMain = newViewChannel.ToList().Take(8).ToList();
                            newViewChannelWithMain.Add("54");
                            _viewViewGroups[viewGroup] = newViewChannelWithMain.Select(i=> int.Parse(i)).ToArray();
                        }
                    }
                }
                else if (m.StartsWith($"BMSG^SYNC^{_syncID}"))
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
                           .Where(c => c.Channel == _mixerChannels[ch].ChannelNumberInMixer).FirstOrDefault();
                        if (channelNumber != null)
                        {
                            _midiController.SetSelectLed(channelNumber.controllerChannelNumber, true);
                        }
                        else
                            _midiController.SetSelectLed(0, false);
                    }
                }
            }
        }

        protected void SendMessage(string message)
        {
            if (_sendMessageAction != null)
                _sendMessageAction(message);
        }

        public void Dispose()
        {
            SendMessage("Disconnecting UI24R....");
            if (_client != null)
            {
                _client.Dispose();
                _client = null;
            }
        }
    }
}
