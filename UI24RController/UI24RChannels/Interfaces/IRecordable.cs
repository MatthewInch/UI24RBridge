using System;
using System.Collections.Generic;
using System.Text;

namespace UI24RController.UI24RChannels.Interfaces
{
    interface IRecordable
    {
        bool IsRec { get; set; }
        string RecMessage();

    }
}
