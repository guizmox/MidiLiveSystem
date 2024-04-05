using MessagePack;
using MicroLibrary;
using RtMidi.Core;
using RtMidi.Core.Devices;
using RtMidi.Core.Enums;
using RtMidi.Core.Messages;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Timers;
using VSTHost;
using static MidiTools.MidiDevice;

namespace MidiTools
{
    [MessagePackObject]
    [Serializable]
    public class MidiEvent
    {
        [Key("EventDate")]
        public DateTime EventDate { get; set; }

        [Key("Type")]
        public MidiDevice.TypeEvent Type { get; set; }

        [Key("Channel")]
        public Channel Channel { get; set; }

        [Key("Values")]
        public List<int> Values { get; set; } = new List<int>();

        [Key("Device")]
        public string Device { get; set; }

        [Key("SysExData")]
        public string SysExData { get; set; }

        [Key("Delay")]
        public int Delay { get; set; } = 0;

        internal bool ReleaseCC = false;

        internal MidiEvent(TypeEvent evType, List<int> values, Channel ch, string device)
        {
            //Event = ev;
            Values = CheckNote(evType, values);
            EventDate = DateTime.Now;
            Type = evType;
            Channel = ch;
            Device = device;
        }

        private List<int> CheckNote(TypeEvent evType, List<int> values)
        {
            if (evType == TypeEvent.NOTE_ON || evType == TypeEvent.NOTE_OFF)
            {
                if (values[0] > 127)
                { values[0] = 127; }
                else if (values[0] < 0)
                { values[0] = 0; }
            }
            return values;
        }

        internal MidiEvent(TypeEvent evType, string sSysex, string device)
        {
            //Event = ev;
            EventDate = DateTime.Now;
            Type = evType;
            SysExData = sSysex;
            Device = device;
        }

        public MidiEvent()
        {

        }

        internal Key GetKey()
        {
            if (Type == TypeEvent.NOTE_ON || Type == TypeEvent.NOTE_OFF || Type == TypeEvent.POLY_PRES)
            {
                Key noteValue;
                Enum.TryParse<Key>(string.Concat("Key", Values[0].ToString()), out noteValue);
                return noteValue;
            }
            else { return Key.Key0; }
        }

        internal Channel GetChannel()
        {
            Channel channelValue;
            Enum.TryParse<Channel>(string.Concat("Channel", Values[0].ToString()), out channelValue);
            return channelValue;
        }

        internal MidiEvent Clone()
        {
            return (MidiEvent)this.MemberwiseClone();
        }
    }

    public class MidiDevice
    {
        private bool[,] BlockIncomingCC = new bool[16,128];

        public int[] CC = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64, 65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 80, 81, 82, 83, 84, 85, 86, 87, 88, 89, 90, 91, 92, 93, 94, 95, 96, 97, 98, 99, 100, 101, 102, 103, 104, 105, 106, 107, 108, 109, 110, 111, 112, 113, 114, 115, 116, 117, 118, 119, 120, 121, 122, 123, 124, 125, 126, 127 };

        //public int CC_Pan = 10;
        //public int CC_Volume = 7;
        //public int CC_Reverb = 91;
        //public int CC_Chorus = 93;
        //public int CC_Release = 72;
        //public int CC_Attack = 73;
        //public int CC_Decay = 75;
        //public int CC_Timbre = 71;
        //public int CC_FilterCutOff = 74;

        public enum TypeEvent
        {
            CC = 0,
            PC = 1,
            PB = 2,
            CH_PRES = 3,
            POLY_PRES = 4,
            NRPN = 5,
            CLOCK = 6,
            STOP = 7,
            START = 8,
            SYSEX = 9,
            NOTE_ON = 10,
            NOTE_OFF = 11,
        }

        public string Name { get; internal set; }
        public DateTime LastMessage { get; private set; } = DateTime.Now;

        private readonly int MIDI_InOrOut; //IN = 1, OUT = 2

        internal VSTPlugin Plugin;
        private bool IsOutVST = false;

        private MidiInputDeviceEvents MIDI_InputEvents;
        private MidiOutputDeviceEvents MIDI_OutputEvents;
        private VSTOutputDeviceEvents VST_OutputEvents;
        internal bool UsedForRouting = false;
        internal bool IsReserverdForInternalPurposes = false;

        internal delegate void LogEventHandler(string sDevice, bool bIn, string sLog);
        internal static event LogEventHandler OnLogAdded;

        internal delegate void MidiEventHandler(bool bIn, MidiEvent ev);
        internal event MidiEventHandler OnMidiEvent;

        internal delegate void MidiClockEventHandler(object sender, MicroTimerEventArgs e);
        internal event MidiClockEventHandler OnMidiClockEvent;

        internal delegate void MidiEventSequenceHandlerIN(MidiEvent ev);
        internal static event MidiEventSequenceHandlerIN OnMidiSequenceEventIN;
        internal delegate void MidiEventSequenceHandlerOUT(MidiEvent ev);
        internal static event MidiEventSequenceHandlerOUT OnMidiSequenceEventOUT;

        internal MidiDevice(RtMidi.Core.Devices.Infos.IMidiInputDeviceInfo inputDevice)
        {
            MIDI_InOrOut = 1;
            Name = inputDevice.Name;
            OpenDevice();
        }

        internal MidiDevice(RtMidi.Core.Devices.Infos.IMidiOutputDeviceInfo outputDevice, InstrumentData instr)
        {
            MIDI_InOrOut = 2;
            Name = outputDevice.Name;
            OpenDevice();

            if (instr != null)
            {
                SendSysExInitializer(instr);
            }
        }

        internal MidiDevice(VSTPlugin vst, string sPluginSlot)
        {
            MIDI_InOrOut = 2;
            Plugin = vst;
            IsOutVST = true;
            Name = sPluginSlot;
            var b = OpenDevice();
            if (b) { vst.LoadVST(); }
        }

        private void MIDI_InputEvents_OnMidiEvent(MidiEvent ev)
        {
            LastMessage = DateTime.Now;

            //renvoyer l'évènement plus haut
            if (ev.Type == TypeEvent.CLOCK)
            {
                OnMidiClockEvent?.Invoke(null, null);
            }
            else
            {
                OnMidiEvent?.Invoke(true, ev);
                OnMidiSequenceEventIN?.Invoke(ev);
            }
        }

        private void MIDI_OutputEvents_OnMidiEvent(MidiEvent ev)
        {
            LastMessage = DateTime.Now;

            //renvoyer l'évènement plus haut
            OnMidiEvent?.Invoke(false, ev);
            OnMidiSequenceEventOUT?.Invoke(ev);
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
                        MIDI_InputEvents.OnMidiEvent -= MIDI_InputEvents_OnMidiEvent;
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
                if (IsOutVST)
                {
                    try
                    {
                        VST_OutputEvents = new VSTOutputDeviceEvents(Plugin);
                        VST_OutputEvents.OnMidiEvent -= MIDI_OutputEvents_OnMidiEvent;
                        VST_OutputEvents.OnMidiEvent += MIDI_OutputEvents_OnMidiEvent;
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                }
                else
                {
                    try
                    {
                        if (MIDI_OutputEvents == null)
                        {
                            MIDI_OutputEvents = new MidiOutputDeviceEvents(Name);
                            MIDI_OutputEvents.OnMidiEvent -= MIDI_OutputEvents_OnMidiEvent;
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
        }

        internal bool CloseDevice()
        {
            if (IsOutVST)
            {
                if (Plugin != null)
                {
                    Plugin.DisposeVST();
                }
                if (VST_OutputEvents != null)
                {
                    VST_OutputEvents.OnMidiEvent -= MIDI_OutputEvents_OnMidiEvent;
                }
                VST_OutputEvents = null;
                Plugin = null;
                return true;
            }
            else
            {
                if (MIDI_InOrOut == 1) //IN
                {
                    try
                    {
                        if (MIDI_InputEvents != null)
                        {
                            MIDI_InputEvents.Stop();
                            MIDI_InputEvents.OnMidiEvent -= MIDI_InputEvents_OnMidiEvent;
                            MIDI_InputEvents = null;
                        }
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
                        if (MIDI_OutputEvents != null)
                        {
                            MIDI_OutputEvents.Stop();
                            MIDI_OutputEvents.OnMidiEvent -= MIDI_OutputEvents_OnMidiEvent;
                            MIDI_OutputEvents = null;
                        }
                        return true;
                    }
                    catch
                    {
                        MIDI_OutputEvents = null;
                        return false;
                    }
                }
            }
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
            if (midiEvent.Type == TypeEvent.CC) { midiEvent.Values[0] = CC[midiEvent.Values[0]]; } //transcodage des CC

            if (IsOutVST)
            {
                VST_OutputEvents.SendEvent(midiEvent);
            }
            else
            {
                MIDI_OutputEvents.SendEvent(midiEvent);
            }
        }

        internal void SendSysExInitializer(InstrumentData instr)
        {
            SendMidiEvent(new MidiEvent(TypeEvent.SYSEX, instr.SysExInitializer, Name));
        }

        internal void DisableClock()
        {
            if (IsOutVST)
            {
                //if (VST_OutputEvents != null)
                //{
                //    VST_OutputEvents.EnableDisableMidiClockEvents(false);
                //}
            }
            else
            {
                if (MIDI_InputEvents != null)
                {
                    MIDI_InputEvents.EnableDisableMidiClockEvents(false);
                }
            }
        }

        internal void EnableClock()
        {
            if (IsOutVST)
            {
            }
            else
            {
                if (MIDI_InputEvents != null)
                {
                    MIDI_InputEvents.EnableDisableMidiClockEvents(true);
                }
            }
        }

        internal int GetLiveCCValue(int channelOut, int iCC)
        {
            if (IsOutVST)
            {
                if (VST_OutputEvents != null)
                {
                    return VST_OutputEvents.CCmemory[channelOut - 1, iCC];
                }
            }
            else
            {
                if (MIDI_OutputEvents != null)
                {
                    return MIDI_OutputEvents.CCmemory[channelOut - 1, iCC];
                }
            }
            return -1;
        }

        internal List<int[]> GetLiveCCData(Channel iChannel)
        {
            List<int[]> ccval = new List<int[]>();

            if (IsOutVST)
            {
                if (VST_OutputEvents != null)
                {
                    int idxch = Tools.GetChannelInt(iChannel) - 1;

                    for (int i = 0; i < 128; i++)
                    {
                        if (VST_OutputEvents.CCmemory[idxch, i] > -1)
                        {
                            ccval.Add(new int[2] { i, VST_OutputEvents.CCmemory[idxch, i] });
                        }
                    }
                }
            }
            else
            {
                if (MIDI_OutputEvents != null)
                {
                    int idxch = Tools.GetChannelInt(iChannel) - 1;

                    for (int i = 0; i < 128; i++)
                    {
                        if (MIDI_OutputEvents.CCmemory[idxch, i] > -1)
                        {
                            ccval.Add(new int[2] { i, MIDI_OutputEvents.CCmemory[idxch, i] });
                        }
                    }
                }
            }

            return ccval;
        }

        internal bool GetLiveNOTEValue(int channelOut, int iNote)
        {
            if (IsOutVST)
            {
                if (VST_OutputEvents != null)
                {
                    return VST_OutputEvents.NOTEmemory[channelOut - 1, iNote];
                }
            }
            else
            {
                if (MIDI_OutputEvents != null)
                {
                    return MIDI_OutputEvents.NOTEmemory[channelOut - 1, iNote];
                }
            }
            return false;
        }

        internal List<int[]> GetLiveNotes(int channelOut)
        {
            List<int[]> notes = new List<int[]>();

            if (IsOutVST)
            {
                for (int i = 0; i < 128; i++)
                {
                    if (VST_OutputEvents.NOTEmemory[channelOut - 1, i])
                    {
                        notes.Add(new int[2] { i, VST_OutputEvents.VELOCITYmemory[channelOut - 1, i] });
                    }
                }
            }
            else
            {
                for (int i = 0; i < 128; i++)
                {
                    if (MIDI_OutputEvents.NOTEmemory[channelOut - 1, i])
                    {
                        notes.Add(new int[2] { i, MIDI_OutputEvents.VELOCITYmemory[channelOut - 1, i] });
                    }
                }
            }
            return notes;
        }

        internal void UnblockCC(int iCC, int iChannelOut)
        {
            BlockIncomingCC[iChannelOut - 1, iCC] = false;
        }

        internal bool IsBlocked(int iCC, int iChannelOut)
        {
            return BlockIncomingCC[iChannelOut - 1, iCC];
        }

        internal void UnblockAllCC(int iChannelOut)
        {
            for (int i = 0; i < 128; i++)
            {
                BlockIncomingCC[iChannelOut - 1, i] = false;
            }
        }

        internal List<MidiEvent> SetCC(int iCC, int iCCValue, MidiOptions options, int channelout)
        {
            //int iChannel = Tools.GetChannelInt(channel);
            List<MidiEvent> intermediate = new List<MidiEvent>();

            if (options.SmoothCC)
            {
                int liveCC = GetLiveCCValue(channelout, iCC);

                if (!Tools.CCToNotBlock.Contains(iCC))
                {
                    if ((liveCC + 16) < iCCValue)
                    {
                        for (int i = liveCC; i < iCCValue; i += 2)
                        {
                            intermediate.Add(new MidiEvent(TypeEvent.CC, new List<int> { iCC, i }, Tools.GetChannel(channelout), Name));
                        }
                        BlockIncomingCC[channelout - 1, iCC] = true;
                    }
                    else if ((liveCC - 16) > iCCValue)
                    {
                        for (int i = liveCC; i > iCCValue; i -= 2)
                        {
                            intermediate.Add(new MidiEvent(TypeEvent.CC, new List<int> { iCC, i }, Tools.GetChannel(channelout), Name));
                        }
                        BlockIncomingCC[channelout - 1, iCC] = true;
                    }
                }
            }

            return intermediate;
        }

        internal void BlockCC(int iCC, int channelout)
        {
            if (!Tools.CCToNotBlock.Contains(iCC))
            {
                BlockIncomingCC[channelout - 1, iCC] = true;
            }
        }

        internal int GetLiveVelocityValue(int channelOut, int iNote)
        {
            if (IsOutVST)
            {
                if (VST_OutputEvents != null)
                {
                    return VST_OutputEvents.VELOCITYmemory[channelOut - 1, iNote];
                }
            }
            else
            {
                if (MIDI_OutputEvents != null)
                {
                    return MIDI_OutputEvents.VELOCITYmemory[channelOut - 1, iNote];
                }
            }
            return 0;
        }

        internal int GetLiveLowestNote()
        {
            int iNote = -1;

            if (IsOutVST)
            {
                if (VST_OutputEvents != null)
                {
                    for (int iC = 0; iC < 16; iC++)
                    {
                        for (int iN = 0; iN < 128; iN++)
                        {
                            if (VST_OutputEvents.NOTEmemory[iC, iN])
                            {
                                return iN;
                            }

                        }
                    }
                }
            }
            else
            {
                if (MIDI_OutputEvents != null)
                {
                    for (int iC = 0; iC < 16; iC++)
                    {
                        for (int iN = 0; iN < 128; iN++)
                        {
                            if (MIDI_OutputEvents.NOTEmemory[iC, iN])
                            {
                                return iN;
                            }

                        }
                    }
                }
            }

            return iNote;
        }

        internal void SaveVSTParameters()
        {
            if (VST_OutputEvents != null)
            {
                VST_OutputEvents.SaveParameters();
            }
        }

    }

    internal class VSTOutputDeviceEvents
    {
        internal delegate void MidiEventHandler(MidiEvent ev);
        internal event MidiEventHandler OnMidiEvent;

        internal int[,] CCmemory = new int[16, 128];
        internal bool[,] NOTEmemory = new bool[16, 128];
        internal int[,] VELOCITYmemory = new int[16, 128];

        private VSTHostInfo InitialInfoToStartAudio;
        private VSTPlugin Plugin;
        internal bool Locked = false;

        internal VSTOutputDeviceEvents(VSTPlugin plugin)
        {
            Plugin = plugin;
        }

        internal void SendEvent(MidiEvent ev)
        {
            int iChannel = Tools.GetChannelInt(ev.Channel) - 1;

            if (Plugin != null && Plugin.VSTSynth != null)
            {
                ev.EventDate = DateTime.Now; //à cause des problèmes de timing à la lecture d'une séquence

                switch (ev.Type)
                {
                    case TypeEvent.CLOCK:
                        break;
                    case TypeEvent.NOTE_ON:
                        NOTEmemory[iChannel, ev.Values[0]] = true;
                        VELOCITYmemory[iChannel, ev.Values[0]] = ev.Values[1];
                        Plugin.VSTSynth.MIDI_NoteOn(ev.Values[0], ev.Values[1], iChannel + 1);
                        AddLog(Plugin.VSTHostInfo.VSTName, false, ev.Channel, "Note On", ev.Values[0].ToString(), "Velocity", ev.Values[1].ToString());
                        break;
                    case TypeEvent.NOTE_OFF:
                        NOTEmemory[iChannel, ev.Values[0]] = false;
                        VELOCITYmemory[iChannel, ev.Values[0]] = ev.Values[1];
                        Plugin.VSTSynth.MIDI_NoteOff(ev.Values[0], ev.Values[1], iChannel + 1);
                        AddLog(Plugin.VSTHostInfo.VSTName, false, ev.Channel, "Note Off", ev.Values[0].ToString(), "Velocity", ev.Values[1].ToString());
                        break;
                    case TypeEvent.NRPN:
                        AddLog(Plugin.VSTHostInfo.VSTName, false, ev.Channel, "Nrpn", ev.Values[0].ToString(), "Value", ev.Values[1].ToString());
                        break;
                    case TypeEvent.PB:
                        Plugin.VSTSynth.MIDI_PitchBend(ev.Values[0], iChannel + 1);
                        AddLog(Plugin.VSTHostInfo.VSTName, false, ev.Channel, "Pitch Bend", ev.Values[0].ToString(), "", "");
                        break;
                    case TypeEvent.PC:
                        Plugin.VSTSynth.MIDI_ProgramChange(ev.Values[0], iChannel + 1);
                        AddLog(Plugin.VSTHostInfo.VSTName, false, ev.Channel, "Program Change", ev.Values[0].ToString(), "", "");
                        break;
                    case TypeEvent.SYSEX:
                        AddLog(Plugin.VSTHostInfo.VSTName, false, 0, "SysEx", ev.SysExData, "", "");
                        break;
                    case TypeEvent.CC:
                        CCmemory[iChannel, ev.Values[0]] = ev.Values[1];
                        Plugin.VSTSynth.MIDI_CC(ev.Values[0], ev.Values[1], iChannel + 1);
                        AddLog(Plugin.VSTHostInfo.VSTName, false, ev.Channel, "Control Change", ev.Values[0].ToString(), "Value", ev.Values[1].ToString());
                        break;
                    case TypeEvent.CH_PRES:
                        Plugin.VSTSynth.MIDI_Aftertouch(Convert.ToByte(ev.Values[0]), iChannel + 1);
                        AddLog(Plugin.VSTHostInfo.VSTName, false, ev.Channel, "Channel Pressure", ev.Values[0].ToString(), "", "");
                        break;
                    case TypeEvent.POLY_PRES:
                        Plugin.VSTSynth.MIDI_PolyphonicAftertouch(Convert.ToByte(ev.Values[0]), Convert.ToByte(ev.Values[1]), iChannel + 1);
                        AddLog(Plugin.VSTHostInfo.VSTName, false, ev.Channel, "Poly. Channel Key", ev.Values[0].ToString(), "Pressure", ev.Values[1].ToString());
                        break;
                }
                OnMidiEvent?.Invoke(ev);
            }
        }

        internal void SaveParameters()
        {
            if (Plugin != null)
            {
                Plugin.GetParameters();
            }
        }
    }

    internal class MidiOutputDeviceEvents
    {
        internal delegate void MidiEventHandler(MidiEvent ev);
        internal event MidiEventHandler OnMidiEvent;

        internal int[,] CCmemory = new int[16, 128];
        internal bool[,] NOTEmemory = new bool[16, 128];
        internal int[,] VELOCITYmemory = new int[16, 128];

        private IMidiOutputDevice outputDevice;

        internal MidiOutputDeviceEvents(string sDevice)
        {
            for (int i = 0; i < 16; i++)
            {
                for (int i2 = 0; i2 < 128; i2++)
                {
                    CCmemory[i, i2] = -1;
                }
            }

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
                    AddLog(outputDevice.Name, false, Channel.Channel1, "[OPEN]", "", "", "");
                }
                else if (outputDevice != null && !outputDevice.IsOpen)
                {
                    outputDevice.Open();
                    AddLog(outputDevice.Name, false, Channel.Channel1, "[OPEN]", "", "", "");
                }
            }
            catch (Exception ex)
            {
                AddLog(outputDevice.Name, false, Channel.Channel1, "Unable to open MIDI OUT port : ", ex.Message, "", "");

                if (outputDevice.IsOpen)
                {
                    outputDevice.Close();
                    AddLog(outputDevice.Name, false, Channel.Channel1, "[CLOSE]", "", "", "");
                }
                outputDevice.Dispose();

                throw ex;
            }
        }

        internal void SendEvent(MidiEvent ev)
        {
            ev.EventDate = DateTime.Now; //à cause des problèmes de timing à la lecture d'une séquence

            switch (ev.Type)
            {
                case TypeEvent.CLOCK:
                    if (outputDevice != null && outputDevice.IsOpen)
                    {
                        ClockMessage msg = new ClockMessage();
                        outputDevice.Send(msg);
                    }
                    //AddLog(outputDevice.Name, false, ev.Channel, "Clock", "", "", ""); //pas de log de l'horloge. Trop coûteux
                    break;
                case TypeEvent.NOTE_ON:
                    if (outputDevice != null && outputDevice.IsOpen)
                    {
                        NOTEmemory[Tools.GetChannelInt(ev.Channel) - 1, ev.Values[0]] = true;
                        VELOCITYmemory[Tools.GetChannelInt(ev.Channel) - 1, ev.Values[0]] = ev.Values[1];
                        NoteOnMessage msg = new NoteOnMessage(ev.Channel, ev.GetKey(), ev.Values[1]);
                        outputDevice.Send(msg);
                        AddLog(outputDevice.Name, false, ev.Channel, "Note On", ev.Values[0].ToString(), "Velocity", ev.Values[1].ToString());
                    }
                    break;
                case TypeEvent.NOTE_OFF:
                    if (outputDevice != null && outputDevice.IsOpen)
                    {
                        NOTEmemory[Tools.GetChannelInt(ev.Channel) - 1, ev.Values[0]] = false;
                        VELOCITYmemory[Tools.GetChannelInt(ev.Channel) - 1, ev.Values[0]] = ev.Values[1];
                        NoteOffMessage msg = new NoteOffMessage(ev.Channel, ev.GetKey(), ev.Values[1]);
                        outputDevice.Send(msg);
                        AddLog(outputDevice.Name, false, ev.Channel, "Note Off", ev.Values[0].ToString(), "Velocity", ev.Values[1].ToString());
                    }
                    break;
                case TypeEvent.NRPN:
                    if (outputDevice != null && outputDevice.IsOpen)
                    {
                        NrpnMessage msg = new NrpnMessage(ev.Channel, ev.Values[0], ev.Values[1]);
                        outputDevice.Send(msg);
                        AddLog(outputDevice.Name, false, ev.Channel, "Nrpn", ev.Values[0].ToString(), "Value", ev.Values[1].ToString());
                    }
                    break;
                case TypeEvent.PB:
                    if (outputDevice != null && outputDevice.IsOpen)
                    {
                        PitchBendMessage msg = new PitchBendMessage(ev.Channel, ev.Values[0]);
                        outputDevice.Send(msg);
                        AddLog(outputDevice.Name, false, ev.Channel, "Pitch Bend", ev.Values[0].ToString(), "", "");
                    }
                    break;
                case TypeEvent.PC:
                    if (outputDevice != null && outputDevice.IsOpen)
                    {
                        ProgramChangeMessage msg = new ProgramChangeMessage(ev.Channel, ev.Values[0]);
                        outputDevice.Send(msg);
                        AddLog(outputDevice.Name, false, ev.Channel, "Program Change", ev.Values[0].ToString(), "", "");
                    }
                    break;
                case TypeEvent.SYSEX:
                    if (outputDevice != null && outputDevice.IsOpen)
                    {
                        var data = Tools.SendSysExData(ev.SysExData);
                        foreach (var sysex in data)
                        {
                            SysExMessage msg = new SysExMessage(sysex);
                            outputDevice.Send(msg);
                            Thread.Sleep(SysExWaiter(sysex.Length));
                            AddLog(outputDevice.Name, false, 0, "SysEx", ev.SysExData, "", "");
                        }
                    }
                    break;
                case TypeEvent.CC:
                    if (outputDevice != null && outputDevice.IsOpen && ev.Values[1] > -1)
                    {
                        CCmemory[Tools.GetChannelInt(ev.Channel) - 1, ev.Values[0]] = ev.Values[1];
                        ControlChangeMessage msg = new ControlChangeMessage(ev.Channel, ev.Values[0], ev.Values[1]);
                        outputDevice.Send(msg);
                        AddLog(outputDevice.Name, false, ev.Channel, "Control Change", ev.Values[0].ToString(), "Value", ev.Values[1].ToString());
                    }
                    break;
                case TypeEvent.CH_PRES:
                    if (outputDevice != null && outputDevice.IsOpen)
                    {
                        ChannelPressureMessage msg = new ChannelPressureMessage(ev.Channel, ev.Values[0]);
                        outputDevice.Send(msg);
                        AddLog(outputDevice.Name, false, ev.Channel, "Channel Pressure", ev.Values[0].ToString(), "", "");
                    }
                    break;
                case TypeEvent.POLY_PRES:
                    if (outputDevice != null && outputDevice.IsOpen)
                    {
                        PolyphonicKeyPressureMessage msg = new PolyphonicKeyPressureMessage(ev.Channel, ev.GetKey(), ev.Values[1]);
                        outputDevice.Send(msg);
                        AddLog(outputDevice.Name, false, ev.Channel, "Poly. Channel Key", ev.Values[0].ToString(), "Pressure", ev.Values[1].ToString());
                    }
                    break;
            }
            OnMidiEvent?.Invoke(ev);
        }

        private int SysExWaiter(int length)
        {
            // Définir la vitesse de transmission MIDI en kbps
            double vitesseTransmissionMIDIEnKbps = 31.25 * 1000;

            // Convertir la vitesse de transmission MIDI en octets par seconde
            double vitesseTransmissionEnOctetsParSeconde = vitesseTransmissionMIDIEnKbps / 8;

            // Calculer le temps de transmission en secondes
            double tpsMs = (length / vitesseTransmissionEnOctetsParSeconde) * 1500; //je mets 1500 pour temporiser (temps machine, software...)

            int iWait = (int)tpsMs + 100; //temporisateur
            return iWait;
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
                        AddLog(outputDevice.Name, false, Channel.Channel1, "[CLOSE]", "", "", "");
                    }

                    outputDevice = null;
                }
                catch (Exception ex)
                {
                    AddLog(outputDevice.Name, false, Channel.Channel1, "Unable to close MIDI OUT port : ", ex.Message, "", "");
                    throw ex;
                }
            }
        }
    }

    internal class MidiInputDeviceEvents
    {
        internal delegate void MidiEventHandler(MidiEvent ev);
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
                    inputDevice.Start += InputDevice_Start;
                    inputDevice.Stop += InputDevice_Stop;

                    //inputDevice.Clock += ClockHandler;
                    inputDevice.Open();
                    AddLog(inputDevice.Name, true, Channel.Channel1, "[OPEN]", "", "", "");
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
                    inputDevice.Clock -= ClockHandler;
                    inputDevice.Start -= InputDevice_Start;
                    inputDevice.Stop -= InputDevice_Stop;

                    inputDevice.Open();
                    AddLog(inputDevice.Name, true, Channel.Channel1, "[OPEN]", "", "", "");
                }
            }
            catch (Exception ex)
            {
                AddLog(inputDevice.Name, true, Channel.Channel1, "Unable to open MIDI IN port : ", ex.Message, "", "");
                inputDevice.ControlChange -= ControlChangeHandler;
                inputDevice.NoteOn -= NoteOnChangeHandler;
                inputDevice.NoteOff -= NoteOffChangeHandler;
                inputDevice.PitchBend -= PitchBendChangeHandler;
                inputDevice.SysEx -= SysexChangeHandler;
                inputDevice.ProgramChange -= ProgramChangeChangeHandler;
                inputDevice.Nrpn -= NrPnChangeHandler;
                inputDevice.ChannelPressure -= ChannelPressureHandler;
                inputDevice.PolyphonicKeyPressure -= PolyphonicPressure;
                inputDevice.Clock -= ClockHandler;
                inputDevice.Start -= InputDevice_Start;
                inputDevice.Stop -= InputDevice_Stop;

                if (inputDevice.IsOpen)
                {
                    inputDevice.Close();
                    AddLog(inputDevice.Name, true, Channel.Channel1, "[CLOSE]", "", "", "");
                }
                inputDevice.Dispose();

                throw ex;
            }
        }

        private void InputDevice_Stop(IMidiInputDevice sender, in StopMessage msg)
        {
            AddLog(inputDevice.Name, true, Channel.Channel1, "Stop", "", "", "");

            AddEvent(TypeEvent.STOP, null, Channel.Channel1);
        }

        private void InputDevice_Start(IMidiInputDevice sender, in StartMessage msg)
        {
            AddLog(inputDevice.Name, true, Channel.Channel1, "Start", "", "", "");

            AddEvent(TypeEvent.START, null, Channel.Channel1);
        }

        private void ClockHandler(IMidiInputDevice sender, in ClockMessage msg)
        {
            //on log la clock ?
            AddLog(inputDevice.Name, true, Channel.Channel1, "Clock", "", "", "");

            AddEvent(TypeEvent.CLOCK, null, Channel.Channel1);
        }

        private void NrPnChangeHandler(IMidiInputDevice sender, in NrpnMessage msg)
        {
            AddLog(inputDevice.Name, true, msg.Channel, "Nrpn", msg.Parameter.ToString(), "Value", msg.Value.ToString());

            AddEvent(TypeEvent.NRPN, new List<int> { msg.Parameter, msg.Value }, msg.Channel);
        }

        private void ProgramChangeChangeHandler(IMidiInputDevice sender, in ProgramChangeMessage msg)
        {
            AddLog(inputDevice.Name, true, msg.Channel, "Program Change", msg.Program.ToString(), "", "");

            AddEvent(TypeEvent.PC, new List<int> { msg.Program }, msg.Channel);
        }

        private void SysexChangeHandler(IMidiInputDevice sender, in SysExMessage msg)
        {
            string sData = Tools.GetSysExString(msg.Data);
            AddLog(inputDevice.Name, true, 0, "SysEx", sData, "", "");

            AddEventSysEx(TypeEvent.SYSEX, sData);
        }

        private void PitchBendChangeHandler(IMidiInputDevice sender, in PitchBendMessage msg)
        {
            AddLog(inputDevice.Name, true, msg.Channel, "Pitch Bend", msg.Value.ToString(), "", "");

            AddEvent(TypeEvent.PB, new List<int> { msg.Value }, msg.Channel);
        }

        private void NoteOffChangeHandler(IMidiInputDevice sender, in NoteOffMessage msg)
        {
            AddLog(inputDevice.Name, true, msg.Channel, "Note Off", msg.Key.ToString(), "Velocity", msg.Velocity.ToString());

            AddEvent(TypeEvent.NOTE_OFF, new List<int> { Tools.GetNoteInt(msg.Key), msg.Velocity }, msg.Channel);
        }

        private void NoteOnChangeHandler(IMidiInputDevice sender, in NoteOnMessage msg)
        {
            AddLog(inputDevice.Name, true, msg.Channel, "Note On", msg.Key.ToString(), "Velocity", msg.Velocity.ToString());

            if (msg.Velocity == 0)
            {
                AddEvent(TypeEvent.NOTE_OFF, new List<int> { Tools.GetNoteInt(msg.Key), msg.Velocity }, msg.Channel);
            }
            else
            {
                AddEvent(TypeEvent.NOTE_ON, new List<int> { Tools.GetNoteInt(msg.Key), msg.Velocity }, msg.Channel);
            }
        }

        private void ControlChangeHandler(IMidiInputDevice sender, in ControlChangeMessage msg)
        {
            AddLog(inputDevice.Name, true, msg.Channel, "Control Change", msg.Control.ToString(), "Value", msg.Value.ToString());

            AddEvent(TypeEvent.CC, new List<int> { msg.Control, msg.Value }, msg.Channel);
        }

        private void PolyphonicPressure(IMidiInputDevice sender, in PolyphonicKeyPressureMessage msg)
        {
            AddLog(inputDevice.Name, true, msg.Channel, "Poly. Channel Pressure", msg.Pressure.ToString(), "Key", msg.Key.ToString());

            AddEvent(TypeEvent.POLY_PRES, new List<int> { Tools.GetNoteInt(msg.Key), msg.Pressure }, msg.Channel);
        }

        private void ChannelPressureHandler(IMidiInputDevice sender, in ChannelPressureMessage msg)
        {
            AddLog(inputDevice.Name, true, msg.Channel, "Channel Pressure", msg.Pressure.ToString(), "", "");

            AddEvent(TypeEvent.CH_PRES, new List<int> { msg.Pressure }, msg.Channel);
        }

        private void AddEvent(MidiDevice.TypeEvent evType, List<int> values, Channel ch)
        {
            OnMidiEvent?.Invoke(new MidiEvent(evType, values, ch, inputDevice.Name));
        }

        private void AddEventSysEx(MidiDevice.TypeEvent evType, string sData)
        {
            OnMidiEvent?.Invoke(new MidiEvent(evType, sData, inputDevice.Name));
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
                        AddLog(inputDevice.Name, true, Channel.Channel1, "[CLOSE]", "", "", "");
                    }

                    inputDevice = null;
                }
                catch (Exception ex)
                {
                    AddLog(inputDevice.Name, true, Channel.Channel1, "Unable to close MIDI IN port : ", ex.Message, "", "");
                    throw ex;
                }
            }
        }

        internal void EnableDisableMidiClockEvents(bool bActivate)
        {
            if (inputDevice != null)
            {
                inputDevice.Clock -= ClockHandler;

                if (bActivate)
                {
                    inputDevice.Clock += ClockHandler;
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
}
