# How to install UI24RBridge to raspberry PI

- Install arm64 ubuntu linux to raspberry
- Install .net Core 3.1 runtime from [here]( https://docs.microsoft.com/en-us/dotnet/core/install/linux-ubuntu)
    - manual installation steps (article)[https://sumodh.com/2020/05/05/how-to-install-net-core-x64-sdk-in-raspberry-pi-4/?doing_wp_cron=1618749257.7842419147491455078125]:
        - Download the Arm64 linux binaries from [here](https://dotnet.microsoft.com/download/dotnet/3.1)
        - sudo mkdir -p $HOME/dotnet  
          sudo tar zxf dotnet-sdk-3.1.408-linux-arm.tar.gz -C $HOME/dotnet  
          export DOTNET_ROOT=$HOME/dotnet  
          export PATH=$PATH:$HOME/dotnet
        - Add the two last row to the profle
          sudo nano .profile
