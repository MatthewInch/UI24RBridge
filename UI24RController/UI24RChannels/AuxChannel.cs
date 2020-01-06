using System;
using System.Collections.Generic;
using System.Text;

namespace UI24RController.UI24RChannels
{
    public class AuxChannel: ChannelBase
    {
        public AuxChannel(int channelNumber): base(channelNumber)
        {

        }

        public override string MixFaderMessage()
        {
            return $"3:::SETD^a.{this.ChannelNumber}.mix^{this.ChannelFaderValue.ToString().Replace(',', '.')}";
        }
    }
}
