using RtMidi.Core.Devices;
using RtMidi.Core.Enums;
using RtMidi.Core.Messages;
using RtMidi.Core;
using System;
using System.Collections.Generic;
using static MidiTools.MidiDevice;
using System.Text.RegularExpressions;
using System.Threading;
using System.Timers;
using System.Linq;
using System.IO;

namespace MidiTools
{

    [Serializable]
    public class MidiEvent
    {
        public DateTime EventDate;
        public MidiDevice.TypeEvent Type;
        public Channel Channel;
        public List<int> Values = new List<int>();
        public string Device;
        public string SysExData;
        public int Delay = 0;
        internal bool ReleaseCC = false;

        internal MidiEvent(MidiDevice.TypeEvent evType, List<int> values, Channel ch, string device)
        {
            //Event = ev;
            Values = values;
            EventDate = DateTime.Now;
            Type = evType;
            Channel = ch;
            Device = device;
        }

        internal MidiEvent(MidiDevice.TypeEvent evType, string sSysex, string device)
        {
            //Event = ev;
            EventDate = DateTime.Now;
            Type = evType;
            SysExData = sSysex;
            Device = device;
        }

        internal MidiEvent()
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
        public int CC_Pan = 10;
        public int CC_Volume = 7;
        public int CC_Reverb = 91;
        public int CC_Chorus = 93;
        public int CC_Release = 72;
        public int CC_Attack = 73;
        public int CC_Decay = 75;
        public int CC_Timbre = 71;
        public int CC_FilterCutOff = 74;

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
            POLY_PRES = 8,
            CLOCK = 9,
            STOP = 10,
            START = 11
        }

        public string Name { get; internal set; }
        public DateTime LastMessage { get; private set; }

        private readonly int MIDI_InOrOut; //IN = 1, OUT = 2

        private MidiInputDeviceEvents MIDI_InputEvents;
        private MidiOutputDeviceEvents MIDI_OutputEvents;
        internal bool UsedForRouting = false;
        internal bool IsReserverdForInternalPurposes = false;

        internal delegate void LogEventHandler(string sDevice, bool bIn, string sLog);
        internal static event LogEventHandler OnLogAdded;

        internal delegate void MidiEventHandler(bool bIn, MidiEvent ev);
        internal event MidiEventHandler OnMidiEvent;

        internal delegate void MidiClockEventHandler(object sender, ElapsedEventArgs e);
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

        internal bool CloseDevice()
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
            MIDI_OutputEvents.SendEvent(midiEvent); 
        }

        internal void SendSysExInitializer(InstrumentData instr)
        {
            SendMidiEvent(new MidiEvent(TypeEvent.SYSEX, instr.SysExInitializer, Name));
        }

        internal void DisableClock()
        {
            if (MIDI_InputEvents != null)
            {
                MIDI_InputEvents.EnableDisableMidiClockEvents(false);
            }
        }

        internal void EnableClock()
        {
            if (MIDI_InputEvents != null)
            {
                MIDI_InputEvents.EnableDisableMidiClockEvents(true);
            }
        }

        internal int GetLiveCCValue(int channelOut, int iCC)
        {
            if (MIDI_OutputEvents != null)
            {
                return MIDI_OutputEvents.CCmemory[channelOut - 1, iCC];
            }
            else { return -1; }
        }

        internal List<int[]> GetLiveCCData(Channel iChannel)
        {
            List<int[]> ccval = new List<int[]>();

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

            return ccval;
        }

        internal bool GetLiveNOTEValue(int channelOut, int iNote)
        {
            if (MIDI_OutputEvents != null)
            {
                return MIDI_OutputEvents.NOTEmemory[channelOut - 1, iNote];
            }
            else { return false; }
        }

        internal int GetLiveLowestNote()
        {
            int iNote = -1;

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

            return iNote;
        }
    }

    internal class MidiOutputDeviceEvents
    {
        internal delegate void MidiEventHandler(MidiEvent ev);
        internal event MidiEventHandler OnMidiEvent;

        internal int[,] CCmemory = new int[16,128];
        internal bool[,] NOTEmemory = new bool[16, 128];

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
                        NoteOnMessage msg = new NoteOnMessage(ev.Channel, ev.GetKey(), ev.Values[1]);
                        outputDevice.Send(msg);
                    }
                    AddLog(outputDevice.Name, false, ev.Channel, "Note On", ev.Values[0].ToString(), "Velocity", ev.Values[1].ToString());
                    break;
                case TypeEvent.NOTE_OFF:
                    if (outputDevice != null && outputDevice.IsOpen)
                    {
                        NOTEmemory[Tools.GetChannelInt(ev.Channel) - 1, ev.Values[0]] = false;
                        NoteOffMessage msg = new NoteOffMessage(ev.Channel, ev.GetKey(), ev.Values[1]);
                        outputDevice.Send(msg);
                    }
                    AddLog(outputDevice.Name, false, ev.Channel, "Note Off", ev.Values[0].ToString(), "Velocity", ev.Values[1].ToString());
                    break;
                case TypeEvent.NRPN:
                    if (outputDevice != null && outputDevice.IsOpen)
                    {
                        NrpnMessage msg = new NrpnMessage(ev.Channel, ev.Values[0], ev.Values[1]);
                        outputDevice.Send(msg);
                    }
                    AddLog(outputDevice.Name, false, ev.Channel, "Nrpn", ev.Values[0].ToString(), "Value", ev.Values[1].ToString());
                    break;
                case TypeEvent.PB:
                    if (outputDevice != null && outputDevice.IsOpen)
                    {
                        PitchBendMessage msg = new PitchBendMessage(ev.Channel, ev.Values[0]);
                        outputDevice.Send(msg);
                    }
                    AddLog(outputDevice.Name, false, ev.Channel, "Pitch Bend", ev.Values[0].ToString(), "", "");
                    break;
                case TypeEvent.PC:
                    if (outputDevice != null && outputDevice.IsOpen)
                    {
                        ProgramChangeMessage msg = new ProgramChangeMessage(ev.Channel, ev.Values[0]);
                        outputDevice.Send(msg);
                    }
                    AddLog(outputDevice.Name, false, ev.Channel, "Program Change", ev.Values[0].ToString(), "", "");
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
                        }
                    }
                    AddLog(outputDevice.Name, false, 0, "SysEx", ev.SysExData, "", "");
                    break;
                case TypeEvent.CC:
                    if (outputDevice != null && outputDevice.IsOpen && ev.Values[1] > -1)
                    {
                        CCmemory[Tools.GetChannelInt(ev.Channel) - 1, ev.Values[0]] = ev.Values[1];
                        ControlChangeMessage msg = new ControlChangeMessage(ev.Channel, ev.Values[0], ev.Values[1]);
                        outputDevice.Send(msg);
                    }
                    AddLog(outputDevice.Name, false, ev.Channel, "Control Change", ev.Values[0].ToString(), "Value", ev.Values[1].ToString());
                    break;
                case TypeEvent.CH_PRES:
                    if (outputDevice != null && outputDevice.IsOpen)
                    {
                        ChannelPressureMessage msg = new ChannelPressureMessage(ev.Channel, ev.Values[0]);
                        outputDevice.Send(msg);
                    }
                    AddLog(outputDevice.Name, false, ev.Channel, "Channel Pressure", ev.Values[0].ToString(), "", "");
                    break;
                case TypeEvent.POLY_PRES:
                    if (outputDevice != null && outputDevice.IsOpen)
                    {
                        PolyphonicKeyPressureMessage msg = new PolyphonicKeyPressureMessage(ev.Channel, ev.GetKey(), ev.Values[1]);
                        outputDevice.Send(msg);
                    }
                    AddLog(outputDevice.Name, false, ev.Channel, "Poly. Channel Key", ev.Values[0].ToString(), "Pressure", ev.Values[1].ToString());
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
