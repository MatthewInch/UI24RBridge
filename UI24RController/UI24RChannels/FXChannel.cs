using System;
using System.Collections.Generic;
using System.Text;

namespace UI24RController.UI24RChannels
{
    public class FXChannel: ChannelBase
    {
        public FXChannel(int channelNumber): base(channelNumber)
        {

        }

        public override string MixFaderMessage()
        {
            return $"3:::SETD^f.{this.ChannelNumber}.mix^{this.ChannelFaderValue.ToString().Replace(',', '.')}";
        }
    }
}
