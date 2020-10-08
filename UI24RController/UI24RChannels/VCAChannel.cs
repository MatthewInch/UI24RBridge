using System;
using System.Collections.Generic;
using System.Text;

namespace UI24RController.UI24RChannels
{
    public class VCAChannel: ChannelBase
    {
        public VCAChannel(int channelNumber): base(channelNumber)
        {
            channelTypeID = "v";

        }
        public override int ChannelNumberInMixer => this.ChannelNumber + 48;
        protected override string GetDefaultName()
        {
            return $"VCA {(this.ChannelNumber + 1):D2}";
        }

        public override string MixFaderMessage()
        {
            return $"3:::SETD^v.{this.ChannelNumber}.mix^{this.ChannelFaderValue.ToString().Replace(',', '.')}";
        }
        public override string SoloMessage()
        {
            return $"3:::SETD^v.{this.ChannelNumber}.solo^{(this.IsSolo ? 1 : 0)}";
        }

        public override void VCAMuteSetter()
        {
            if (this.IsMute)
                ChannelBase.VCAMuteMask |= 1 << this.ChannelNumber;
            else
                ChannelBase.VCAMuteMask &= ~(1 << this.ChannelNumber);
        }
    }
}
