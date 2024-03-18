using RtMidi.Core.Devices;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;

namespace RtMidi.Core.Messages
{
    public readonly struct StartMessage
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<ClockMessage>();

        public StartMessage()
        {

        }

        internal byte[] Encode()
        {
            return new[]
            {
                Midi.Status.Start
            };
        }

        internal static bool TryDecode(byte[] message, out StartMessage msg)
        {
            if (message.Length != 1)
            {
                Log.Error("Incorrect number of bytes ({Length}) received for Start message", message.Length);
                msg = default;
                return false;
            }

            msg = new();
            return true;
        }
    }
}
