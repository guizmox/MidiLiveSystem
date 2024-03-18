using RtMidi.Core.Devices;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;

namespace RtMidi.Core.Messages
{
    public readonly struct StopMessage
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<ClockMessage>();

        public StopMessage()
        {

        }

        internal byte[] Encode()
        {
            return new[]
            {
                Midi.Status.Stop
            };
        }

        internal static bool TryDecode(byte[] message, out StopMessage msg)
        {
            if (message.Length != 1)
            {
                Log.Error("Incorrect number of bytes ({Length}) received for Stop message", message.Length);
                msg = default;
                return false;
            }

            msg = new();
            return true;
        }
    }
}
