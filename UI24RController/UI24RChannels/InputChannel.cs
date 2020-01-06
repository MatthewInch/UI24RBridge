using System;
using System.Collections.Generic;
using System.Text;

namespace UI24RController.UI24RChannels
{
    public class InputChannel: ChannelBase
    {
        public InputChannel(int channelNumber): base(channelNumber)
        {

        }

        public override string MixFaderMessage()
        {
            return $"3:::SETD^i.{this.ChannelNumber}.mix^{this.MainFaderValue.ToString().Replace(',', '.')}";
        }
    }
}
