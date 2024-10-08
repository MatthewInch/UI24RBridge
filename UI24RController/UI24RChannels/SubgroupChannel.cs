﻿using System;
using System.Collections.Generic;
using System.Text;

namespace UI24RController.UI24RChannels
{
    public class SubgroupChannel: ChannelBase
    {
        public SubgroupChannel(int channelNumber): base(channelNumber)
        {
            channelTypeID = "s";
            Eq.ChannelType = channelTypeID;
        }
        public override int ChannelNumberInMixer => this.ChannelNumber + 32;
        protected override string GetDefaultName()
        {
            return $"SUB {(this.ChannelNumber + 1):D2}";
        }

        public override string MixFaderMessage()
        {
            return $"3:::SETD^s.{this.ChannelNumber}.mix^{this.ChannelFaderValue.ToString().Replace(',', '.')}";
        }
        public override string SoloMessage()
        {
            return $"3:::SETD^s.{this.ChannelNumber}.solo^{(this.IsSolo ? 1 : 0)}";
        }
    }
}
