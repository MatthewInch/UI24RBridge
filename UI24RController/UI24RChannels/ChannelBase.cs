using System;
using System.Collections.Generic;
using System.Text;

namespace UI24RController.UI24RChannels

{
    public abstract class ChannelBase
    {
        /// <summary>
        /// Between 0 and 1.0
        /// </summary>
        /// 
        public ChannelBase(int channelNumber)
        {
            ChannelFaderValue = 0;
            ChannelNumber = channelNumber;
            IsSelected = false;
            IsMute = false;
            _muteGroupMask = 0;
            _forceUnMute = false;
            _muteGroupMaskDefault = 1 << Mixer._muteAllBit;
            GlobalMuteGroup = 0;
            IsSolo = false;
            Name = GetDefaultName();
            AuxSendValues = new Dictionary<SelectedLayoutEnum, double>()
            {
                {SelectedLayoutEnum.Aux1, 0 },
                {SelectedLayoutEnum.Aux2, 0 },
                {SelectedLayoutEnum.Aux3, 0 },
                {SelectedLayoutEnum.Aux4, 0 },
                {SelectedLayoutEnum.Aux5, 0 },
                {SelectedLayoutEnum.Aux6, 0 },
                {SelectedLayoutEnum.Aux7, 0 },
                {SelectedLayoutEnum.Aux8, 0 },
                {SelectedLayoutEnum.Fx1, 0 },
                {SelectedLayoutEnum.Fx2, 0 },
                {SelectedLayoutEnum.Fx3, 0 },
                {SelectedLayoutEnum.Fx4, 0 },
            };
            channelTypeID = "i";
            VCAMuteMask = 0;
        }
        public double ChannelFaderValue { get; set; }
        protected string _name = "";
        public virtual string Name {
            get
            {
                return _name;
            }
            set
            {
                if (value == "")
                {
                    _name = GetDefaultName();
                }
                else _name = value;
            }
        }

        public int ChannelNumber { get; internal set; }
        public bool IsSelected { get; set; }
        public int VCA { get; set; }
        protected bool _muteBtn;
        public bool IsMute
        {
            get
            {
                return ( _muteBtn | ((_muteGroupMask & GlobalMuteGroup) > 0) | ((VCA & VCAMuteMask) > 0)) & !_forceUnMute ;
            }
            set
            {
                if ( ((_muteGroupMask & GlobalMuteGroup) > 0) | ((VCA & VCAMuteMask) > 0))
                    _forceUnMute = !value;
                else
                    _muteBtn = value;
                VCAMuteSetter();
            }
        }

        public virtual void VCAMuteSetter()
        {
        }

        protected UInt32 _muteGroupMask;
        protected UInt32 _muteGroupMaskDefault;
        protected bool _forceUnMute;
        public static UInt32 GlobalMuteGroup { get; set; }
        public static int VCAMuteMask { get; set; }
        public UInt32 MuteGroupMask
        {
            get
            {
                return _muteGroupMask;
            }
            set
            {
                _muteGroupMask = value | _muteGroupMaskDefault;
            }
        }
        public bool ForceUnMute
        {
            get
            {
                return _forceUnMute;
            }
            set
            {
                _forceUnMute = value;
            }
        }
        public bool IsSolo { get; set; }
        public virtual int ChannelNumberInMixer => ChannelNumber;
        public Dictionary<SelectedLayoutEnum, double> AuxSendValues { get; set; }
        protected string channelTypeID { get; set; }

        protected virtual string GetDefaultName()
        {
            return "CH";
        }

        public virtual string SetAuxValueMessage(SelectedLayoutEnum selectedLayout)
        {
            int auxNumber = selectedLayout.AuxToInt();
            return $"3:::SETD^{this.channelTypeID}.{this.ChannelNumber}.aux.{auxNumber}.value^{this.AuxSendValues[selectedLayout].ToString().Replace(',', '.')}";
        }

        public virtual string SetFxValueMessage(SelectedLayoutEnum selectedLayout)
        {
            int auxNumber = selectedLayout.FxToInt();
            return $"3:::SETD^{this.channelTypeID}.{this.ChannelNumber}.fx.{auxNumber}.value^{this.AuxSendValues[selectedLayout].ToString().Replace(',', '.')}";
        }

        public virtual string MixFaderMessage()
        {
            return $"3:::SETD^i.{this.ChannelNumber}.mix^{this.ChannelFaderValue.ToString().Replace(',', '.')}";
        }


        public virtual string SelectChannelMessage(string syncID)
        {
            return $"3:::BMSG^SYNC^{syncID}^{this.ChannelNumberInMixer}";
        }

        public virtual string TurnOnRTAMessage()
        {
            
            return $"3:::SETS^var.rta^{this.channelTypeID}.{this.ChannelNumber}";
        }

        public virtual string MuteMessage()
        {
            if ((_muteGroupMask & GlobalMuteGroup) > 0 | ((VCA & VCAMuteMask) > 0))
                return ForceUnMuteMessage();
            else
                return $"3:::SETD^{channelTypeID}.{this.ChannelNumber}.mute^{(this.IsMute ? 1 : 0)}";

        }
        public virtual string ForceUnMuteMessage()
        {
            return $"3:::SETD^{channelTypeID}.{this.ChannelNumber}.forceunmute^{(this._forceUnMute ? 1 : 0)}";
        }
        public virtual string SoloMessage()
        {
            return $"3:::SETD^i.{this.ChannelNumber}.solo^{(this.IsSolo ? 1 : 0)}";
        }
    }
}
