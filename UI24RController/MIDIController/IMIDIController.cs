using System;
using System.Collections.Generic;
using UI24RController.MIDIController;

public interface IMIDIController
{
    #region Connection
    string[] GetInputDeviceNames();
    string[] GetOutputDeviceNames();
    bool ConnectInputDevice(string deviceName);
    bool ConnectOutputDevice(string deviceName);

    bool ReConnectDevice();

    bool IsConnectionErrorOccured { get; }

    event EventHandler<EventArgs> ConnectionErrorEvent;

    #endregion

    event EventHandler<FaderEventArgs> FaderEvent;
    event EventHandler<MessageEventArgs> MessageReceived;
    event EventHandler<GainEventArgs> GainEvent;
    event EventHandler<ChannelEventArgs> SelectChannelEvent;
    event EventHandler<ChannelEventArgs> MuteChannelEvent;
    event EventHandler<ChannelEventArgs> SoloChannelEvent;
    event EventHandler<ChannelEventArgs> RecChannelEvent;

    #region Button Events
    event EventHandler<EventArgs> PresetUp;
    event EventHandler<EventArgs> PresetDown;

    event EventHandler<EventArgs> SaveEvent;
    event EventHandler<EventArgs> UndoEvent;
    event EventHandler<EventArgs> CancelEvent;
    event EventHandler<EventArgs> EnterEvent;

    event EventHandler<EventArgs> UpEvent;
    event EventHandler<EventArgs> DownEvent;
    event EventHandler<EventArgs> LeftEvent;
    event EventHandler<EventArgs> RightEvent;
    event EventHandler<EventArgs> CenterEvent;

    event EventHandler<EventArgs> PrevEvent;
    event EventHandler<EventArgs> NextEvent;
    event EventHandler<EventArgs> StopEvent;
    event EventHandler<EventArgs> PlayEvent;
    event EventHandler<EventArgs> RecEvent;

    event EventHandler<FunctionEventArgs> FunctionButtonEvent;


    #endregion

    Dictionary<string, byte> ButtonsID { get; set; }

    bool SetFader(int channelNumber, double faderValue);
    bool SetGainLed(int channelNumber, double gainValue);

    void SetSelectLed(int channelNumber, bool turnOn);
    void SetMuteLed(int channelNumber, bool turnOn);
    void SetSoloLed(int channelNumber, bool turnOn);
    void SetRecLed(int channelNumber, bool turnOn);
    void SetLed(string buttonName, bool turnOn);

    public void WriteTextToChannelLCD(int channelNumber, string text);
    public void WriteTextToLCD(string text);
    public void WriteTextToLCD(string text, int delay);

    void WriteChannelMeter(int channelNumber, byte value);
    void TurnOffClipLed(int channelNumber);
}
