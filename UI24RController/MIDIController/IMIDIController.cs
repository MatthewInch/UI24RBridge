using System;
using UI24RController.MIDIController;

public interface IMIDIController
{
    #region Connection
    string[] GetInputDeviceNames();
    string[] GetOutputDeviceNames();
    bool ConnectInputDevice(string deviceName);
    bool ConnectOutputDevice(string deviceName);
    #endregion

    event EventHandler<MessageEventArgs> MessageReceived;
    event EventHandler<FaderEventArgs> FaderEvent;
    event EventHandler<EventArgs> PresetUp;
    event EventHandler<EventArgs> PresetDown;

    bool SetFader(int channelNumber, double faderValue);

}
