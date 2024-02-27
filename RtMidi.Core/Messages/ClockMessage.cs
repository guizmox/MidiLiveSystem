using RtMidi.Core.Devices;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;

namespace RtMidi.Core.Messages
{
    public readonly struct ClockMessage
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<ClockMessage>();

        public ClockMessage()
        {
            
        }

        internal byte[] Encode()
        {
            return new[]
            {
                Midi.Status.Clock
            };
        }

        internal static bool TryDecode(byte[] message, out ClockMessage msg)
        {
            if (message.Length != 1)
            {
                Log.Error("Incorrect number of bytes ({Length}) received for Clock message", message.Length);
                msg = default;
                return false;
            }

            msg = new();
            return true;
        }
    }
}
