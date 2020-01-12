using System;
using System.Collections.Generic;
using System.Text;

namespace UI24RController.UI24RChannels
{
    public class InputChannel: ChannelBase
    {


        public InputChannel(int channelNumber): base(channelNumber)
        {
           this.Name = $"CH {this.ChannelNumber:D2}";
        }

        public override string MixFaderMessage()
        {
            return $"3:::SETD^i.{this.ChannelNumber}.mix^{this.ChannelFaderValue.ToString().Replace(',', '.')}";
        }
        public string GainMessage()
        {
            return $"3:::SETD^hw.{this.ChannelNumber}.gain^{this.Gain.ToString().Replace(',', '.')}";
        }
    }
}
