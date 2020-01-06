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

        //0-23: input channels
        //24-25: Linie In L/R
        //26-27: Player L/R
        //28-31: FX channels
        //32-37: Subgroups
        //38-47: AUX 1-10
        //48-53: VCA 1-6
        protected string[][] _viewViewGroups = {
            new string[] { "0", "1", "2", "3", "4", "5", "6", "7"},
            new string[] { "8", "9", "10", "11", "12", "13", "14", "15"},
            new string[] { "16", "17", "18", "19", "20", "21", "22", "23"},
            new string[] { "24", "25", "26", "27", "28", "29", "30", "31"},
            new string[] { "32", "33", "34", "35", "36", "37", "38", "39"},
            new string[] { "40", "41", "42", "43", "44", "45", "46", "47"}
        };
        protected int _selectedViewGroup = 0;
        /// <summary>
        /// Contains the channels of the mixer. the channel number like the view groups 0-23 input channels, 24-25 Line in etc.
        /// </summary>
        protected List<ChannelBase> _mixerChannels;


        public UI24RBridge(string address, IMIDIController midiController):this(address, midiController, null)
        {
        }

        /// <summary>
        ///  Represent the bridge between the UI24R and a DAW controller
        /// </summary>
        /// <param name="address">address of the mixer (eg: 'ws://192.168.5.2')</param>
        /// <param name="midiController">the daw controller connection object</param>
        /// <param name="sendMessageAction">the logging function (implemented in the host app)</param>
        public UI24RBridge(string address, IMIDIController midiController, Action<string> sendMessageAction)
        {
            InitializeChannels();
            _sendMessageAction = sendMessageAction;
            _midiController = midiController;
            _midiController.FaderEvent += MidiController_FaderEvent;
            _midiController.PresetUp += _midiController_PresetUp;
            _midiController.PresetDown += _midiController_PresetDown;
            _client = new WebsocketClient(new Uri(address));
            _client.MessageReceived.Subscribe(msg => UI24RMessageReceived(msg));
            SendMessage("Connecting to UI24R....");
            _client.Start();
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
            var ch = int.Parse(_viewViewGroups[_selectedViewGroup][e.ChannelNumber]);
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
            foreach (var ch in channels)
            {
                var channelNumber = int.Parse(ch.Channel);
                _midiController.SetFader(ch.controllerChannelNumber, _mixerChannels[channelNumber].ChannelFaderValue);
            }
        }

        private void ProcessUI24Message(string text)
        {
            var messageparts = text.Replace("3:::","").Split('\n');
            foreach (var m in messageparts)
            {
                if (m.Contains("SETD^i.") ||
                    m.Contains("SETD^l.") ||
                    m.Contains("SETD^p.") ||
                    m.Contains("SETD^f.") ||
                    m.Contains("SETD^s.") ||
                    m.Contains("SETD^a.") ||
                    m.Contains("SETD^v.")
                    )
                {
                    if (m.Contains(".mix"))
                    {
                        var ui24Message = new UI24Message(m);
                        var channelNumber = _viewViewGroups[_selectedViewGroup].Select((item, i) => new { Channel = item, controllerChannelNumber = i })
                            .Where(c => c.Channel == ui24Message.ChannelNumber.ToString()).FirstOrDefault();
                        _mixerChannels[ui24Message.ChannelNumber].ChannelFaderValue = ui24Message.FaderValue;
                        if (ui24Message.IsValid && channelNumber != null && channelNumber.controllerChannelNumber < 8)
                        {
                            _midiController.SetFader(channelNumber.controllerChannelNumber, ui24Message.FaderValue);
                        }
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
                            _viewViewGroups[viewGroup] = newViewChannel;
                        }
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
