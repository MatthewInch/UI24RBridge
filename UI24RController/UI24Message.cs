using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace UI24RController
{
    public class UI24Message
    {
        // 3:::SETD^i.0.mix^0.7012820512871794
        public int ChannelNumber { get; set; }
        public double FaderValue { get; set; }
        public bool IsValid { get; internal set; }

        public UI24Message(int channelNumber)
        {

        }

        public UI24Message(string message)
        {
            IsValid = false;
            var messageParts = message.Split('^');
            if (messageParts.Count() > 2)
            {
                var messageTypes = messageParts[1].Split('.');
                var channelNumber = 0;
                if ((messageTypes.Count() >= 3) && messageTypes[0] == "i" &&
                    messageTypes[2] == "mix" &&
                    int.TryParse(messageTypes[1], out channelNumber))
                {
                    this.ChannelNumber = channelNumber;
                    double faderValue;
                    if (double.TryParse(messageParts[2], NumberStyles.Number, CultureInfo.InvariantCulture, out faderValue))
                    {
                        FaderValue = faderValue;
                        IsValid = true;
                    }
                }
            }
        }


    }
}
