# Raspberry PI CM5
The most efficient approach is to use a Raspberry Pi CM5 with a carrier board
(such as WaveShare CM5-IO-BASE-B) and a M.2 SSD to have the fastest boot times.

## Hardware
Below tested hardware configuration
 - Raspberry Pi Compute Module 5, 16 GB RAM, Wireless
 - M2 2242 size SSD, 128 GB (SK M2 NVME 2242 128GB)
 - WaveShare CM5-IO-BASE-B carrier board
 - Raspberry Pi Compute Module 5 Passive Cooler
 - (Optional) Compute Module CM4/CM5 Antenna kit

Install the M2 SSD and CM5 module with the provided screws.

To fit the passive cooler, unscrew the fan that comes with the CM5-IO-BASE-B
carrier board and position it outside the box, routing the cable back inside
through one of the openings.

The antenna kit is required to use WiFi, as the box will significantly
attenuate any WiFi signals from reaching the onboard antenna. Note you
must [update the raspberry pi configuration](https://www.jeffgeerling.com/blog/2022/enable-external-antenna-connector-on-raspberry-pi-compute-module-45/)
to use the external antenna.

## Flashing Raspberry Pi OS
1. Install [Raspberry Pi Imager](https://www.raspberrypi.com/software/)
2. Install or build the latest version of [rpiboot](https://github.com/raspberrypi/usbboot)
(On Windows, download the installer available in the [releases](https://github.com/raspberrypi/usbboot/releases)
page)
3. Plug a USB-C cable to your PC while holding down the `BOOT` button. In
other carrier boards, instead, place a jumper between `nRPIBOOT` and `GND`
(J2 pins 1-2 in the official CM5 carrier board) before plugging in the
USB-C cable.
4. Run `sudo rpiboot` (On windows, launch `rpi-mass-storage-gadget64.bat`).
This should mount a writeable USB drive on which we'll write the OS image.
5. Flash the image using the Raspberry Pi Imager utility. (Recommended to use 64-bit
Raspberry Pi OS)

Source: https://www.waveshare.com/wiki/Compute_Module_Burn_EMMC

## Boot time Tweaks
A few optional tweaks to improve boot times.

1. Ensure everything is up to date
```
sudo apt update && sudo apt full-upgrade
```

2. Update bootloader and reboot:
```
sudo rpi-eeprom-update -a
sudo reboot
```

3. Change the boot order to prefer NVMe:
    - Open the Raspberry Pi configuration editor (`sudo raspi-config`)
    - Navigate to `Advanced Options` > `Boot` > `Boot Order`
    - Highlight `NVMe/USB Boot` and press enter
    - Follow the prompts

4. Disable network wait:
```
sudo systemctl disable --now NetworkManager-wait-online.service
sudo systemctl mask NetworkManager-wait-online.service
```

## Build Ui24RBridge
```
# Install dependencies
sudo apt install libasound2-dev

# Install the .NET SDK
wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
chmod +x ./dotnet-install.sh
./dotnet-install.sh
rm ./dotnet-install.sh
echo 'export DOTNET_ROOT=$HOME/.dotnet' >> ~/.bashrc
echo 'export PATH=$PATH:$HOME/.dotnet' >> ~/.bashrc
source ~/.bashrc

# Clone the repository
git clone https://github.com/MatthewInch/UI24RBridge.git

# Build
cd UI24RBridge/UI24RBridgeTest/
dotnet build -c Release

```