﻿using System;
using System.Collections.Generic;
using RtMidi.Core.Messages;
using RtMidi.Core.Unmanaged.Devices;
using Serilog;
using RtMidi.Core.Devices.Nrpn;

namespace RtMidi.Core.Devices
{
    internal class MidiInputDevice : MidiDevice, IMidiInputDevice
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<MidiInputDevice>();
        private readonly IRtMidiInputDevice _inputDevice;
        private readonly NrpnInterpreter[] _nrpnInterpreters;
        private List<byte> _sysExBuffer;

        public MidiInputDevice(IRtMidiInputDevice rtMidiInputDevice, string name) : base(rtMidiInputDevice, name)
        {
            _inputDevice = rtMidiInputDevice;
            _inputDevice.Message += RtMidiInputDevice_Message;

            _nrpnInterpreters = new NrpnInterpreter[16];
            for (var i = 0; i < 16; i++)
            {
                _nrpnInterpreters[i] = new(OnControlChange, OnNrpn);
            }

            // Set default NRPN mode
            SetNrpnMode(NrpnMode.On);
        }

        public event NoteOffMessageHandler NoteOff;
        public event NoteOnMessageHandler NoteOn;
        public event PolyphonicKeyPressureMessageHandler PolyphonicKeyPressure;
        public event ControlChangeMessageHandler ControlChange;
        public event ProgramChangeMessageHandler ProgramChange;
        public event ChannelPressureMessageHandler ChannelPressure;
        public event PitchBendMessageHandler PitchBend;
        public event NrpnMessageHandler Nrpn;
        public event SysExMessageHandler SysEx;
        public event MidiTimeCodeQuarterFrameHandler MidiTimeCodeQuarterFrame;
        public event SongPositionPointerHandler SongPositionPointer;
        public event SongSelectHandler SongSelect;
        public event TuneRequestHandler TuneRequest;
        public event ClockHandler Clock;
        public event StartHandler Start;
        public event StopHandler Stop;

        private void RtMidiInputDevice_Message(object sender, byte[] message)
        {
            if (message == null)
            {
                Log.Error("Received null message from device");
                return;
            }

            if (message.Length == 0)
            {
                Log.Error("Received empty message from device");
                return;
            }

            // TODO Decode and propagate midi events on separate thread as not to block receiving thread

            try
            {
                Decode(message);
            }
            catch (Exception e)
            {
                Log.Error(e, "Exception occurred while decoding midi message");
            }
        }

        private void Decode(byte[] message)
        {
            if (_sysExBuffer != null)
            {
                _sysExBuffer.AddRange(message);

                // Check for end of SysEx
                if (message[message.Length - 1] == Midi.Status.SysExEnd)
                {
                    try
                    {
                        if (SysExMessage.TryDecode(_sysExBuffer.ToArray(), out var sysExMessage))
                            SysEx?.Invoke(this, in sysExMessage);
                    }
                    finally
                    {
                        _sysExBuffer = null;
                    }
                }
            }

            byte status = message[0];
            switch (status & 0b1111_0000)
            {
                case Midi.Status.NoteOffBitmask:
                    if (NoteOffMessage.TryDecode(message, out var noteOffMessage))
                        NoteOff?.Invoke(this, in noteOffMessage);
                    break;
                case Midi.Status.NoteOnBitmask:
                    if (NoteOnMessage.TryDecode(message, out var noteOnMessage))
                        NoteOn?.Invoke(this, in noteOnMessage);
                    break;
                case Midi.Status.PolyphonicKeyPressureBitmask:
                    if (PolyphonicKeyPressureMessage.TryDecode(message, out var polyphonicKeyPressureMessage))
                        PolyphonicKeyPressure?.Invoke(this, in polyphonicKeyPressureMessage);
                    break;
                case Midi.Status.ControlChangeBitmask:
                    if (ControlChangeMessage.TryDecode(message, out var controlChangeMessage))
                        _nrpnInterpreters[(int) controlChangeMessage.Channel]
                            .HandleControlChangeMessage(in controlChangeMessage);
                    break;
                case Midi.Status.ProgramChangeBitmask:
                    if (ProgramChangeMessage.TryDecode(message, out var programChangeMessage))
                        ProgramChange?.Invoke(this, in programChangeMessage);
                    break;
                case Midi.Status.ChannelPressureBitmask:
                    if (ChannelPressureMessage.TryDecode(message, out var channelPressureMessage))
                        ChannelPressure?.Invoke(this, in channelPressureMessage);
                    break;
                case Midi.Status.PitchBendChange:
                    if (PitchBendMessage.TryDecode(message, out var pitchBendMessage))
                        PitchBend?.Invoke(this, in pitchBendMessage);
                    break;

                case Midi.Status.System:
                    switch (status)
                    {
                        case Midi.Status.SysExStart:
                            // Check if message is truncated
                            if (message[message.Length - 1] == Midi.Status.SysExEnd)
                            {
                                if (SysExMessage.TryDecode(message, out var sysExMessage))
                                    SysEx?.Invoke(this, in sysExMessage);
                            }
                            else
                            {
                                _sysExBuffer = new(message);
                            }

                            break;
                        case Midi.Status.MidiTimeCodeQuarterFrame:
                            if (MidiTimeCodeQuarterFrameMessage.TryDecode(message,
                                    out var timeCodeQuarterFrameMessage))
                                MidiTimeCodeQuarterFrame?.Invoke(this, in timeCodeQuarterFrameMessage);
                            break;
                        case Midi.Status.SongPositionPointer:
                            if (SongPositionPointerMessage.TryDecode(message, out var songPositionPointerMessage))
                                SongPositionPointer?.Invoke(this, in songPositionPointerMessage);
                            break;
                        case Midi.Status.SongSelect:
                            if (SongSelectMessage.TryDecode(message, out var songSelectMessage))
                                SongSelect?.Invoke(this, in songSelectMessage);
                            break;
                        case Midi.Status.TuneRequest:
                            if (TuneRequestMessage.TryDecode(message, out var tuneRequestMessage))
                                TuneRequest?.Invoke(this, in tuneRequestMessage);
                            break;
                        case Midi.Status.Clock:
                            if (ClockMessage.TryDecode(message,
                                   out var clockmessage))
                                Clock?.Invoke(this, in clockmessage);
                            break;
                        case Midi.Status.Start:
                            if (StartMessage.TryDecode(message,
                                   out var startmessage))
                                Start?.Invoke(this, in startmessage);
                            break;
                        case Midi.Status.Continue:
                            if (StartMessage.TryDecode(message,
                                  out var continuemessage))
                                Start?.Invoke(this, in continuemessage);
                            break;
                        case Midi.Status.Stop:
                            if (StopMessage.TryDecode(message,
                             out var stopmessage))
                                Stop?.Invoke(this, in stopmessage);
                            break;
                        default:
                            Log.Error("Unknown system message type {Status}", $"{status:X2}");
                            break;
                    }

                    break;

                default:
                    Log.Error("Unknown message type {Bitmask}", $"{status & 0b1111_0000:X2}");
                    break;
            }
        }

        protected override void Disposing()
        {
            _inputDevice.Message -= RtMidiInputDevice_Message;

            // Clear all subscribers
            NoteOff = null;
            NoteOn = null;
            PolyphonicKeyPressure = null;
            ControlChange = null;
            ProgramChange = null;
            ChannelPressure = null;
            PitchBend = null;
            SysEx = null;
        }

        private void OnControlChange(in ControlChangeMessage e)
        {
            ControlChange?.Invoke(this, in e);
        }

        private void OnNrpn(in NrpnMessage e)
        {
            Nrpn?.Invoke(this, in e);
        }

        public void SetNrpnMode(NrpnMode nrpnMode)
        {
            foreach (var nrpnInterpreter in _nrpnInterpreters)
            {
                nrpnInterpreter.SetNrpnMode(nrpnMode);
            }
        }
    }
}