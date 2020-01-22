# UI24RBridge
Bridge between the UI24R and a MIDI controller

This is a beta project.
It tested only in windows, a Behringer BCF2000 and a begringer X-Touch midi controller

You can download the latest release for windows: https://github.com/MatthewInch/UI24RBridge/releases/download/v0.6.6/win-x86-core.zip


for Linux: https://github.com/MatthewInch/UI24RBridge/releases/download/v0.6.6/linux-x64.zip

Implemented the Mackie Control protocol (It can work with any daw controller that can use in MC mode)
The earlier protocol has not been removed but the new functions only implemented in MC mode.

In the settings file (appsettings.json)

"UI24R-Url": the mixer url (simply copy the url from the browser and replace http to ws and remove the /mixer.html from the end)

"MIDI-Input-Name","MIDI-Output-Name": The controller name. If you don't know it simple remove these to row from the config file and modify the mixer url to an invalid value. The UI24RBridge write all of the availabel MIDI device to the console.

"Protocol": MC or HUI, or empty.

"SyncID" if you want to use the select button you can set the syncID to the same value that you use in the mixer's default surface (you can set it on the Settings/Locals page)

The Linux binary wasn't tested.

Example of the settings file:

{

"UI24R-Url": "ws://192.168.5.2",

"MIDI-Input-Name": "X-Touch",

"MIDI-Output-Name": "X-Touch",

"Protocol": "MC",

"SyncID": "Abaliget"

}

The Bridge functionalities:

-Use 6 layer (you can select the channels in the mixer own surface with the Global view groups. You have to select 8 channels per viewgroup at least.

If there isn't any global view group in the mixer the Bridge use the channel in this order:

input 1-8;

input 9-16;

input 17-22, Line in;

Player, FX 1-4;

SUB 1-6; AUX 1-2

AUX 3-10;

-The faders work on every type of channels

-The knobs set the gain on the input channels

-Select buttons work on every channel
