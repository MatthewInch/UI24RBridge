using System;
using System.Collections.Generic;
using System.Text;

namespace UI24RController.UI24RChannels
{
    public class VCAChannel: ChannelBase
    {
        public VCAChannel(int channelNumber): base(channelNumber)
        {

        }

        public override string MixFaderMessage()
        {
            return $"3:::SETD^v.{this.ChannelNumber}.mix^{this.ChannelFaderValue.ToString().Replace(',', '.')}";
        }
    }
}
