# UI24RBridge
Bridge between the UI24R and a MIDI controller.\
This is a beta project. It tested only in Windows, with Behringer BCF2000 and with Behringer X-Touch midi controller.

You can download the latest release for
- [Windows 32bit core](https://github.com/MatthewInch/UI24RBridge/blob/master/UI24RBridgeTest/Publish/win-x86-core.rar)
- [Windows 32bit](https://github.com/MatthewInch/UI24RBridge/blob/master/UI24RBridgeTest/Publish/win-x86.rar)
- [Windows 64bit](https://github.com/MatthewInch/UI24RBridge/blob/master/UI24RBridgeTest/Publish/win-x64.rar)
- [Linux 32bit](https://github.com/MatthewInch/UI24RBridge/blob/master/UI24RBridgeTest/Publish/linux-x86.rar)
- [Linux 64bit](https://github.com/MatthewInch/UI24RBridge/blob/master/UI24RBridgeTest/Publish/linux-x64.rar)
- [MacOS](https://github.com/MatthewInch/UI24RBridge/blob/master/UI24RBridgeTest/Publish/MacOS.rar)

The Linux and MacOS binaries wasn't tested.

Implemented the Mackie Control protocol (It can work with any DAW controller that can use in MC mode).\
The earlier protocol has not been removed but the new functions only implemented in MC mode.



### The Bridge functionalities:
 - Use 2 groups with 6 layers of faders for each bank
	- Bank I
		- Layer 1: Input 1-8
		- Layer 2: Input 9-16
		- Layer 3: Input 17-24
		- Layer 4: Line in, Player, FX 1-4
		- Layer 5: SUB 1-6; AUX 1-2
		- Layer 6: AUX 3-10
	- Bank V (configurable with Global View Groups in mixer app)
		- Layer 1: VIEW 1 (if set)
		- Layer 2: VIEW 2 (if set)
		- Layer 3: VIEW 3 (if set)
		- Layer 4: VIEW 4 (if set)
		- Layer 5: VIEW 5 (if set)
		- Layer 6: VIEW 6 (if set)
	- Bank U (user defined layers, load from ViewGroups.json file, initially the same as Bank I)
		- Layer 1: User defined
		- Layer 2: User defined
		- Layer 3: User defined
		- Layer 4: User defined
		- Layer 5: User defined
		- Layer 6: User defined
	- You have to select 8 channels per view group at least otherwise the view group will be ignored
	- Switch between Banks with Fader Bank << and >> buttons
	- Switch between Layers in current Bank with Channel Bank << and >> buttons
 - The faders work on every type of channels
 - The knobs set the gain on the input channels
 - Select, Solo, Mute buttons work on every channel
 - Buttons F1-F8 switch to AUX1-8 sends
 - Button Switch, Option, Control and Alt switch to FX1-4 sends
 - Control Media player with <<, >>, Stop, Play buttons
 - Start Recording with Rec button
 - Scrub button to save Layer Bank U
 - SMTPE/Beats Button to Tap Tempo

### Future functions
 - User configurable Layers in bank U with Rotary wheel
 - Mute Groups, Mute All, Mute FX
 - Pan, HPF, EQ, Dyn


### Configuration
In the settings file (**appsettings.json**):
- **UI24R-Url**: the mixer url (simply copy the url from the browser and replace http to ws and remove the /mixer.html from the end)
- **MIDI-Input-Name**,**MIDI-Output-Name**: The controller name. If you don't know it simple remove these to row from the config file and modify the mixer url to an invalid value. The UI24RBridge write all of the available MIDI device to the console.
- **Protocol**: MC or HUI, or empty (for now use **MC**)
- **SyncID** if you want to use the select button you can set the syncID to the same value that you use in the mixer's default surface (you can set it on the Settings/Locals page)
 - **DefaultRecButton**: If you press the rec button on the controller, the bridge start/stop the MTK and/or 2 track recording it depend the value of the "DefaultRecButton". Possible value is: **onlyMTK**, **only2Track**, **2TrackAndMTK**
 - **DefaultChannelRecButton**: Sets what function has a rec button on controller. You can use **phantom** for controlling phantom voltage or **rec** to set multi-track recording for this channel; default is "rec
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
