# VRCFT iPhone/iPad ARKit LiveLink Module
Temporary host for Dazmbe's LiveLink module, compatible with VRCFaceTracking v5.0.0+

Note that the tracking will only realistically work *without a headset on your face*, so this is primary meant for use in VRChat's *desktop mode*.

## ▶ Usage

You need an IPhone X/XS/XR or newer, 12.9-inch IPad Pro 3rd gen or newer, or 11-inch IPad Pro 1st gen or newer to make use of this module.

- Install [VRCFaceTracking](https://github.com/benaclejames/VRCFaceTracking)
- Copy the `VRCFT-LiveLink.dll` into your `%AppData%\Roaming\VRCFaceTracking\CustomLibs` folder
  - **Find the .dll under "Assets" in the [latest release](https://github.com/kusomaigo/VRCFaceTracking-LiveLink/releases/latest)**
  - Until the module gets approved to show in the module registry, this custom method is how it must be!
- Install the app "[Live Link Face](https://apps.apple.com/us/app/live-link-face/id1495370836)" by Unreal Engine on your Apple device
  - Ensure that your Apple device is connected to the same network as your computer
- Open Live Link Face on the Apple device, and open settings, then tap the Live Link option at the top
- Start VRCFaceTracking, then change to the Output page
- Add your computer's local IP address here, which should have been printed out in the Output page of VRCFaceTracking. There is no way to change the port for now, so leave it as the LiveLink default `11111`
- Return to the main screen and tap the Live button at the top. If it is *green*, the app is streaming data
- Start VRChat in desktop mode, equip a VRCFT-enabled avatar, and enjoy facial tracking!

## 🔍 Troubleshooting

- Right click, open the properties of the .dll, and check this box if you see it to unblock the file
![](https://github.com/Dazbme/VRCFaceTracking-LiveLink/raw/master/images/unblock_dll.png "")
- Double check to make sure that your Apple device and computer are connected to the same network
- Double check the IP address and port number entered in LiveLink match your computer's local IP address and that the port is either left blank or set to `11111`
- Check that the IP address enetered is the local IP for the shared network, and not for any other networks your computer may be connected to (e.g. Hamachi, Public IP)
- Ensure that your avatar supports VRCFT, and check in the toggles to make sure it is enabled
- Check your Windows network settings, and ensure that the network is set as a private network

## 👋 Credits

* [Unreal Engine Live Link Face](https://apps.apple.com/us/app/live-link-face/id1495370836)
* [benaclejames/VRCFaceTracking](https://github.com/benaclejames/VRCFaceTracking)
* [Dazbme/OG module author](https://github.com/Dazbme/VRCFaceTracking-LiveLink)
* [regzo2(Azmidi)/VRCFT module lord](https://github.com/regzo2)