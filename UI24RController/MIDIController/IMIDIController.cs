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
    event EventHandler<GainEventArgs> GainEvent;
    event EventHandler<ChannelEventArgs> SelectChannelEvent;

    bool SetFader(int channelNumber, double faderValue);
    bool SetGainLed(int channelNumber, double gainValue);

    void SetSelectLed(int channelNumber, bool turnOn);

    public void WriteTextToChannelLCD(int channelNumber, string text);
}
