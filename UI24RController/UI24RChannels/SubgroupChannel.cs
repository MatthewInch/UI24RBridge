using System;
using System.Collections.Generic;
using System.Text;

namespace UI24RController.UI24RChannels
{
    public class SubgroupChannel: ChannelBase
    {
        public SubgroupChannel(int channelNumber): base(channelNumber)
        {

        }
        public override int ChannelNumberInMixer => this.ChannelNumber + 32;

        public override string MixFaderMessage()
        {
            return $"3:::SETD^s.{this.ChannelNumber}.mix^{this.ChannelFaderValue.ToString().Replace(',', '.')}";
        }
    }
}
