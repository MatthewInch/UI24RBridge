# UI24RBridge
Bridge between the UI24R and a MIDI controller.\
This is a beta project. It tested only in windows, a Behringer BCF2000 and a begringer X-Touch midi controller.

You can download the latest release for
- **Windows**: https://github.com/MatthewInch/UI24RBridge/blob/master/UI24RBridgeTest/Publish/win-x86-core.zip
- **Linux**: https://github.com/MatthewInch/UI24RBridge/blob/master/UI24RBridgeTest/Publish/linux-x64.zip
 The Linux binary wasn't tested.

Implemented the Mackie Control protocol (It can work with any daw controller that can use in MC mode)\
The earlier protocol has not been removed but the new functions only implemented in MC mode.



### The Bridge functionalities:
 - Use 6 layer of faders\
You can select the channels in the mixer own surface with the Global view groups.\
You have to select 8 channels per viewgroup at least.\
Switch between layers with Fader Bank << and >> buttons\
If there isn't any global view group in the mixer the Bridge use the channel in this order:
    - input 1-8;
    - input 9-16;
    - input 17-24;
    - Line in, Player, FX 1-4;
    - SUB 1-6; AUX 1-2
    - AUX 3-10;
 - The faders work on every type of channels
 - The knobs set the gain on the input channels
 - Select, Solo, Mute buttons work on every channel
 - Buttons F1 - F8 switch to AUX1-8 sends
 - Button Switch, Option, Control and Alt switch to FX1-4 sends
 - Control Media player with <<, >>, Stop, Play buttons
 - Strart Recording with Rec button




### Configuration
In the settings file (**appsettings.json**):
- **UI24R-Url**: the mixer url (simply copy the url from the browser and replace http to ws and remove the /mixer.html from the end)
- **MIDI-Input-Name**,**MIDI-Output-Name**: The controller name. If you don't know it simple remove these to row from the config file and modify the mixer url to an invalid value. The UI24RBridge write all of the availabel MIDI device to the console.
- **Protocol**: MC or HUI, or empty (for now use **MC**)
- **SyncID** if you want to use the select button you can set the syncID to the same value that you use in the mixer's default surface (you can set it on the Settings/Locals page)
 - **DefaultRecButton**: If you press the rec button on the controller, the bridge start/stop the MTK and/or 2 track recording it depend the value of the "DefaultRecButton". Possible value is: **onlyMTK**, **only2Track**, **2TrackAndMTK**
 - **DefaultChannelRecButton**: Sets what function has a rec button on controller. You can use **phantom** for controlling phantom voltage or **rec** to set multitrack recording for this channel; default is "rec
 - **AuxButtonBehavior**: If you want faders switched between main send, aux sends and fx send only during holding respective buttons (**Release**) or to be switched (**Lock**) to current aux/fx send until next press of aux/fx select button happened.


**Example of the settings file**

{\
    "UI24R-Url": "ws://192.168.5.2",\
    "MIDI-Input-Name": "X-Touch",\
    "MIDI-Output-Name": "X-Touch",\
    "Protocol": "MC",\
    "SyncID": "Abaliget",\
    "DefaultRecButton": "2TrackAndMTK", //possible values: "onlyMTK", "only2Track", "2TrackAndMTK"; default is "2TrackAndMTK\
    "DefaultChannelRecButton": "phantom", //possible values: "phantom","rec"; default is "rec\
    "AuxButtonBehavior": "Lock", //possible values: "Release", "Lock"; Default is "Release"\
    "DebugMessages": "true",\
}
