using System;
using System.Collections.Generic;
using System.Text;

namespace UI24RController.UI24RChannels
{
    public class PlayerChannel: ChannelBase
    {
        public PlayerChannel(int channelNumber): base(channelNumber)
        {

        }
        public override int ChannelNumberInMixer => this.ChannelNumber + 26;

        public override string MixFaderMessage()
        {
            return $"3:::SETD^p.{this.ChannelNumber}.mix^{this.ChannelFaderValue.ToString().Replace(',', '.')}";
        }
    }
}
