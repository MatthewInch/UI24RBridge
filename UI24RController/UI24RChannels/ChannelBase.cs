using System;
using System.Collections.Generic;
using System.Text;

namespace UI24RController.UI24RChannels

{
    public abstract class ChannelBase
    {
        /// <summary>
        /// Between 0 and 1.0
        /// </summary>
        public double MainFaderValue { get; set; }
        public string Name { get; set; }
        public int ChannelNumber { get; internal set; }

        public ChannelBase(int channelNumber)
        {
            MainFaderValue = 0;
            ChannelNumber = channelNumber;
        }

        public virtual string MixFaderMessage()
        {
            return $"3:::SETD^i.{this.ChannelNumber}.mix^{this.MainFaderValue.ToString().Replace(',', '.')}";
        }

    }
}
