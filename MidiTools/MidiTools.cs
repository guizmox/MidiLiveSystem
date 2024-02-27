using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Xml.Serialization;
using RtMidi.Core;
using RtMidi.Core.Devices;
using RtMidi.Core.Enums;
using RtMidi.Core.Messages;
using static MidiTools.MidiDevice;

namespace MidiTools
{
    internal class MatrixItem
    {
        internal bool Active { get; set; } = true;
        internal int ChannelIn = 1;
        internal int ChannelOut = 1;
        internal MidiOptions Options { get; set; } = new MidiOptions();
        internal MidiDevice DeviceIn;
        internal MidiDevice DeviceOut;
        internal MidiPreset Preset { get; set; }
        internal Guid RoutingGuid { get; private set; }

        public MatrixItem(MidiDevice MidiIN, MidiDevice MidiOUT, int iChIN, int iChOUT, MidiOptions options, MidiPreset preset)
        {
            DeviceIn = MidiIN;
            DeviceOut = MidiOUT;
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
            else
            {
                if (DeviceIn == null && sDeviceIn.Length > 0)
                { return true; }
                else { return false; }
            }
        }

        internal bool CheckDeviceOut(string sDeviceOut)
        {
            if (DeviceOut != null && DeviceOut.Name != sDeviceOut)
            {
                return true;
            }
            else
            {
                if (DeviceOut == null && sDeviceOut.Length > 0)
                { return true; }
                else { return false; }
            }
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

        public NoteGenerator PlayNote;
        public bool PlayNote_LowestNote = false;

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

        public int CC_Pan_Value { get { return _CC_Pan_Value; } set { if (value < -1) { _CC_Pan_Value = -1; } else if (value > 127) { _CC_Pan_Value = 64; } else { _CC_Pan_Value = value; } } }
        public int CC_Volume_Value { get { return _CC_Volume_Value; } set { if (value < -1) { _CC_Volume_Value = -1; } else if (value > 127) { _CC_Volume_Value = 127; } else { _CC_Volume_Value = value; } } }
        public int CC_Reverb_Value { get { return _CC_Reverb_Value; } set { if (value < -1) { _CC_Reverb_Value = -1; } else if (value > 127) { _CC_Reverb_Value = 127; } else { _CC_Reverb_Value = value; } } }
        public int CC_Chorus_Value { get { return _CC_Chorus_Value; } set { if (value < -1) { _CC_Chorus_Value = -1; } else if (value > 127) { _CC_Chorus_Value = 127; } else { _CC_Chorus_Value = value; } } }
        public int CC_Release_Value { get { return _CC_Release_Value; } set { if (value < -1) { _CC_Release_Value = -1; } else if (value > 127) { _CC_Release_Value = 127; } else { _CC_Release_Value = value; } } }
        public int CC_Attack_Value { get { return _CC_Attack_Value; } set { if (value < -1) { _CC_Attack_Value = -1; } else if (value > 127) { _CC_Attack_Value = 127; } else { _CC_Attack_Value = value; } } }
        public int CC_Decay_Value { get { return _CC_Decay_Value; } set { if (value < -1) { _CC_Decay_Value = -1; } else if (value > 127) { _CC_Decay_Value = 127; } else { _CC_Decay_Value = value; } } }
        public int CC_Brightness_Value { get { return _CC_Brightness_Value; } set { if (value < -1) { _CC_Brightness_Value = -1; } else if (value > 127) { _CC_Brightness_Value = 127; } else { _CC_Brightness_Value = value; } } }

        public List<int[]> CC_Converters { get; private set; } = new List<int[]>();
        public List<int[]> Note_Converters { get; private set; } = new List<int[]>();

        public List<string[]> Translators { get; private set; } = new List<string[]>();

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

        public void AddTranslator(string sScript, string sName)
        {
            //TODO : réaliser un contrôle supplémentaire de l'UI ?
            Translators.Add(new string[] { sScript, sName });
        }
    }

    public class MidiRouting
    {
        private static double CLOCK_INTERVAL = 1000;

        private bool ClockRunning = false;
        private int ClockBPM = 120;
        private string ClockDevice = "";

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

        public delegate void MidiInputEventHandler(MidiEvent ev);
        public event MidiInputEventHandler IncomingMidiMessage;

        public int Events { get { return _eventsProcessedINLast + _eventsProcessedOUTLast; } }

        public string CyclesInfo
        {
            get
            {
                string sMessage = "MIDI Average Processing Messages / Sec. : ";
                sMessage = string.Concat(sMessage, " [IN] : " + (_eventsProcessedINLast).ToString());
                sMessage = string.Concat(sMessage, " / [OUT] : " + (_eventsProcessedOUTLast).ToString());

                return sMessage;
            }
        }
        public int LowestNoteRunning { get { return _lowestNotePlayed; } }

        private List<MatrixItem> MidiMatrix = new List<MatrixItem>();
        private List<MidiDevice> UsedDevicesIN = new List<MidiDevice>();
        private List<MidiDevice> UsedDevicesOUT = new List<MidiDevice>();

        private System.Timers.Timer EventsCounter;
        private System.Timers.Timer MidiClock;

        private int _eventsProcessedIN = 0;
        private int _eventsProcessedOUT = 0;
        private int _eventsProcessedINLast = 0;
        private int _eventsProcessedOUTLast = 0;

        private int _lowestNotePlayed = -1;

        private List<MidiEvent> _notesentforpanic = new List<MidiEvent>();

        public MidiRouting()
        {
            //MidiDevice.OnLogAdded += MidiDevice_OnLogAdded;

            EventsCounter = new System.Timers.Timer();
            EventsCounter.Elapsed += QueueProcessor_OnEvent;
            EventsCounter.Interval = CLOCK_INTERVAL;
            EventsCounter.Start();

            MidiClock = new System.Timers.Timer();
            MidiClock.Elapsed += MidiClock_OnEvent;
            MidiClock.Interval = 60000.0 / 120; //valeur par défaut
        }

        #region PRIVATE

        private void QueueProcessor_OnEvent(object sender, ElapsedEventArgs e)
        {
            _eventsProcessedINLast = _eventsProcessedIN;
            _eventsProcessedOUTLast = _eventsProcessedOUT;
            _eventsProcessedIN = 0;
            _eventsProcessedOUT = 0;
        }

        private void MidiClock_OnEvent(object sender, ElapsedEventArgs e)
        {
            foreach (var device in UsedDevicesOUT)
            {
                device.SendMidiEvent(new MidiEvent(TypeEvent.CLOCK, null, Channel.Channel1, device.Name));
            }
        }

        public void RemovePendingNotes(bool bTransposed)
        {
            MidiEvent[] copy;

            lock (_notesentforpanic)
            {
                copy = new MidiEvent[_notesentforpanic.Count];
                _notesentforpanic.CopyTo(copy);
                _notesentforpanic.Clear();
            }

            if (bTransposed)
            {
                foreach (var pending in copy)
                {
                    for (int i = 0; i <= 127; i++)
                    {
                        var eventout = new MidiEvent(TypeEvent.NOTE_OFF, new List<int> { i, 0 }, pending.Channel, pending.Device);
                        CreateOUTEvent(eventout, true);
                    }
                }
            }
            else
            {

                foreach (var note in copy)
                {
                    var eventout = new MidiEvent(TypeEvent.NOTE_OFF, new List<int> { note.Values[0], 0 }, note.Channel, note.Device);
                    CreateOUTEvent(eventout, true);
                }
            }
        }

        public int AdjustUIRefreshRate()
        {
            int iAdjust = 1000; //1sec par défaut
            int iEvents = (_eventsProcessedINLast + _eventsProcessedOUTLast);

            iAdjust = ((iEvents * 2) / 10);

            if (iAdjust < 2)
            {
                iAdjust = 2;
            }

            return iAdjust;
        }

        private void CheckAndCloseUnusedDevices()
        {
            var usedin = MidiMatrix.Where(d => d.DeviceIn != null).Select(d => d.DeviceIn.Name).Distinct();
            var usedout = MidiMatrix.Where(d => d.DeviceOut != null).Select(d => d.DeviceOut.Name).Distinct();

            List<string> ToRemoveIN = new List<string>();
            foreach (var devin in UsedDevicesIN)
            {
                if (!usedin.Contains(devin.Name))
                {
                    ToRemoveIN.Add(devin.Name);
                }
            }
            List<string> ToRemoveOUT = new List<string>();
            foreach (var devout in UsedDevicesOUT)
            {
                if (!usedout.Contains(devout.Name))
                {
                    ToRemoveOUT.Add(devout.Name);
                }
            }
            foreach (string s in ToRemoveIN)
            {
                var d = UsedDevicesIN.FirstOrDefault(d => d.Name.Equals(s));
                d.CloseDevice();
                d.OnMidiEvent -= DeviceIn_OnMidiEvent;
                UsedDevicesIN.Remove(d);
            }
            foreach (string s in ToRemoveOUT)
            {
                var d = UsedDevicesOUT.FirstOrDefault(d => d.Name.Equals(s));
                d.CloseDevice();
                d.OnMidiEvent -= DeviceOut_OnMidiEvent;
                UsedDevicesOUT.Remove(d);
            }
        }

        private void DeviceOut_OnMidiEvent(bool bIn, MidiDevice.MidiEvent ev)
        {
            //lock (_eventsOUT) { _eventsOUT.Add(ev); }
        }

        private void DeviceIn_OnMidiEvent(bool bIn, MidiDevice.MidiEvent ev)
        {
            _eventsProcessedIN += 1;

            //mémorisation de la note pressée la plus grave
            if (ev.Type == TypeEvent.NOTE_ON && ev.Values[0] < _lowestNotePlayed && ev.Values[1] > 0) { _lowestNotePlayed = ev.Values[0]; }
            else if (ev.Type == TypeEvent.NOTE_OFF && ev.Values[0] == _lowestNotePlayed) { _lowestNotePlayed = -1; }
            else if (ev.Type == TypeEvent.NOTE_ON && ev.Values[0] == _lowestNotePlayed && ev.Values[1] == 0) { _lowestNotePlayed = -1; }

            IncomingMidiMessage?.Invoke(ev);

            CreateOUTEvent(ev, false);
        }

        private void MidiDevice_OnLogAdded(string sDevice, bool bIn, string sLog)
        {
            NewLog?.Invoke(sDevice, bIn, sLog);
        }

        private void CreateOUTEvent(MidiDevice.MidiEvent ev, bool bForce)
        {
            //attention : c'est bien un message du device IN qui arrive ! ne marche pas si on a envoyé un message issu d'un device out
            List<MatrixItem> matrix = new List<MatrixItem>();

            if (!bForce)
            {
                matrix = MidiMatrix.Where(i => i.DeviceOut != null && i.Active && i.DeviceIn != null && i.DeviceIn.Name == ev.Device && (i.ChannelIn == 0 || Tools.GetChannel(i.ChannelIn) == ev.Channel)).ToList();
            }
            else if (bForce) //envoyé pour un device out
            {
                matrix = MidiMatrix.Where(i => i.Active && i.DeviceOut != null && i.DeviceOut.Name == ev.Device && (i.ChannelOut == 0 || Tools.GetChannel(i.ChannelOut) == ev.Channel)).ToList();
            }

            foreach (MatrixItem item in matrix)
            {
                bool bTranslated = false;
                if (!bForce) { bTranslated = MidiTranslator(item, ev); } //TRANSLATEUR DE MESSAGES

                if (!bTranslated)
                {
                    for (int i = (item.ChannelOut == 0 ? 1 : item.ChannelOut); i <= (item.ChannelOut == 0 ? 16 : item.ChannelOut); i++)
                    {
                        List<MidiDevice.MidiEvent> _eventsOUT = new List<MidiDevice.MidiEvent>();

                        //opérations de filtrage 
                        switch (ev.Type)
                        {
                            case TypeEvent.CC:
                                if (item.Options.AllowAllCC || bForce)
                                {
                                    if (item.Options.CC_ToVolume == ev.Values[0])
                                    {
                                        _eventsOUT.Add(new MidiEvent(ev.Type, new List<int> { 7, ev.Values[1] }, Tools.GetChannel(i), item.DeviceOut.Name));
                                    }
                                    else
                                    {
                                        var convertedCC = item.Options.CC_Converters.FirstOrDefault(i => i[0] == ev.Values[0]);
                                        _eventsOUT.Add(new MidiEvent(ev.Type, new List<int> { convertedCC != null ? convertedCC[1] : ev.Values[0], ev.Values[1] }, Tools.GetChannel(i), item.DeviceOut.Name));
                                    }
                                }
                                break;
                            case TypeEvent.CH_PRES:
                                if ((!item.Options.AftertouchVolume && item.Options.AllowAftertouch) || bForce)
                                {
                                    _eventsOUT.Add(new MidiEvent(ev.Type, new List<int> { ev.Values[0] }, Tools.GetChannel(i), item.DeviceOut.Name));
                                }
                                else if (item.Options.AftertouchVolume)
                                {
                                    _eventsOUT.Add(new MidiEvent(TypeEvent.CC, new List<int> { item.DeviceOut.CC_Volume, ev.Values[0] }, Tools.GetChannel(i), item.DeviceOut.Name));
                                }
                                break;
                            case TypeEvent.NOTE_OFF:
                                if (item.Options.AllowNotes || bForce)
                                {
                                    int iNote = Tools.GetNoteIndex(ev.Values[0], ev.Values[1], item.Options);
                                    var convertedNote = item.Options.Note_Converters.FirstOrDefault(i => i[0] == Tools.GetNoteIndex(ev.Values[0], 0, null));
                                    if (convertedNote != null) { iNote = convertedNote[1]; } //TODO DOUTE

                                    if (iNote > -1)
                                    {
                                        var eventout = new MidiEvent(ev.Type, new List<int> { iNote, item.Options.AftertouchVolume ? 0 : ev.Values[1] }, Tools.GetChannel(i), item.DeviceOut.Name);
                                        _eventsOUT.Add(eventout);

                                        lock (_notesentforpanic)
                                        {
                                            var toremove = _notesentforpanic.FirstOrDefault(n => n.Device == eventout.Device && n.Channel == eventout.Channel && n.Values[0] == eventout.Values[0]);
                                            if (toremove != null)
                                            {
                                                _notesentforpanic.Remove(toremove);
                                            }
                                        }
                                    }
                                }
                                break;
                            case TypeEvent.NOTE_ON:
                                if (item.Options.AllowNotes || bForce)
                                {
                                    int iNote = Tools.GetNoteIndex(ev.Values[0], ev.Values[1], item.Options);
                                    var convertedNote = item.Options.Note_Converters.FirstOrDefault(i => i[0] == Tools.GetNoteIndex(ev.Values[0], 0, null));
                                    if (convertedNote != null) { iNote = convertedNote[1]; } //TODO DOUTE

                                    if (iNote > -1) //NOTE : il faut systématiquement mettre au moins une véolcité de 1 pour que la note se déclenche
                                    {
                                        int iVelocity = item.Options.AftertouchVolume && ev.Values[1] > 0 ? 1 : ev.Values[1];

                                        var eventout = new MidiEvent(ev.Type, new List<int> { iNote, iVelocity }, Tools.GetChannel(i), item.DeviceOut.Name);
                                        _eventsOUT.Add(eventout);

                                        if (iVelocity == 0) //le genos n'envoie pas de note off mais que des ON à 0
                                        {
                                            lock (_notesentforpanic)
                                            {
                                                var toremove = _notesentforpanic.FirstOrDefault(n => n.Device == eventout.Device && n.Channel == eventout.Channel && n.Values[0] == eventout.Values[0]);
                                                if (toremove != null)
                                                {
                                                    _notesentforpanic.Remove(toremove);
                                                }
                                            }
                                        }
                                        else
                                        {
                                            lock (_notesentforpanic)
                                            {
                                                _notesentforpanic.Add(eventout);
                                            }
                                        }
                                    }
                                }
                                break;
                            case TypeEvent.NRPN:
                                if (item.Options.AllowNrpn || bForce)
                                {
                                    _eventsOUT.Add(new MidiEvent(ev.Type, new List<int> { ev.Values[0], ev.Values[1] }, Tools.GetChannel(i), item.DeviceOut.Name));
                                }
                                break;
                            case TypeEvent.PB:
                                if (item.Options.AllowPitchBend || bForce)
                                {
                                    _eventsOUT.Add(new MidiEvent(ev.Type, new List<int> { ev.Values[0] }, Tools.GetChannel(i), item.DeviceOut.Name));
                                }
                                break;
                            case TypeEvent.PC:
                                if (item.Options.AllowProgramChange || bForce)
                                {
                                    _eventsOUT.Add(new MidiEvent(ev.Type, new List<int> { ev.Values[0] }, Tools.GetChannel(i), item.DeviceOut.Name));
                                }
                                break;
                            case TypeEvent.POLY_PRES:
                                if (item.Options.AllowAftertouch || bForce)
                                {
                                    _eventsOUT.Add(new MidiEvent(ev.Type, new List<int> { ev.Values[0], ev.Values[1] }, Tools.GetChannel(i), item.DeviceOut.Name));
                                }
                                break;
                            case TypeEvent.SYSEX:
                                if (item.Options.AllowSysex || bForce)
                                {
                                    _eventsOUT.Add(new MidiEvent(ev.Type, SysExTranscoder(ev.SysExData), item.DeviceOut.Name));
                                }
                                break;
                        }

                        if (_eventsOUT.Count > 0)
                        {
                            for (int iEv = 0; iEv < _eventsOUT.Count; iEv++)
                            {
                                item.DeviceOut.SendMidiEvent(_eventsOUT[iEv]);
                            }

                            _eventsProcessedOUT += _eventsOUT.Count;
                        }
                    }
                }
            }
        }

        private void ChangeOptions(MatrixItem routing, MidiOptions newop, bool bInit)
        {
            if (routing.DeviceOut != null)
            {
                if (newop == null) //tout charger
                {
                    if (routing.Options.CC_Attack_Value > -1 || (bInit && routing.Options.CC_Attack_Value > -1))
                    {
                        CreateOUTEvent(new MidiDevice.MidiEvent(TypeEvent.CC, new List<int> { routing.DeviceOut.CC_Attack, routing.Options.CC_Attack_Value }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name), true);
                    }
                    if (routing.Options.CC_Brightness_Value > -1 || (bInit && routing.Options.CC_Brightness_Value > -1))
                    {
                        CreateOUTEvent(new MidiDevice.MidiEvent(TypeEvent.CC, new List<int> { routing.DeviceOut.CC_Brightness, routing.Options.CC_Brightness_Value }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name), true);
                    }
                    if (routing.Options.CC_Chorus_Value > -1 || (bInit && routing.Options.CC_Chorus_Value > -1))
                    {
                        CreateOUTEvent(new MidiDevice.MidiEvent(TypeEvent.CC, new List<int> { routing.DeviceOut.CC_Chorus, routing.Options.CC_Chorus_Value }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name), true);
                    }
                    if (routing.Options.CC_Decay_Value > -1 || (bInit && routing.Options.CC_Decay_Value > -1))
                    {
                        CreateOUTEvent(new MidiDevice.MidiEvent(TypeEvent.CC, new List<int> { routing.DeviceOut.CC_Decay, routing.Options.CC_Decay_Value }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name), true);
                    }
                    if (routing.Options.CC_Pan_Value > -1 || (bInit && routing.Options.CC_Pan_Value > -1))
                    {
                        CreateOUTEvent(new MidiDevice.MidiEvent(TypeEvent.CC, new List<int> { routing.DeviceOut.CC_Pan, routing.Options.CC_Pan_Value }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name), true);
                    }
                    if (routing.Options.CC_Release_Value > -1 || (bInit && routing.Options.CC_Release_Value > -1))
                    {
                        CreateOUTEvent(new MidiDevice.MidiEvent(TypeEvent.CC, new List<int> { routing.DeviceOut.CC_Release, routing.Options.CC_Release_Value }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name), true);
                    }
                    if (routing.Options.CC_Reverb_Value > -1 || (bInit && routing.Options.CC_Reverb_Value > -1))
                    {
                        CreateOUTEvent(new MidiDevice.MidiEvent(TypeEvent.CC, new List<int> { routing.DeviceOut.CC_Reverb, routing.Options.CC_Reverb_Value }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name), true);
                    }
                    if (routing.Options.CC_Volume_Value > -1 || (bInit && routing.Options.CC_Volume_Value > -1))
                    {
                        CreateOUTEvent(new MidiDevice.MidiEvent(TypeEvent.CC, new List<int> { routing.DeviceOut.CC_Volume, routing.Options.CC_Volume_Value }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name), true);
                    }
                }
                else
                {
                    if (newop.TranspositionOffset != routing.Options.TranspositionOffset) //midi panic
                    {
                        RemovePendingNotes(true);
                    }

                    //comparer
                    if (newop.CC_Attack_Value != routing.Options.CC_Attack_Value || (bInit && routing.Options.CC_Attack_Value > -1))
                    {
                        CreateOUTEvent(new MidiDevice.MidiEvent(TypeEvent.CC, new List<int> { routing.DeviceOut.CC_Attack, newop.CC_Attack_Value }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name), true);
                    }
                    if (newop.CC_Brightness_Value != routing.Options.CC_Brightness_Value || (bInit && routing.Options.CC_Brightness_Value > -1))
                    {
                        CreateOUTEvent(new MidiDevice.MidiEvent(TypeEvent.CC, new List<int> { routing.DeviceOut.CC_Brightness, newop.CC_Brightness_Value }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name), true);
                    }
                    if (newop.CC_Chorus_Value != routing.Options.CC_Chorus_Value || (bInit && routing.Options.CC_Chorus_Value > -1))
                    {
                        CreateOUTEvent(new MidiDevice.MidiEvent(TypeEvent.CC, new List<int> { routing.DeviceOut.CC_Chorus, newop.CC_Chorus_Value }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name), true);
                    }
                    if (newop.CC_Decay_Value != routing.Options.CC_Decay_Value || (bInit && routing.Options.CC_Decay_Value > -1))
                    {
                        CreateOUTEvent(new MidiDevice.MidiEvent(TypeEvent.CC, new List<int> { routing.DeviceOut.CC_Decay, newop.CC_Decay_Value }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name), true);
                    }
                    if (newop.CC_Pan_Value != routing.Options.CC_Pan_Value || (bInit && routing.Options.CC_Pan_Value > -1))
                    {
                        CreateOUTEvent(new MidiDevice.MidiEvent(TypeEvent.CC, new List<int> { routing.DeviceOut.CC_Pan, newop.CC_Pan_Value }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name), true);
                    }
                    if (newop.CC_Release_Value != routing.Options.CC_Release_Value || (bInit && routing.Options.CC_Release_Value > -1))
                    {
                        CreateOUTEvent(new MidiDevice.MidiEvent(TypeEvent.CC, new List<int> { routing.DeviceOut.CC_Release, newop.CC_Release_Value }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name), true);
                    }
                    if (newop.CC_Reverb_Value != routing.Options.CC_Reverb_Value || (bInit && routing.Options.CC_Reverb_Value > -1))
                    {
                        CreateOUTEvent(new MidiDevice.MidiEvent(TypeEvent.CC, new List<int> { routing.DeviceOut.CC_Reverb, newop.CC_Reverb_Value }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name), true);
                    }
                    if (newop.CC_Volume_Value != routing.Options.CC_Volume_Value || (bInit && routing.Options.CC_Volume_Value > -1))
                    {
                        CreateOUTEvent(new MidiDevice.MidiEvent(TypeEvent.CC, new List<int> { routing.DeviceOut.CC_Volume, newop.CC_Volume_Value }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name), true);
                    }
                }
            }

            if (newop != null) { routing.Options = newop; }
        }

        private void SendProgramChange(MatrixItem routing, MidiPreset preset)
        {
            if (routing != null)
            {
                ControlChangeMessage pc00 = new ControlChangeMessage(Tools.GetChannel(preset.Channel), 0, preset.Msb);
                ControlChangeMessage pc32 = new ControlChangeMessage(Tools.GetChannel(preset.Channel), 32, preset.Lsb);
                ProgramChangeMessage prg = new ProgramChangeMessage(Tools.GetChannel(preset.Channel), preset.Prg);

                if (routing.DeviceOut != null)
                {
                    CreateOUTEvent(new MidiDevice.MidiEvent(TypeEvent.CC, new List<int> { pc00.Control, pc00.Value }, pc00.Channel, routing.DeviceOut.Name), true);
                    CreateOUTEvent(new MidiDevice.MidiEvent(TypeEvent.CC, new List<int> { pc32.Control, pc32.Value }, pc32.Channel, routing.DeviceOut.Name), true);
                    CreateOUTEvent(new MidiDevice.MidiEvent(TypeEvent.PC, new List<int> { prg.Program }, prg.Channel, routing.DeviceOut.Name), true);
                }
            }
        }

        private void ChangeProgram(MatrixItem routing, MidiPreset newpres, bool bInit)
        {
            if (routing.DeviceOut != null)
            {
                if (bInit || (newpres.Lsb != routing.Preset.Lsb || newpres.Msb != routing.Preset.Msb || newpres.Prg != routing.Preset.Prg || newpres.Channel != routing.Preset.Channel))
                {
                    SendProgramChange(routing, newpres);
                    routing.Preset = newpres;
                }
                else
                {
                    routing.Preset = newpres;
                }
            }
            else
            {
                routing.Preset = newpres;
            }
        }

        private string SysExTranscoder(string data)
        {
            //TODO SCRIPT TRANSCO
            return data;
        }

        private bool MidiTranslator(MatrixItem routing, MidiEvent ev)
        {
            bool bMustTranslate = false;

            if (routing.DeviceOut != null)
            {
                foreach (var translate in routing.Options.Translators)
                {
                    Match matchIN = null;

                    switch (ev.Type)
                    {
                        case TypeEvent.NOTE_ON:
                            //[IN=KEY#0-127:0-127]
                            //[IN=KEY#64:64]
                            matchIN = Regex.Match(translate[0], "(\\[)(IN)=(KEY#)(\\d{1,3})((-)(\\d{1,3}))*(:)(\\d{1,3})((-)(\\d{1,3}))*(\\])");
                            if (matchIN.Success)
                            {
                                string sNote1 = matchIN.Groups[4].Value;
                                string sNote2 = matchIN.Groups[7].Value;
                                string sVelo1 = matchIN.Groups[9].Value;
                                string sVelo2 = matchIN.Groups[12].Value;
                                if (sNote2.Length == 0) //une seule note
                                {
                                    if (Convert.ToInt32(sNote1) == ev.Values[0])
                                    {
                                        if (sVelo2.Length == 0)
                                        {
                                            if (Convert.ToInt32(sVelo1) == ev.Values[1])
                                            { bMustTranslate = true; }
                                        }
                                        else
                                        {
                                            if (Convert.ToInt32(sVelo1) <= ev.Values[1] && Convert.ToInt32(sVelo2) >= ev.Values[1])
                                            { bMustTranslate = true; }
                                        }
                                    }
                                }
                                else
                                {
                                    if (Convert.ToInt32(sNote1) <= ev.Values[0] && Convert.ToInt32(sNote2) >= ev.Values[0])
                                    {
                                        if (sVelo2.Length == 0)
                                        {
                                            if (Convert.ToInt32(sVelo1) == ev.Values[1])
                                            { bMustTranslate = true; }
                                        }
                                        else
                                        {
                                            if (Convert.ToInt32(sVelo1) <= ev.Values[1] && Convert.ToInt32(sVelo2) >= ev.Values[1])
                                            { bMustTranslate = true; }
                                        }
                                    }
                                }
                            }
                            break;
                        case TypeEvent.CC:
                            //[IN=CC#7:0-127]
                            //[IN=CC#7:64]
                            matchIN = Regex.Match(translate[0], "(\\[)(IN)=(CC#)(\\d{1,3})(:)(\\d{1,3})((-)(\\d{1,3}))*(\\])");
                            if (matchIN.Success)
                            {
                                string sCC = matchIN.Groups[4].Value;
                                string sVal1 = matchIN.Groups[6].Value;
                                string sVal2 = matchIN.Groups[9].Value;
                                if (sVal2.Length == 0)
                                {
                                    if (Convert.ToInt32(sCC) == ev.Values[0] && Convert.ToInt32(sVal1) == ev.Values[1])
                                    { bMustTranslate = true; }
                                }
                                else
                                {
                                    if (Convert.ToInt32(sCC) == ev.Values[0] && Convert.ToInt32(sVal1) <= ev.Values[1] && Convert.ToInt32(sVal2) >= ev.Values[1])
                                    { bMustTranslate = true; }
                                }
                            }
                            break;
                        case TypeEvent.PC:
                            //[IN=PC#0-127]
                            //[IN=PC#64]
                            matchIN = Regex.Match(translate[0], "(\\[)(IN)=(PC#)(\\d{1,3})((-)(\\d{1,3}))*(\\])");
                            if (matchIN.Success)
                            {
                                string prg1 = matchIN.Groups[4].Value;
                                string prg2 = matchIN.Groups[7].Value;
                                if (prg2.Length == 0) //un seul PRG
                                {
                                    if (Convert.ToInt32(prg1) == ev.Values[0])
                                    { bMustTranslate = true; }
                                }
                                else
                                {
                                    if (Convert.ToInt32(prg1) <= ev.Values[0] && Convert.ToInt32(prg2) >= ev.Values[0])
                                    { bMustTranslate = true; }
                                }
                            }
                            break;
                        case TypeEvent.CH_PRES:
                            matchIN = Regex.Match(translate[0], "(\\[)(IN)=(AT#)(\\d{1,3})((-)(\\d{1,3}))*(\\])");
                            if (matchIN.Success)
                            {
                                string at1 = matchIN.Groups[4].Value;
                                string at2 = matchIN.Groups[7].Value;
                                if (at2.Length == 0) //un seul PRG
                                {
                                    if (Convert.ToInt32(at1) == ev.Values[0])
                                    { bMustTranslate = true; }
                                }
                                else
                                {
                                    if (Convert.ToInt32(at1) <= ev.Values[0] && Convert.ToInt32(at2) >= ev.Values[0])
                                    { bMustTranslate = true; }
                                }
                            }
                            break;
                        case TypeEvent.SYSEX:
                            //[IN=SYS#F0...F7]
                            matchIN = Regex.Match(translate[0], "(\\[)(IN)=(SYS#)([A-f0-9]+)(\\])");
                            if (matchIN.Success)
                            {
                                string sys = matchIN.Groups[4].Value;
                                if (sys.Contains(ev.SysExData))
                                { bMustTranslate = true; }
                            }
                            break;
                    }

                    if (bMustTranslate)
                    {
                        Match matchOUT = null;
                        //lecture du message OUT qui doit être construit
                        Match mType = Regex.Match(translate[0], "(\\[)(OUT)=(SYS#|PC#|CC#|KEY#)");
                        if (mType.Success)
                        {
                            switch (mType.Groups[3].Value)
                            {
                                case "PC#":
                                    //[OUT=PC#0:0:0]
                                    //[OUT= PC#0-127:0:0]
                                    matchOUT = Regex.Match(translate[0], "(\\[)(OUT)=(PC#)(\\d{1,3})((-)(\\d{1,3}))*:(\\d{1,3}):(\\d{1,3})(\\])");
                                    string sPrg1 = matchOUT.Groups[4].Value;
                                    string sPrg2 = matchOUT.Groups[7].Value;
                                    string sMsb = matchOUT.Groups[8].Value;
                                    string sLsb = matchOUT.Groups[9].Value;
                                    if (sPrg2.Length == 0) //valeur fixée
                                    {
                                        routing.DeviceOut.SendMidiEvent(new MidiEvent(TypeEvent.CC, new List<int> { 0, Convert.ToInt32(sMsb) }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name));
                                        routing.DeviceOut.SendMidiEvent(new MidiEvent(TypeEvent.CC, new List<int> { 32, Convert.ToInt32(sLsb) }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name));
                                        routing.DeviceOut.SendMidiEvent(new MidiEvent(TypeEvent.PC, new List<int> { Convert.ToInt32(sPrg1) }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name));
                                        _eventsProcessedOUT += 3;
                                    }
                                    else //valeur IN
                                    {
                                        int iPrgValue = 0;
                                        if (ev.Type == TypeEvent.NOTE_ON) { iPrgValue = Convert.ToInt32(ev.Values[0]); }
                                        else if (ev.Type == TypeEvent.CC) { iPrgValue = Convert.ToInt32(ev.Values[1]); }
                                        else if (ev.Type == TypeEvent.PC) { iPrgValue = Convert.ToInt32(ev.Values[0]); }
                                        else if (ev.Type == TypeEvent.CH_PRES) { iPrgValue = Convert.ToInt32(ev.Values[0]); }
                                        else if (ev.Type == TypeEvent.SYSEX) { iPrgValue = 0; } //absurde.

                                        routing.DeviceOut.SendMidiEvent(new MidiEvent(TypeEvent.CC, new List<int> { 0, Convert.ToInt32(sMsb) }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name));
                                        routing.DeviceOut.SendMidiEvent(new MidiEvent(TypeEvent.CC, new List<int> { 32, Convert.ToInt32(sLsb) }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name));
                                        routing.DeviceOut.SendMidiEvent(new MidiEvent(TypeEvent.PC, new List<int> { iPrgValue }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name));
                                        _eventsProcessedOUT += 3;
                                    }
                                    break;
                                case "CC#":
                                    //[OUT=CC#64:64-127] 4,5,8
                                    //[OUT= CC#64:64]
                                    matchOUT = Regex.Match(translate[0], "(\\[)(OUT)=(CC#)(\\d{1,3}):(\\d{1,3})((-)(\\d{1,3}))*(\\])");
                                    string sCC = matchOUT.Groups[4].Value;
                                    string sValue1 = matchOUT.Groups[5].Value;
                                    string sValue2 = matchOUT.Groups[8].Value;
                                    if (sValue2.Length == 0) //la valeur est fixée
                                    {
                                        routing.DeviceOut.SendMidiEvent(new MidiEvent(TypeEvent.CC, new List<int> { Convert.ToInt32(sCC), Convert.ToInt32(sValue1) }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name));
                                        _eventsProcessedOUT += 1;
                                    }
                                    else //la valeur suit la valeur d'entrée
                                    {
                                        int iCCValue = 0;
                                        if (ev.Type == TypeEvent.NOTE_ON) { iCCValue = Convert.ToInt32(ev.Values[0]); }
                                        else if (ev.Type == TypeEvent.CC) { iCCValue = Convert.ToInt32(ev.Values[1]); }
                                        else if (ev.Type == TypeEvent.PC) { iCCValue = Convert.ToInt32(ev.Values[0]); }
                                        else if (ev.Type == TypeEvent.CH_PRES) { iCCValue = Convert.ToInt32(ev.Values[0]); }
                                        else if (ev.Type == TypeEvent.SYSEX) { iCCValue = 0; } //absurde.

                                        routing.DeviceOut.SendMidiEvent(new MidiEvent(TypeEvent.CC, new List<int> { Convert.ToInt32(sCC), iCCValue }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name));
                                        _eventsProcessedOUT += 1;
                                    }
                                    break;
                                case "KEY#":
                                    //[OUT=KEY#64:64:1000] -> fixed key, fixed velo, length  //4,8,12
                                    //[OUT=KEY#0-127:0-127:1000] -> key range, velo range, length  //4,7,8,11,12
                                    matchOUT = Regex.Match(translate[0], "(\\[)(OUT)=(KEY#)(\\d{1,3})((-)(\\d{1,3}))*:(\\d{1,3})((-)(\\d{1,3}))*:(\\d+)(\\])");
                                    string sKey1 = matchOUT.Groups[4].Value;
                                    string sKey2 = matchOUT.Groups[7].Value;
                                    string sVelo1 = matchOUT.Groups[8].Value;
                                    string sVelo2 = matchOUT.Groups[11].Value;
                                    string sLen = matchOUT.Groups[12].Value;

                                    int iNote = 0;
                                    int iVelo = 0;

                                    //NOTE FIXE
                                    if (sKey2.Length == 0) //note fixe saisie
                                    {
                                        iNote = Convert.ToInt32(sKey1);
                                        iVelo = Convert.ToInt32(sVelo1);

                                        if (sVelo2.Length == 0) //note fixe ET vélocité fixe
                                        {
                                            Task.Factory.StartNew(() =>
                                            {
                                                routing.DeviceOut.SendMidiEvent(new MidiEvent(TypeEvent.NOTE_ON, new List<int> { iNote, iVelo }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name));
                                                Thread.Sleep(Convert.ToInt32(sLen));
                                                routing.DeviceOut.SendMidiEvent(new MidiEvent(TypeEvent.NOTE_OFF, new List<int> { iNote, 0 }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name));
                                                _eventsProcessedOUT += 2;
                                            });

                                        }
                                        else //note fixe MAIS vélocité dépendante de la valeur entrante
                                        {
                                            if (ev.Type == TypeEvent.NOTE_ON) { iVelo = Convert.ToInt32(ev.Values[0]); }
                                            else if (ev.Type == TypeEvent.CC) { iVelo = Convert.ToInt32(ev.Values[1]); }
                                            else if (ev.Type == TypeEvent.PC) { iVelo = Convert.ToInt32(ev.Values[0]); }
                                            else if (ev.Type == TypeEvent.CH_PRES) { iVelo = Convert.ToInt32(ev.Values[0]); }
                                            else if (ev.Type == TypeEvent.SYSEX) { iVelo = 64; } //absurde.

                                            Task.Factory.StartNew(() =>
                                            {
                                                routing.DeviceOut.SendMidiEvent(new MidiEvent(TypeEvent.NOTE_ON, new List<int> { iNote, iVelo }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name));
                                                Thread.Sleep(Convert.ToInt32(sLen));
                                                routing.DeviceOut.SendMidiEvent(new MidiEvent(TypeEvent.NOTE_OFF, new List<int> { iNote, 0 }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name));
                                                _eventsProcessedOUT += 2;
                                            });
                                        }
                                    }
                                    else //NOTE FONCTION DES VALEURS ENTRANTES
                                    {
                                        if (sVelo2.Length == 0) //note mobile ET vélocité fixe
                                        {
                                            iVelo = Convert.ToInt32(sVelo1);

                                            if (ev.Type == TypeEvent.NOTE_ON) { iNote = Convert.ToInt32(ev.Values[0]); }
                                            else if (ev.Type == TypeEvent.CC) { iNote = Convert.ToInt32(ev.Values[1]); }
                                            else if (ev.Type == TypeEvent.PC) { iNote = Convert.ToInt32(ev.Values[0]); }
                                            else if (ev.Type == TypeEvent.CH_PRES) { iNote = Convert.ToInt32(ev.Values[0]); }
                                            else if (ev.Type == TypeEvent.SYSEX) { iNote = 64; } //absurde.

                                            Task.Factory.StartNew(() =>
                                            {
                                                routing.DeviceOut.SendMidiEvent(new MidiEvent(TypeEvent.NOTE_ON, new List<int> { iNote, iVelo }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name));
                                                Thread.Sleep(Convert.ToInt32(sLen));
                                                routing.DeviceOut.SendMidiEvent(new MidiEvent(TypeEvent.NOTE_OFF, new List<int> { iNote, 0 }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name));
                                                _eventsProcessedOUT += 2;
                                            });
                                        }
                                        else //note mobile ET vélocité mobile (un peu débile)
                                        {
                                            if (ev.Type == TypeEvent.NOTE_ON) { iNote = Convert.ToInt32(ev.Values[0]); iVelo = Convert.ToInt32(ev.Values[0]); }
                                            else if (ev.Type == TypeEvent.CC) { iNote = Convert.ToInt32(ev.Values[1]); iVelo = Convert.ToInt32(ev.Values[1]); }
                                            else if (ev.Type == TypeEvent.PC) { iNote = Convert.ToInt32(ev.Values[0]); iVelo = Convert.ToInt32(ev.Values[0]); }
                                            else if (ev.Type == TypeEvent.CH_PRES) { iNote = Convert.ToInt32(ev.Values[0]); iVelo = Convert.ToInt32(ev.Values[0]); } //absurde.
                                            else if (ev.Type == TypeEvent.SYSEX) { iNote = 64; iVelo = 64; } //absurde.

                                            Task.Factory.StartNew(() =>
                                            {
                                                routing.DeviceOut.SendMidiEvent(new MidiEvent(TypeEvent.NOTE_ON, new List<int> { iNote, iVelo }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name));
                                                Thread.Sleep(Convert.ToInt32(sLen));
                                                routing.DeviceOut.SendMidiEvent(new MidiEvent(TypeEvent.NOTE_OFF, new List<int> { iNote, 0 }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name));
                                                _eventsProcessedOUT += 2;
                                            });
                                        }
                                    }
                                    break;
                                case "SYS#":
                                    matchOUT = Regex.Match(translate[0], "(\\[)(OUT)=(SYS#)([A-f0-9]+)(\\])");
                                    string sys = matchOUT.Groups[4].Value;
                                    routing.DeviceOut.SendMidiEvent(new MidiEvent(TypeEvent.SYSEX, sys, routing.DeviceOut.Name));
                                    _eventsProcessedOUT += 1;
                                    break;
                                case "AT#":
                                    //[OUT=PC#0:0:0]
                                    //[OUT= PC#0-127:0:0]
                                    matchOUT = Regex.Match(translate[0], "(\\[)(OUT)=(AT#)(\\d{1,3})((-)(\\d{1,3}))*(\\])");
                                    string sAT1 = matchOUT.Groups[4].Value;
                                    string sAT2 = matchOUT.Groups[7].Value;
                                    if (sAT2.Length == 0) //valeur fixée
                                    {
                                        routing.DeviceOut.SendMidiEvent(new MidiEvent(TypeEvent.CH_PRES, new List<int> { Convert.ToInt32(sAT1) }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name));
                                        _eventsProcessedOUT += 1;
                                    }
                                    else //valeur IN
                                    {
                                        int iATValue = 0;
                                        if (ev.Type == TypeEvent.NOTE_ON) { iATValue = Convert.ToInt32(ev.Values[0]); }
                                        else if (ev.Type == TypeEvent.CC) { iATValue = Convert.ToInt32(ev.Values[1]); }
                                        else if (ev.Type == TypeEvent.PC) { iATValue = Convert.ToInt32(ev.Values[0]); }
                                        else if (ev.Type == TypeEvent.CH_PRES) { iATValue = Convert.ToInt32(ev.Values[0]); }
                                        else if (ev.Type == TypeEvent.SYSEX) { iATValue = 0; } //absurde.

                                        routing.DeviceOut.SendMidiEvent(new MidiEvent(TypeEvent.CH_PRES, new List<int> { iATValue }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name));
                                        _eventsProcessedOUT += 1;
                                    }
                                    break;
                            }
                        }
                    }
                }
            }

            return bMustTranslate;
        }


        #endregion

        #region PUBLIC

        public Guid AddRouting(string sDeviceIn, string sDeviceOut, int iChIn, int iChOut, MidiOptions options, MidiPreset preset = null)
        {
            var devIN = InputDevices.FirstOrDefault(d => d.Name.Equals(sDeviceIn));
            var devOUT = OutputDevices.FirstOrDefault(d => d.Name.Equals(sDeviceOut));

            if (devIN != null && UsedDevicesIN.Count(d => d.Name.Equals(sDeviceIn)) == 0)
            {
                var device = new MidiDevice(devIN);
                device.OnMidiEvent += DeviceIn_OnMidiEvent;
                UsedDevicesIN.Add(device);
            }
            if (devOUT != null && UsedDevicesOUT.Count(d => d.Name.Equals(sDeviceOut)) == 0)
            {
                var device = new MidiDevice(devOUT);
                device.OnMidiEvent += DeviceOut_OnMidiEvent;
                UsedDevicesOUT.Add(device);
            }
            //iAction : 0 = delete, 1 = adds

            //devIN != null && devOUT != null && 
            if (iChIn >= 0 && iChIn <= 16 && iChOut >= 0 && iChOut <= 16)
            {
                MidiMatrix.Add(new MatrixItem(UsedDevicesIN.FirstOrDefault(d => d.Name.Equals(sDeviceIn)), UsedDevicesOUT.FirstOrDefault(d => d.Name.Equals(sDeviceOut)), iChIn, iChOut, options, preset));

                CheckAndCloseUnusedDevices();
                //hyper important d'être à la fin !
                ChangeOptions(MidiMatrix.Last(), options, true);
                ChangeProgram(MidiMatrix.Last(), preset, true);

                var guid = MidiMatrix.Last().RoutingGuid;
                return guid;

            }
            else { return Guid.Empty; }
        }

        public bool ModifyRouting(Guid routingGuid, string sDeviceIn, string sDeviceOut, int iChIn, int iChOut, MidiOptions options, MidiPreset preset = null)
        {
            var routing = MidiMatrix.FirstOrDefault(m => m.RoutingGuid == routingGuid);

            if (routing != null)
            {
                routing.ChannelIn = iChIn;
                routing.ChannelOut = iChOut;
                bool bINChanged = routing.CheckDeviceIn(sDeviceIn);
                bool bOUTChanged = routing.CheckDeviceOut(sDeviceOut);

                if (bINChanged)
                {
                    bool active = routing.Active;
                    routing.Active = false;

                    if (!sDeviceIn.Equals(Tools.INTERNAL_GENERATOR))
                    {
                        if (sDeviceIn.Length > 0 && UsedDevicesIN.Count(d => d.Name.Equals(sDeviceIn)) == 0)
                        {
                            var device = new MidiDevice(InputDevices.FirstOrDefault(d => d.Name.Equals(sDeviceIn)));
                            device.OnMidiEvent += DeviceIn_OnMidiEvent;
                            UsedDevicesIN.Add(device);
                        }
                        routing.DeviceIn = UsedDevicesIN.FirstOrDefault(d => d.Name.Equals(sDeviceIn));
                    }

                    routing.Active = active;

                    CheckAndCloseUnusedDevices();
                }
                if (bOUTChanged)
                {
                    bool active = routing.Active;
                    routing.Active = false;

                    if (sDeviceOut.Length > 0 && UsedDevicesOUT.Count(d => d.Name.Equals(sDeviceOut)) == 0)
                    {
                        var device = new MidiDevice(OutputDevices.FirstOrDefault(d => d.Name.Equals(sDeviceOut)));
                        device.OnMidiEvent += DeviceOut_OnMidiEvent;
                        UsedDevicesOUT.Add(device);
                    }
                    routing.DeviceOut = UsedDevicesOUT.FirstOrDefault(d => d.Name.Equals(sDeviceOut));

                    routing.Active = active;

                    CheckAndCloseUnusedDevices();
                }

                //hyper important d'être à la fin !
                ChangeOptions(routing, options, false);
                ChangeProgram(routing, preset, false);

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
                RemovePendingNotes(false);
                r.Active = false;
            }
        }

        public void MuteRouting(Guid routingGuid)
        {
            var routingOn = MidiMatrix.FirstOrDefault(m => m.RoutingGuid == routingGuid);
            if (routingOn != null)
            {
                RemovePendingNotes(false);
                routingOn.Active = false;
            }
        }

        public void UnmuteRouting(Guid routingGuid)
        {
            var routingOff = MidiMatrix.FirstOrDefault(m => m.RoutingGuid == routingGuid);
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

        //public void InitRouting(Guid routingGuid, MidiOptions options, MidiPreset preset)
        //{
        //    //init des périphériques OUT
        //    var routing = MidiMatrix.FirstOrDefault(m => m.RoutingGuid == routingGuid);

        //    if (routing.DeviceOut != null) //dans le cas ou on a crée un routing sans OUT mais uniquement avec un IN
        //    {
        //        ChangeOptions(routing, options, true);
        //        ChangeProgram(routing, preset, true);
        //    }
        //}

        public bool DeleteRouting(Guid routingGuid)
        {
            var routing = MidiMatrix.FirstOrDefault(m => m.RoutingGuid == routingGuid);
            if (routing != null)
            {
                MidiMatrix.Remove(routing);
                CheckAndCloseUnusedDevices();
                return true;
            }
            else { return false; }

        }

        public void DeleteAllRouting()
        {
            foreach (var device in UsedDevicesIN)
            {
                try
                {
                    device.CloseDevice();
                    device.OnMidiEvent -= DeviceIn_OnMidiEvent;
                }
                catch { throw; }
            }
            foreach (var device in UsedDevicesOUT)
            {
                try
                {
                    device.CloseDevice();
                    device.OnMidiEvent -= DeviceOut_OnMidiEvent;
                }
                catch { throw; }
            }
            UsedDevicesIN.Clear();
            UsedDevicesOUT.Clear();

            MidiMatrix.Clear();
        }

        public bool CheckChannelUsage(Guid routingGuid, int iChannel, bool bIn)
        {
            var routing = MidiMatrix.FirstOrDefault(m => m.RoutingGuid == routingGuid);
            if (routing != null)
            {
                if (routing.DeviceOut != null && MidiMatrix.Any(r => r.DeviceOut != null && r.RoutingGuid != routing.RoutingGuid && r.DeviceOut.Name.Equals(routing.DeviceOut.Name) && r.ChannelOut == routing.ChannelOut))
                { return true; }
                else { return false; }
            }
            else { return false; }
        }

        public void SendSysEx(Guid routingGuid, InstrumentData instr)
        {
            var matrix = MidiMatrix.FirstOrDefault(d => d.RoutingGuid == routingGuid && d.DeviceOut != null);
            if (matrix != null && matrix.DeviceOut != null && instr.SysExInitializer.Length > 0)
            {
                CreateOUTEvent(new MidiDevice.MidiEvent(TypeEvent.SYSEX, instr.SysExInitializer, matrix.DeviceOut.Name), true);
            }
        }

        public void SendCC(Guid routingGuid, int iCC, int iValue, int iChannel, string sDevice)
        {
            if (MidiMatrix.Any(d => d.RoutingGuid == routingGuid && d.DeviceOut != null))
            {
                CreateOUTEvent(new MidiDevice.MidiEvent(TypeEvent.CC, new List<int> { iCC, iValue }, Tools.GetChannel(iChannel), sDevice), true);
            }
        }

        public void SendNote(Guid routingGuid, NoteGenerator note)
        {
            var routing = MidiMatrix.FirstOrDefault(d => d.RoutingGuid == routingGuid && d.DeviceOut != null);

            if (routing != null)
            {
                Task.Factory.StartNew(() =>
                {
                    int iNote = note.Note;
                    if (routing.Options.PlayNote_LowestNote)
                    {
                        iNote = LowestNoteRunning;
                    }

                    CreateOUTEvent(new MidiDevice.MidiEvent(TypeEvent.NOTE_ON, new List<int> { iNote, note.Velocity }, Tools.GetChannel(note.Channel), routing.DeviceOut.Name), true);
                    Thread.Sleep((int)(note.Length * 1000));
                    CreateOUTEvent(new MidiDevice.MidiEvent(TypeEvent.NOTE_OFF, new List<int> { iNote, note.Velocity }, Tools.GetChannel(note.Channel), routing.DeviceOut.Name), false);
                });
            }
        }

        public void SendProgramChange(Guid routingGuid, MidiPreset mp)
        {
            var routing = MidiMatrix.FirstOrDefault(d => d.RoutingGuid == routingGuid && d.DeviceOut != null);
            if (routing != null)
            {
                ChangeProgram(routing, mp, false);
            }
        }

        public void StopLog()
        {
            OnLogAdded -= MidiDevice_OnLogAdded;
        }

        public void StartLog()
        {
            OnLogAdded += MidiDevice_OnLogAdded;
        }

        public void Debug()
        {
            //4,8,9
            //var regex = Regex.Match("[IN=KEY#64:1-127]", "(\\[)(IN)=(KEY#)(\\d{1,3})((-)(\\d{1,3}))*(:)(\\d{1,3})((-)(\\d{1,3}))*(\\])");
            //4,7,8,9
            //var regex2 = Regex.Match("[IN=KEY#64-127:64]", "(\\[)(IN)=(KEY#)(\\d{1,3})((-)(\\d{1,3}))*(:)(\\d{1,3})((-)(\\d{1,3}))*(\\])");

            //MidiTranslator(null, null);
        }

        public void OpenUsedPorts()
        {
            CheckAndCloseUnusedDevices();

            foreach (var dev in UsedDevicesOUT)
            {
                dev.OpenDevice();
                dev.OnMidiEvent += DeviceOut_OnMidiEvent;
            }
        }

        public void CloseUsedPorts()
        {
            CheckAndCloseUnusedDevices();

            foreach (var dev in UsedDevicesOUT)
            {
                dev.CloseDevice();
                dev.OnMidiEvent -= DeviceOut_OnMidiEvent;
            }
        }

        public void SetClock(bool bActivated, int iBPM, string sDevice)
        {
            bool bChanged = false;

            if (ClockRunning != bActivated || ClockBPM != iBPM || !ClockDevice.Equals(sDevice))
            {
                bChanged = true;
            }

            if (bChanged)
            {
                MidiClock.Stop(); //coupure de l'horloge interne

                foreach (var device in UsedDevicesIN)  //coupure des horloges externes
                {
                    device.DisableClock();
                    device.OnMidiClockEvent -= MidiClock_OnEvent;
                }

                if (bActivated)
                {
                    var masterdevice = UsedDevicesIN.FirstOrDefault(d => d.Name.Equals(sDevice));
                    if (masterdevice == null)
                    {
                        MidiClock.Interval = 60000.0 / iBPM;
                        MidiClock.Start();
                    }
                    else
                    {
                        masterdevice.EnableClock();
                        masterdevice.OnMidiClockEvent += MidiClock_OnEvent;
                    }
                }
            }

            ClockRunning = bActivated;
            ClockBPM = iBPM;
            ClockDevice = sDevice;
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
            POLY_PRES = 8,
            CLOCK = 9
        }

        [Serializable]
        public class MidiEvent
        {
            public DateTime EventDate;
            public MidiDevice.TypeEvent Type;
            public Channel Channel;
            public List<int> Values = new List<int>();
            public string Device;
            public string SysExData;

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
        }

        public string Name { get; internal set; }

        private int MIDI_InOrOut; //IN = 1, OUT = 2

        private MidiInputDeviceEvents MIDI_InputEvents;
        private MidiOutputDeviceEvents MIDI_OutputEvents;

        internal delegate void LogEventHandler(string sDevice, bool bIn, string sLog);
        internal static event LogEventHandler OnLogAdded;

        internal delegate void MidiEventHandler(bool bIn, MidiDevice.MidiEvent ev);
        internal event MidiEventHandler OnMidiEvent;

        internal delegate void MidiClockEventHandler(object sender, ElapsedEventArgs e);
        internal event MidiClockEventHandler OnMidiClockEvent;


        internal delegate void MidiEventSequenceHandlerIN(MidiDevice.MidiEvent ev);
        internal static event MidiEventSequenceHandlerIN OnMidiSequenceEventIN;
        internal delegate void MidiEventSequenceHandlerOUT(MidiDevice.MidiEvent ev);
        internal static event MidiEventSequenceHandlerOUT OnMidiSequenceEventOUT;

        internal MidiDevice(RtMidi.Core.Devices.Infos.IMidiInputDeviceInfo inputDevice)
        {
            MIDI_InOrOut = 1;
            Name = inputDevice.Name;
            OpenDevice();
        }

        internal MidiDevice(RtMidi.Core.Devices.Infos.IMidiOutputDeviceInfo outputDevice)
        {
            MIDI_InOrOut = 2;
            Name = outputDevice.Name;
            OpenDevice();
        }

        private void MIDI_InputEvents_OnMidiEvent(MidiEvent ev)
        {
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

        internal void SendEvent(MidiDevice.MidiEvent ev)
        {
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
                        NoteOnMessage msg = new NoteOnMessage(ev.Channel, ev.GetKey(), ev.Values[1]);
                        outputDevice.Send(msg);
                    }
                    AddLog(outputDevice.Name, false, ev.Channel, "Note On", ev.Values[0].ToString(), "Velocity", ev.Values[1].ToString());
                    break;
                case TypeEvent.NOTE_OFF:
                    if (outputDevice != null && outputDevice.IsOpen)
                    {
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
                        SysExMessage msg = new SysExMessage(Tools.SendSysExData(ev.SysExData));
                        outputDevice.Send(msg);
                    }
                    AddLog(outputDevice.Name, false, 0, "SysEx", ev.SysExData, "", "");
                    break;
                case TypeEvent.CC:
                    if (outputDevice != null && outputDevice.IsOpen && ev.Values[1] > -1)
                    {
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
                if (inputDevice.IsOpen)
                {
                    inputDevice.Close();
                    AddLog(inputDevice.Name, true, Channel.Channel1, "[CLOSE]", "", "", "");
                }
                inputDevice.Dispose();

                throw ex;
            }
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

            AddEvent(TypeEvent.NOTE_ON, new List<int> { Tools.GetNoteInt(msg.Key), msg.Velocity }, msg.Channel);
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
            OnMidiEvent?.Invoke(new MidiDevice.MidiEvent(evType, values, ch, inputDevice.Name));
        }

        private void AddEventSysEx(MidiDevice.TypeEvent evType, string sData)
        {
            OnMidiEvent?.Invoke(new MidiDevice.MidiEvent(evType, sData, inputDevice.Name));
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

    public static class Tools
    {
        public static string SYSEX_CHECK = @"^(F0)([A-f0-9]*)(F7)$";
        public static string INTERNAL_GENERATOR = "Internal Generator";

        internal static int GetNoteInt(Key key)
        {
            return Convert.ToInt32(key.ToString().Substring(3));
        }

        internal static Key GetNote(int iNote)
        {
            Enum.TryParse("Key" + iNote, out RtMidi.Core.Enums.Key nt);
            return nt;
        }

        internal static Channel GetChannel(int iC)
        {
            switch (iC)
            {
                case 1:
                    return Channel.Channel1;
                case 2:
                    return Channel.Channel2;
                case 3:
                    return Channel.Channel3;
                case 4:
                    return Channel.Channel4;
                case 5:
                    return Channel.Channel5;
                case 6:
                    return Channel.Channel6;
                case 7:
                    return Channel.Channel7;
                case 8:
                    return Channel.Channel8;
                case 9:
                    return Channel.Channel9;
                case 10:
                    return Channel.Channel10;
                case 11:
                    return Channel.Channel11;
                case 12:
                    return Channel.Channel12;
                case 13:
                    return Channel.Channel13;
                case 14:
                    return Channel.Channel14;
                case 15:
                    return Channel.Channel15;
                case 16:
                    return Channel.Channel16;
                default:
                    return Channel.Channel1;
            }
        }

        internal static int GetNoteIndex(int key, int vel, MidiOptions options)
        {
            int iNote = -1;

            if (options != null)
            {
                if (key >= options.NoteFilterLow && key <= options.NoteFilterHigh
                    && (vel == 0 || (vel >= options.VelocityFilterLow && vel <= options.VelocityFilterHigh))) //attention, la vélocité d'un noteoff est souvent à 0 (dépend des devices mais généralement)
                {
                    iNote = key + options.TranspositionOffset;
                }
            }

            return iNote;
        }

        internal static string GetSysExString(byte[] data)
        {
            //on ne reçoit que les valeurs entre F0 et F7
            string sysex = BitConverter.ToString(data).Replace("-", string.Empty);
            return string.Concat("F0", sysex, "F7");
        }

        internal static byte[] SendSysExData(string sData)
        {
            //F0 ... F7 to byte[]
            string sbytes = sData[2..^2];
            var bytes = Enumerable.Range(0, sbytes.Length)
                         .Where(x => x % 2 == 0)
                         .Select(x => Convert.ToByte(sbytes.Substring(x, 2), 16))
                         .ToArray();
            return bytes;
        }
    }

    [Serializable]
    public class MidiSequence
    {
        public delegate void SequenceFinishedHandler(string sInfo);
        public event SequenceFinishedHandler SequenceFinished;

        public bool StopSequenceRequested = false;

        private List<MidiEvent> _events = new List<MidiEvent>();

        public List<MidiEvent> Events { get { return _events; } }

        public bool IsStopped = true;

        public MidiSequence()
        {

        }

        public string GetSequenceInfo()
        {
            int iTracks = 0;
            StringBuilder sbInfo = new StringBuilder();
            sbInfo.AppendLine("Sequence Status :");
            sbInfo.AppendLine(Environment.NewLine);
            var devices = _events.Select(e => e.Device).Distinct();

            foreach (var s in devices)
            {
                string sChannels = " [Ch : ";
                for (int i = 1; i <= 16; i++)
                {
                    if (_events.Count(e => e.Device.Equals(s) && e.Channel == Tools.GetChannel(i)) > 0)
                    {
                        iTracks++;
                        sbInfo.Append(i.ToString() + ", ");
                        break;
                    }
                }
                sChannels = string.Concat(sChannels.Substring(0, sChannels.Length - 2), "]");
                sbInfo.AppendLine(sChannels);
            }
            sbInfo.AppendLine(Environment.NewLine);
            sbInfo.AppendLine(string.Concat("Tracks : ", iTracks.ToString(), " (", Events.Count, " event(s))"));
            return sbInfo.ToString();
        }

        public void StartRecording(bool bIn, bool bOut)
        {
            if (bIn)
            {
                OnMidiSequenceEventIN += MidiEventSequenceHandler_OnMidiEventIN;
            }
            if (bOut)
            {
                OnMidiSequenceEventOUT += MidiEventSequenceHandler_OnMidiEventOUT;
            }

            IsStopped = false;
        }

        public void StopRecording(bool bIn, bool bOut)
        {
            if (bIn)
            {
                OnMidiSequenceEventIN -= MidiEventSequenceHandler_OnMidiEventIN;
            }
            if (bOut)
            {
                OnMidiSequenceEventOUT -= MidiEventSequenceHandler_OnMidiEventOUT;
            }

            IsStopped = true;
        }

        private void MidiEventSequenceHandler_OnMidiEventIN(MidiEvent ev)
        {
            lock (_events) { _events.Add(ev); }
        }

        private void MidiEventSequenceHandler_OnMidiEventOUT(MidiEvent ev)
        {
            lock (_events) { _events.Add(ev); }
        }

        public void Clear()
        {
            _events.Clear();
        }


        public async void PlaySequenceAsync(List<MidiEvent> events)
        {
            StopSequenceRequested = false;
            bool bPlay = true;

            //ouvrir tous les devices
            List<MidiDevice> devicesPlay = new List<MidiDevice>();
            var devices = events.Select(e => e.Device).Distinct().ToList();
            foreach (var dev in devices)
            {
                try
                {
                    devicesPlay.Add(new MidiDevice(MidiRouting.OutputDevices.FirstOrDefault(d => d.Name.Equals(dev))));
                }
                catch
                {
                    bPlay = false;
                    SequenceFinished?.Invoke("Missing MIDI Device [" + dev + "]. Can't Play Sequence.");
                }
            }

            if (bPlay)
            {
                foreach (var dev in devicesPlay)
                {
                    dev.OpenDevice();
                }

                await Task.Factory.StartNew(() =>
                {
                    PlaySequence(events, devicesPlay);
                });
            }
        }

        private void PlaySequence(List<MidiEvent> events, List<MidiDevice> devices)
        {
            int iPendingNotes = 0;

            Stopwatch stopwatch = new Stopwatch(); // Créer un chronomètre

            for (int i = 0; i < events.Count; i++)
            {
                MidiEvent eventtoplay = new MidiEvent(events[i].Type, events[i].Values, events[i].Channel, events[i].Device);

                if (StopSequenceRequested && iPendingNotes == 0)
                {
                    break; // Sortir de la boucle si l'arrêt est demandé et toutes les notes sont relâchées
                }

                var device = devices.FirstOrDefault(d => d.Name.Equals(events[i].Device));

                long elapsedTicks = 0;
                double waitingTime = 0;

                if (i < events.Count - 1)
                {
                    elapsedTicks = events[i + 1].EventDate.Ticks - events[i].EventDate.Ticks;
                    waitingTime = elapsedTicks / (double)TimeSpan.TicksPerMillisecond;
                }

                if (waitingTime > 0)
                {
                    // Utiliser le chronomètre pour attendre avec une précision supérieure à une milliseconde
                    stopwatch.Restart(); // Redémarrer le chronomètre
                    while (stopwatch.ElapsedTicks < elapsedTicks)
                    {
                        // Attente active jusqu'à ce que le temps écoulé soit égal au temps à attendre
                    }
                    stopwatch.Stop(); // Arrêter le chronomètre
                }

                if (device != null)
                {
                    if (eventtoplay.Type == TypeEvent.NOTE_ON) { iPendingNotes--; }
                    if (eventtoplay.Type == TypeEvent.NOTE_OFF) { iPendingNotes--; }

                    //Task.Factory.StartNew(() => { device.SendMidiEvent(eventtoplay); });
                    device.SendMidiEvent(eventtoplay);
                }
            }

            //for (int i = 0; i < events.Count; i++)
            //{
            //    //pour éviter des notes non relâchées
            //    if (StopSequenceRequested && iPendingNotes == 0)
            //    { break; }

            //    var device = devices.FirstOrDefault(d => d.Name.Equals(events[i].Device));

            //    double waitingTime = 0;

            //    if (i < events.Count - 1)
            //    {
            //        long elapsedTicks = events[i + 1].EventDate.Ticks - events[i].EventDate.Ticks;
            //        waitingTime = elapsedTicks / (double)TimeSpan.TicksPerMillisecond;
            //    }

            //    if (device != null)
            //    {
            //        if (events[i].Type == TypeEvent.NOTE_ON) { iPendingNotes--; }
            //        if (events[i].Type == TypeEvent.NOTE_OFF) { iPendingNotes--; }

            //        Task.Factory.StartNew(() => { device.SendMidiEvent(events[i]); });

            //        if (waitingTime > 0)
            //        {
            //            Thread.Sleep((int)waitingTime);
            //        }
            //    }
            //}

            foreach (var dev in devices)
            {
                dev.CloseDevice();
            }

            SequenceFinished?.Invoke(events.Count.ToString() + " event(s) have been played.");
        }

        public void StopSequence()
        {
            StopSequenceRequested = true;
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
        public string InstrumentGroup = "";
        public int Prg = 0;
        public int Msb = 0;
        public int Lsb = 0;
        public string PresetName = "";
        public int Channel = 1;

        public string Id { get { return string.Concat(Prg, "-", Msb, "-", Lsb); } }

        public string Tag { get { return string.Concat(Prg, "-", Msb, "-", Lsb); } }

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
        public int Level = 0;
        public int IndexInFile = 0;
        public string Raw = "";
        public List<MidiPreset> Presets { get; set; } = new List<MidiPreset>();

        public PresetHierarchy(int iIndex, string sRaw, string sCategory, int iLevel)
        {
            this.Category = sCategory;
            this.Level = iLevel;
            this.Raw = sRaw;
            this.IndexInFile = iIndex;
        }

        public PresetHierarchy()
        {

        }
    }

    [Serializable]
    public class InstrumentData
    {
        public List<PresetHierarchy> Categories { get; } = new List<PresetHierarchy>();
        public string Device { get; set; } = "";
        public string CubaseFile { get; set; } = "";
        public bool SortedByBank = false;
        public string SysExInitializer { get; set; } = "";

        public InstrumentData()
        {
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

    [Serializable]
    public class NoteGenerator
    {
        public int Velocity = 64;
        public int Note = 64;
        public int Octave = 3;
        public int Channel = 1;
        public decimal Length = 1;

        public NoteGenerator(int iChannel, int iOctave, int iNote, int iVelocity, decimal dLength)
        {
            Channel = iChannel;
            SetOctave(iOctave);
            SetNote(iNote.ToString());
            SetVelocity(iVelocity.ToString());
            SetLength(dLength);
        }

        public NoteGenerator()
        {

        }

        private void SetLength(decimal dLength)
        {
            if (dLength > 9) { Length = 9; }
            else if (dLength < 0) { Length = 0; }
            else { Length = dLength; }
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
                    Note = (iNote + (12 * Octave));
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
    }
}

