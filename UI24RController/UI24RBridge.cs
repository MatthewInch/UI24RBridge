using System;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using Websocket.Client;

namespace UI24RController
{
    public class UI24RBridge : IDisposable
    {

        protected WebsocketClient _client;
        protected Action<string> _sendMessageAction;
        protected IMIDIController _midiController;
        protected string[] _viewChannel = new string[] { "0", "1", "2", "3", "4", "5", "6", "7" };

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
            _sendMessageAction = sendMessageAction;
            _midiController = midiController;
            _midiController.FaderEvent += MidiController_FaderEvent;
            _client = new WebsocketClient(new Uri(address));
            _client.MessageReceived.Subscribe(msg => UI24RMessageReceived(msg));
            SendMessage("Connecting to UI24R....");
            _client.Start();
        }

        private void MidiController_FaderEvent(object sender, MIDIController.FaderEventArgs e)
        {
            _client.Send($"3:::SETD^i.{_viewChannel[e.ChannelNumber]}.mix^{e.FaderValue.ToString().Replace(',', '.')}");
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

        private void ProcessUI24Message(string text)
        {
            var messageparts = text.Split('\n');
            foreach (var m in messageparts)
            {
                if (m.Contains("SETD^i.") && m.Contains(".mix"))
                {
                    var ui24Message = new UI24Message(m);
                    var channelNumber = _viewChannel.Select((item, i)=> new { Item = item, viewChNumber = i } )
                        .Where(c => c.Item == ui24Message.ChannelNumber.ToString()).FirstOrDefault();
                    if (ui24Message.IsValid && channelNumber != null && channelNumber.viewChNumber < 8)
                    {
                        _midiController.SetFader(channelNumber.viewChNumber, ui24Message.FaderValue);
                    }
                }
                else if (m.StartsWith("SETS^vg.0^")) //first global view group (e.g:"SETS^vg.0^[0,1,2,3,4,5,6,17,18,19,20,22,23,38,39,40,41,42,43,44,45,48,49,21]")
                {
                    var newViewChannel = m.Split('^').Last().Trim('[',']').Split(',');

                    if (newViewChannel.Length > 7)
                    {
                        _viewChannel = newViewChannel;
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
