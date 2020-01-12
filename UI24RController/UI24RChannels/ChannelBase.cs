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
        public double ChannelFaderValue { get; set; }
        public string Name { get; set; }
        public int ChannelNumber { get; internal set; }
        public bool IsSelected { get; set; }
        public virtual int ChannelNumberInMixer => ChannelNumber;

        public double Gain { get; set; }

        public ChannelBase(int channelNumber)
        {
            ChannelFaderValue = 0;
            ChannelNumber = channelNumber;
            IsSelected = false;
            
        }

        public virtual string MixFaderMessage()
        {
            return $"3:::SETD^i.{this.ChannelNumber}.mix^{this.ChannelFaderValue.ToString().Replace(',', '.')}";
        }

        public virtual string SelectChannelMessage(string syncID)
        {
            return $"3:::BMSG^SYNC^{syncID}^{this.ChannelNumberInMixer}";
        }
    }
}
