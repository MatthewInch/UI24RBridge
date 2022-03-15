using System;
using System.Collections.Generic;
using System.Text;

namespace UI24RController.UI24RChannels
{
    public class Mixer
    {

        public Mixer()
        {
            initLayers();
            initMuteGroups();
            TapInit();
        }

        #region Layers
        private List<int[]> _layers = new List<int[]>();
        private int _selectedLayer;
        private int _selectedBank;
        private int _numLayersPerBank;
        private int _numBanks;
        private int _numFaders;
        public bool UserLayerEdit { get; set; }
        public int UserLayerEditNewChannel { get; set; }

        private void initLayers(int startBank = 0)
        {
            _numLayersPerBank = 6;
            _numBanks = 3;
            _selectedLayer = 0;
            _selectedBank = startBank;
            _numFaders = 9;
            UserLayerEdit = false;
            UserLayerEditNewChannel = -1;

            //Inititalize Initial Layers
            for (int i = 0; i < _numLayersPerBank; ++i)
            {
                int[] channelLayer = new int[9];
                for (int j = 0; j < _numFaders - 1; ++j)
                    channelLayer[j] = j + i * (_numFaders - 1);
                channelLayer[_numFaders - 1] = 54;
                _layers.Add(channelLayer);
            }
            //some rearangements in default layers
            for (int j = 0; j < 8; ++j)
                _layers[4][j] = 38 + j;
            _layers[5][0] = 46;
            _layers[5][1] = 47;
            for (int j = 0; j < 6; ++j)
                _layers[5][j+2] = 48 + j;

            //initialize View group layers
            for (int i = 0; i < _numLayersPerBank; ++i)
            {
                int[] channelLayer = new int[9];
                channelLayer[0] = -1;
                channelLayer[_numFaders - 1] = 54;
                _layers.Add(channelLayer);
            }

            //initialize User layers
            for (int i = 0; i < _numLayersPerBank; ++i)
            {
                int[] channelLayer = new int[9];
                for (int j = 0; j < _numFaders - 1; ++j)
                    channelLayer[j] = j + i * (_numFaders - 1);
                channelLayer[_numFaders - 1] = 54;
                _layers.Add(channelLayer);
            }
        }


        public void setBank(int bankNumber)
        {
            _selectedBank = bankNumber % _numBanks;
            _selectedLayer = _selectedBank * _numLayersPerBank;
            skipUnusedLayerUp();
        }
        public void setUserLayerFromArray(int[][] input)
        {
            for (int i = 0; i < input.Length && i < _numLayersPerBank; ++i)
                if (input[i].Length >= _numFaders - 1)
                    for (int j = 0; j < _numFaders - 1; ++j)
                        _layers[i + 2 * _numLayersPerBank][j] = input[i][j];
        }
        public int[][] getUserLayerToArray()
        {
            int[][] output = new int[_numLayersPerBank][];
            for (int i = 0; i < _numLayersPerBank; ++i)
            {
                output[i] = new int[_numFaders - 1];
                for (int j = 0; j < _numFaders - 1; ++j)
                    output[i][j] = _layers[i + 2 * _numLayersPerBank][j];
            }
            return output;
        }
        public void setNewUserChannelInCurrentBank(int controllerPosition)
        {
            if (controllerPosition >= 0 & controllerPosition < 8)
                _layers[_selectedLayer][controllerPosition] = UserLayerEditNewChannel;
        }
        public void findNextAvailableChannelForUserLayer(int controllerPos, int dir)
        {
            bool goOneMoreChannel = true;

            while(goOneMoreChannel)
            {
                goOneMoreChannel = false;
                UserLayerEditNewChannel = (UserLayerEditNewChannel + dir + 54) % 54;

                for (int i = 0; i < 8; ++i)
                {
                    if (i != controllerPos)
                    {
                        if (getCurrentLayer()[i] == UserLayerEditNewChannel)
                            goOneMoreChannel = true;
                    }
                }
            }

        }

        public void setChannelToViewLayerAndPosition(int channel, int layer, int position)
        {
            setChannelToBankLayerAndPosition(channel, 1, layer, position);
        }

        public void setChannelToUserLayerAndPosition(int channel, int layer, int position)
        {
            setChannelToBankLayerAndPosition(channel, 3, layer, position);
        }

        public void setChannelToBankLayerAndPosition(int channel, int bank, int layer, int position)
        {
            if (position < 0 || position >= 8)
                return;
            if (bank < 0 || bank >= _numBanks)
                return;
            if (layer < 0 || layer >= _numLayersPerBank)
                return;
            _layers[bank * _numLayersPerBank + layer][position] = channel;
        }

        private void skipUnusedLayerUp()
        {
            int initialLayer = _selectedLayer;
            //skip unused layers
            while (_layers[_selectedLayer][0] < 0)
            {
                _selectedLayer = (_selectedLayer + 1) % (_numLayersPerBank * _numBanks);
                if (_selectedLayer % _numLayersPerBank == 0 && initialLayer >= 0)
                {
                    _selectedLayer = _selectedBank * _numLayersPerBank;
                    initialLayer = -1;
                }
            }
            //update bank if needed
            _selectedBank = _selectedLayer / _numLayersPerBank;
        }

        private void skipUnusedLayerDown()
        {
            int initialLayer = _selectedLayer;
            //skip unused layers
            while (_layers[_selectedLayer][0] < 0)
            {
                _selectedLayer = (_numLayersPerBank * _numBanks + _selectedLayer - 1) % (_numLayersPerBank * _numBanks);
                if (_selectedLayer % _numLayersPerBank == _numLayersPerBank - 1 && initialLayer >= 0)
                {
                    _selectedLayer = _selectedBank * _numLayersPerBank;
                    initialLayer = -1;
                }
            }

            //update bank if needed
            _selectedBank = _selectedLayer / _numLayersPerBank;
        }

        public void setLayerUp()
        {
            _selectedLayer = (_selectedLayer + 1) % _numLayersPerBank + _numLayersPerBank * _selectedBank;
            skipUnusedLayerUp();
        }
        public void setLayerDown()
        {
            _selectedLayer = (_selectedLayer + _numLayersPerBank - 1) % _numLayersPerBank + _numLayersPerBank * _selectedBank;
            skipUnusedLayerDown();
        }

        public void setBankUp()
        {
            _selectedBank = (_selectedBank + 1) % _numBanks;
            _selectedLayer = _selectedBank * _numLayersPerBank;
            skipUnusedLayerUp();
        }
        public void setBankDown()
        {
            _selectedBank = (_selectedBank + _numBanks - 1) % _numBanks;
            _selectedLayer = _selectedBank * _numLayersPerBank;
            skipUnusedLayerDown();
        }
        public bool goToUserBank()
        {
            bool updated = false;
            while (_selectedBank < 2)
            {
                setBankUp();
                updated = true;
            }
            return updated;
        }
        public char getBankChar(int bank)
        {
            switch (bank)
            {
                case 0:
                    return 'I'; // Initial Layer
                case 1:
                    return 'V'; // View Layer
                case 2:
                default:
                    return 'U';
            }
        }
        public string getCurrentLayerString()
        {
            return getBankChar(_selectedBank).ToString() + ((_selectedLayer % _numLayersPerBank) + 1).ToString();
        }

        public int[] getCurrentLayer()
        {
            if (_selectedLayer < _layers.Count)
                return _layers[_selectedLayer];
            return new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 54 };
        }

        public int getChannelNumberInCurrentLayer(int ch)
        {
            ch = ch % _numFaders;
            if (ch < 0) ch = 0;
            return getCurrentLayer()[ch];
        }
        #endregion

        #region Mute Group

        private UInt32 _muteMask;
        public UInt32 MuteMask { 
            get
            {
                return _muteMask;
            }
            set
            {
                ChannelBase.GlobalMuteGroup = value;
                _muteMask = value;
            }
        }
        public const int _muteAllFxBit = 22;
        public const int _muteAllBit = 23;

        private void initMuteGroups()
        {
            MuteMask = 0;
        }
        public string GetMuteGroupsMessage()
        {
            return "3:::SETD^mgmask^" + MuteMask.ToString();
        }
        public void ClearMute()
        {
            MuteMask = 0;
        }
        public bool ToggleMuteGroup(int groupNumber)
        {
            MuteMask ^= (UInt32)1 << groupNumber;
            return ((MuteMask >> groupNumber) & 1) == (UInt32)1;
        }
        public bool ToggleMuteAllFx()
        {
            return ToggleMuteGroup(_muteAllFxBit);
        }
        public bool ToggleMuteAll()
        {
            return ToggleMuteGroup(_muteAllBit);
        }
        public void SetMuteGroup(int groupNumber, bool value)
        {
            MuteMask |= (UInt32)(value ? 1 : 0) << groupNumber;
        }
        public void SetMuteAllFx(bool value)
        {
            SetMuteGroup(_muteAllFxBit, value);
        }
        public void SetMuteAll(bool value)
        {
            SetMuteGroup(_muteAllBit, value);
        }




        #endregion

        #region Tap Tempo

        private DateTime _lastTick;
        private List<int> _tempo;


        private void TapInit()
        {
            _lastTick = default;
            _tempo = new List<int>();
        }

        public int TapTempo()
        {
            //store tempo from last Tick
            var newTick = DateTime.Now;

            //if first Tick
            if (_lastTick == null)
            {
                _lastTick = newTick;
                return -1;
            }

            double timeDiff = (newTick - _lastTick).TotalSeconds;

            //if tick after more than 5s, clear ticks
            if (timeDiff > 5)
            {
                _tempo.Clear();
                _lastTick = newTick;
                return -1;
            }

            //new tempo
            int newTempo = (int)(60 / timeDiff);
            _lastTick = newTick;

            //if too slow (min tempo set to 20 BPM)
            if (newTempo < 20)
                return -1;

            //add new tempo to list
            _tempo.Add(newTempo);
            if (_tempo.Count > 3)
                _tempo.RemoveAt(0);

            //compute new tempo
            int sum = 0;
            for (int i = 0; i < _tempo.Count; ++i)
                sum += _tempo[i];
            return sum /_tempo.Count;
        }

        public string GetStartMTKRecordMessage(int fx, int tempo)
        {
            return "3:::SETD^f." + fx.ToString() + ".bpm^" + tempo.ToString();
        }

        #endregion

        #region Media and Record
        public bool IsMultitrackRecordingRun { get; set; }
        public bool IsTwoTrackRecordingRun { get; set; }

        public string GetStartMTKRecordMessage()
        {
            return "3:::MTK_REC_TOGGLE";
        }

        public string GetStopMTKRecordMessage()
        {
            return "3:::MTK_REC_TOGGLE";
        }

        public string GetStartRecordMessage()
        {
            return "3:::RECTOGGLE";
        }
        public string GetStopRecordMessage()
        {
            return "3:::RECTOGGLE";
        }

        public string Get2TrackPlayMessage()
        {
            return "3:::MEDIA_PLAY";
        }
        public string Get2TrackStopMessage()
        {
            return "3:::MEDIA_STOP";
        }

        public string Get2TrackNextMessage()
        {
            return "3:::MEDIA_NEXT";
        }
        public string Get2TrackPrevMessage()
        {
            return "3:::MEDIA_PREV";
        }
        #endregion

    }
}
