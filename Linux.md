# How to install UI24RBridge to raspberry PI

- Install arm64 ubuntu linux to raspberry
- Install .net Core 3.1 runtime from [here]( https://docs.microsoft.com/en-us/dotnet/core/install/linux-ubuntu)
    - manual installation steps (article)[https://sumodh.com/2020/05/05/how-to-install-net-core-x64-sdk-in-raspberry-pi-4/?doing_wp_cron=1618749257.7842419147491455078125]:
        - Download the Arm64 linux binaries from [here](https://dotnet.microsoft.com/download/dotnet/3.1)
        - install .net Core
          ```bash
          sudo mkdir -p $HOME/dotnet  
          sudo tar zxf dotnet-sdk-3.1.408-linux-arm.tar.gz -C $HOME/dotnet  
          export DOTNET_ROOT=$HOME/dotnet  
          export PATH=$PATH:$HOME/dotnet
          ```
        - Add the two last row to the profle
          sudo nano .profile

Visual studio:
```bash
sudo apt-get install code 
```
dotnet:
```bash
curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel Current
echo 'export DOTNET_ROOT=$HOME/.dotnet' >> ~/.bashrc
echo 'export PATH=$PATH:$HOME/.dotnet' >> ~/.bashrc
source ~/.bashrc

sudo apt-get install libasound2-dev
```

Manjaro linux
```bash
sudo pacman -S git
sudo pacman -S base-devel
cd ~/Downloads
git clone https://AUR.archlinux.org/visual-studio-code-bin.git
cd visual-studio-code-bin/
makepkg -s
sudo pacman -U visual-studio-code-bin-1.52.1-1-aarch64.pkg.tar.zst
```

.net core 3.1
```bash
sudo mkdir -p $HOME/dotnet
sudo tar zxf dotnet-sdk-3.1.100-linux-arm.tar.gz -C $HOME/dotnet
export DOTNET_ROOT=$HOME/dotnet
export PATH=$PATH:$HOME/dotnet
```

# Raspberry PI 64bit (.NET 6)

```bash
sudo apt-get install code
sudo apt-get install libasound2-dev
```

Make UI24RBridgeTest executable
```bash
chmod +x UI24RBridgeTest 
```

