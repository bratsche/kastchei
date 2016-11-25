using System;
using System.ComponentModel;

namespace Kastchei
{
    public enum SocketState
    {
        [Description("None")]    None,
        [Description("Closed")]  Closed,
        [Description("Closing")] Closing,
        [Description("Open")]    Open,
        [Description("Opening")] Opening
    }
}
