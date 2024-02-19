using System;
using System.Collections.Generic;
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
    public class MidiOptions
    {
        public int Pan = 64;
        public int VelocityFilterLow = 0;
        public int VelocityFilterHigh = 127;
        public int NoteFilterLow = 0;
        public int NoteFilterHigh = 127;
        public int CC_ToVolume = -1; //convertisseur de CC pour le volume (ex : CC 102 -> CC 7)
        public int CC_Volume = 100;
        public int CC_Reverb = -1;
        public int CC_Chorus = -1;
        public int CC_Release = -1;
        public int CC_Attack = -1;
        public int CC_Decay = -1;
        public int CC_Cutoff = -1;
        public bool AllowModulation = true;
        public bool AllowNotes = true;
        public bool AllowAllCC = true;
        public bool AllowSysex = true;
        public bool AllowNrpn = true;
        public bool AllowAftertouch = true;
        public bool AllowPitchBend = true;
        public bool AllowProgramChange = true;
        public int TranspositionOffset = 0;
    }

    public class MidiRouting
    {
        private MidiDevice DeviceIN;
        private MidiDevice DeviceOUT;

        public delegate void LogEventHandler(string sDevice, bool bIn, string sLog);
        public static event LogEventHandler NewLog;

        public MidiRouting(string sMidiIN, string sMidiOUT, string sFilePresetsIN = "", string sFilePresetsOUT = "")
        {
            if (sMidiIN != "")
            {
                var deviceIN = MidiDeviceManager.Default.InputDevices.FirstOrDefault(p => p.Name.Equals(sMidiIN));
                DeviceIN = new MidiDevice(sFilePresetsIN, deviceIN);
                DeviceIN.OnMidiEvent += DeviceIN_OnMidiEvent;
            }

            if (sMidiOUT != "")
            {
                var deviceOUT = MidiDeviceManager.Default.OutputDevices.FirstOrDefault(p => p.Name.Equals(sMidiOUT));
                DeviceOUT = new MidiDevice(sFilePresetsOUT, deviceOUT);
            }

            MidiDevice.OnLogAdded += MidiDevice_OnLogAdded;
        }

        private void MidiDevice_OnLogAdded(string sDevice, bool bIn, string sLog)
        {
            NewLog(sDevice, bIn, sLog);
        }

        public string CyclesInfo
        {
            get
            {
                string sMessage = "MIDI Average Processing Messages / Cycle : ";
                if (DeviceIN != null)
                {
                    sMessage = string.Concat(sMessage, " [IN] : " + DeviceIN.AverageEventsCycle.ToString());
                }
                if (DeviceOUT != null)
                {
                    sMessage = string.Concat(sMessage, " / [OUT] : " + DeviceOUT.AverageEventsCycle.ToString());
                }
                return sMessage;
            }
        }

        public void Terminate()
        {
            if (DeviceIN != null)
            {
                DeviceIN.CloseDevice();
            }
            if (DeviceOUT != null)
            {
                DeviceOUT.CloseDevice();
            }
        }

        public bool AddOrModifyRouting(int iAction, int iChIn, int iChOut, MidiOptions options)
        {
            if (DeviceOUT != null)
            {
                //iAction : 0 = delete, 1 = add
                if (iChIn > 0 && iChIn <= 16 && iChOut > 0 && iChOut <= 16)
                {
                    DeviceOUT.ChangeRoutingMatrix(iAction, iChIn, iChOut, options);
                    return true;
                }
                else { return false; }
            }
            else { return false; }
        }

        public void DeleteAllRouting()
        {
            if (DeviceOUT != null)
            {
                DeviceOUT.DeleteRouting();
            }
        }

        public void SetMidiOUTPreset(MidiPreset preset, int iChannel)
        {
            if (DeviceOUT != null)
            {
                DeviceOUT.ChangeOUTPreset(preset, iChannel);
            }
        }

        private void DeviceIN_OnMidiEvent(MidiDevice.MidiEvent ev)
        {
            if (DeviceOUT != null)
            {
                DeviceOUT.MIDI_OutputEvents_OnMidiEvent(ev);
            }
        }

        public void GenerateNoteEvent(NoteGenerator note, bool bOn)
        {
            if (DeviceOUT != null)
            {
                DeviceOUT.GenerateNoteEvent(note, bOn);
            }
        }
    }

    public class MidiDevice
    {
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
            internal MidiEvent(MidiDevice.TypeEvent evType, object ev, Channel ch)
            {
                Event = ev;
                EventDate = DateTime.Now;
                Type = evType;
                MidiChannel = ch;
            }
        }

        internal class MatrixItem
        {
            internal Channel In = Channel.Channel1;
            internal Channel Out = Channel.Channel1;
            internal MidiOptions Options = new MidiOptions();

            public MatrixItem(Channel chIN, Channel chOUT, MidiOptions options)
            {
                In = chIN;
                Out = chOUT;
                Options = options;
            }
        }

        public decimal AverageEventsCycle { get { return MIDI_InOrOut == 1 ? MIDI_InputEvents.ProcessingAverage : MIDI_OutputEvents.ProcessingAverage; } }

        int MIDI_InOrOut; //IN = 1, OUT = 2

        MidiDeviceContent MIDI_DeviceContent;
        IMidiInputDevice MIDI_InputDevice;
        IMidiOutputDevice MIDI_OutputDevice;

        MidiInputDeviceEvents MIDI_InputEvents;
        MidiOutputDeviceEvents MIDI_OutputEvents;

        public readonly string DeviceName;

        public delegate void LogEventHandler(string sDevice, bool bIn, string sLog);
        public static event LogEventHandler OnLogAdded;

        internal delegate void MidiEventHandler(MidiDevice.MidiEvent ev);
        internal event MidiEventHandler OnMidiEvent;

        private static StringBuilder _sblog = new StringBuilder();

        public MidiDevice(string sFile, RtMidi.Core.Devices.Infos.IMidiInputDeviceInfo inputDevice)
        {
            DeviceName = inputDevice.Name;
            MIDI_DeviceContent = new MidiDeviceContent(sFile);
            MIDI_InOrOut = 1;
            MIDI_InputDevice = inputDevice.CreateDevice();
        }

        public MidiDevice(string sFile, RtMidi.Core.Devices.Infos.IMidiOutputDeviceInfo outputDevice)
        {
            MIDI_DeviceContent = new MidiDeviceContent(sFile);
            MIDI_InOrOut = 2;
            MIDI_OutputDevice = outputDevice.CreateDevice();
        }

        public bool OpenDevice()
        {
            if (MIDI_InOrOut == 1) //IN
            {
                try
                {
                    MIDI_InputEvents = new MidiInputDeviceEvents(DeviceName);
                    MIDI_InputEvents.OnMidiEvent += MIDI_InputEvents_OnMidiEvent;
                    return true;
                }
                catch { return false; }
            }
            else //OUT
            {
                try
                {
                    MIDI_OutputEvents = new MidiOutputDeviceEvents(DeviceName);
                    return true;
                }
                catch { return false; }
            }
        }

        private void MIDI_InputEvents_OnMidiEvent(MidiEvent ev)
        {
            //renvoyer l'évènement plus haut
            OnMidiEvent(ev);
        }

        internal void MIDI_OutputEvents_OnMidiEvent(MidiEvent ev)
        {
            MIDI_OutputEvents.ProcessIncomingMessage(ev, false);
        }

        public bool CloseDevice()
        {
            if (MIDI_InOrOut == 1) //IN
            {
                try
                {
                    MIDI_InputEvents.Stop();
                    MIDI_InputEvents.OnMidiEvent -= MIDI_InputEvents_OnMidiEvent;
                    return true;
                }
                catch { return false; }
            }
            else //OUT
            {
                try { MIDI_OutputEvents.Stop(); return true; } catch { return false; }
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
            string sCh = iChannel.ToString();

            sCh = Regex.Replace(sCh, @"([A-z]+)(\d+)", "$1 $2");
            sValue = Regex.Replace(sValue, @"([A-z]+)(\d+)", "$1 $2");
            sExtraValue = Regex.Replace(sExtraValue, @"([A-z]+)(\d+)", "$1 $2");

            string sMessage = string.Concat(sCh, " : ", sType, " = ", sValue);
            if (sExtra.Length > 0)
            {
                sMessage = string.Concat(sMessage, " / ", sExtra, " = ", sExtraValue);
            }
            lock (_sblog)
            {
                _sblog.AppendLine(sMessage);
            }

            OnLogAdded(sDevice, bIn, sMessage);
        }

        internal void ChangeOUTPreset(MidiPreset preset, int iChannel)
        {
            ControlChangeMessage pc00 = new ControlChangeMessage(GetChannel(iChannel), 0, preset.Msb);
            ControlChangeMessage pc32 = new ControlChangeMessage(GetChannel(iChannel), 32, preset.Lsb);
            ProgramChangeMessage prg = new ProgramChangeMessage(GetChannel(iChannel), preset.Prg);

            MIDI_OutputEvents.ProcessIncomingMessage(new MidiEvent(TypeEvent.CC, pc00, pc00.Channel), true);
            MIDI_OutputEvents.ProcessIncomingMessage(new MidiEvent(TypeEvent.CC, pc32, pc32.Channel), true);
            MIDI_OutputEvents.ProcessIncomingMessage(new MidiEvent(TypeEvent.PC, prg, prg.Channel), true);
        }

        internal void GenerateNoteEvent(NoteGenerator note, bool bOn)
        {
            if (bOn)
            {
                NoteOnMessage msg = new NoteOnMessage(note.Channel, note.Note, note.Velocity);
                MIDI_OutputEvents.ProcessIncomingMessage(new MidiEvent(TypeEvent.NOTE_ON, msg, msg.Channel), true);
            }
            else
            {
                NoteOffMessage msg = new NoteOffMessage(note.Channel, note.Note, note.Velocity);
                MIDI_OutputEvents.ProcessIncomingMessage(new MidiEvent(TypeEvent.NOTE_OFF, msg, msg.Channel), true);
            }
        }

        internal void ChangeRoutingMatrix(int iAction, int iChIn, int iChOut, MidiOptions options)
        {
            //iAction : 0 = delete, 1 = add

            if (iAction == 0)
            {
                int iRem = -1;
                for (int i = 0; i < MIDI_OutputEvents.MidiMatrix.Count; i++)
                {
                    if (MIDI_OutputEvents.MidiMatrix[i].In == GetChannel(iChIn) && MIDI_OutputEvents.MidiMatrix[i].Out == GetChannel(iChOut))
                    {
                        iRem = i;
                        break;
                    }
                }
                if (iRem > -1)
                {
                    MIDI_OutputEvents.MidiMatrix.RemoveAt(iRem);
                }
            }
            else
            {
                bool bExists = false;
                for (int i = 0; i < MIDI_OutputEvents.MidiMatrix.Count; i++)
                {
                    if (MIDI_OutputEvents.MidiMatrix[i].In == GetChannel(iChIn) && MIDI_OutputEvents.MidiMatrix[i].Out == GetChannel(iChOut))
                    {
                        bExists = true;
                        break;
                    }
                }
                if (!bExists)
                {
                    MIDI_OutputEvents.MidiMatrix.Add(new MatrixItem(GetChannel(iChIn), GetChannel(iChOut), options));
                }
            }
        }

        internal void DeleteRouting()
        {
            MIDI_OutputEvents.MidiMatrix.Clear();
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
                    sFileData.Add(new MidiPreset(group.Category, iPrg, iMsb, iLsb, sPName));
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

    internal class MidiOutputDeviceEvents
    {
        internal decimal ProcessingAverage { get { return _eventsProcessed / _iCycles; } }

        private System.Timers.Timer Clock;

        private IMidiOutputDevice outputDevice;

        private List<MidiDevice.MidiEvent> _events = new List<MidiDevice.MidiEvent>();
        private Int64 _eventsProcessed = 0;
        private Int64 _iCycles = 1;
        public List<MidiDevice.MatrixItem> MidiMatrix = new List<MidiDevice.MatrixItem>();

        internal bool IsBusy { get { return _events.Count > 0 ? true : false; } }

        internal MidiOutputDeviceEvents(string sDevice)
        {
            SetMidiOUT(sDevice);

            Clock = new System.Timers.Timer();
            Clock.Elapsed += Event_Process;
            Clock.Interval = 10;
            Clock.Start();
        }

        private void SetMidiOUT(string sMidiOut)
        {
            var device = MidiDeviceManager.Default.OutputDevices.FirstOrDefault(p => p.Name.Equals(sMidiOut));

            try
            {
                if (outputDevice != null)
                {
                    if (outputDevice.IsOpen)
                    {
                        outputDevice.Close();
                        MidiDevice.AddLog(outputDevice.Name, false, Channel.Channel1, outputDevice.Name, " [CLOSED]", "", "");
                    }
                    outputDevice.Dispose();
                }

                outputDevice = device.CreateDevice();
                outputDevice.Open();
                MidiDevice.AddLog(outputDevice.Name, false, Channel.Channel1, outputDevice.Name, " [OPENED]", "", "");
            }
            catch (Exception ex)
            {
                MidiDevice.AddLog(outputDevice.Name, false, Channel.Channel1, "Unable to open MIDI OUT port : ", ex.Message, "", "");

                if (outputDevice.IsOpen)
                {
                    outputDevice.Close();
                    MidiDevice.AddLog(outputDevice.Name, false, Channel.Channel1, outputDevice.Name, " [CLOSED]", "", "");
                }
                outputDevice.Dispose();

                throw ex;
            }
        }

        private void SendEvent(MidiDevice.MidiEvent ev)
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
        }

        private void Event_Process(object sender, ElapsedEventArgs e)
        {
            MidiDevice.MidiEvent[] evTmp = null;
            lock (_events)
            {
                evTmp = new MidiDevice.MidiEvent[_events.Count];
                _events.CopyTo(evTmp);
                _events.Clear();
            }

            if (evTmp != null)
            {
                for (int i = 0; i < evTmp.Length; i++)
                {
                    SendEvent(evTmp[i]);
                }

                if (evTmp.Length > 0)
                {
                    _iCycles++;
                    _eventsProcessed += evTmp.Length;
                }
            }
        }

        internal void Stop()
        {
            Clock.Stop();

            if (outputDevice != null && outputDevice.IsOpen)
            {
                try
                {
                    outputDevice.Close();
                    outputDevice.Dispose();
                }
                catch (Exception ex)
                {
                    MidiDevice.AddLog(outputDevice.Name, false, Channel.Channel1, "Unable to close MIDI OUT port : ", ex.Message, "", "");
                    throw ex;
                }
            }
        }

        internal void ProcessIncomingMessage(MidiDevice.MidiEvent ev, bool bForce)
        {
            foreach (MidiDevice.MatrixItem item in MidiMatrix.Where(i => i.In == ev.MidiChannel))
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
                                var msgout = new ControlChangeMessage(item.Out, 7, msg.Value);
                                AddEvent(ev.Type, msgout, msgout.Channel);
                            }
                            else
                            {
                                var msgout = new ControlChangeMessage(item.Out, msg.Control, msg.Value);
                                AddEvent(ev.Type, msgout, msgout.Channel);
                            }
                        }
                        break;
                    case MidiDevice.TypeEvent.CH_PRES:
                        if (item.Options.AllowAftertouch || bForce)
                        {
                            var msg = (ChannelPressureMessage)ev.Event;
                            var msgout = new ChannelPressureMessage(item.Out, msg.Pressure);
                            AddEvent(ev.Type, msgout, msgout.Channel);
                        }
                        break;
                    case MidiDevice.TypeEvent.NOTE_OFF:
                        if (item.Options.AllowNotes || bForce)
                        {
                            var msg = (NoteOffMessage)ev.Event;

                            int iNote = GetNoteIndex(msg.Key, msg.Velocity, item.Options);
                            if (iNote >= -1)
                            {
                                var msgout = new NoteOffMessage(item.Out, MidiDevice.GetNote(iNote), msg.Velocity);
                                AddEvent(ev.Type, msgout, msgout.Channel);
                            }
                        }
                        break;
                    case MidiDevice.TypeEvent.NOTE_ON:
                        if (item.Options.AllowNotes || bForce)
                        {
                            var msg = (NoteOnMessage)ev.Event;

                            int iNote = GetNoteIndex(msg.Key, msg.Velocity, item.Options);
                            if (iNote >= -1)
                            {
                                var msgout = new NoteOnMessage(item.Out, MidiDevice.GetNote(iNote), msg.Velocity);
                                AddEvent(ev.Type, msgout, msgout.Channel);
                            }
                        }
                        break;
                    case MidiDevice.TypeEvent.NRPN:
                        if (item.Options.AllowNrpn || bForce)
                        {
                            var msg = (NrpnMessage)ev.Event;
                            var msgout = new NrpnMessage(item.Out, msg.Parameter, msg.Value);
                            AddEvent(ev.Type, msgout, msgout.Channel);
                        }
                        break;
                    case MidiDevice.TypeEvent.PB:
                        if (item.Options.AllowPitchBend || bForce)
                        {
                            var msg = (PitchBendMessage)ev.Event;
                            var msgout = new PitchBendMessage(item.Out, msg.Value);
                            AddEvent(ev.Type, msgout, msgout.Channel);
                        }
                        break;
                    case MidiDevice.TypeEvent.PC:
                        if (item.Options.AllowProgramChange || bForce)
                        {
                            var msg = (ProgramChangeMessage)ev.Event;
                            var msgout = new ProgramChangeMessage(item.Out, msg.Program);
                            AddEvent(ev.Type, msgout, msgout.Channel);
                        }
                        break;
                    case MidiDevice.TypeEvent.POLY_PRES:
                        if (item.Options.AllowAftertouch || bForce)
                        {
                            var msg = (PolyphonicKeyPressureMessage)ev.Event;
                            var msgout = new PolyphonicKeyPressureMessage(item.Out, msg.Key, msg.Pressure);
                            AddEvent(ev.Type, msgout, msgout.Channel);
                        }
                        break;
                    case MidiDevice.TypeEvent.SYSEX:
                        if (item.Options.AllowSysex || bForce)
                        {
                            var msg = (SysExMessage)ev.Event;
                            var msgout = new SysExMessage(SysExTranscoder(msg.Data));
                            AddEvent(ev.Type, msgout, Channel.Channel1);
                        }
                        break;
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
            if (iKey >= options.NoteFilterLow && iKey <= options.NoteFilterHigh && vel >= options.VelocityFilterLow && vel <= options.VelocityFilterHigh)
            {
                iNote = iKey + options.TranspositionOffset;
            }

            return iNote;
        }

        private void AddEvent(MidiDevice.TypeEvent evType, object ev, Channel ch)
        {
            lock (_events) { _events.Add(new MidiDevice.MidiEvent(evType, ev, ch)); }
        }

        internal int ChannelToInt(Channel ch)
        {
            return Convert.ToInt32(ch.ToString().Substring(7));
        }
    }

    internal class MidiInputDeviceEvents
    {
        internal delegate void MidiEventHandler(MidiDevice.MidiEvent ev);
        internal event MidiEventHandler OnMidiEvent;

        internal decimal ProcessingAverage { get { return _eventsProcessed / _iCycles; } }

        private System.Timers.Timer Clock;

        private IMidiInputDevice inputDevice;

        private List<MidiDevice.MidiEvent> _events = new List<MidiDevice.MidiEvent>();
        private Int64 _eventsProcessed = 0;
        private Int64 _iCycles = 1;

        internal bool IsBusy { get { return _events.Count > 0 ? true : false; } }

        internal MidiInputDeviceEvents(string sDevice)
        {
            SetMidiIN(sDevice);

            Clock = new System.Timers.Timer();
            Clock.Elapsed += Event_Process;
            Clock.Interval = 10;
            Clock.Start();
        }

        private void SetMidiIN(string sMidiIn)
        {
            var device = MidiDeviceManager.Default.InputDevices.FirstOrDefault(p => p.Name.Equals(sMidiIn));

            try
            {
                if (inputDevice != null)
                {
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
                        MidiDevice.AddLog(inputDevice.Name, true, Channel.Channel1, inputDevice.Name, " [CLOSED]", "", "");
                    }
                    inputDevice.Dispose();
                }

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
                MidiDevice.AddLog(inputDevice.Name, true, Channel.Channel1, inputDevice.Name, " [OPENED]", "", "");
                //inputDevice.Close();
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
                    MidiDevice.AddLog(inputDevice.Name, true, Channel.Channel1, inputDevice.Name, " [CLOSED]", "", "");
                }
                inputDevice.Dispose();

                throw ex;
            }
        }

        private void NrPnChangeHandler(IMidiInputDevice sender, in NrpnMessage msg)
        {
            AddEvent(MidiDevice.TypeEvent.NRPN, msg, msg.Channel);
        }

        private void ProgramChangeChangeHandler(IMidiInputDevice sender, in ProgramChangeMessage msg)
        {
            AddEvent(MidiDevice.TypeEvent.PC, msg, msg.Channel);
        }

        private void SysexChangeHandler(IMidiInputDevice sender, in SysExMessage msg)
        {
            AddEvent(MidiDevice.TypeEvent.SYSEX, msg, Channel.Channel1);
        }

        private void PitchBendChangeHandler(IMidiInputDevice sender, in PitchBendMessage msg)
        {
            AddEvent(MidiDevice.TypeEvent.PB, msg, msg.Channel);
        }

        private void NoteOffChangeHandler(IMidiInputDevice sender, in NoteOffMessage msg)
        {
            AddEvent(MidiDevice.TypeEvent.NOTE_OFF, msg, msg.Channel);
        }

        private void NoteOnChangeHandler(IMidiInputDevice sender, in NoteOnMessage msg)
        {
            AddEvent(MidiDevice.TypeEvent.NOTE_ON, msg, msg.Channel);
        }

        private void ControlChangeHandler(IMidiInputDevice sender, in ControlChangeMessage msg)
        {
            AddEvent(MidiDevice.TypeEvent.CC, msg, msg.Channel);
        }

        private void PolyphonicPressure(IMidiInputDevice sender, in PolyphonicKeyPressureMessage msg)
        {
            AddEvent(MidiDevice.TypeEvent.POLY_PRES, msg, msg.Channel);
        }

        private void ChannelPressureHandler(IMidiInputDevice sender, in ChannelPressureMessage msg)
        {
            AddEvent(MidiDevice.TypeEvent.CH_PRES, msg, msg.Channel);
        }

        private void Event_Process(object sender, ElapsedEventArgs e)
        {
            MidiDevice.MidiEvent[] evTmp = null;
            lock (_events)
            {
                evTmp = new MidiDevice.MidiEvent[_events.Count];
                _events.CopyTo(evTmp);
                _events.Clear();
            }

            if (evTmp != null)
            {
                for (int i = 0; i < evTmp.Length; i++)
                {
                    GetEvent(evTmp[i]);
                }

                if (evTmp.Length > 0)
                {
                    _iCycles++;
                    _eventsProcessed += evTmp.Length;
                }
            }
        }

        private void AddEvent(MidiDevice.TypeEvent evType, object ev, Channel ch)
        {
            lock (_events)
            {
                _events.Add(new MidiDevice.MidiEvent(evType, ev, ch));
            }
        }

        private void GetEvent(MidiDevice.MidiEvent ev)
        {
            OnMidiEvent(ev);

            switch (ev.Type)
            {
                case MidiDevice.TypeEvent.NOTE_ON:
                    var midi1 = (NoteOnMessage)ev.Event;
                    MidiDevice.AddLog(inputDevice.Name, true, midi1.Channel, "Note On", midi1.Key.ToString(), "Velocity", midi1.Velocity.ToString());
                    break;
                case MidiDevice.TypeEvent.NOTE_OFF:
                    var midi2 = (NoteOffMessage)ev.Event;

                    MidiDevice.AddLog(inputDevice.Name, true, midi2.Channel, "Note Off", midi2.Key.ToString(), "Velocity", midi2.Velocity.ToString());
                    break;
                case MidiDevice.TypeEvent.NRPN:
                    var midi3 = (NrpnMessage)ev.Event;

                    MidiDevice.AddLog(inputDevice.Name, true, midi3.Channel, "Nrpn", midi3.Parameter.ToString(), "Value", midi3.Value.ToString());
                    break;
                case MidiDevice.TypeEvent.PB:
                    var midi4 = (PitchBendMessage)ev.Event;

                    MidiDevice.AddLog(inputDevice.Name, true, midi4.Channel, "Pitch Bend", midi4.Value.ToString(), "", "");
                    break;
                case MidiDevice.TypeEvent.PC:
                    var midi5 = (ProgramChangeMessage)ev.Event;

                    MidiDevice.AddLog(inputDevice.Name, true, midi5.Channel, "Program Change", midi5.Program.ToString(), "", "");
                    break;
                case MidiDevice.TypeEvent.SYSEX:
                    var midi6 = (SysExMessage)ev.Event;

                    MidiDevice.AddLog(inputDevice.Name, true, 0, "SysEx", System.Text.Encoding.UTF8.GetString(midi6.Data), "", "");
                    break;
                case MidiDevice.TypeEvent.CC:
                    var midi7 = (ControlChangeMessage)ev.Event;

                    MidiDevice.AddLog(inputDevice.Name, true, midi7.Channel, "Control Change", midi7.Control.ToString(), "Value", midi7.Value.ToString());
                    break;
                case MidiDevice.TypeEvent.CH_PRES:
                    var midi8 = (ChannelPressureMessage)ev.Event;

                    MidiDevice.AddLog(inputDevice.Name, true, midi8.Channel, "Channel Pressure", midi8.Pressure.ToString(), "", "");
                    break;
                case MidiDevice.TypeEvent.POLY_PRES:
                    var midi9 = (RtMidi.Core.Messages.PolyphonicKeyPressureMessage)ev.Event;

                    MidiDevice.AddLog(inputDevice.Name, true, midi9.Channel, "Poly. Channel Pressure", midi9.Pressure.ToString(), "Key", midi9.Key.ToString());
                    break;
            }
        }

        internal void Stop()
        {
            Clock.Stop();

            if (inputDevice != null && inputDevice.IsOpen)
            {
                try
                {
                    inputDevice.Close();
                    inputDevice.Dispose();
                }
                catch (Exception ex)
                {
                    MidiDevice.AddLog(inputDevice.Name, true, Channel.Channel1, "Unable to close MIDI IN port : ", ex.Message, "", "");
                    throw ex;
                }

            }
        }
    }

    public class MidiPreset
    {
        public readonly string InstrumentGroup;
        public readonly int Prg;
        public readonly int Msb;
        public readonly int Lsb;
        public readonly string PresetName;
        public string Id { get { return string.Concat(Prg, "-", Msb, "-", Lsb); } }

        public string TechName { get { return string.Concat("PRG : ", Prg, " / MSB : ", Msb, " / LSB : ", Lsb); } }

        public MidiPreset(string sSection, int iPrg, int iMsb, int iLsb, string sPName)
        {
            this.InstrumentGroup = sSection;
            this.Prg = iPrg;
            this.Msb = iMsb;
            this.Lsb = iLsb;
            this.PresetName = sPName;
        }
    }

    public class PresetHierarchy
    {
        public string Category;
        public readonly int Level;
        public readonly int IndexInFile;
        public readonly string Raw;
        public List<MidiPreset> Presets { get; set; } = new List<MidiPreset>();
        internal PresetHierarchy(int iIndex, string sRaw, string sCategory, int iLevel)
        {
            this.Category = sCategory;
            this.Level = iLevel;
            this.Raw = sRaw;
            this.IndexInFile = iIndex;
        }
    }

    public class InstrumentData
    {
        public List<PresetHierarchy> Categories { get; } = new List<PresetHierarchy>();
        public string Device { get; } = "";
        public string CubaseFile { get; } = "";
        public bool SortedByBank = false;

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

