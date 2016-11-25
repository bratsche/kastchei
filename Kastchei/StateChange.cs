using System;

namespace Kastchei
{
    class StateChange
    {
        public SocketState Previous { get; set; }
        public SocketState Current { get; set; }

        public StateChange()
            : this(SocketState.None, SocketState.None)
        {
        }

        public StateChange(SocketState prev, SocketState current)
        {
            Previous = prev;
            Current = current;
        }
    }
}
