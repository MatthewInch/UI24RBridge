using System;
using System.Collections.Generic;
using System.Text;

namespace UI24RController.UI24RChannels
{
    public class LineInChannel: ChannelBase
    {
        public LineInChannel(int channelNumber): base(channelNumber)
        {

        }

        public override string MixFaderMessage()
        {
            return $"3:::SETD^l.{this.ChannelNumber}.mix^{this.ChannelFaderValue.ToString().Replace(',', '.')}";
        }
    }
}
