using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;
using System.Xml.Serialization;
using MessagePack;
using RtMidi.Core;
using RtMidi.Core.Devices;
using RtMidi.Core.Enums;
using RtMidi.Core.Messages;
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
            Values = CheckValues(evType, values);
            Type = evType;
            Channel = ch;
            Device = device;
        }

        private static List<int> CheckValues(TypeEvent evType, List<int> values)
        {
            if (evType == TypeEvent.NOTE_ON || evType == TypeEvent.NOTE_OFF)
            {
                if (values[0] > 127)
                { values[0] = 127; }
                else if (values[0] < 0)
                { values[0] = 0; }
            }
            else if (evType == TypeEvent.CC)
            {
                if (values[1] > 127)
                { values[1] = 127; }
                else if (values[1] < 0)
                { values[1] = 0; }
            }
            return values;
        }

        internal MidiEvent(TypeEvent evType, string sSysex, string device)
        {
            //Event = ev;
            Type = evType;
            SysExData = sSysex;
            Device = device;
        }

        public MidiEvent()
        {

        }

        internal Key GetKey()
        {
            return (Key)Values[0];
        }

        internal MidiEvent Clone()
        {
            return (MidiEvent)this.MemberwiseClone();
        }
    }

    [MessagePackObject]
    [Serializable]
    public class MessageTranslator
    {
        [Key("Name")]
        public string Name { get; set; } = "";
        [Key("OutAddsOriginalEvent")]
        public bool OutAddsOriginalEvent { get; set; } = false;
        [Key("InType")]
        public TypeEvent InType { get; set; } = TypeEvent.STOP;
        [Key("OutType")]
        public TypeEvent OutType { get; set; } = TypeEvent.STOP;
        [IgnoreDataMember]
        public bool IsReady { get { if (InType != TypeEvent.STOP && OutType != TypeEvent.STOP) { return true; } else { return false; } } }

        [Key("InData")]
        private int[] InData { get; set; }
        [Key("InSysExValue")]
        private string InSysExValue { get; set; }
        [Key("InPitchBendDirection")]
        private int InPitchBendDirection { get; set; }        //0=UP, 1=DOWN, 2=BOTH
        [Key("OutData")]
        private int[] OutData { get; set; }
        [Key("OutSysExValue")]
        private string OutSysExValue { get; set; }
        [Key("OutPitchBendDirection")]
        private int OutPitchBendDirection { get; set; }
        [Key("InIsFixedValue")]
        private bool InIsFixedValue { get; set; }
        [Key("OutIsFixedValue")]
        private bool OutIsFixedValue { get; set; }

        public MessageTranslator()
        {

        }

        public MessageTranslator(string name)
        {
            Name = name;
        }

        private int CheckData128(string sData)
        {
            int.TryParse(sData, out int idata);
            if (idata < 0) { return 0; }
            else if (idata > 127) { return 127; }
            else { return idata; }
        }

        private int CheckData16384(string sData)
        {
            int.TryParse(sData, out int idata);
            if (idata < -8192) { return -8192; }
            else if (idata > 8192) { return 8192; }
            else { return idata; }
        }

        public void IN_AddNote(string key, string velLow, string velHigh)
        {
            InType = TypeEvent.NOTE_ON;

            int lowvel = CheckData128(velLow);
            int highvel = CheckData128(velHigh);
            if (lowvel > highvel) { lowvel = 0; }

            InData = new int[4] { CheckData128(key), CheckData128(key), lowvel, highvel };
            InIsFixedValue = true;
        }

        public void IN_AddNoteRange(string keyLow, string keyHigh, string velLow, string velHigh)
        {
            InType = TypeEvent.NOTE_ON;
            int lowk = CheckData128(keyLow);
            int highk = CheckData128(keyHigh);
            int lowvel = CheckData128(velLow);
            int highvel = CheckData128(velHigh);
            if (lowk > highk) { lowk = 0; }
            if (lowvel > highvel) { lowvel = 0; }
            InData = new int[4] { lowk, highk, lowvel, highvel };
            InIsFixedValue = false;
        }

        public void IN_AddCC(string cc, string ccvalue)
        {
            InType = TypeEvent.CC;
            InData = new int[3] { CheckData128(cc), CheckData128(ccvalue), CheckData128(ccvalue) };
            InIsFixedValue = true;
        }

        public void IN_AddCCRange(string cc, string ccValueLow, string ccValueHigh)
        {
            InType = TypeEvent.CC;
            int icc = CheckData128(cc);
            int icclow = CheckData128(ccValueLow);
            int icchigh = CheckData128(ccValueHigh);

            if (icclow > icchigh) { icclow = 0; }

            InData = new int[3] { icc, icclow, icchigh };
            InIsFixedValue = false;
        }

        public void IN_AddPC(string pcvalue)
        {
            InType = TypeEvent.PC;
            InData = new int[2] { CheckData128(pcvalue), CheckData128(pcvalue) };
            InIsFixedValue = true;
        }

        public void IN_AddPCRange(string pcValueLow, string pcValueHigh)
        {
            InType = TypeEvent.PC;
            int ipclow = CheckData128(pcValueLow);
            int ipchigh = CheckData128(pcValueHigh);

            if (ipclow > ipchigh) { ipclow = 0; }

            InData = new int[2] { ipclow, ipchigh };
            InIsFixedValue = false;
        }

        public void IN_AddSysEx(string sysex)
        {
            InType = TypeEvent.SYSEX;
            InSysExValue = sysex;
            InIsFixedValue = true;
        }

        public void IN_AddPB(string sDirection, string lowpb, string highpb)
        {
            InType = TypeEvent.PB;

            int.TryParse(sDirection, out int dir);
            InPitchBendDirection = dir;

            int ipblow = CheckData16384(lowpb);
            int ipbhigh = CheckData16384(highpb);

            if (ipblow > ipbhigh) { ipblow = 0; }
            InData = new int[2] { ipblow, ipbhigh };

            InIsFixedValue = false;
        }

        public void IN_AddAT(string atvalue)
        {
            InType = TypeEvent.CH_PRES;
            InData = new int[2] { CheckData128(atvalue), CheckData128(atvalue) };
            InIsFixedValue = true;
        }

        public void IN_AddATRange(string atValueLow, string atValueHigh)
        {
            InType = TypeEvent.CH_PRES;
            int iatlow = CheckData128(atValueLow);
            int iathigh = CheckData128(atValueHigh);

            if (iatlow > iathigh) { iatlow = 0; }

            InData = new int[2] { iatlow, iathigh };
            InIsFixedValue = false;
        }

        public void OUT_AddNote(string key, string velocity, string length)
        {
            int.TryParse(velocity, out int ivelo);
            int.TryParse(length, out int ilen);

            if (ilen < 1) { ilen = 1; }
            else if (ilen > 10000) { ilen = 10000; }

            int ivelo2 = ivelo == -1 ? 127 : ivelo;
            ivelo = ivelo == -1 ? 0 : ivelo;

            OutType = TypeEvent.NOTE_ON;
            OutData = new int[5] { CheckData128(key), CheckData128(key), ivelo, ivelo2, ilen };
            InIsFixedValue = true;
        }

        public void OUT_AddNoteRange(string key, string velocity, string length)
        {
            int.TryParse(key, out int ikey);
            int.TryParse(velocity, out int ivelo);
            int.TryParse(length, out int ilen);

            if (ilen < 1) { ilen = 1; }
            else if (ilen > 10000) { ilen = 10000; }

            int ivelo2 = ivelo == -1 ? 127 : ivelo;
            ivelo = ivelo == -1 ? 0 : ivelo;

            int ikey2 = ikey == -1 ? 127 : ikey;
            ikey = ikey == -1 ? 0 : ikey;

            OutType = TypeEvent.NOTE_ON;
            OutData = new int[5] { ikey, ikey2, ivelo, ivelo2, ilen };
            InIsFixedValue = false;
        }

        public void OUT_AddCC(string cc, string ccvalue)
        {
            OutType = TypeEvent.CC;
            OutData = new int[3] { CheckData128(cc), CheckData128(ccvalue), CheckData128(ccvalue) };
            OutIsFixedValue = true;
        }

        public void OUT_AddCCRange(string cc)
        {
            OutType = TypeEvent.CC;
            OutData = new int[3] { CheckData128(cc), 0, 127 };
            OutIsFixedValue = false;
        }

        public void OUT_AddPC(string pcvalue, string msb, string lsb)
        {
            OutType = TypeEvent.PC;
            OutData = new int[4] { CheckData128(msb), CheckData128(lsb), CheckData128(pcvalue), CheckData128(pcvalue) };
            OutIsFixedValue = true;
        }

        public void OUT_AddPCRange(string msb, string lsb)
        {
            OutType = TypeEvent.PC;
            OutData = new int[4] { CheckData128(msb), CheckData128(lsb), 0, 127 };
            OutIsFixedValue = false;
        }

        public void OUT_AddSysEx(string sysex)
        {
            OutType = TypeEvent.SYSEX;
            OutSysExValue = sysex;
            OutIsFixedValue = true;
        }

        public void OUT_AddAT(string atvalue)
        {
            OutType = TypeEvent.CH_PRES;
            OutData = new int[2] { CheckData128(atvalue), CheckData128(atvalue) };
            OutIsFixedValue = true;
        }

        public void OUT_AddATRange()
        {
            OutType = TypeEvent.CH_PRES;
            OutData = new int[2] { 0, 127 };
            OutIsFixedValue = false;
        }

        public void OUT_AddPB(string sDirection, string lowpb, string highpb)
        {
            //[OUT=PB#0:0-127] //0=UP, 1=DOWN, 2=BOTH
            OutType = TypeEvent.PB;
            int.TryParse(sDirection, out int dir);
            OutPitchBendDirection = dir;

            int ipblow = CheckData16384(lowpb);
            int ipbhigh = CheckData16384(highpb);

            if (ipblow > ipbhigh) { ipblow = 0; }
            OutData = new int[2] { ipblow, ipbhigh };

            OutIsFixedValue = false;
        }

        public string Tag()
        {
            //comment identifier une différence entre plusieurs translateurs (cas du proteus 2000 ou l'on transforme une plage CC en information SYSEX)
            string sInData = "";
            string sOutData = "";

            if (InType == TypeEvent.SYSEX)
            {
                sInData = InSysExValue;
            }
            else
            {
                foreach (int val in InData)
                {
                    sInData = string.Concat(sInData, val, "-");
                }
            }
            if (OutType == TypeEvent.SYSEX)
            {
                sOutData = OutSysExValue;
            }
            else
            {
                foreach (int val in OutData)
                {
                    sOutData = string.Concat(sOutData, val, "-");
                }
            }

            return string.Concat(InType.ToString(), "-", OutType.ToString(), "-", sInData, sOutData);
        }

        public List<MidiEvent> TranslatedMessages(MidiEvent ev, int channel, MidiDevice deviceout)
        {
            List<MidiEvent> newEvents = new();

            switch (OutType)
            {
                case TypeEvent.PC:
                    if (OutIsFixedValue) //valeur fixe
                    {
                        newEvents.Add(new MidiEvent(TypeEvent.CC, new List<int> { 0, OutData[0] }, Tools.GetChannel(channel), deviceout.Name));
                        newEvents.Add(new MidiEvent(TypeEvent.CC, new List<int> { 32, OutData[1] }, Tools.GetChannel(channel), deviceout.Name));
                        newEvents.Add(new MidiEvent(TypeEvent.PC, new List<int> { OutData[2] }, Tools.GetChannel(channel), deviceout.Name));
                    }
                    else
                    {
                        int iPrgValue = 0;
                        if (ev.Type == TypeEvent.NOTE_ON) { iPrgValue = ev.Values[0]; }
                        else if (ev.Type == TypeEvent.CC) { iPrgValue = ev.Values[1]; }
                        else if (ev.Type == TypeEvent.PC) { iPrgValue = ev.Values[0]; }
                        else if (ev.Type == TypeEvent.CH_PRES) { iPrgValue = ev.Values[0]; }
                        else if (ev.Type == TypeEvent.SYSEX) { iPrgValue = 0; } //absurde.
                        else if (ev.Type == TypeEvent.PB)
                        {
                            if (InPitchBendDirection == 0)
                            { iPrgValue = ev.Values[0] / 128; }
                            else if (InPitchBendDirection == 1)
                            { iPrgValue = ev.Values[0] / 64; }
                            else //0=64, 8192=128, -9192=0
                            { iPrgValue = (ev.Values[0] + 8192) / 256; }
                        }

                        newEvents.Add(new MidiEvent(TypeEvent.CC, new List<int> { 0, OutData[0] }, Tools.GetChannel(channel), deviceout.Name));
                        newEvents.Add(new MidiEvent(TypeEvent.CC, new List<int> { 32, OutData[1] }, Tools.GetChannel(channel), deviceout.Name));
                        newEvents.Add(new MidiEvent(TypeEvent.PC, new List<int> { iPrgValue }, Tools.GetChannel(channel), deviceout.Name));
                    }
                    break;
                case TypeEvent.CC:
                    if (OutIsFixedValue) //valeur fixe
                    {
                        newEvents.Add(new MidiEvent(TypeEvent.CC, new List<int> { Convert.ToInt32(OutData[0]), OutData[1] }, Tools.GetChannel(channel), deviceout.Name));
                    }
                    else
                    {
                        int iCCValue = 0;
                        if (ev.Type == TypeEvent.NOTE_ON) { iCCValue = ev.Values[0]; }
                        else if (ev.Type == TypeEvent.CC) { iCCValue = ev.Values[1]; }
                        else if (ev.Type == TypeEvent.PC) { iCCValue = ev.Values[0]; }
                        else if (ev.Type == TypeEvent.CH_PRES) { iCCValue = ev.Values[0]; }
                        else if (ev.Type == TypeEvent.SYSEX) { iCCValue = 0; } //absurde.
                        else if (ev.Type == TypeEvent.PB)
                        {
                            if (InPitchBendDirection == 0)
                            { iCCValue = ev.Values[0] / 128; }
                            else if (InPitchBendDirection == 1)
                            { iCCValue = ev.Values[0] / 64; }
                            else //0=64, 8192=128, -9192=0
                            { iCCValue = (ev.Values[0] + 8192) / 256; }
                        }

                        newEvents.Add(new MidiEvent(TypeEvent.CC, new List<int> { Convert.ToInt32(OutData[0]), iCCValue }, Tools.GetChannel(channel), deviceout.Name));
                    }
                    break;
                case TypeEvent.NOTE_ON:
                case TypeEvent.NOTE_OFF:
                    if (OutIsFixedValue) //valeur fixe
                    {
                        int iNote = OutData[0];
                        int iVelo = OutData[2];

                        if (OutData[2] == OutData[3]) //note fixe ET vélocité fixe
                        {
                            newEvents.Add(new MidiEvent(TypeEvent.NOTE_ON, new List<int> { iNote, iVelo }, Tools.GetChannel(channel), deviceout.Name));
                            newEvents.Last().Delay = OutData[4];
                            newEvents.Add(new MidiEvent(TypeEvent.NOTE_OFF, new List<int> { iNote, 0 }, Tools.GetChannel(channel), deviceout.Name));

                        }
                        else //note fixe MAIS vélocité dépendante de la valeur entrante
                        {
                            if (ev.Type == TypeEvent.NOTE_ON) { iVelo = ev.Values[0]; }
                            else if (ev.Type == TypeEvent.CC) { iVelo = ev.Values[1]; }
                            else if (ev.Type == TypeEvent.PC) { iVelo = ev.Values[0]; }
                            else if (ev.Type == TypeEvent.CH_PRES) { iVelo = ev.Values[0]; }
                            else if (ev.Type == TypeEvent.SYSEX) { iVelo = 64; } //absurde.
                            else if (ev.Type == TypeEvent.PB)
                            {
                                if (InPitchBendDirection == 0)
                                { iVelo = ev.Values[0] / 128; }
                                else if (InPitchBendDirection == 1)
                                { iVelo = ev.Values[0] / 64; }
                                else //0=64, 8192=128, -9192=0
                                { iVelo = (ev.Values[0] + 8192) / 256; }
                            }

                            newEvents.Add(new MidiEvent(TypeEvent.NOTE_ON, new List<int> { iNote, iVelo }, Tools.GetChannel(channel), deviceout.Name));
                            newEvents.Last().Delay = OutData[4];
                            newEvents.Add(new MidiEvent(TypeEvent.NOTE_OFF, new List<int> { iNote, 0 }, Tools.GetChannel(channel), deviceout.Name));
                        }
                    }
                    else
                    {
                        if (OutData[2] == OutData[3]) //note mobile ET vélocité fixe
                        {
                            int iNote = 0;
                            int iVelo = OutData[2];

                            if (ev.Type == TypeEvent.NOTE_ON) { iNote = ev.Values[0]; }
                            else if (ev.Type == TypeEvent.CC) { iNote = ev.Values[1]; }
                            else if (ev.Type == TypeEvent.PC) { iNote = ev.Values[0]; }
                            else if (ev.Type == TypeEvent.CH_PRES) { iNote = ev.Values[0]; }
                            else if (ev.Type == TypeEvent.SYSEX) { iNote = 64; } //absurde.
                            else if (ev.Type == TypeEvent.PB)
                            {
                                if (InPitchBendDirection == 0)
                                { iNote = ev.Values[0] / 128; }
                                else if (InPitchBendDirection == 1)
                                { iNote = ev.Values[0] / 64; }
                                else //0=64, 8192=128, -9192=0
                                { iNote = (ev.Values[0] + 8192) / 256; }
                            }

                            newEvents.Add(new MidiEvent(TypeEvent.NOTE_ON, new List<int> { iNote, iVelo }, Tools.GetChannel(channel), deviceout.Name));
                            newEvents.Last().Delay = OutData[4];
                            newEvents.Add(new MidiEvent(TypeEvent.NOTE_OFF, new List<int> { iNote, 0 }, Tools.GetChannel(channel), deviceout.Name));
                        }
                        else //note mobile ET vélocité mobile (un peu débile)
                        {
                            int iNote = 0;
                            int iVelo = 0;

                            if (ev.Type == TypeEvent.NOTE_ON) { iNote = ev.Values[0]; iVelo = ev.Values[0]; }
                            else if (ev.Type == TypeEvent.CC) { iNote = ev.Values[1]; iVelo = ev.Values[1]; }
                            else if (ev.Type == TypeEvent.PC) { iNote = ev.Values[0]; iVelo = ev.Values[0]; }
                            else if (ev.Type == TypeEvent.CH_PRES) { iNote = ev.Values[0]; iVelo = ev.Values[0]; } //absurde.
                            else if (ev.Type == TypeEvent.SYSEX) { iNote = 64; iVelo = 64; } //absurde.
                            else if (ev.Type == TypeEvent.PB)
                            {
                                if (InPitchBendDirection == 0)
                                { iNote = ev.Values[0] / 128; iVelo = ev.Values[0] / 128; }
                                else if (InPitchBendDirection == 1)
                                { iNote = ev.Values[0] / 64; iVelo = ev.Values[0] / 64; }
                                else //0=64, 8192=128, -9192=0
                                { iNote = (ev.Values[0] + 8192) / 256; iVelo = (ev.Values[0] + 8192) / 256; }
                            }

                            newEvents.Add(new MidiEvent(TypeEvent.NOTE_ON, new List<int> { iNote, iVelo }, Tools.GetChannel(channel), deviceout.Name));
                            newEvents.Last().Delay = OutData[4];
                            newEvents.Add(new MidiEvent(TypeEvent.NOTE_OFF, new List<int> { iNote, 0 }, Tools.GetChannel(channel), deviceout.Name));
                        }
                    }
                    break;
                case TypeEvent.SYSEX:
                    newEvents.Add(new MidiEvent(TypeEvent.SYSEX, OutSysExValue, deviceout.Name));
                    break;
                case TypeEvent.CH_PRES:
                    if (OutIsFixedValue)
                    {
                        newEvents.Add(new MidiEvent(TypeEvent.CH_PRES, new List<int> { Convert.ToInt32(OutData[0]) }, Tools.GetChannel(channel), deviceout.Name));
                    }
                    else
                    {
                        int iATValue = 0;
                        if (ev.Type == TypeEvent.NOTE_ON) { iATValue = ev.Values[0]; }
                        else if (ev.Type == TypeEvent.CC) { iATValue = ev.Values[1]; }
                        else if (ev.Type == TypeEvent.PC) { iATValue = ev.Values[0]; }
                        else if (ev.Type == TypeEvent.CH_PRES) { iATValue = ev.Values[0]; }
                        else if (ev.Type == TypeEvent.SYSEX) { iATValue = 0; } //absurde.
                        else if (ev.Type == TypeEvent.PB)
                        {
                            if (InPitchBendDirection == 0)
                            { iATValue = ev.Values[0] / 128; }
                            else if (InPitchBendDirection == 1)
                            { iATValue = ev.Values[0] / 64; }
                            else //0=64, 8192=128, -9192=0
                            { iATValue = (ev.Values[0] + 8192) / 256; }
                        }

                        newEvents.Add(new MidiEvent(TypeEvent.CH_PRES, new List<int> { iATValue }, Tools.GetChannel(channel), deviceout.Name));
                    }
                    break;
                case TypeEvent.PB:
                    int iPBValue = 0;
                    if (ev.Type == TypeEvent.NOTE_ON) { iPBValue = ev.Values[0]; }
                    else if (ev.Type == TypeEvent.CC) { iPBValue = ev.Values[1]; }
                    else if (ev.Type == TypeEvent.PC) { iPBValue = ev.Values[0]; }
                    else if (ev.Type == TypeEvent.CH_PRES) { iPBValue = ev.Values[0]; }
                    else if (ev.Type == TypeEvent.SYSEX) { iPBValue = 0; } //absurde.
                    else if (ev.Type == TypeEvent.PB)
                    {
                        if (InPitchBendDirection == 0)
                        { iPBValue = ev.Values[0] / 128; }
                        else if (InPitchBendDirection == 1)
                        { iPBValue = ev.Values[0] / 64; }
                        else //0=64, 8192=128, -9192=0
                        { iPBValue = (ev.Values[0] + 8192) / 256; }
                    }
                    if (OutPitchBendDirection == 0)
                    {
                        iPBValue *= 128;
                    }
                    else if (OutPitchBendDirection == 1)
                    {
                        iPBValue *= 64;
                    }
                    else
                    {
                        iPBValue = iPBValue >= 64 ? iPBValue * 128 : iPBValue * 64;
                    }

                    newEvents.Add(new MidiEvent(TypeEvent.PB, new List<int> { iPBValue }, Tools.GetChannel(channel), deviceout.Name));

                    break;
            }

            if (OutAddsOriginalEvent)
            {
                if (ev.Type == TypeEvent.SYSEX)
                {
                    newEvents.Add(new MidiEvent(ev.Type, ev.SysExData, deviceout.Name));
                }
                else
                {
                    newEvents.Add(new MidiEvent(ev.Type, ev.Values, Tools.GetChannel(channel), deviceout.Name));
                }
            }

            return newEvents;
        }

        public bool CanTranslate(MidiEvent ev)
        {
            bool bMustTranslate = false;

            if (ev.Type == InType)
            {
                switch (ev.Type)
                {
                    case TypeEvent.NOTE_ON:
                    case TypeEvent.NOTE_OFF:
                        if (InIsFixedValue && ev.Values[0] == InData[0])
                        {
                            if (ev.Values[1] >= InData[2] && ev.Values[1] <= InData[3])
                            {
                                bMustTranslate = true;
                            }
                        }
                        else if (!InIsFixedValue && ev.Values[0] >= InData[0] && ev.Values[0] <= InData[1])
                        {
                            if (ev.Values[1] >= InData[2] && ev.Values[1] <= InData[3])
                            {
                                bMustTranslate = true;
                            }
                        }
                        break;
                    case TypeEvent.CC:
                        if (ev.Values[0] == InData[0] && ev.Values[1] >= InData[1] && ev.Values[1] <= InData[2])
                        {
                            bMustTranslate = true;
                        }
                        break;
                    case TypeEvent.PC:
                        if (ev.Values[0] >= InData[0] && ev.Values[0] <= InData[1])
                        {
                            bMustTranslate = true;
                        }
                        break;
                    case TypeEvent.CH_PRES:
                        if (ev.Values[0] >= InData[0] && ev.Values[0] <= InData[1])
                        {
                            bMustTranslate = true;
                        }
                        break;
                    case TypeEvent.PB:
                        //le message arrive entre 0 et 16384 et le translator a -8192 -> 8192 
                        if (ev.Values[0] <= 8192 && (InPitchBendDirection == 1 || InPitchBendDirection == 2))   //0=UP, 1=DOWN, 2=BOTH
                        {
                            bMustTranslate = true;
                        }
                        else if (ev.Values[0] >= 8192 && (InPitchBendDirection == 0 || InPitchBendDirection == 2))
                        {
                            bMustTranslate = true;
                        }
                        break;
                    case TypeEvent.SYSEX:
                        if (ev.SysExData.Equals(InSysExValue, StringComparison.OrdinalIgnoreCase))
                        {
                            bMustTranslate = true;
                        }
                        break;
                }
            }

            return bMustTranslate;
        }

        public override string ToString()
        {
            return Name.Length > 0 ? Name : string.Concat(InType.ToString(), " -> ", OutType.ToString());
        }
    }

    public class TrackerGuid
    {
        public ulong Part1 { get; private set; }
        public ulong Part2 { get; private set; }

        public TrackerGuid()
        {
            GenerateNewGuid();
        }

        private void GenerateNewGuid()
        {
            string guidString = Guid.NewGuid().ToString("N");

            guidString = guidString.Replace("-", "");
            if (guidString.Length != 32)
                throw new ArgumentException("Invalid GUID format", nameof(guidString));

            Part1 = ulong.Parse(guidString[..16], System.Globalization.NumberStyles.HexNumber);
            Part2 = ulong.Parse(guidString[16..], System.Globalization.NumberStyles.HexNumber);
        }

        public void Clear()
        {
            Part1 = 0;
            Part2 = 0;
        }

        public override string ToString()
        {
            if (Part1 == 0 && Part2 == 0)
            {
                return "";
            }
            else
            {
                return $"{Part1:X16}-{Part2:X16}";
            }
        }
    }


    public static class Tools
    {
        public readonly static int[] CCToNotBlock = new int[16] { 0, 6, 32, 64, 65, 66, 67, 68, 120, 121, 122, 123, 124, 125, 126, 127 };

        public static string SYSEX_CHECK = @"^(F0)([A-f0-9]*)(F7)$";
        public static string INTERNAL_GENERATOR = "Internal Note Generator";
        public static string INTERNAL_SEQUENCER = "Internal Sequencer";
        public static string ALL_INPUTS = "All Inputs";

        public static string VST_HOST = "VST 2 Internal Host";

        public static string MidiNoteNumberToNoteName(int noteNumber)
        {
            string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

            int octave = (noteNumber / 12) - 1;
            int noteIndex = noteNumber % 12;

            string noteName = noteNames[noteIndex];
            return noteName + octave;
        }

        internal static int GetNoteInt(Key key)
        {
            return (int)key;
        }

        internal static int GetChannelInt(Channel ch)
        {
            return (int)ch + 1;
        }

        internal static Key GetNote(int iNote)
        {
            return (Key)iNote;
        }

        internal static Channel GetChannel(int iC)
        {
            return (Channel)iC - 1;
        }

        internal static int[] GetNoteIndex(int key, int vel, MatrixItem routing, bool bNoteOff)
        {
            int iNote = -1;

            if (routing.Options != null)
            {
                if (routing.Options.CompressVelocityRange)
                {
                    if (vel > routing.Options.VelocityFilterHigh)
                    { vel = routing.Options.VelocityFilterHigh; }
                    else if (vel < routing.Options.VelocityFilterLow)
                    { vel = routing.Options.VelocityFilterLow; }
                }

                if (routing.Options.TransposeNoteRange)
                {
                    if (key < routing.Options.NoteFilterLow)
                    {
                        while (key < routing.Options.NoteFilterLow)
                        {
                            key += 12;
                        }
                    }
                    else if (key > routing.Options.NoteFilterHigh)
                    {
                        while (key > routing.Options.NoteFilterHigh)
                        {
                            key -= 12;
                        }
                    }
                }

                if (bNoteOff)
                {
                    iNote = key + routing.Options.TranspositionOffset;
                    //if (!routing.DeviceOut.GetLiveNOTEValue(routing.ChannelOut, iNote))
                    //{
                    //    iNote = -1;
                    //}   
                }
                else
                {
                    if (key >= routing.Options.NoteFilterLow && key <= routing.Options.NoteFilterHigh && (vel == 0 || (vel >= routing.Options.VelocityFilterLow && vel <= routing.Options.VelocityFilterHigh))) //attention, la vélocité d'un noteoff est souvent à 0 (dépend des devices mais généralement)
                    {
                        iNote = key + routing.Options.TranspositionOffset;
                    }
                }
            }

            return new int[] { iNote, vel };
        }

        internal static string GetSysExString(byte[] data)
        {
            //on ne reçoit que les valeurs entre F0 et F7
            string sysex = BitConverter.ToString(data).Replace("-", string.Empty);
            return string.Concat("F0", sysex, "F7");
        }

        internal static List<byte[]> SendSysExData(string sData)
        {
            List<byte[]> list = new();
            List<string> sMessages = new();
            //F0 ... F7 to byte[]
            //on va envoyer les différents messages par paquets séparés
            StringBuilder sbSysEx = new();
            if (string.IsNullOrEmpty(sData))
            {

            }
            else
            {
                for (int i = 0; i <= sData.Length - 2; i += 2)
                {
                    var sys = sData.Substring(i, 2);
                    if (!sys.Equals("F7", StringComparison.OrdinalIgnoreCase))
                    {
                        sbSysEx.Append(sys);
                    }
                    else
                    {
                        sMessages.Add(sbSysEx.ToString());
                        sbSysEx.Clear();
                    }
                }

                foreach (var sysex in sMessages)
                {
                    var hex = sysex[2..];

                    if (hex.Length > 0)
                    {
                        var bytes = Enumerable.Range(0, hex.Length / 2)
                          .Select(x => Convert.ToByte(hex.Substring(x * 2, 2), 16))
                          .ToArray();

                        list.Add(bytes);
                    }
                }
            }

            return list;
        }

        internal static long GetMidiClockInterval(int iTempo)
        {

            // Nombre de MIDI Clocks par quart de note
            int midiClocksPerQuarterNote = 24;

            // Calcul du délai entre chaque MIDI Clock en microsecondes
            double microsecondsPerQuarterNote = 60000000.0 / iTempo;
            double microsecondsPerMIDIClock = microsecondsPerQuarterNote / midiClocksPerQuarterNote;

            // Conversion du délai en millisecondes
            //double millisecondsPerMIDIClock = microsecondsPerMIDIClock / 1000;

            return (long)microsecondsPerMIDIClock;
        }

        public static long GetMidiClockIntervalLong(int iTempo, string sQuantization)
        {
            double dDiviseur = 4.0;
            int iQt;

            if (sQuantization.EndsWith("T", StringComparison.OrdinalIgnoreCase))
            {
                dDiviseur = 3.0;
                iQt = Convert.ToInt32(sQuantization[0..^1]);
            }
            else
            {
                iQt = Convert.ToInt32(sQuantization);
            }

            // Durée d'une noire en millisecondes
            double dureeNoireMs = 60000000.0 / iTempo;

            // Nombre de noires par mesure (quantification)
            int noiresParMesure = iQt;

            // Durée d'une mesure en millisecondes
            //double dureeMesureMs = dureeNoireMs * noiresParMesure;
            double dureeMesureMs = dureeNoireMs * (dDiviseur / noiresParMesure);

            // Calcul de l'intervalle du timer en millisecondes
            double intervalleTimerMs = dureeMesureMs; // On prend un quart de la mesure

            return (long)intervalleTimerMs;
        }

        internal static int FindIndexIn2dArray(int iChannel, bool[,] notesSentForPanic, bool bHighest)
        {
            if (bHighest)
            {
                for (int i = 127; i >= 0; i--)
                {
                    if (notesSentForPanic[iChannel, i]) { return i; }
                }
            }
            else
            {
                for (int i = 0; i < 128; i++)
                {
                    if (notesSentForPanic[iChannel, i]) { return i; }
                }
            }
            return -1;
        }
    }

}

