using System;
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
            _client.Send($"3:::SETD^i.{e.ChannelNumber}.mix^{e.FaderValue.ToString().Replace(',', '.')}");
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
                    if (ui24Message.IsValid && ui24Message.ChannelNumber < 8)
                    {
                        _midiController.SetFader(ui24Message.ChannelNumber, ui24Message.FaderValue);
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
