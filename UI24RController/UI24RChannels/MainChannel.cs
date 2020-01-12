using System;
using System.Collections.Generic;
using System.Text;

namespace UI24RController.UI24RChannels
{
    public class MainChannel: ChannelBase
    {
        public MainChannel(): base(0)
        {
            this.Name = "Main";
        }
        public override int ChannelNumberInMixer => 54;

        public override string MixFaderMessage()
        {
            return $"3:::SETD^m.mix^{this.ChannelFaderValue.ToString().Replace(',', '.')}";
        }
        public override string SelectChannelMessage(string syncID)
        {
            return $"3:::BMSG^SYNC^{syncID}^-1";
        }

    }
}
