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
            _client = new WebsocketClient(new Uri(address));
            _client.MessageReceived.Subscribe(msg => UI24RMessageReceived(msg));
            SendMessage("Connecting to UI24R....");
            _client.Start();
        }

        protected void UI24RMessageReceived(ResponseMessage msg)
        {
            if (msg.Text.Length > 3)
            {
                SendMessage(msg.Text);
            }
            else
            {
                _client.Send(msg.Text);
                _client.Send("3:::ALIVE");
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
