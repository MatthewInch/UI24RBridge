using System;
using System.Collections.Generic;
using System.Text;

namespace UI24RController.UI24RChannels.Interfaces
{
    interface IPhantomable
    {
        bool IsPhantom { get; set; }
        string PhantomMessage();

    }
}
