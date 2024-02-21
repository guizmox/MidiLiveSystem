﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Timers;
using RtMidi.Core;
using RtMidi.Core.Devices;
using RtMidi.Core.Enums;
using RtMidi.Core.Messages;

namespace MidiTools
{
    internal class MatrixItem
    {
        internal bool Active { get; set; } = true;
        internal int ChannelIn = 1;
        internal int ChannelOut = 1;
        internal MidiOptions Options = new MidiOptions();
        internal MidiDevice DeviceIn;
        internal MidiDevice DeviceOut;
        internal MidiPreset Preset;
        internal Guid RoutingGuid { get; private set; }

        public MatrixItem(MidiDevice deviceIN, MidiDevice deviceOUT, int iChIN, int iChOUT, MidiOptions options, MidiPreset preset)
        {
            DeviceIn = deviceIN;
            DeviceOut = deviceOUT;
            ChannelIn = iChIN;
            ChannelOut = iChOUT;
            Options = options;
            RoutingGuid = Guid.NewGuid();
            Preset = preset;
        }

        internal bool CheckDeviceIn(string sDeviceIn)
        {
            if (DeviceIn != null && DeviceIn.Name != sDeviceIn)
            {
                return true;
            }
            else { return false; }
        }

        internal bool CheckDeviceOut(string sDeviceOut)
        {
            if (DeviceOut != null && DeviceOut.Name != sDeviceOut)
            {
                return true;
            }
            else { return false; }
        }

        internal bool CheckPreset(MidiPreset preset)
        {
            if (preset.Lsb != Preset.Lsb || preset.Msb != Preset.Msb || preset.Prg != Preset.Prg)
            {
                return true;
            }
            else { return false; }
        }
    }

    public class MidiOptions
    {
        private int _TranspositionOffset = 0;

        private int _VelocityFilterLow = 0;
        private int _VelocityFilterHigh = 127;
        private int _NoteFilterLow = 0;
        private int _NoteFilterHigh = 127;

        private int _CC_Pan_Value = 64;
        private int _CC_Volume_Value = 100;
        private int _CC_Reverb_Value = -1;
        private int _CC_Chorus_Value = -1;
        private int _CC_Release_Value = -1;
        private int _CC_Attack_Value = -1;
        private int _CC_Decay_Value = -1;
        private int _CC_Brightness_Value = -1;

        public int VelocityFilterLow
        {
            get { return _VelocityFilterLow; }
            set
            {
                if (value < 0) { _VelocityFilterLow = 0; }
                else if (value > 127) { _VelocityFilterLow = 127; }
                else { _VelocityFilterLow = value; }
                if (_VelocityFilterLow > _VelocityFilterHigh) { _VelocityFilterLow = 0; }
            }
        }
        public int VelocityFilterHigh
        {
            get { return _VelocityFilterHigh; }
            set
            {
                if (value < 0) { _VelocityFilterHigh = 0; }
                else if (value > 127) { _VelocityFilterHigh = 127; }
                else { _VelocityFilterHigh = value; }
                if (_VelocityFilterHigh < _VelocityFilterLow) { _VelocityFilterHigh = 127; }
            }
        }
        public int NoteFilterLow
        {
            get { return _NoteFilterLow; }
            set
            {
                if (value < 0) { _NoteFilterLow = 0; }
                else if (value > 127) { _NoteFilterLow = 127; }
                else { _NoteFilterLow = value; }
                if (_NoteFilterLow > _NoteFilterHigh) { _NoteFilterLow = 0; }
            }
        }
        public int NoteFilterHigh
        {
            get { return _NoteFilterHigh; }
            set
            {
                if (value < 0)
                { _NoteFilterHigh = 0; }
                else if (value > 127) { _NoteFilterHigh = 127; }
                else { _NoteFilterHigh = value; }
                if (_NoteFilterHigh < _NoteFilterLow) { _NoteFilterHigh = 127; }
            }
        }


        public int TranspositionOffset
        {
            get { return _TranspositionOffset; }
            set
            {
                if (value < -127) { _TranspositionOffset = -127; }
                else if (value > 127) { _TranspositionOffset = 127; }
                else { _TranspositionOffset = value; }
            }
        }

        public bool AftertouchVolume = false;

        public bool AllowModulation = true;
        public bool AllowNotes = true;
        public bool AllowAllCC = true;
        public bool AllowSysex = true;
        public bool AllowNrpn = true;
        public bool AllowAftertouch = true;
        public bool AllowPitchBend = true;
        public bool AllowProgramChange = true;

        public int CC_ToVolume { get; set; } = -1; //convertisseur de CC pour le volume (ex : CC 102 -> CC 7)

        public int CC_Pan_Value { get { return _CC_Pan_Value; } set { if (value < -1) { _CC_Pan_Value = -1; } else if (value > 127) { _CC_Pan_Value = 127; } } }
        public int CC_Volume_Value { get { return _CC_Volume_Value; } set { if (value < -1) { _CC_Volume_Value = -1; } else if (value > 127) { _CC_Volume_Value = 127; } } }
        public int CC_Reverb_Value { get { return _CC_Reverb_Value; } set { if (value < -1) { _CC_Reverb_Value = -1; } else if (value > 127) { _CC_Reverb_Value = 127; } } }
        public int CC_Chorus_Value { get { return _CC_Chorus_Value; } set { if (value < -1) { _CC_Chorus_Value = -1; } else if (value > 127) { _CC_Chorus_Value = 127; } } }
        public int CC_Release_Value { get { return _CC_Release_Value; } set { if (value < -1) { _CC_Release_Value = -1; } else if (value > 127) { _CC_Release_Value = 127; } } }
        public int CC_Attack_Value { get { return _CC_Attack_Value; } set { if (value < -1) { _CC_Attack_Value = -1; } else if (value > 127) { _CC_Attack_Value = 127; } } }
        public int CC_Decay_Value { get { return _CC_Decay_Value; } set { if (value < -1) { _CC_Decay_Value = -1; } else if (value > 127) { _CC_Decay_Value = 127; } } }
        public int CC_Brightness_Value { get { return _CC_Brightness_Value; } set { if (value < -1) { _CC_Brightness_Value = -1; } else if (value > 127) { _CC_Brightness_Value = 127; } } }

        public List<int[]> CC_Converters { get; private set; } = new List<int[]>();
        public List<int[]> Note_Converters { get; private set; } = new List<int[]>();

        public bool AddCCConverter(int iFrom, int iTo)
        {
            if (iFrom != iTo && (iFrom >= 0 && iFrom <= 127) && (iTo >= 0 && iTo <= 127))
            {
                CC_Converters.Add(new int[] { iFrom, iTo });
                return true;
            }
            else { return false; }
        }

        public bool AddNoteConverter(int iFrom, int iTo)
        {
            if (iFrom != iTo && (iFrom >= 0 && iFrom <= 127) && (iTo >= 0 && iTo <= 127))
            {
                Note_Converters.Add(new int[] { iFrom, iTo });
                return true;
            }
            else { return false; }
        }
    }

    public class MidiRouting
    {
        private static double CLOCK_INTERVAL = 20;

        public static List<RtMidi.Core.Devices.Infos.IMidiInputDeviceInfo> InputDevices
        {
            get
            {
                List<RtMidi.Core.Devices.Infos.IMidiInputDeviceInfo> list = new List<RtMidi.Core.Devices.Infos.IMidiInputDeviceInfo>();
                foreach (var inputDeviceInfo in MidiDeviceManager.Default.InputDevices)
                {
                    list.Add(inputDeviceInfo);
                }
                return list;
            }
        }

        public static List<RtMidi.Core.Devices.Infos.IMidiOutputDeviceInfo> OutputDevices
        {
            get
            {
                List<RtMidi.Core.Devices.Infos.IMidiOutputDeviceInfo> list = new List<RtMidi.Core.Devices.Infos.IMidiOutputDeviceInfo>();
                foreach (var outputDeviceInfo in MidiDeviceManager.Default.OutputDevices)
                {
                    list.Add(outputDeviceInfo);
                }
                return list;
            }
        }

        public delegate void LogEventHandler(string sDevice, bool bIn, string sLog);
        public static event LogEventHandler NewLog;

        public string CyclesInfo
        {
            get
            {
                string sMessage = "MIDI Average Processing Messages / Cycle : ";
                sMessage = string.Concat(sMessage, " [IN] : " + (_eventsProcessedIN / _iCyclesIN).ToString());
                sMessage = string.Concat(sMessage, " / [OUT] : " + (_eventsProcessedOUT / _iCyclesOUT).ToString());

                return sMessage;
            }
        }


        private List<MatrixItem> MidiMatrix = new List<MatrixItem>();

        private System.Timers.Timer Clock;

        private List<MidiDevice> DevicesIN = new List<MidiDevice>();
        private List<MidiDevice> DevicesOUT = new List<MidiDevice>();

        private List<MidiDevice.MidiEvent> _eventsIN = new List<MidiDevice.MidiEvent>();
        private Int64 _eventsProcessedIN = 0;
        private Int64 _iCyclesIN = 1;

        private List<Tuple<string, NoteOnMessage, bool>> _pendingATNoteMessages = new List<Tuple<string, NoteOnMessage, bool>>();
        private List<MidiDevice.MidiEvent> _eventsOUT = new List<MidiDevice.MidiEvent>();
        private Int64 _eventsProcessedOUT = 0;
        private Int64 _iCyclesOUT = 1;

        public MidiRouting()
        {
            foreach (var dev in InputDevices)
            {
                DevicesIN.Add(new MidiDevice(dev));
            }

            foreach (var dev in OutputDevices)
            {
                DevicesOUT.Add(new MidiDevice(dev));
            }

            //MidiDevice.OnLogAdded += MidiDevice_OnLogAdded;

            Clock = new System.Timers.Timer();
            Clock.Elapsed += QueueProcessor_OnEvent;
            Clock.Interval = CLOCK_INTERVAL;
            Clock.Start();
        }

        #region PRIVATE

        private void QueueProcessor_OnEvent(object sender, ElapsedEventArgs e)
        {
            MidiDevice.MidiEvent[] evTmp = null;
            lock (_eventsIN)
            {
                evTmp = new MidiDevice.MidiEvent[_eventsIN.Count];
                _eventsIN.CopyTo(evTmp);
                _eventsIN.Clear();
            }

            StoreNoteOnMessage(_eventsIN.Where(ev => ev.Type == MidiDevice.TypeEvent.NOTE_ON).ToList());
            ReleaseNoteOffMessage(_eventsIN.Where(ev => ev.Type == MidiDevice.TypeEvent.NOTE_OFF).ToList());

            if (evTmp != null)
            {
                for (int i = 0; i < evTmp.Length; i++)
                {
                    //ajouter l'évènement entrant à la liste des évènements sortants à traiter
                    CreateOUTEvent(evTmp[i], false);
                }

                if (evTmp.Length > 0)
                {
                    _iCyclesIN++;
                    _eventsProcessedIN += evTmp.Length;
                }
            }

            //évènements à transférer dans les différents routings
            lock (_eventsOUT)
            {
                evTmp = new MidiDevice.MidiEvent[_eventsOUT.Count];
                _eventsOUT.CopyTo(evTmp);
                _eventsOUT.Clear();
            }

            if (evTmp != null)
            {
                for (int i = 0; i < evTmp.Length; i++)
                {
                    var dev = DevicesOUT.FirstOrDefault(d => d.Name == evTmp[i].Device);
                    if (dev != null)
                    { dev.SendMidiEvent(evTmp[i]); }
                }

                if (evTmp.Length > 0)
                {
                    _iCyclesOUT++;
                    _eventsProcessedOUT += evTmp.Length;
                }
            }
        }

        private void ReleaseNoteOffMessage(List<MidiDevice.MidiEvent> events)
        {
            foreach (var ev in events)
            {
                var noteoff = (NoteOffMessage)ev.Event; //TODO : contrôle avec vélocité aussi ?
                var noteon = _pendingATNoteMessages.FirstOrDefault(n => n.Item2.Key == noteoff.Key && n.Item2.Channel == noteoff.Channel && ev.Device == n.Item1 && n.Item3);
                lock (_pendingATNoteMessages) { _pendingATNoteMessages.Remove(noteon); }
            }
        }

        private void StoreNoteOnMessage(List<MidiDevice.MidiEvent> events)
        {
            foreach (var ev in events)
            {
                lock (_pendingATNoteMessages) { _pendingATNoteMessages.Add(new Tuple<string, NoteOnMessage, bool>(ev.Device, (NoteOnMessage)ev.Event, false)); }
            }
        }

        private void DeviceOut_OnMidiEvent(bool bIn, MidiDevice.MidiEvent ev)
        {
            //lock (_eventsOUT) { _eventsOUT.Add(ev); }
        }

        private void DeviceIn_OnMidiEvent(bool bIn, MidiDevice.MidiEvent ev)
        {
            lock (_eventsIN) { _eventsIN.Add(ev); }
        }

        private void MidiDevice_OnLogAdded(string sDevice, bool bIn, string sLog)
        {
            NewLog?.Invoke(sDevice, bIn, sLog);
        }

        private void CheckPortUsage(bool bIN)
        {
            if (bIN)
            {
                List<string> usedIn = new List<string>();
                foreach (var mat in MidiMatrix)
                {
                    if (mat.Active && !usedIn.Contains(mat.DeviceIn.Name))
                    {
                        usedIn.Add(mat.DeviceIn.Name);
                    }
                }

                foreach (var dev in usedIn)
                {
                    var device = MidiMatrix.FirstOrDefault(m => m.DeviceIn.Name.Equals(dev));
                    device.DeviceIn.OpenDevice();
                }

                var notusedIN = MidiMatrix.Where(m => !usedIn.Contains(m.DeviceIn.Name)).ToList();
                foreach (var dev in notusedIN)
                {
                    dev.DeviceIn.CloseDevice();
                }
            }
            else
            {
                List<string> usedOut = new List<string>();

                foreach (var mat in MidiMatrix)
                {
                    if (mat.Active && !usedOut.Contains(mat.DeviceOut.Name))
                    {
                        usedOut.Add(mat.DeviceOut.Name);
                    }
                }

                foreach (var dev in usedOut)
                {
                    var device = MidiMatrix.FirstOrDefault(m => m.DeviceOut.Name.Equals(dev));
                    device.DeviceOut.OpenDevice();
                }


                var notusedOUT = MidiMatrix.Where(m => !usedOut.Contains(m.DeviceOut.Name)).ToList();
                foreach (var dev in notusedOUT)
                {
                    dev.DeviceOut.CloseDevice();
                }

            }

        }

        private void CheckAndOpenPorts(MidiDevice devIn, MidiDevice devOut)
        {
            List<MatrixItem> listUse;
            if (devIn != null)
            {
                listUse = MidiMatrix.Where(d => d.DeviceIn.Name == devIn.Name).ToList();
                if (listUse.Count == 1) //ouverture du port
                {
                    try
                    {
                        listUse[0].DeviceIn.OpenDevice();
                        listUse[0].DeviceIn.OnMidiEvent += DeviceIn_OnMidiEvent;
                    }
                    catch { throw; }
                }
            }

            if (devOut != null)
            {
                listUse = MidiMatrix.Where(d => d.DeviceOut.Name == devOut.Name).ToList();
                if (listUse.Count == 1) //ouverture du port
                {
                    try
                    {
                        listUse[0].DeviceOut.OpenDevice();
                        listUse[0].DeviceOut.OnMidiEvent += DeviceOut_OnMidiEvent;
                    }
                    catch { throw; }
                }
            }
        }

        private void CheckAndClosePorts(MidiDevice devIn, MidiDevice devOut)
        {
            List<MatrixItem> listUse = MidiMatrix.Where(d => d.DeviceIn != null && d.DeviceIn.Name == devIn.Name).ToList();
            if (listUse.Count == 1) //ouverture du port
            {
                try
                {
                    listUse[0].DeviceIn.OnMidiEvent -= DeviceIn_OnMidiEvent;
                    listUse[0].DeviceIn.CloseDevice();
                }
                catch { throw; }
            }
            listUse = MidiMatrix.Where(d => d.DeviceOut != null && d.DeviceOut.Name == devOut.Name).ToList();
            if (listUse.Count == 1) //ouverture du port
            {
                try
                {
                    listUse[0].DeviceOut.OnMidiEvent -= DeviceOut_OnMidiEvent;
                    listUse[0].DeviceOut.CloseDevice();
                }
                catch { throw; }
            }
        }

        private void CreateOUTEvent(MidiDevice.MidiEvent ev, bool bForce)
        {
            var matrix = MidiMatrix.Where(i => i.Active && i.DeviceIn != null && i.DeviceIn.Name == ev.Device && (i.ChannelIn == 0 || MidiDevice.GetChannel(i.ChannelIn) == ev.MidiChannel)).ToList();

            //gestion des évènements du synthé externe : pas un vrai device
            if (matrix.Count == 0 && MidiMatrix.Count == 1)
            {
                matrix.Add(MidiMatrix[0]);
            }
            //----------------------

            foreach (MatrixItem item in matrix)
            {
                for (int i = (item.ChannelOut == 0 ? 1 : item.ChannelOut); i <= (item.ChannelOut == 0 ? 16 : item.ChannelOut); i++)
                {
                    //opérations de filtrage 
                    switch (ev.Type)
                    {
                        case MidiDevice.TypeEvent.CC:
                            if (item.Options.AllowAllCC || bForce)
                            {
                                var msg = (ControlChangeMessage)ev.Event;

                                if (item.Options.CC_ToVolume == msg.Control)
                                {
                                    var msgout = new ControlChangeMessage(MidiDevice.GetChannel(i), 7, msg.Value);
                                    lock (_eventsOUT) { _eventsOUT.Add(new MidiDevice.MidiEvent(ev.Type, msgout, msgout.Channel, item.DeviceOut.Name)); }
                                }
                                else
                                {
                                    var convertedCC = item.Options.CC_Converters.FirstOrDefault(i => i[0] == msg.Control);
                                    var msgout = new ControlChangeMessage(MidiDevice.GetChannel(i), convertedCC != null ? convertedCC[1] : msg.Control, msg.Value);
                                    lock (_eventsOUT) { _eventsOUT.Add(new MidiDevice.MidiEvent(ev.Type, msgout, msgout.Channel, item.DeviceOut.Name)); }
                                }
                            }
                            break;
                        case MidiDevice.TypeEvent.CH_PRES:
                            if (item.Options.AllowAftertouch || bForce)
                            {
                                var msg = (ChannelPressureMessage)ev.Event;
                                var msgout = new ChannelPressureMessage(MidiDevice.GetChannel(i), msg.Pressure);
                                lock (_eventsOUT) { _eventsOUT.Add(new MidiDevice.MidiEvent(ev.Type, msgout, msgout.Channel, item.DeviceOut.Name)); }
                            }
                            if (item.Options.AftertouchVolume)
                            {
                                var msg = (ChannelPressureMessage)ev.Event;
                                if (msg.Pressure == 0) //on balance un note off à tous les évènements en attente
                                {
                                    foreach (var noteon in _pendingATNoteMessages)
                                    {
                                        if (noteon.Item3) //a déjà été déclenché
                                        {
                                            var msgout = new NoteOffMessage(MidiDevice.GetChannel(i), noteon.Item2.Key, msg.Pressure);
                                            lock (_eventsOUT) { _eventsOUT.Add(new MidiDevice.MidiEvent(ev.Type, msgout, msgout.Channel, item.DeviceOut.Name)); }
                                        }
                                    }
                                }
                                else //il y a de la pression sur l'AT
                                {
                                    for (int ipd = 0; ipd <= _pendingATNoteMessages.Count(); ipd++)
                                    {
                                        if (!_pendingATNoteMessages[ipd].Item3) //n'a pas encore été déclenché
                                        {
                                            //réécriture de la valeur pour signifier que la note a déjà été appuyée (pour éviter qu'à chaque modulation de l'AT, ça retrigger une note)
                                            _pendingATNoteMessages[ipd] = new Tuple<string, NoteOnMessage, bool>(_pendingATNoteMessages[ipd].Item1, _pendingATNoteMessages[ipd].Item2, true);

                                            var msgout = new NoteOnMessage(MidiDevice.GetChannel(i), _pendingATNoteMessages[ipd].Item2.Key, msg.Pressure);
                                            lock (_eventsOUT) { _eventsOUT.Add(new MidiDevice.MidiEvent(ev.Type, msgout, msgout.Channel, item.DeviceOut.Name)); }
                                        }
                                    }
                                }
                                var msgoutCC = new ControlChangeMessage(MidiDevice.GetChannel(i), 7, msg.Pressure); //envoi du CC pour moduler le volume
                                lock (_eventsOUT) { _eventsOUT.Add(new MidiDevice.MidiEvent(ev.Type, msgoutCC, msgoutCC.Channel, item.DeviceOut.Name)); }
                            }
                            break;
                        case MidiDevice.TypeEvent.NOTE_OFF:
                            if (item.Options.AllowNotes || bForce)
                            {
                                var msg = (NoteOffMessage)ev.Event;

                                int iNote = GetNoteIndex(msg.Key, msg.Velocity, item.Options);
                                var convertedNote = item.Options.Note_Converters.FirstOrDefault(i => i[0] == GetNoteIndex(msg.Key, 0, null));
                                if (convertedNote != null) { iNote = convertedNote[1]; } //TODO DOUTE

                                if (iNote >= -1)
                                {
                                    var msgout = new NoteOffMessage(MidiDevice.GetChannel(i), MidiDevice.GetNote(iNote), msg.Velocity);
                                    lock (_eventsOUT) { _eventsOUT.Add(new MidiDevice.MidiEvent(ev.Type, msgout, msgout.Channel, item.DeviceOut.Name)); }
                                }
                            }
                            break;
                        case MidiDevice.TypeEvent.NOTE_ON:
                            if (item.Options.AllowNotes || bForce)
                            {
                                var msg = (NoteOnMessage)ev.Event;

                                int iNote = GetNoteIndex(msg.Key, msg.Velocity, item.Options);
                                var convertedNote = item.Options.Note_Converters.FirstOrDefault(i => i[0] == GetNoteIndex(msg.Key, 0, null));
                                if (convertedNote != null) { iNote = convertedNote[1]; } //TODO DOUTE

                                if (iNote >= -1)
                                {
                                    var msgout = new NoteOnMessage(MidiDevice.GetChannel(i), MidiDevice.GetNote(iNote), msg.Velocity);
                                    lock (_eventsOUT) { _eventsOUT.Add(new MidiDevice.MidiEvent(ev.Type, msgout, msgout.Channel, item.DeviceOut.Name)); }
                                }
                            }
                            break;
                        case MidiDevice.TypeEvent.NRPN:
                            if (item.Options.AllowNrpn || bForce)
                            {
                                var msg = (NrpnMessage)ev.Event;
                                var msgout = new NrpnMessage(MidiDevice.GetChannel(i), msg.Parameter, msg.Value);
                                lock (_eventsOUT) { _eventsOUT.Add(new MidiDevice.MidiEvent(ev.Type, msgout, msgout.Channel, item.DeviceOut.Name)); }
                            }
                            break;
                        case MidiDevice.TypeEvent.PB:
                            if (item.Options.AllowPitchBend || bForce)
                            {
                                var msg = (PitchBendMessage)ev.Event;
                                var msgout = new PitchBendMessage(MidiDevice.GetChannel(i), msg.Value);
                                lock (_eventsOUT) { _eventsOUT.Add(new MidiDevice.MidiEvent(ev.Type, msgout, msgout.Channel, item.DeviceOut.Name)); }
                            }
                            break;
                        case MidiDevice.TypeEvent.PC:
                            if (item.Options.AllowProgramChange || bForce)
                            {
                                var msg = (ProgramChangeMessage)ev.Event;
                                var msgout = new ProgramChangeMessage(MidiDevice.GetChannel(i), msg.Program);
                                lock (_eventsOUT) { _eventsOUT.Add(new MidiDevice.MidiEvent(ev.Type, msgout, msgout.Channel, item.DeviceOut.Name)); }
                            }
                            break;
                        case MidiDevice.TypeEvent.POLY_PRES:
                            if (item.Options.AllowAftertouch || bForce)
                            {
                                var msg = (PolyphonicKeyPressureMessage)ev.Event;
                                var msgout = new PolyphonicKeyPressureMessage(MidiDevice.GetChannel(i), msg.Key, msg.Pressure);
                                lock (_eventsOUT) { _eventsOUT.Add(new MidiDevice.MidiEvent(ev.Type, msgout, msgout.Channel, item.DeviceOut.Name)); }
                            }
                            break;
                        case MidiDevice.TypeEvent.SYSEX:
                            if (item.Options.AllowSysex || bForce)
                            {
                                var msg = (SysExMessage)ev.Event;
                                var msgout = new SysExMessage(SysExTranscoder(msg.Data));
                                lock (_eventsOUT) { _eventsOUT.Add(new MidiDevice.MidiEvent(ev.Type, msgout, Channel.Channel1, item.DeviceOut.Name)); }
                            }
                            break;
                    }
                }
            }
        }

        private byte[] SysExTranscoder(byte[] data)
        {
            //TODO SCRIPT TRANSCO
            return data;
        }

        private int GetNoteIndex(Key key, int vel, MidiOptions options)
        {
            int iNote = -1;
            int iKey = Convert.ToInt32(key.ToString().Substring(3));

            if (options != null)
            {
                if (iKey >= options.NoteFilterLow && iKey <= options.NoteFilterHigh 
                    && (vel == 0 || (vel >= options.VelocityFilterLow && vel <= options.VelocityFilterHigh))) //attention, la vélocité d'un noteoff est souvent à 0 (dépend des devices mais généralement)
                {
                    iNote = iKey + options.TranspositionOffset;
                }
            }

            return iNote;
        }

        #endregion

        #region PUBLIC

        public Guid AddRouting(string sDeviceIn, string sDeviceOut, int iChIn, int iChOut, MidiOptions options, MidiPreset preset = null)
        {
            var devIN = DevicesIN.FirstOrDefault(d => d.Name.Equals(sDeviceIn));
            var devOUT = DevicesOUT.FirstOrDefault(d => d.Name.Equals(sDeviceOut));
            //iAction : 0 = delete, 1 = adds

            //devIN != null && devOUT != null && 
            if (iChIn >= 0 && iChIn <= 16 && iChOut >= 0 && iChOut <= 16)
            {
                var exists = MidiMatrix.FirstOrDefault(m => m.DeviceIn.Name == sDeviceIn && m.DeviceOut.Name == sDeviceOut && m.ChannelIn == iChIn && m.ChannelOut == iChOut);
                if (exists == null)
                {
                    MidiMatrix.Add(new MatrixItem(devIN, devOUT, iChIn, iChOut, options, preset));
                    CheckAndOpenPorts(devIN, devOUT);
                    var guid = MidiMatrix.Last().RoutingGuid;
                    return guid;
                }
                else
                {
                    return exists.RoutingGuid;
                }
            }
            else { return Guid.Empty; }
        }

        public bool ModifyRoutingOptions(Guid routingGuid, MidiOptions options)
        {
            var routing = MidiMatrix.FirstOrDefault(m => m.RoutingGuid == routingGuid);
            if (routing != null)
            {
                routing.Options = options;
                return true;
            }
            else { return false; }
        }

        public bool ModifyRouting(Guid routingGuid, string sDeviceIn, string sDeviceOut, int iChIn, int iChOut, MidiOptions options, MidiPreset preset = null)
        {
            var routing = MidiMatrix.FirstOrDefault(m => m.RoutingGuid == routingGuid);
            if (routing != null)
            {
                routing.Options = options;
                routing.ChannelIn = iChIn;
                routing.ChannelOut = iChOut;
                bool bINChanged = routing.CheckDeviceIn(sDeviceIn);
                bool bOUTChanged = routing.CheckDeviceOut(sDeviceOut);
                if (bINChanged)
                {
                    bool active = routing.Active;
                    routing.Active = false;
                    CheckPortUsage(true);
                    routing.DeviceIn = new MidiDevice(MidiRouting.InputDevices.FirstOrDefault(m => m.Name.Equals(sDeviceIn)));
                    routing.DeviceIn.OpenDevice();
                    routing.Active = active;
                }
                if (bOUTChanged)
                {
                    bool active = routing.Active;
                    routing.Active = false;
                    CheckPortUsage(false);
                    routing.DeviceOut = new MidiDevice(MidiRouting.OutputDevices.FirstOrDefault(m => m.Name.Equals(sDeviceOut)));
                    routing.DeviceOut.OpenDevice();
                    routing.Active = active;
                }

                bool bPresetChanged = routing.CheckPreset(preset);
                routing.Preset = preset;
                if (bPresetChanged)
                {
                    SendPresetChange(routingGuid, preset);
                }

                return true;
            }
            else { return false; }
        }


        public bool ModifyRoutingPreset(Guid routingGuid, MidiPreset preset)
        {
            var routing = MidiMatrix.FirstOrDefault(m => m.RoutingGuid == routingGuid);
            if (routing != null)
            {
                routing.Preset = preset;

                SendPresetChange(routingGuid, preset);
                return true;
            }
            else { return false; }
        }

        public void SetSolo(Guid routingGuid)
        {
            var routingOn = MidiMatrix.FirstOrDefault(m => m.RoutingGuid != routingGuid);

            if (routingOn != null)
            {
                routingOn.Active = true;
            }

            var routingOff = MidiMatrix.Where(m => m.RoutingGuid != routingGuid);
            foreach (var r in routingOff)
            {
                r.Active = false;
            }
        }

        public void MuteRouting(Guid routingGuid)
        {
            var routingOn = MidiMatrix.FirstOrDefault(m => m.RoutingGuid != routingGuid);
            if (routingOn != null)
            {
                routingOn.Active = false;
            }
        }

        public void UnmuteRouting(Guid routingGuid)
        {
            var routingOff = MidiMatrix.FirstOrDefault(m => m.RoutingGuid != routingGuid);
            if (routingOff != null)
            {
                routingOff.Active = true;
            }
        }

        public void UnmuteAllRouting()
        {
            foreach (var r in MidiMatrix)
            {
                r.Active = true;
            }
        }

        public void InitRouting(Guid routingGuid)
        {
            //init des périphériques OUT
            var midi = MidiMatrix.FirstOrDefault(m => m.RoutingGuid == routingGuid);

            if (midi.DeviceOut != null) //dans le cas ou on a crée un routing sans OUT mais uniquement avec un IN
            {
                midi.DeviceOut.OpenDevice();

                for (int i = (midi.ChannelOut == 0 ? 1 : midi.ChannelOut); i <= (midi.ChannelOut == 0 ? 16 : midi.ChannelOut); i++)
                {
                    if (midi.Options.CC_Attack_Value >= 0)
                    {
                        var msg = new ControlChangeMessage(MidiDevice.GetChannel(i), midi.DeviceOut.CC_Attack, midi.Options.CC_Attack_Value);
                        midi.DeviceOut.SendMidiEvent(new MidiDevice.MidiEvent(MidiDevice.TypeEvent.CC, msg, MidiDevice.GetChannel(i), midi.DeviceOut.Name));
                    }
                    if (midi.Options.CC_Brightness_Value >= 0)
                    {
                        var msg = new ControlChangeMessage(MidiDevice.GetChannel(i), midi.DeviceOut.CC_Brightness, midi.Options.CC_Brightness_Value);
                        midi.DeviceOut.SendMidiEvent(new MidiDevice.MidiEvent(MidiDevice.TypeEvent.CC, msg, MidiDevice.GetChannel(i), midi.DeviceOut.Name));
                    }
                    if (midi.Options.CC_Chorus_Value >= 0)
                    {
                        var msg = new ControlChangeMessage(MidiDevice.GetChannel(i), midi.DeviceOut.CC_Chorus, midi.Options.CC_Chorus_Value);
                        midi.DeviceOut.SendMidiEvent(new MidiDevice.MidiEvent(MidiDevice.TypeEvent.CC, msg, MidiDevice.GetChannel(i), midi.DeviceOut.Name));
                    }
                    if (midi.Options.CC_Decay_Value >= 0)
                    {
                        var msg = new ControlChangeMessage(MidiDevice.GetChannel(i), midi.DeviceOut.CC_Decay, midi.Options.CC_Decay_Value);
                        midi.DeviceOut.SendMidiEvent(new MidiDevice.MidiEvent(MidiDevice.TypeEvent.CC, msg, MidiDevice.GetChannel(i), midi.DeviceOut.Name));
                    }
                    if (midi.Options.CC_Pan_Value >= 0)
                    {
                        var msg = new ControlChangeMessage(MidiDevice.GetChannel(i), midi.DeviceOut.CC_Pan, midi.Options.CC_Pan_Value);
                        midi.DeviceOut.SendMidiEvent(new MidiDevice.MidiEvent(MidiDevice.TypeEvent.CC, msg, MidiDevice.GetChannel(i), midi.DeviceOut.Name));
                    }
                    if (midi.Options.CC_Release_Value >= 0)
                    {
                        var msg = new ControlChangeMessage(MidiDevice.GetChannel(i), midi.DeviceOut.CC_Release, midi.Options.CC_Release_Value);
                        midi.DeviceOut.SendMidiEvent(new MidiDevice.MidiEvent(MidiDevice.TypeEvent.CC, msg, MidiDevice.GetChannel(i), midi.DeviceOut.Name));
                    }
                    if (midi.Options.CC_Reverb_Value >= 0)
                    {
                        var msg = new ControlChangeMessage(MidiDevice.GetChannel(i), midi.DeviceOut.CC_Reverb, midi.Options.CC_Reverb_Value);
                        midi.DeviceOut.SendMidiEvent(new MidiDevice.MidiEvent(MidiDevice.TypeEvent.CC, msg, MidiDevice.GetChannel(i), midi.DeviceOut.Name));
                    }
                    if (midi.Options.CC_Volume_Value >= 0)
                    {
                        var msg = new ControlChangeMessage(MidiDevice.GetChannel(i), midi.DeviceOut.CC_Volume, midi.Options.CC_Volume_Value);
                        midi.DeviceOut.SendMidiEvent(new MidiDevice.MidiEvent(MidiDevice.TypeEvent.CC, msg, MidiDevice.GetChannel(i), midi.DeviceOut.Name));
                    }
                }
            }
        }

        public bool DeleteRouting(Guid routingGuid)
        {
            var routing = MidiMatrix.FirstOrDefault(m => m.RoutingGuid == routingGuid);
            if (routing != null)
            {
                CheckAndClosePorts(routing.DeviceIn, routing.DeviceOut);
                MidiMatrix.Remove(routing);
                return true;
            }
            else { return false; }

        }

        public void DeleteAllRouting()
        {
            foreach (var item in MidiMatrix)
            {
                if (item.DeviceIn != null)
                {
                    try
                    {
                        item.DeviceIn.CloseDevice();
                        item.DeviceIn.OnMidiEvent -= DeviceIn_OnMidiEvent;
                    }
                    catch { throw; }
                }

                if (item.DeviceOut != null)
                {
                    try
                    {
                        item.DeviceOut.CloseDevice();
                        item.DeviceOut.OnMidiEvent -= DeviceOut_OnMidiEvent;
                    }
                    catch { throw; }
                }
            }
            MidiMatrix.Clear();
        }

        public void SendPresetChange(Guid routingGuid, MidiPreset preset)
        {
            var routing = MidiMatrix.FirstOrDefault(r => r.RoutingGuid == routingGuid);

            if (routing != null)
            {
                ControlChangeMessage pc00 = new ControlChangeMessage(MidiDevice.GetChannel(preset.Channel), 0, preset.Msb);
                ControlChangeMessage pc32 = new ControlChangeMessage(MidiDevice.GetChannel(preset.Channel), 32, preset.Lsb);
                ProgramChangeMessage prg = new ProgramChangeMessage(MidiDevice.GetChannel(preset.Channel), preset.Prg);

                var dev = DevicesOUT.FirstOrDefault(d => d.Name.Equals(routing.DeviceOut.Name));
                if (dev != null)
                {
                    CreateOUTEvent(new MidiDevice.MidiEvent(MidiDevice.TypeEvent.CC, pc00, pc00.Channel, routing.DeviceOut.Name), true);
                    CreateOUTEvent(new MidiDevice.MidiEvent(MidiDevice.TypeEvent.CC, pc32, pc32.Channel, routing.DeviceOut.Name), true);
                    CreateOUTEvent(new MidiDevice.MidiEvent(MidiDevice.TypeEvent.PC, prg, prg.Channel, routing.DeviceOut.Name), true);
                }
            }
        }

        public void SendCC(int iCC, int iValue, int iChannel, string sDevice)
        {
            var dev = DevicesOUT.FirstOrDefault(d => d.Name.Equals(sDevice));
            if (dev != null)
            {
                var cc = new ControlChangeMessage(MidiDevice.GetChannel(iChannel), iCC, iValue);
                CreateOUTEvent(new MidiDevice.MidiEvent(MidiDevice.TypeEvent.CC, cc, cc.Channel, sDevice), true);
            }
        }

        public void SendNote(NoteGenerator note, bool bOn, string sDevice)
        {
            if (bOn)
            {
                NoteOnMessage msg = new NoteOnMessage(note.Channel, note.Note, note.Velocity);
                var dev = DevicesOUT.FirstOrDefault(d => d.Name.Equals(sDevice));
                if (dev != null)
                {
                    CreateOUTEvent(new MidiDevice.MidiEvent(MidiDevice.TypeEvent.NOTE_ON, msg, msg.Channel, sDevice), true);
                }
            }
            else
            {
                NoteOffMessage msg = new NoteOffMessage(note.Channel, note.Note, note.Velocity);
                var dev = DevicesOUT.FirstOrDefault(d => d.Name.Equals(sDevice));
                if (dev != null)
                {
                    CreateOUTEvent(new MidiDevice.MidiEvent(MidiDevice.TypeEvent.NOTE_OFF, msg, msg.Channel, sDevice), true);
                }
            }
        }

        public void StopLog()
        {
            MidiDevice.OnLogAdded -= MidiDevice_OnLogAdded;
        }

        public void StartLog()
        {
            MidiDevice.OnLogAdded += MidiDevice_OnLogAdded;
        }

        #endregion

    }

    public class MidiDevice
    {
        public int CC_Pan = 10;
        public int CC_Volume = 7;
        public int CC_Reverb = 91;
        public int CC_Chorus = 93;
        public int CC_Release = 72;
        public int CC_Attack = 73;
        public int CC_Decay = 75;
        public int CC_Brightness = 74;
        public enum TypeEvent
        {
            NOTE_ON = 0,
            NOTE_OFF = 1,
            CC = 2,
            SYSEX = 3,
            PB = 4,
            NRPN = 5,
            PC = 6,
            CH_PRES = 7,
            POLY_PRES = 8
        }

        public class MidiEvent
        {
            internal DateTime EventDate { get; set; }
            internal MidiDevice.TypeEvent Type { get; set; }
            internal Channel MidiChannel { get; set; }
            internal object Event { get; set; }
            internal string Device { get; set; }

            internal MidiEvent(MidiDevice.TypeEvent evType, object ev, Channel ch, string device)
            {
                Event = ev;
                EventDate = DateTime.Now;
                Type = evType;
                MidiChannel = ch;
                Device = device;
            }
        }

        public string Name { get; internal set; }

        private int MIDI_InOrOut; //IN = 1, OUT = 2

        private MidiInputDeviceEvents MIDI_InputEvents;
        private MidiOutputDeviceEvents MIDI_OutputEvents;

        internal delegate void LogEventHandler(string sDevice, bool bIn, string sLog);
        internal static event LogEventHandler OnLogAdded;

        internal delegate void MidiEventHandler(bool bIn, MidiDevice.MidiEvent ev);
        internal event MidiEventHandler OnMidiEvent;

        internal MidiDevice(RtMidi.Core.Devices.Infos.IMidiInputDeviceInfo inputDevice)
        {
            MIDI_InOrOut = 1;
            Name = inputDevice.Name;
            //OpenDevice(inputDevice.Name);
        }

        internal MidiDevice(RtMidi.Core.Devices.Infos.IMidiOutputDeviceInfo outputDevice)
        {
            MIDI_InOrOut = 2;
            Name = outputDevice.Name;
            //OpenDevice(outputDevice.Name);
        }

        private void MIDI_InputEvents_OnMidiEvent(MidiEvent ev)
        {
            //renvoyer l'évènement plus haut
            OnMidiEvent?.Invoke(true, ev);
        }

        private void MIDI_OutputEvents_OnMidiEvent(MidiEvent ev)
        {
            //renvoyer l'évènement plus haut
            OnMidiEvent?.Invoke(false, ev);
        }

        internal bool OpenDevice()
        {
            if (MIDI_InOrOut == 1) //IN
            {
                try
                {
                    if (MIDI_InputEvents == null)
                    {
                        MIDI_InputEvents = new MidiInputDeviceEvents(Name);
                        MIDI_InputEvents.OnMidiEvent += MIDI_InputEvents_OnMidiEvent;
                    }
                    else
                    {
                        MIDI_InputEvents.Open();
                    }
                    return true;
                }
                catch { return false; }
            }
            else //OUT
            {
                try
                {
                    if (MIDI_OutputEvents == null)
                    {
                        MIDI_OutputEvents = new MidiOutputDeviceEvents(Name);
                        MIDI_OutputEvents.OnMidiEvent += MIDI_OutputEvents_OnMidiEvent;
                    }
                    else
                    {
                        MIDI_OutputEvents.Open();
                    }
                    return true;
                }
                catch { return false; }
            }
        }

        internal bool CloseDevice()
        {
            if (MIDI_InOrOut == 1) //IN
            {
                try
                {
                    MIDI_InputEvents.Stop();
                    MIDI_InputEvents.OnMidiEvent -= MIDI_InputEvents_OnMidiEvent;
                    MIDI_InputEvents = null;
                    return true;
                }
                catch
                {
                    MIDI_InputEvents = null;
                    return false;
                }
            }
            else //OUT
            {
                try
                {
                    MIDI_OutputEvents.Stop();
                    MIDI_OutputEvents.OnMidiEvent -= MIDI_OutputEvents_OnMidiEvent;
                    MIDI_OutputEvents = null;
                    return true;
                }
                catch
                {
                    MIDI_OutputEvents = null;
                    return false;
                }
            }
        }

        internal static Channel GetChannel(int iC)
        {
            switch (iC)
            {
                case 1:
                    return RtMidi.Core.Enums.Channel.Channel1;
                case 2:
                    return RtMidi.Core.Enums.Channel.Channel2;
                case 3:
                    return RtMidi.Core.Enums.Channel.Channel3;
                case 4:
                    return RtMidi.Core.Enums.Channel.Channel4;
                case 5:
                    return RtMidi.Core.Enums.Channel.Channel5;
                case 6:
                    return RtMidi.Core.Enums.Channel.Channel6;
                case 7:
                    return RtMidi.Core.Enums.Channel.Channel7;
                case 8:
                    return RtMidi.Core.Enums.Channel.Channel8;
                case 9:
                    return RtMidi.Core.Enums.Channel.Channel9;
                case 10:
                    return RtMidi.Core.Enums.Channel.Channel10;
                case 11:
                    return RtMidi.Core.Enums.Channel.Channel11;
                case 12:
                    return RtMidi.Core.Enums.Channel.Channel12;
                case 13:
                    return RtMidi.Core.Enums.Channel.Channel13;
                case 14:
                    return RtMidi.Core.Enums.Channel.Channel14;
                case 15:
                    return RtMidi.Core.Enums.Channel.Channel15;
                case 16:
                    return RtMidi.Core.Enums.Channel.Channel16;
                default:
                    return RtMidi.Core.Enums.Channel.Channel1;
            }
        }

        internal static Key GetNote(int iNote)
        {
            Enum.TryParse("Key" + iNote, out RtMidi.Core.Enums.Key nt);
            return nt;
        }

        internal static void AddLog(string sDevice, bool bIn, Channel iChannel, string sType, string sValue, string sExtra, string sExtraValue)
        {
            if (OnLogAdded != null)
            {
                string sCh = iChannel.ToString();

                sCh = Regex.Replace(sCh, @"([A-z]+)(\d+)", "$1 $2");
                sValue = Regex.Replace(sValue, @"([A-z]+)(\d+)", "$1 $2");
                sExtraValue = Regex.Replace(sExtraValue, @"([A-z]+)(\d+)", "$1 $2");

                string sMessage = string.Concat(bIn ? "[IN]".PadRight(6, ' ') : "[OUT]".PadRight(6, ' '), sDevice, " - ", sCh.PadRight(10, ' '), " : ", sType, (sValue.Length > 0 ? (" = " + sValue) : ""));
                if (sExtra.Length > 0)
                {
                    sMessage = string.Concat(sMessage, " / ", sExtra, (sExtraValue.Length > 0 ? (" = " + sExtraValue) : ""));
                }

                OnLogAdded?.Invoke(sDevice, bIn, sMessage);
            }
        }

        internal void SendMidiEvent(MidiEvent midiEvent)
        {
            if (MIDI_OutputEvents != null)
            {
                MIDI_OutputEvents.SendEvent(midiEvent);
            }
            else
            {

            }
        }

    }

    internal class MidiOutputDeviceEvents
    {
        internal delegate void MidiEventHandler(MidiDevice.MidiEvent ev);
        internal event MidiEventHandler OnMidiEvent;

        private IMidiOutputDevice outputDevice;

        internal MidiOutputDeviceEvents(string sDevice)
        {
            SetMidiOUT(sDevice);
        }

        internal void Open()
        {
            SetMidiOUT(outputDevice.Name);
        }

        private void SetMidiOUT(string sMidiOut)
        {
            var device = MidiDeviceManager.Default.OutputDevices.FirstOrDefault(p => p.Name.Equals(sMidiOut));

            try
            {
                if (outputDevice == null)
                {
                    outputDevice = device.CreateDevice();
                    outputDevice.Open();
                    MidiDevice.AddLog(outputDevice.Name, false, Channel.Channel1, "[OPEN]", "", "", "");
                }
                else if (outputDevice != null && !outputDevice.IsOpen)
                {
                    outputDevice.Open();
                    MidiDevice.AddLog(outputDevice.Name, false, Channel.Channel1, "[OPEN]", "", "", "");
                }
            }
            catch (Exception ex)
            {
                MidiDevice.AddLog(outputDevice.Name, false, Channel.Channel1, "Unable to open MIDI OUT port : ", ex.Message, "", "");

                if (outputDevice.IsOpen)
                {
                    outputDevice.Close();
                    MidiDevice.AddLog(outputDevice.Name, false, Channel.Channel1, "[CLOSE]", "", "", "");
                }
                outputDevice.Dispose();

                throw ex;
            }
        }

        internal void SendEvent(MidiDevice.MidiEvent ev)
        {
            switch (ev.Type)
            {
                case MidiDevice.TypeEvent.NOTE_ON:
                    var midi1 = (NoteOnMessage)ev.Event;

                    if (outputDevice != null && outputDevice.IsOpen)
                    {
                        NoteOnMessage msg = new NoteOnMessage(midi1.Channel, midi1.Key, midi1.Velocity);
                        outputDevice.Send(msg);
                    }
                    MidiDevice.AddLog(outputDevice.Name, false, midi1.Channel, "Note On", midi1.Key.ToString(), "Velocity", midi1.Velocity.ToString());
                    break;
                case MidiDevice.TypeEvent.NOTE_OFF:
                    var midi2 = (NoteOffMessage)ev.Event;

                    if (outputDevice != null && outputDevice.IsOpen)
                    {
                        NoteOffMessage msg = new NoteOffMessage(midi2.Channel, midi2.Key, midi2.Velocity);
                        outputDevice.Send(msg);
                    }
                    MidiDevice.AddLog(outputDevice.Name, false, midi2.Channel, "Note Off", midi2.Key.ToString(), "Velocity", midi2.Velocity.ToString());
                    break;
                case MidiDevice.TypeEvent.NRPN:
                    var midi3 = (NrpnMessage)ev.Event;

                    if (outputDevice != null && outputDevice.IsOpen)
                    {
                        NrpnMessage msg = new NrpnMessage(midi3.Channel, midi3.Parameter, midi3.Value);
                        outputDevice.Send(msg);
                    }
                    MidiDevice.AddLog(outputDevice.Name, false, midi3.Channel, "Nrpn", midi3.Parameter.ToString(), "Value", midi3.Value.ToString());
                    break;
                case MidiDevice.TypeEvent.PB:
                    var midi4 = (PitchBendMessage)ev.Event;

                    if (outputDevice != null && outputDevice.IsOpen)
                    {
                        PitchBendMessage msg = new PitchBendMessage(midi4.Channel, midi4.Value);
                        outputDevice.Send(msg);
                    }
                    MidiDevice.AddLog(outputDevice.Name, false, midi4.Channel, "Pitch Bend", midi4.Value.ToString(), "", "");
                    break;
                case MidiDevice.TypeEvent.PC:
                    var midi5 = (ProgramChangeMessage)ev.Event;

                    if (outputDevice != null && outputDevice.IsOpen)
                    {
                        ProgramChangeMessage msg = new ProgramChangeMessage(midi5.Channel, midi5.Program);
                        outputDevice.Send(msg);
                    }
                    MidiDevice.AddLog(outputDevice.Name, false, midi5.Channel, "Program Change", midi5.Program.ToString(), "", "");
                    break;
                case MidiDevice.TypeEvent.SYSEX:
                    var midi6 = (SysExMessage)ev.Event;

                    if (outputDevice != null && outputDevice.IsOpen)
                    {
                        SysExMessage msg = new SysExMessage(midi6.Data);
                        outputDevice.Send(msg);
                    }
                    MidiDevice.AddLog(outputDevice.Name, false, 0, "SysEx", System.Text.Encoding.UTF8.GetString(midi6.Data), "", "");
                    break;
                case MidiDevice.TypeEvent.CC:
                    var midi7 = (ControlChangeMessage)ev.Event;

                    if (outputDevice != null && outputDevice.IsOpen)
                    {
                        ControlChangeMessage msg = new ControlChangeMessage(midi7.Channel, midi7.Control, midi7.Value);
                        outputDevice.Send(msg);
                    }
                    MidiDevice.AddLog(outputDevice.Name, false, midi7.Channel, "Control Change", midi7.Control.ToString(), "Value", midi7.Value.ToString());
                    break;
                case MidiDevice.TypeEvent.CH_PRES:
                    var midi8 = (ChannelPressureMessage)ev.Event;

                    if (outputDevice != null && outputDevice.IsOpen)
                    {
                        ChannelPressureMessage msg = new ChannelPressureMessage(midi8.Channel, midi8.Pressure);
                        outputDevice.Send(msg);
                    }
                    MidiDevice.AddLog(outputDevice.Name, false, midi8.Channel, "Channel Pressure", midi8.Pressure.ToString(), "", "");
                    break;
                case MidiDevice.TypeEvent.POLY_PRES:
                    var midi9 = (RtMidi.Core.Messages.PolyphonicKeyPressureMessage)ev.Event;

                    if (outputDevice != null && outputDevice.IsOpen)
                    {
                        PolyphonicKeyPressureMessage msg = new PolyphonicKeyPressureMessage(midi9.Channel, midi9.Key, midi9.Pressure);
                        outputDevice.Send(msg);
                    }
                    MidiDevice.AddLog(outputDevice.Name, false, midi9.Channel, "Poly. Channel Pressure", midi9.Pressure.ToString(), "Key", midi9.Key.ToString());
                    break;
            }
            OnMidiEvent?.Invoke(ev);
        }

        internal void Stop()
        {
            if (outputDevice != null)
            {
                try
                {
                    if (outputDevice.IsOpen)
                    {
                        outputDevice.Close();
                        outputDevice.Dispose();
                        MidiDevice.AddLog(outputDevice.Name, false, Channel.Channel1, "[CLOSE]", "", "", "");
                    }

                    outputDevice = null;
                }
                catch (Exception ex)
                {
                    MidiDevice.AddLog(outputDevice.Name, false, Channel.Channel1, "Unable to close MIDI OUT port : ", ex.Message, "", "");
                    throw ex;
                }
            }
        }
    }

    internal class MidiInputDeviceEvents
    {
        internal delegate void MidiEventHandler(MidiDevice.MidiEvent ev);
        internal event MidiEventHandler OnMidiEvent;

        private IMidiInputDevice inputDevice;

        internal MidiInputDeviceEvents(string sDevice)
        {
            SetMidiIN(sDevice);
        }

        internal void Open()
        {
            SetMidiIN(inputDevice.Name);
        }

        private void SetMidiIN(string sMidiIn)
        {
            var device = MidiDeviceManager.Default.InputDevices.FirstOrDefault(p => p.Name.Equals(sMidiIn));

            try
            {
                if (inputDevice == null)
                {
                    inputDevice = device.CreateDevice();
                    inputDevice.ControlChange += ControlChangeHandler;
                    inputDevice.NoteOn += NoteOnChangeHandler;
                    inputDevice.NoteOff += NoteOffChangeHandler;
                    inputDevice.PitchBend += PitchBendChangeHandler;
                    inputDevice.SysEx += SysexChangeHandler;
                    inputDevice.ProgramChange += ProgramChangeChangeHandler;
                    inputDevice.Nrpn += NrPnChangeHandler;
                    inputDevice.ChannelPressure += ChannelPressureHandler;
                    inputDevice.PolyphonicKeyPressure += PolyphonicPressure;
                    inputDevice.Open();
                    MidiDevice.AddLog(inputDevice.Name, true, Channel.Channel1, "[OPEN]", "", "", "");
                }
                else if (inputDevice != null && !inputDevice.IsOpen)
                {
                    inputDevice.ControlChange += ControlChangeHandler;
                    inputDevice.NoteOn += NoteOnChangeHandler;
                    inputDevice.NoteOff += NoteOffChangeHandler;
                    inputDevice.PitchBend += PitchBendChangeHandler;
                    inputDevice.SysEx += SysexChangeHandler;
                    inputDevice.ProgramChange += ProgramChangeChangeHandler;
                    inputDevice.Nrpn += NrPnChangeHandler;
                    inputDevice.ChannelPressure += ChannelPressureHandler;
                    inputDevice.PolyphonicKeyPressure += PolyphonicPressure;
                    inputDevice.Open();
                    MidiDevice.AddLog(inputDevice.Name, true, Channel.Channel1, "[OPEN]", "", "", "");
                }
            }
            catch (Exception ex)
            {
                MidiDevice.AddLog(inputDevice.Name, true, Channel.Channel1, "Unable to open MIDI IN port : ", ex.Message, "", "");
                inputDevice.ControlChange -= ControlChangeHandler;
                inputDevice.NoteOn -= NoteOnChangeHandler;
                inputDevice.NoteOff -= NoteOffChangeHandler;
                inputDevice.PitchBend -= PitchBendChangeHandler;
                inputDevice.SysEx -= SysexChangeHandler;
                inputDevice.ProgramChange -= ProgramChangeChangeHandler;
                inputDevice.Nrpn -= NrPnChangeHandler;
                inputDevice.ChannelPressure -= ChannelPressureHandler;
                inputDevice.PolyphonicKeyPressure -= PolyphonicPressure;
                if (inputDevice.IsOpen)
                {
                    inputDevice.Close();
                    MidiDevice.AddLog(inputDevice.Name, true, Channel.Channel1, "[CLOSE]", "", "", "");
                }
                inputDevice.Dispose();

                throw ex;
            }
        }

        private void NrPnChangeHandler(IMidiInputDevice sender, in NrpnMessage msg)
        {
            MidiDevice.AddLog(inputDevice.Name, true, msg.Channel, "Nrpn", msg.Parameter.ToString(), "Value", msg.Value.ToString());

            AddEvent(MidiDevice.TypeEvent.NRPN, msg, msg.Channel);
        }

        private void ProgramChangeChangeHandler(IMidiInputDevice sender, in ProgramChangeMessage msg)
        {
            MidiDevice.AddLog(inputDevice.Name, true, msg.Channel, "Program Change", msg.Program.ToString(), "", "");

            AddEvent(MidiDevice.TypeEvent.PC, msg, msg.Channel);
        }

        private void SysexChangeHandler(IMidiInputDevice sender, in SysExMessage msg)
        {
            MidiDevice.AddLog(inputDevice.Name, true, 0, "SysEx", System.Text.Encoding.UTF8.GetString(msg.Data), "", "");

            AddEvent(MidiDevice.TypeEvent.SYSEX, msg, Channel.Channel1);
        }

        private void PitchBendChangeHandler(IMidiInputDevice sender, in PitchBendMessage msg)
        {
            MidiDevice.AddLog(inputDevice.Name, true, msg.Channel, "Pitch Bend", msg.Value.ToString(), "", "");

            AddEvent(MidiDevice.TypeEvent.PB, msg, msg.Channel);
        }

        private void NoteOffChangeHandler(IMidiInputDevice sender, in NoteOffMessage msg)
        {
            MidiDevice.AddLog(inputDevice.Name, true, msg.Channel, "Note Off", msg.Key.ToString(), "Velocity", msg.Velocity.ToString());

            AddEvent(MidiDevice.TypeEvent.NOTE_OFF, msg, msg.Channel);
        }

        private void NoteOnChangeHandler(IMidiInputDevice sender, in NoteOnMessage msg)
        {
            MidiDevice.AddLog(inputDevice.Name, true, msg.Channel, "Note On", msg.Key.ToString(), "Velocity", msg.Velocity.ToString());

            AddEvent(MidiDevice.TypeEvent.NOTE_ON, msg, msg.Channel);
        }

        private void ControlChangeHandler(IMidiInputDevice sender, in ControlChangeMessage msg)
        {
            MidiDevice.AddLog(inputDevice.Name, true, msg.Channel, "Control Change", msg.Control.ToString(), "Value", msg.Value.ToString());

            AddEvent(MidiDevice.TypeEvent.CC, msg, msg.Channel);
        }

        private void PolyphonicPressure(IMidiInputDevice sender, in PolyphonicKeyPressureMessage msg)
        {
            MidiDevice.AddLog(inputDevice.Name, true, msg.Channel, "Poly. Channel Pressure", msg.Pressure.ToString(), "Key", msg.Key.ToString());

            AddEvent(MidiDevice.TypeEvent.POLY_PRES, msg, msg.Channel);
        }

        private void ChannelPressureHandler(IMidiInputDevice sender, in ChannelPressureMessage msg)
        {
            MidiDevice.AddLog(inputDevice.Name, true, msg.Channel, "Channel Pressure", msg.Pressure.ToString(), "", "");

            AddEvent(MidiDevice.TypeEvent.CH_PRES, msg, msg.Channel);
        }

        private void AddEvent(MidiDevice.TypeEvent evType, object ev, Channel ch)
        {
            OnMidiEvent?.Invoke(new MidiDevice.MidiEvent(evType, ev, ch, inputDevice.Name));
        }

        internal void Stop()
        {
            if (inputDevice != null)
            {
                try
                {
                    if (inputDevice.IsOpen)
                    {
                        inputDevice.Close();
                        inputDevice.Dispose();
                        MidiDevice.AddLog(inputDevice.Name, true, Channel.Channel1, "[CLOSE]", "", "", "");
                    }

                    inputDevice = null;
                }
                catch (Exception ex)
                {
                    MidiDevice.AddLog(inputDevice.Name, true, Channel.Channel1, "Unable to close MIDI IN port : ", ex.Message, "", "");
                    throw ex;
                }
            }
        }
    }

    internal class MidiDeviceContent
    {
        public InstrumentData InstrumentContent;

        internal MidiDeviceContent(string sFile)
        {
            InstrumentContent = LoadInstrumentData(sFile);
        }

        private InstrumentData LoadInstrumentData(string sFile)
        {
            if (File.Exists(sFile))
            {
                string[] sData = File.ReadAllLines(sFile);
                string sDevice = SearchDevice(sData);
                if (sDevice.Length > 0)
                {
                    var groups = GetCategories(sData);
                    for (int iG = 0; iG < groups.Count; iG++)
                    {
                        groups[iG].Presets = GetPresets(groups[iG], sData);
                    }
                    return new InstrumentData(groups, sDevice, sFile, false);
                }
                else
                {
                    return null;
                }
            }
            else { return null; }
        }

        internal static List<PresetHierarchy> GetCategories(string[] sData)
        {
            List<PresetHierarchy> list = new List<PresetHierarchy>();
            int i = 0;
            foreach (string s in sData)
            {
                if (Regex.IsMatch(s, @"^\[g\d\]", RegexOptions.IgnoreCase))
                {
                    list.Add(new PresetHierarchy(i, s, s.Substring(4).Trim(), Convert.ToInt32(s.Substring(2, 1))));
                }
                i++;
            }

            return list;
        }

        internal static List<MidiPreset> GetPresets(PresetHierarchy group, string[] sData)
        {
            List<MidiPreset> sFileData = new List<MidiPreset>();

            for (int i = group.IndexInFile + 1; i < sData.Length; i++)
            {
                if (Regex.IsMatch(sData[i], @"^\[g\d\]", RegexOptions.IgnoreCase))
                {
                    break;
                }
                else if (Regex.IsMatch(sData[i], @"^\[p\d\s*,", RegexOptions.IgnoreCase))
                {
                    string[] sPG = sData[i].Split(',');
                    int iPrg = Convert.ToInt32(sPG[1].Trim());
                    int iMsb = Convert.ToInt32(sPG[2].Trim());
                    int iLsb = Convert.ToInt32(sPG[3].Substring(0, sPG[3].IndexOf("]")).Trim());
                    string sPName = sData[i].Substring(sData[i].IndexOf("]") + 1).Trim();
                    sFileData.Add(new MidiPreset(group.Category, 1, iPrg, iMsb, iLsb, sPName));
                }
            }
            return sFileData;
        }

        internal static string SearchDevice(string[] sData)
        {
            string sMan = "";
            string sDev = "";
            foreach (var data in sData)
            {
                if (data.StartsWith("[device manufacturer]", StringComparison.OrdinalIgnoreCase))
                {
                    sMan = data.Substring(21).Trim();
                }
                else if (data.StartsWith("[device name]", StringComparison.OrdinalIgnoreCase))
                {
                    sDev = data.Substring(13).Trim();
                }

                if (sMan.Length > 0 && sDev.Length > 0)
                {
                    return string.Concat(sMan, " ", sDev.Replace(sMan, "").Trim());
                }
            }
            return "";
        }
    }

    [Serializable]
    public class MidiPreset
    {
        public readonly string InstrumentGroup = "";
        public readonly int Prg = 0;
        public readonly int Msb = 0;
        public readonly int Lsb = 0;
        public readonly string PresetName = "";
        public int Channel = 1;

        public string Id { get { return string.Concat(Prg, "-", Msb, "-", Lsb); } }

        public string TechName { get { return string.Concat("PRG : ", Prg, " / MSB : ", Msb, " / LSB : ", Lsb); } }

        public MidiPreset(string sSection, int iChannel, int iPrg, int iMsb, int iLsb, string sPName)
        {
            this.InstrumentGroup = sSection;
            this.Prg = iPrg;
            this.Msb = iMsb;
            this.Lsb = iLsb;
            this.PresetName = sPName;
            this.Channel = iChannel;
        }
        public MidiPreset()
        {

        }
    }

    [Serializable]
    public class PresetHierarchy
    {
        public string Category = "";
        public readonly int Level = 0;
        public readonly int IndexInFile = 0;
        public readonly string Raw = "";
        public List<MidiPreset> Presets { get; set; } = new List<MidiPreset>();

        internal PresetHierarchy(int iIndex, string sRaw, string sCategory, int iLevel)
        {
            this.Category = sCategory;
            this.Level = iLevel;
            this.Raw = sRaw;
            this.IndexInFile = iIndex;
        }

        internal PresetHierarchy()
        {

        }
    }

    [Serializable]
    public class InstrumentData
    {
        public List<PresetHierarchy> Categories { get; } = new List<PresetHierarchy>();
        public string Device { get; set; } = "";
        public string CubaseFile { get; } = "";
        public bool SortedByBank = false;

        public InstrumentData()
        {
            Categories = new List<PresetHierarchy>();
        }

        internal InstrumentData(List<PresetHierarchy> sCats, string sDevice, string sCubaseFile, bool bSortedByBank)
        {
            Categories = sCats;
            Device = sDevice;
            CubaseFile = sCubaseFile;
            SortedByBank = bSortedByBank;
        }

        public InstrumentData(string sFile)
        {
            if (File.Exists(sFile))
            {
                string[] sData = File.ReadAllLines(sFile);
                string sDevice = MidiDeviceContent.SearchDevice(sData);
                if (sDevice.Length > 0)
                {
                    CubaseFile = sFile;

                    var groups = MidiDeviceContent.GetCategories(sData);
                    for (int iG = 0; iG < groups.Count; iG++)
                    {
                        groups[iG].Presets = MidiDeviceContent.GetPresets(groups[iG], sData);
                    }
                    Categories = groups;
                    Device = sDevice;
                }
            }
        }

        public MidiPreset GetPreset(string idx)
        {
            foreach (var c in Categories)
            {
                foreach (var p in c.Presets)
                {
                    if (p.Id.Equals(idx))
                    { return p; }
                }
            }
            return null;
        }

        public InstrumentData Sort(bool bByBank)
        {
            bool bTmpSort = SortedByBank;
            SortedByBank = bByBank;
            if (bByBank)
            {
                if (!bTmpSort)
                {
                    List<MidiPreset> instrP = new List<MidiPreset>();
                    List<PresetHierarchy> instrH = new List<PresetHierarchy>();
                    foreach (var cat in Categories)
                    {
                        instrP.AddRange(cat.Presets);
                    }

                    int iMsb = instrP.Select(p => p.Msb).Distinct().Count();
                    int iLsb = instrP.Select(p => p.Lsb).Distinct().Count();

                    bool bMsb = iMsb < iLsb ? true : false;

                    if (bMsb)
                    {
                        instrP = instrP.OrderBy(p => p.Msb).ThenBy(p => p.Lsb).ThenBy(p => p.Prg).ToList();
                    }
                    else
                    {
                        instrP = instrP.OrderBy(p => p.Lsb).ThenBy(p => p.Msb).ThenBy(p => p.Prg).ToList();
                    }

                    for (int iC = 0; iC < instrP.Count; iC++)
                    {
                        string sCat = bMsb ? instrP[iC].Msb.ToString("000") : instrP[iC].Lsb.ToString("000");
                        if (instrH.Count(i => i.Category.Equals(sCat)) == 0)
                        {
                            instrH.Add(new PresetHierarchy(iC, sCat, sCat, 1));
                        }
                    }
                    for (int iC = 0; iC < instrP.Count; iC++)
                    {
                        string sCat = bMsb ? string.Concat(instrP[iC].Msb.ToString("000"), "-", instrP[iC].Lsb.ToString("000")) : string.Concat(instrP[iC].Lsb.ToString("000"), "-", instrP[iC].Msb.ToString("000"));
                        if (instrH.Count(i => i.Category.Equals(sCat) && i.Level == 2) == 0)
                        {
                            instrH.Add(new PresetHierarchy(iC, sCat, sCat, 2));
                        }
                    }

                    foreach (var cat in instrH.Where(i => i.Level == 2))
                    {
                        if (bMsb)
                        {
                            var list = instrP.Where(p => string.Concat(p.Msb.ToString("000"), "-", p.Lsb.ToString("000")).Equals(cat.Category)).ToList();
                            cat.Presets.AddRange(list);
                        }
                        else
                        {
                            var list = instrP.Where(p => string.Concat(p.Lsb.ToString("000"), "-", p.Msb.ToString("000")).Equals(cat.Category)).ToList();
                            cat.Presets.AddRange(list);
                        }
                    }

                    instrH = instrH.OrderBy(c => c.Category).ToList();

                    foreach (var cat in instrH)
                    {
                        if (cat.Level == 1)
                        {
                            cat.Category = string.Concat((bMsb ? "MSB : " : "LSB : ") + Convert.ToInt32(cat.Category.Split('-')[0]).ToString("000"));
                        }
                        else if (cat.Level == 2)
                        {
                            cat.Category = string.Concat((bMsb ? "LSB : " : "MSB : ") + Convert.ToInt32(cat.Category.Split('-')[1]).ToString("000"));
                        }
                    }

                    return new InstrumentData(instrH, Device, CubaseFile, true);
                }
                else { return this; }
            }
            else
            {
                if (bTmpSort)
                {
                    return new InstrumentData(CubaseFile);
                }
                else
                {
                    return this;
                }
            }
        }

        public void ChangeDevice(string sNewName)
        {
            if (sNewName.Length > 0)
            {
                Device = sNewName;
            }
        }
    }

    public class NoteGenerator
    {
        public int Velocity { get; private set; } = 64;
        public RtMidi.Core.Enums.Key Note { get; private set; } = RtMidi.Core.Enums.Key.Key64;
        public int Octave { get; private set; } = 3;
        public RtMidi.Core.Enums.Channel Channel { get; private set; } = RtMidi.Core.Enums.Channel.Channel1;

        public NoteGenerator(int iChannel, int iOctave, int iNote, int iVelocity)
        {
            Channel = MidiDevice.GetChannel(iChannel);
            SetOctave(iOctave);
            SetNote(iNote.ToString());
            SetVelocity(iVelocity.ToString());
        }

        public void SetOctave(int iOctave)
        {
            if (iOctave > -1 && iOctave <= 9)
            {
                Octave = iOctave;
            }
        }

        public void ChangeOctave(int iOffset)
        {
            if ((Octave + iOffset) > -1 && (Octave + iOffset) <= 9)
            {
                Octave += iOffset;
            }
        }

        public void SetNote(string sNote)
        {
            int iNote = 0;
            if (int.TryParse(sNote.Trim(), out iNote))
            {
                if (iNote + (12 * Octave) > -1 && iNote + (12 * Octave) <= 127)
                {
                    Enum.TryParse("Key" + (iNote + (12 * Octave)), out RtMidi.Core.Enums.Key nt);
                    Note = nt;
                }
            }
        }

        public void SetVelocity(string sVel)
        {
            int iVel = 0;
            if (int.TryParse(sVel.Trim(), out iVel))
            {
                if (iVel > -1 && iVel <= 127)
                {
                    Velocity = iVel;
                }
            }
        }

        public void SetChannel(string sChannel)
        {
            int iCh = 0;
            if (int.TryParse(sChannel.Trim(), out iCh))
            {
                if (iCh > 0 && iCh <= 16)
                {
                    Channel = MidiDevice.GetChannel(iCh);
                }
            }
        }
    }
}

