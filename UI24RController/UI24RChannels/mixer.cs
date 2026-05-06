using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using UI24RController;


namespace UI24RController.UI24RChannels
{
    public class Mixer
    {
        private const int _numFadersPerController = 8; // Excluding "main" fader
        private Dictionary<FaderBank, List<int>> _banks = new Dictionary<FaderBank, List<int>>();
        private int _selectedLayer;
        private FaderBank _selectedBank;
        private int _numControllers;
        private int _numLayersPerBank;
        private int _numFadersPerPage => _numControllers * _numFadersPerController;
        private bool _hasUserBank;
        private FaderBank[] _availableBanks;

        private static readonly FaderBank[] ViewGroupBanks = new[]
        {
            FaderBank.ViewGroup1, FaderBank.ViewGroup2, FaderBank.ViewGroup3,
            FaderBank.ViewGroup4, FaderBank.ViewGroup5, FaderBank.ViewGroup6,
        };

        public Mixer(int numControllers, bool hasUserBank = true)
        {
            _numControllers = numControllers;
            _numLayersPerBank = 6;
            _selectedLayer = 0;
            _selectedBank = FaderBank.Initial;
            _hasUserBank = hasUserBank;
            _availableBanks = hasUserBank
                ? new[] { FaderBank.Initial, FaderBank.User }.Concat(ViewGroupBanks).ToArray()
                : new[] { FaderBank.Initial }.Concat(ViewGroupBanks).ToArray();

            initLayers();
            initMuteGroups();
            TapInit();
        }

        #region Layers

        public bool UserLayerEdit { get; set; }
        public int UserLayerEditNewChannel { get; set; }

        // Flat list layout per bank:
        //   Each layer occupies _numFadersPerController consecutive slots
        //   (8 channel indices, no main fader stored).
        //   Index of slot j in layer i = i * _numFadersPerController + j.
        //   Main fader (54) is always appended at query time in getCurrentLayer.
        private static int LayerOffset(int layer) => layer * _numFadersPerController;


        private int NumPagesSelectedBank()
        {
            return (int)Math.Ceiling((double)_banks[_selectedBank].Count / _numFadersPerPage);
        }

        private int NumLayersSelectedBank()
        {
            return (int)Math.Ceiling((double)_banks[_selectedBank].Count / _numFadersPerController);
        }

        private void initLayers()
        {
            UserLayerEdit = false;
            UserLayerEditNewChannel = -1;

            // Initialize Initial bank
            var initialBank = new List<int>(Enumerable.Repeat(0, _numLayersPerBank * _numFadersPerController));
            for (int i = 0; i < _numLayersPerBank; ++i)
            {
                int layerBase = LayerOffset(i);
                for (int j = 0; j < _numFadersPerController; ++j)
                    initialBank[layerBase + j] = j + i * _numFadersPerController;
            }
            // Rearrangements in default layers
            int layer4 = LayerOffset(4);
            for (int j = 0; j < _numFadersPerController; ++j)
                initialBank[layer4 + j] = 38 + j;
            int layer5 = LayerOffset(5);
            initialBank[layer5 + 0] = 46;
            initialBank[layer5 + 1] = 47;
            for (int j = 0; j < 6; ++j)
                initialBank[layer5 + j + 2] = 48 + j;
            _banks.Add(FaderBank.Initial, initialBank);

            // Initialize User bank
            if (_hasUserBank)
            {
                var userBank = new List<int>(Enumerable.Repeat(0, _numLayersPerBank * _numFadersPerController));
                for (int i = 0; i < _numLayersPerBank; ++i)
                {
                    int layerBase = LayerOffset(i);
                    for (int j = 0; j < _numFadersPerController; ++j)
                        userBank[layerBase + j] = j + i * _numFadersPerController;
                }
                _banks.Add(FaderBank.User, userBank);
            }

            // Initialize ViewGroup banks (one bank per view group, full channel list stored, sliced at query time)
            foreach (var vgBank in ViewGroupBanks)
                _banks.Add(vgBank, new List<int>());
        }

        public void setBank(int bankNumber)
        {
            _selectedBank = _availableBanks[bankNumber % _availableBanks.Length];
            _selectedLayer = 0;
        }

        public void setUserLayerFromArray(int[][] input)
        {
            if (!_hasUserBank) return;
            for (int i = 0; i < input.Length && i < _numLayersPerBank; ++i)
            {
                int layerBase = LayerOffset(i);
                for (int j = 0; j < _numFadersPerController && j < input[i].Length; ++j)
                    _banks[FaderBank.User][layerBase + j] = input[i][j];
            }
        }

        public int[][] getUserLayerToArray()
        {
            if (!_hasUserBank) return Array.Empty<int[]>();
            int[][] output = new int[_numLayersPerBank][];
            for (int i = 0; i < _numLayersPerBank; ++i)
            {
                int layerBase = LayerOffset(i);
                output[i] = new int[_numFadersPerController];
                for (int j = 0; j < _numFadersPerController; ++j)
                    output[i][j] = _banks[FaderBank.User][layerBase + j];
            }
            return output;
        }

        public (FaderBank bank, int layer, int position, int channel) setNewUserChannelInCurrentBank(int controllerPosition)
        {
            if (controllerPosition >= 0 && controllerPosition < _numFadersPerController)
                _banks[_selectedBank][LayerOffset(_selectedLayer) + controllerPosition] = UserLayerEditNewChannel;
            return (_selectedBank, _selectedLayer, controllerPosition, UserLayerEditNewChannel);
        }

        public void findNextAvailableChannelForUserLayer(int controllerPos, int dir, int channelOffset)
        {
            bool goOneMoreChannel = true;
            while (goOneMoreChannel)
            {
                goOneMoreChannel = false;
                UserLayerEditNewChannel = (UserLayerEditNewChannel + dir + 54) % 54;
                for (int i = 0; i < _numFadersPerController; ++i)
                {
                    if (i != controllerPos && getCurrentLayer(channelOffset)[i] == UserLayerEditNewChannel)
                        goOneMoreChannel = true;
                }
            }
        }

        public void setChannelsToViewLayerAndPosition(int[] channels, int viewGroupNumber)
        {
            var bank = ViewGroupBanks[viewGroupNumber];
            _banks[bank] = new List<int>(channels);
        }

        public void setChannelInLayerAndPosition(FaderBank bank, int layerNumber, int position, int channel)
        {
            _banks[bank][LayerOffset(layerNumber) + position] = channel;
        }

        public void setLayerUp()
        {
            int numLayers = NumLayersSelectedBank();
            if (numLayers == 0) return;
            _selectedLayer = (_selectedLayer + _numControllers) % numLayers;
        }
        public void setLayerDown()
        {
            int numLayers = NumLayersSelectedBank();
            if (numLayers == 0) return;
            _selectedLayer = (_selectedLayer + numLayers - _numControllers) % numLayers;
        }

        public void setBankUp()
        {
            int idx = (Array.IndexOf(_availableBanks, _selectedBank) + 1) % _availableBanks.Length;
            _selectedBank = _availableBanks[idx];
            _selectedLayer = 0;
        }
        public void setBankDown()
        {
            int idx = (Array.IndexOf(_availableBanks, _selectedBank) + _availableBanks.Length - 1) % _availableBanks.Length;
            _selectedBank = _availableBanks[idx];
            _selectedLayer = 0;
        }

        public void SetViewGroup(int viewGroupIndex)
        {
            _selectedBank = ViewGroupBanks[viewGroupIndex % ViewGroupBanks.Length];
            _selectedLayer = 0;
        }

        public int GetCurrentViewGroup()
        {
            int idx = Array.IndexOf(ViewGroupBanks, _selectedBank);
            return idx >= 0 ? idx : -1;
        }

        public bool goToUserBank()
        {
            if (!_hasUserBank) return false;
            if (_selectedBank == FaderBank.User) return false;
            _selectedBank = FaderBank.User;
            return true;
        }

        public string GetCurrentBankString()
        {
            int viewGroup = GetCurrentViewGroup();
            if (viewGroup >= 0)
                return " " + (viewGroup + 1).ToString();

            switch (_selectedBank)
            {
                case FaderBank.Initial: return "In";
                case FaderBank.User:    return " U";
                default:                return "  ";
            }
        }

        public string GetCurrentLayerString()
        {
            if(_banks[_selectedBank].Count == 0)
                return "---";

            string page = (_selectedLayer / _numControllers + 1).ToString();
            string total = NumPagesSelectedBank().ToString();
            return  $"{page}-{total}";
        }

        public int[] getCurrentLayer(int channelOffset)
        {
            var bank = _banks[_selectedBank];
            int offset = LayerOffset(_selectedLayer) + channelOffset * _numFadersPerController;
            return bank
                .Skip(offset)
                .Take(_numFadersPerController)
                .ToFixedLength(_numFadersPerController, -1)
                .Append(54)
                .ToArray();
        }

        public int getChannelNumberInCurrentLayer(int ch, int channelOffset)
        {
            ch = ch % (_numFadersPerController + 1); // +1 for main fader slot
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

        private DateTime? _lastTick;
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

            double timeDiff = (newTick - _lastTick.Value).TotalSeconds;

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
