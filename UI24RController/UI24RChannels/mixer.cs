using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;


namespace UI24RController.UI24RChannels
{
    public class Mixer
    {
        //private List<int[]> _layers = new List<int[]>();
        private Dictionary<int, List<int[]>> _banks = new Dictionary<int, List<int[]>>();
        private int _selectedLayer;
        private int _selectedBank;
        private int _numLayersPerBank;
        private int _numBanks;
        private int _numFaders;

        /// <summary>
        /// If it is true show the next layer (not the selected) or in case of viewgroup bank the second 8 channel from view
        /// </summary>
        public bool IsChannelOffset { get; set; }

        public Mixer()
        {
            _numLayersPerBank = 6;
            _numBanks = 3;
            _selectedLayer = 0;
            _selectedBank = 0;
            _numFaders = 9;

            initLayers();
            initMuteGroups();
            TapInit();
        }

        #region Layers

        public bool UserLayerEdit { get; set; }
        public int UserLayerEditNewChannel { get; set; }

        private void initLayers(int startBank = 0)
        {
          
            UserLayerEdit = false;
            UserLayerEditNewChannel = -1;

            //Inititalize Initial Layers
            _banks.Add(0, new List<int[]>());
            for (int i = 0; i < _numLayersPerBank; ++i)
            {
                int[] channelLayer = new int[9];
                for (int j = 0; j < _numFaders - 1; ++j)
                    channelLayer[j] = j + i * (_numFaders - 1);
                channelLayer[_numFaders - 1] = 54;
                _banks[0].Add(channelLayer);
            }
            //some rearangements in default layers
            for (int j = 0; j < 8; ++j)
                _banks[0][4][j] = 38 + j;
            _banks[0][5][0] = 46;
            _banks[0][5][1] = 47;
            for (int j = 0; j < 6; ++j)
                _banks[0][5][j+2] = 48 + j;

            //initialize User layers
            _banks.Add(1, new List<int[]>());
            for (int i = 0; i < _numLayersPerBank; ++i)
            {
                int[] channelLayer = new int[9];
                for (int j = 0; j < _numFaders - 1; ++j)
                    channelLayer[j] = j + i * (_numFaders - 1);
                channelLayer[_numFaders - 1] = 54;
                _banks[1].Add(channelLayer);
            }


            //initialize View group layers
            _banks.Add(2, new List<int[]>());
            for (int i = 0; i < _numLayersPerBank; ++i)
            {
                _banks[2].Add(new int[] { -1, -1, -1, -1, -1, -1, -1, -1, 54 });
            }
        }


        public void setBank(int bankNumber)
        {
            _selectedBank = bankNumber % _numBanks;
            _selectedLayer = 0;
        }
        public void setUserLayerFromArray(int[][] input)
        {
            for (int i = 0; i < input.Length && i < _numLayersPerBank; ++i)
                if (input[i].Length >= _numFaders - 1)
                    for (int j = 0; j < _numFaders - 1; ++j)
                        _banks[1][i][j] = input[i][j];
        }
        public int[][] getUserLayerToArray()
        {
            int[][] output = new int[_numLayersPerBank][];
            for (int i = 0; i < _numLayersPerBank; ++i)
            {
                output[i] = new int[_numFaders - 1];
                for (int j = 0; j < _numFaders - 1; ++j)
                    output[i][j] = _banks[1][i][j];
            }
            return output;
        }
        public (int bank, int layer, int position, int channel) setNewUserChannelInCurrentBank(int controllerPosition)
        {
            var currentLayerNumber = this.IsChannelOffset ? (_selectedLayer + 1) % _numLayersPerBank : _selectedLayer;
            if (controllerPosition >= 0 && controllerPosition < 8)
                _banks[_selectedBank][currentLayerNumber][controllerPosition] = UserLayerEditNewChannel;
            return (_selectedBank, currentLayerNumber, controllerPosition, UserLayerEditNewChannel);
        }
        public void findNextAvailableChannelForUserLayer(int controllerPos, int dir, int channelOffset)
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
                        if (getCurrentLayer(channelOffset)[i] == UserLayerEditNewChannel)
                            goOneMoreChannel = true;
                    }
                }
            }

        }



        /// <summary>
        /// Set channels in a global view in the mixer object
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="layer"></param>
        /// <param name="position"></param>
        public void setChannelsToViewLayerAndPosition(int[] channels, int viewGroupNumber)
        {
            _banks[2][viewGroupNumber] = channels;
        }

        public void setChannelInLayerAndPosition(int bank, int layerNumber, int position, int channel) 
        {
            _banks[bank][layerNumber][position] = channel;
        }


        public void setLayerUp()
        {
            _selectedLayer = (_selectedLayer + 1) % _numLayersPerBank;
        }
        public void setLayerDown()
        {
            _selectedLayer = (_selectedLayer + _numLayersPerBank - 1) % _numLayersPerBank;
        }

        public void setBankUp()
        {
            _selectedBank = (_selectedBank + 1) % _numBanks;
            _selectedLayer = 0;
        }
        public void setBankDown()
        {
            _selectedBank = (_selectedBank + _numBanks - 1) % _numBanks;
            _selectedLayer = 0;
        }
        public bool goToUserBank()
        {
            bool updated = false;
            if (_selectedBank != 1)
            {
                _selectedBank = 1;
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
                    return 'U'; // User Layer
                case 2:
                default:
                    return 'V'; // Global View Layer
            }
        }
        public string getCurrentLayerString()
        {
            return getBankChar(_selectedBank).ToString() + ((_selectedLayer % _numLayersPerBank) + 1).ToString();
        }

        /// <summary>
        /// Return the 8 channel of the current layer plus the main fader
        /// the banks 0 (general) and 1 (user bank) contains 8ch+main fader in every layer
        /// the banks 2 (view groups) contains all view group channel per layer without main fader
        /// in that case the getCurrentLayer return the first (or second) 8 value plus main fader
        /// </summary>
        /// <returns></returns>
        public int[] getCurrentLayer(int channelOffset)
        {
            if (_selectedLayer < _banks[_selectedBank].Count)
            {
                
                if (_selectedBank<2)
                {
                    var selectedLayer = (_selectedLayer + channelOffset) % _numLayersPerBank;
                    return _banks[_selectedBank][selectedLayer];
                }
                else //view groups
                {
                    var offset = channelOffset*8;
                    var result = _banks[_selectedBank][_selectedLayer].Skip(offset).Take(8).ToFixedLength(8,-1).Append(54);
                    return result.ToArray();
                    
                }
            }
            return new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 54 };
        }

        public int getChannelNumberInCurrentLayer(int ch, int channelOffset)
        {
            ch = ch % _numFaders;
            if (ch < 0) ch = 0;
            return getCurrentLayer(channelOffset)[ch];
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
