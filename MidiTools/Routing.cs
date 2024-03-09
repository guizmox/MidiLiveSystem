using RtMidi.Core.Messages;
using RtMidi.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static MidiTools.MidiDevice;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;
using System.Threading;
using RtMidi.Core.Enums;

namespace MidiTools
{

    [Serializable]
    internal class MatrixItem
    {
        private int[] CCToNotBlock = new int[16] { 0, 6, 32, 64, 65, 66, 67, 68, 120, 121, 122, 123, 124, 125, 126, 127 };

        private int[] MinorChordsIntervals = new int[32] { 9, -4, -3, -4, -4, -5, -4, 4, -5, 9, -5, -3, 5, 4, -9, 5, -7, -5, -2, -1, -2, 23, -5, 21, 21, -4, -4, 19, 19, -3, -100, -100 };
        private int[] MajorChordsIntervals = new int[32] { -4, -3, -4, -6, 7, -4, -5, -4, -3, 7, -3, -5, -7, 3, 5, -9, -3, 19, -2, -2, -2, 2, 19, -4, 20, -3, -4, 21, 21, -5, -3, 21 };
        private int[] OtherIntervals = new int[32] { -3, -3, -5, -2, -2, -4, -2, -5, -6, -3, -5, -5, 19, -5, -6, 21, -2, 20, -3, -6, 18, -3, -2, 19, -100, -100, -100, -100, -100, -100, -100, -100 };

        private Harmony _harmony = Harmony.MAJOR;
        private int _noteHarmony = 0;

        public int CurrentATValue = 0;
        public bool[] NotesSentForPanic = new bool[128];
        public List<int> CurrentNotesPlayed
        {
            get
            {
                List<int> list = new List<int>();
                for (int i = 0; i < 128; i++)
                {
                    if (NotesSentForPanic[i])
                    { list.Add(i); }
                }
                return list;
            }
        }

        internal int ChannelIn = 1;
        internal int ChannelOut = 0;

        internal List<LiveCC> LiveData = new List<LiveCC>();
        internal int LiveProgram = 0;

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

        public MatrixItem()
        {

        }

        internal void SetRoutingGuid(Guid guid)
        {
            RoutingGuid = guid;
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

        internal List<MidiEvent> SetCC(int iCC, int iCCValue, Channel channel, string sDevice, bool bSmooth)
        {
            //int iChannel = Tools.GetChannelInt(channel);
            List<MidiEvent> intermediate = new List<MidiEvent>();

            var cc = LiveData.FirstOrDefault(i => i.Device.Equals(sDevice) && i.Channel == channel && i.CC == iCC);
            if (cc == null)
            {
                LiveData.Add(new LiveCC(sDevice, channel, iCC, iCCValue));
            }
            else
            {
                if (bSmooth && !CCToNotBlock.Contains(cc.CC))
                {
                    if ((cc.CCValue + 16) < iCCValue)
                    {
                        for (int i = cc.CCValue; i < iCCValue; i += 2)
                        {
                            intermediate.Add(new MidiEvent(TypeEvent.CC, new List<int> { cc.CC, i }, channel, sDevice));
                        }
                        cc.BlockIncoming = true;
                    }
                    else if ((cc.CCValue - 16) > iCCValue)
                    {
                        for (int i = cc.CCValue; i > iCCValue; i -= 2)
                        {
                            intermediate.Add(new MidiEvent(TypeEvent.CC, new List<int> { cc.CC, i }, channel, sDevice));
                        }
                        cc.BlockIncoming = true;
                    }
                }

                cc.CCValue = iCCValue;
            }

            return intermediate;
        }

        internal int RandomizeCCValue(int iCC, Channel channel, string sDevice, int iMax)
        {
            var cc = LiveData.FirstOrDefault(i => i.Device.Equals(sDevice) && i.Channel == channel && i.CC == iCC);
            if (cc != null)
            {
                Random random = new Random();
                int iVariation = random.Next(-iMax, iMax);
                int iNewValue = cc.CCValue + iVariation;

                if (iNewValue > 0 && iNewValue < 127)
                {
                    return iNewValue;
                }
                else { return 0; }
            }
            else { return 0; }
        }

        internal void SetCurrentProgram(int iProgram)
        {
            LiveProgram = iProgram;
        }

        internal void UnblockCC(int iCC, Channel channel, string sDevice)
        {
            var cc = LiveData.FirstOrDefault(i => i.Device.Equals(sDevice) && i.Channel == channel && i.CC == iCC);
            if (cc != null)
            {
                cc.BlockIncoming = false;
            }
        }

        internal bool IsBlocked(int iCC, int iCCValue, Channel channel, string sDevice)
        {
            var cc = LiveData.FirstOrDefault(i => i.Device.Equals(sDevice) && i.Channel == channel && i.CC == iCC);
            if (cc != null)
            {
                return cc.BlockIncoming;
            }
            else { return false; }
        }

        internal void UnblockAllCC()
        {
            foreach (var item in LiveData)
            {
                item.BlockIncoming = false;
            }
        }

        internal List<MidiEvent> SetPlayMono(int iHigh, MidiEvent incomingEV)
        {
            //NotesSentForPanic[incomingEV.Values[0]] = true;

            //1 = high, 2=low, 3=intermediate high, 4=intermediate low
            List<MidiEvent> eventsOUT = new List<MidiEvent>();
            int iPlayedNotes = NotesSentForPanic.Count(n => n == true);

            //en partant du haut, recherche un groupe de 3 notes jouées sur moins de 2 octaves et isoler celle du milieu ?
            Random r = new Random();
            int iRnd = r.Next(0, iPlayedNotes <= 2 ? 0 : iPlayedNotes - 2);
            int iAvoidNotes = 1 + iRnd;

            if (incomingEV.Type == TypeEvent.NOTE_ON)
            {
                switch (iHigh)
                {
                    case 1:
                        int iHighestNote = Array.LastIndexOf(NotesSentForPanic, true);
                        if (incomingEV.Values[0] >= iHighestNote || iHighestNote == -1)
                        {
                            if (iHighestNote > -1)
                            {
                                eventsOUT.Add(new MidiEvent(TypeEvent.NOTE_OFF, new List<int> { iHighestNote, 0 }, incomingEV.Channel, incomingEV.Device));
                                NotesSentForPanic[iHighestNote] = false;
                            }

                            eventsOUT.Add(incomingEV);
                            NotesSentForPanic[incomingEV.Values[0]] = true;
                        }
                        else
                        {
                            eventsOUT.Add(new MidiEvent(TypeEvent.NOTE_OFF, new List<int> { incomingEV.Values[0], 0 }, incomingEV.Channel, incomingEV.Device));
                        }
                        break;
                    case 2:
                        int iLowestNote = Array.IndexOf(NotesSentForPanic, true);
                        if (incomingEV.Values[0] <= iLowestNote || iLowestNote == -1)
                        {
                            if (iLowestNote > -1)
                            {
                                eventsOUT.Add(new MidiEvent(TypeEvent.NOTE_OFF, new List<int> { iLowestNote, 0 }, incomingEV.Channel, incomingEV.Device));
                                NotesSentForPanic[iLowestNote] = false;
                            }

                            eventsOUT.Add(incomingEV);
                            NotesSentForPanic[incomingEV.Values[0]] = true;
                        }
                        else
                        {
                            eventsOUT.Add(new MidiEvent(TypeEvent.NOTE_OFF, new List<int> { incomingEV.Values[0], 0 }, incomingEV.Channel, incomingEV.Device));
                        }
                        break;
                    case 3:  //note intermédiaire en partant du haut des notes jouées du clavier
                        NotesSentForPanic[incomingEV.Values[0]] = true;
                        if (iPlayedNotes > 2)
                        {
                            int iMediumNoteHigh = incomingEV.Values[0];
                            int iCntA = 0;
                            for (int i = NotesSentForPanic.Length - 1; i > 0; i--)
                            {
                                if (NotesSentForPanic[i] && iCntA < iAvoidNotes)
                                {
                                    iCntA++;
                                }
                                else if (NotesSentForPanic[i] && iCntA == iAvoidNotes)
                                {
                                    iMediumNoteHigh = i;
                                    break;
                                }
                            }
                            if (incomingEV.Values[0] == iMediumNoteHigh)
                            {
                                eventsOUT.Add(new MidiEvent(TypeEvent.NOTE_OFF, new List<int> { iMediumNoteHigh, 0 }, incomingEV.Channel, incomingEV.Device));
                                NotesSentForPanic[iMediumNoteHigh] = false;

                                eventsOUT.Add(incomingEV);
                                NotesSentForPanic[incomingEV.Values[0]] = true;
                            }
                        }
                        else
                        {
                            eventsOUT.Add(new MidiEvent(TypeEvent.NOTE_OFF, new List<int> { incomingEV.Values[0], 0 }, incomingEV.Channel, incomingEV.Device));
                        }
                        break;
                    case 4: //note intermédiaire en partant du bas des notes jouées du clavier              
                        NotesSentForPanic[incomingEV.Values[0]] = true;
                        if (iPlayedNotes > 2)
                        {
                            int iMediumNoteLow = incomingEV.Values[0];
                            int iCntB = 0;
                            for (int i = 0; i < NotesSentForPanic.Length - 1; i++)
                            {
                                if (NotesSentForPanic[i] && iCntB < iAvoidNotes)
                                {
                                    iCntB++;
                                }
                                else if (NotesSentForPanic[i] && iCntB == iAvoidNotes)
                                {
                                    iMediumNoteLow = i;
                                    break;
                                }
                            }
                            if (incomingEV.Values[0] == iMediumNoteLow)
                            {
                                eventsOUT.Add(new MidiEvent(TypeEvent.NOTE_OFF, new List<int> { iMediumNoteLow, 0 }, incomingEV.Channel, incomingEV.Device));
                                NotesSentForPanic[iMediumNoteLow] = false;

                                eventsOUT.Add(incomingEV);
                                NotesSentForPanic[incomingEV.Values[0]] = true;
                            }
                        }
                        else
                        {
                            eventsOUT.Add(new MidiEvent(TypeEvent.NOTE_OFF, new List<int> { incomingEV.Values[0], 0 }, incomingEV.Channel, incomingEV.Device));
                        }
                        break;
                    case 5: //note intermédiaire basée sur la moins représentée dans toutes les notes jouées, si possible 
                            //la faire jouer dans un trou pour remplir le spectre comme un alto ?
                        NotesSentForPanic[incomingEV.Values[0]] = true;

                        var notes = CurrentNotesPlayed;
                        int octavehole = 0;
                        int[] bChord = new int[notes.Count];

                        for (int i = 0; i < notes.Count - 1; i++)
                        {
                            bChord[i] = notes[i] % 12;

                            if (notes[i + 1] >= notes[i] + 7) //espace disponible d'une octave
                            {
                                octavehole = notes[i] / 12;
                            }
                        }

                        if (_noteHarmony > 0)
                        {
                            eventsOUT.Add(new MidiEvent(TypeEvent.NOTE_OFF, new List<int> { _noteHarmony, incomingEV.Values[1] }, incomingEV.Channel, incomingEV.Device));
                            _noteHarmony = 0;
                        }

                        if (octavehole > 0 && notes.Count >= 3)
                        {
                            //recherche de l'occurence la moins présente dans les notes
                            _noteHarmony = FindLessUsedNote(bChord) + (12 * octavehole);

                            eventsOUT.Add(new MidiEvent(TypeEvent.NOTE_ON, new List<int> { _noteHarmony, incomingEV.Values[1] }, incomingEV.Channel, incomingEV.Device));
                            //NotesSentForPanic[_noteHarmony] = true;
                        }
                        break;
                }
            }
            else
            {
                if (_noteHarmony > 0)
                {
                    eventsOUT.Add(new MidiEvent(TypeEvent.NOTE_OFF, new List<int> { _noteHarmony, incomingEV.Values[1] }, incomingEV.Channel, incomingEV.Device));
                    _noteHarmony = 0;
                }
                eventsOUT.Add(new MidiEvent(incomingEV.Type, new List<int> { incomingEV.Values[0], incomingEV.Values[1] }, incomingEV.Channel, incomingEV.Device));
                NotesSentForPanic[incomingEV.Values[0]] = false;
            }

            return eventsOUT;
        }

        internal static int FindLessUsedNote(int[] list)
        {
            Dictionary<int, int> occurrenceCount = new Dictionary<int, int>();

            // Parcourir la liste et compter les occurrences
            foreach (int num in list)
            {
                if (occurrenceCount.ContainsKey(num))
                {
                    occurrenceCount[num]++;
                }
                else
                {
                    occurrenceCount[num] = 1;
                }
            }

            int minOccurrence = occurrenceCount.Values.Min();
            int leastFrequentNumber = occurrenceCount.FirstOrDefault(x => x.Value == minOccurrence).Key;

            return leastFrequentNumber;
        }

        internal void MemorizeNotesPlayed(MidiEvent eventOUT)
        {
            if (eventOUT.Type == TypeEvent.NOTE_ON)
            {
                if (!NotesSentForPanic[eventOUT.Values[0]])
                {
                    NotesSentForPanic[eventOUT.Values[0]] = true;
                }
            }
            else
            {
                if (NotesSentForPanic[eventOUT.Values[0]])
                {
                    NotesSentForPanic[eventOUT.Values[0]] = false;
                }
            }
        }

        internal List<MidiEvent> SetHarmony(MidiEvent eventOUT)
        {
            List<MidiEvent> newNotes = new List<MidiEvent>();

            if (eventOUT.Type == TypeEvent.NOTE_ON)
            {
                NotesSentForPanic[eventOUT.Values[0]] = true;

                List<int> NotesPlayed = CurrentNotesPlayed;
                while (NotesPlayed.Count > 3)
                {
                    NotesPlayed.RemoveAt(0);
                }

                //int iPlayedNotes = NotesPlayed.Count;
                //List<int> closestNotes = FindClosestNumbers(NotesPlayed, 3);

                List<int> bChord = new List<int>();
                //on collecte toutes les notes actives
                for (int i = 0; i < NotesPlayed.Count; i++)
                {
                    if (!bChord.Contains(NotesPlayed[i] % 24))  //tout ramener sur 2 octaves 
                    {
                        bChord.Add(NotesPlayed[i] % 24);
                    }
                }

                //if (_noteHarmony > 0)
                //{
                //    newNotes.Add(new MidiEvent(TypeEvent.NOTE_OFF, new List<int> { _noteHarmony, eventOUT.Values[1] }, eventOUT.Channel, eventOUT.Device));
                //    _noteHarmony = 0;
                //}

                var chordType = IsMinorChord(bChord);

                if (chordType == null)
                {
                    if (_noteHarmony > 0)
                    {
                        //debug pour trouver de nouvelles combinaisons
                        newNotes.Add(new MidiEvent(TypeEvent.NOTE_OFF, new List<int> { _noteHarmony, eventOUT.Values[1] }, eventOUT.Channel, eventOUT.Device));
                        _noteHarmony = 0;
                    }
                }
                else
                {
                    //if (_noteHarmony != Array.LastIndexOf(NotesSentForPanic, true) + chordType.Item2)
                    //{
                        newNotes.Add(new MidiEvent(TypeEvent.NOTE_OFF, new List<int> { _noteHarmony, eventOUT.Values[1] }, eventOUT.Channel, eventOUT.Device));
                        _noteHarmony = 0;
                    //}

                    _harmony = chordType.Item1 ? Harmony.MINOR : Harmony.MAJOR;

                    if (_harmony == Harmony.MINOR)
                    {
                        _noteHarmony = Array.LastIndexOf(NotesSentForPanic, true) + chordType.Item2;
                        //NotesSentForPanic[_noteHarmony] = true;
                        newNotes.Add(new MidiEvent(TypeEvent.NOTE_ON, new List<int> { _noteHarmony, eventOUT.Values[1] }, eventOUT.Channel, eventOUT.Device));
                    }
                    else
                    {
                        _noteHarmony = Array.LastIndexOf(NotesSentForPanic, true) + chordType.Item2;
                        //NotesSentForPanic[_noteHarmony] = true;
                        newNotes.Add(new MidiEvent(TypeEvent.NOTE_ON, new List<int> { _noteHarmony, eventOUT.Values[1] }, eventOUT.Channel, eventOUT.Device));
                    }
                }
            }
            else
            {
                if (_noteHarmony > 0 && NotesSentForPanic.Count(n => n == true) <= 2)
                {
                    newNotes.Add(new MidiEvent(TypeEvent.NOTE_OFF, new List<int> { _noteHarmony, eventOUT.Values[1] }, eventOUT.Channel, eventOUT.Device));
                    _noteHarmony = 0;
                }
                NotesSentForPanic[eventOUT.Values[0]] = false;
                newNotes.Add(new MidiEvent(TypeEvent.NOTE_OFF, new List<int> { eventOUT.Values[0], eventOUT.Values[1] }, eventOUT.Channel, eventOUT.Device));
                return newNotes;
            }

            return newNotes;
        }

        static List<int> FindClosestNumbers(List<int> numbers, int count)
        {
            if (count >= numbers.Count)
            {
                return numbers;
            }

            List<int> sortedNumbers = numbers.OrderBy(x => x).ToList();

            int minDifference = int.MaxValue;
            List<int> closestNumbers = new List<int>();

            for (int i = 0; i <= sortedNumbers.Count - count; i++)
            {
                int difference = sortedNumbers[i + count - 1] - sortedNumbers[i];
                if (difference < minDifference)
                {
                    minDifference = difference;
                    closestNumbers = sortedNumbers.GetRange(i, count);
                }
            }

            return closestNumbers;
        }

        internal Tuple<bool, int> IsMinorChord(List<int> suite)
        {
            bool bFound = false;
            int iOffset = 0;
            Tuple<bool, int> Chord = new Tuple<bool, int>(false, 0);

            for (int i = 0; i < MinorChordsIntervals.Length; i += 2)
            {
                for (int iS = 0; iS < suite.Count - 2; iS++)
                {
                    if (suite[iS] - suite[iS + 1] == MinorChordsIntervals[i] && suite[iS + 1] - suite[iS + 2] == MinorChordsIntervals[i + 1])
                    {
                        switch (i)
                        {
                            case 0:
                            case 2:
                                iOffset = 5;
                                break;
                            case 4:
                            case 6:
                                iOffset = 3;
                                break;
                            case 8:
                            case 10:
                                iOffset = 4;
                                break;
                            case 12:
                                iOffset = -3;
                                break;
                            case 14:
                                iOffset = -4;
                                break;
                            case 16:
                                iOffset = -3;
                                break;
                            case 18:
                            case 20:
                                iOffset = 4;
                                break;
                            case 22:
                                iOffset = 4;
                                break;
                            case 24:
                                iOffset = 5;
                                break;
                            case 26:
                                iOffset = 3;
                                break;
                            case 28:
                                iOffset = 4;
                                break;
                            case 30:
                                iOffset = 4;
                                break;
                        }
                        Chord = new Tuple<bool, int>(true, iOffset);
                        bFound = true;
                    }
                    else if (!Chord.Item1 && suite[iS] - suite[iS + 1] == MajorChordsIntervals[i] && suite[iS + 1] - suite[iS + 2] == MajorChordsIntervals[i + 1])
                    {
                        switch (i)
                        {
                            case 0:
                            case 2:
                                iOffset = 5;
                                break;
                            case 4:
                                iOffset = 0;
                                break;
                            case 6:
                                iOffset = 3;
                                break;
                            case 8:
                            case 10:
                                iOffset = 4;
                                break;
                            case 12:
                                iOffset = -4;
                                break;
                            case 14:
                                iOffset = 3;
                                break;
                            case 16:
                                iOffset = 4;
                                break;
                            case 18:
                                iOffset = 4;
                                break;
                            case 20:
                                iOffset = 3;
                                break;
                            case 22:
                                iOffset = 3;
                                break;
                            case 24:
                                iOffset = 5;
                                break; ;
                            case 26:
                                break;
                            case 28:
                                iOffset = 4;
                                break;
                            case 30:
                                iOffset = 3;
                                break;
                        }
                        Chord = new Tuple<bool, int>(false, iOffset);
                        bFound = true;
                    }
                    else if (!Chord.Item1 && suite[iS] - suite[iS + 1] == OtherIntervals[i] && suite[iS + 1] - suite[iS + 2] == OtherIntervals[i + 1])
                    {
                        switch (i)
                        {
                            case 0:
                                iOffset = 2;
                                break;
                            case 2:
                                iOffset = 3;
                                break;
                            case 4:
                                iOffset = 3;
                                break;
                            case 6:
                                iOffset = 3;
                                break;
                            case 8:
                                iOffset = 5;
                                break;
                            case 10:
                                iOffset = 7;
                                break;
                            case 12:
                                iOffset = 7;
                                break;
                            case 14:
                                iOffset = 3;
                                break;
                            case 16:
                                iOffset = 3;
                                break;
                            case 18:
                                iOffset = 3;
                                break;
                            case 20:
                                iOffset = 3;
                                break;
                            case 22:
                                iOffset = 5;
                                break;
                        }
                        Chord = new Tuple<bool, int>(false, iOffset);
                        bFound = true;
                    }
                }
            }

            return bFound ? Chord : null;
            //int[] copy = new int[suite.Count];
            //suite.CopyTo(copy);

            //suite.Sort();

            //int offset = 0;

            ////la fondamentale de l'accord est la note la plus grave de la tierce la plus grave (tierce mineure ou majeure)
            ////donc recherche de l'intervalle 3 ou 4 le plus bas dans la suite logique
            ////recherche des tierces

            //for (int i = 0; i < suite.Count - 1; i++)
            //{
            //    //0-4-9
            //    if (suite[i] + 3 == suite[i + 1] || suite[suite.Count - 1] - suite[suite.Count - 2] == 5)
            //    {
            //        //4-9-0 => +4 / 0-4-9 => +3 / 9-0-4 => -4
            //        if (copy[0] == suite[0]) { offset = 3; }
            //        else if (copy[1] == suite[0]) { offset = -4; }
            //        else if (copy[2] == suite[0]) { offset = 4; }

            //        return new Tuple<bool, int>(true, offset);
            //    }
            //    else if (suite[i] + 4 == suite[i + 1])
            //    {
            //        //0-4-7 => -3 / 4-7-0 => +4 / 7-0-4 => +3
            //        if (copy[0] == suite[0]) { offset = -3; }
            //        else if (copy[1] == suite[0]) { offset = 3; }
            //        else if (copy[2] == suite[0]) { offset = 4; }
            //        return new Tuple<bool, int>(false, offset);
            //    }
            //}


            //return new Tuple<bool, int>(false, offset);
        }
    }

    [Serializable]
    public class MidiRouting
    {
        private static double CLOCK_INTERVAL = 1000;

        private bool ClockRunning = false;
        private int ClockBPM = 120;
        private string ClockDevice = "";
        private bool GiveLife = false;

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

        public delegate void OutputtingMidiEventHandler(bool b, Guid routingGuid);
        public static event OutputtingMidiEventHandler OutputMidiMessage;

        public delegate void InputMidiEventHandler(MidiEvent ev);
        public static event InputMidiEventHandler InputMidiMessage;

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
        private static List<MidiDevice> UsedDevicesIN = new List<MidiDevice>();
        private static List<MidiDevice> UsedDevicesOUT = new List<MidiDevice>();

        private System.Timers.Timer EventsCounter;
        private System.Timers.Timer MidiClock;

        private int _eventsProcessedIN = 0;
        private int _eventsProcessedOUT = 0;
        private int _eventsProcessedINLast = 0;
        private int _eventsProcessedOUTLast = 0;

        private int _lowestNotePlayed = -1;

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

            InstrumentData.OnSysExInitializerChanged += InstrumentData_OnSysExInitializerChanged;
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

        private void RemovePendingNotes(MatrixItem routing)
        {
            for (int i = 0; i <= 127; i++)
            {
                if (routing.NotesSentForPanic[i])
                {
                    var eventout = new MidiEvent(TypeEvent.NOTE_OFF, new List<int> { i, 0 }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name);
                    CreateOUTEvent(eventout, routing);
                }
            }
        }

        public int AdjustUIRefreshRate()
        {
            int iAdjust = 1000; //1sec par défaut
            int iEvents = (_eventsProcessedINLast + _eventsProcessedOUTLast);

            iAdjust = ((iEvents * 2) / 10);

            if (iAdjust < 1)
            {
                iAdjust = 1;
            }

            return iAdjust;
        }

        private void CheckAndCloseUnusedDevices()
        {
            var usedin = MidiMatrix.Where(d => d.DeviceIn != null).Select(d => d.DeviceIn.Name).Distinct();
            var usedout = MidiMatrix.Where(d => d.DeviceOut != null).Select(d => d.DeviceOut.Name).Distinct();

            List<string> ToRemoveIN = new List<string>();
            foreach (var devin in UsedDevicesIN.Where(d => !d.IsReserverdForInternalPurposes))
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

        private void InstrumentData_OnSysExInitializerChanged(InstrumentData instr)
        {
            var routing = MidiMatrix.FirstOrDefault(m => m.DeviceOut != null && m.DeviceOut.Name.Equals(instr.Device));
            if (routing != null && instr.SysExInitializer.Length > 0)
            {
                SendGenericMidiEvent(new MidiEvent(TypeEvent.SYSEX, instr.SysExInitializer, instr.Device));
            }
        }

        private void DeviceOut_OnMidiEvent(bool bIn, MidiEvent ev)
        {
            //lock (_eventsOUT) { _eventsOUT.Add(ev); }
        }

        private void DeviceIn_OnMidiEvent(bool bIn, MidiEvent ev)
        {
            _eventsProcessedIN += 1;

            //mémorisation de la note pressée la plus grave
            if (ev.Type == TypeEvent.NOTE_ON && ev.Values[0] < _lowestNotePlayed) { _lowestNotePlayed = ev.Values[0]; }
            else if (ev.Type == TypeEvent.NOTE_OFF && ev.Values[0] == _lowestNotePlayed) { _lowestNotePlayed = -1; }

            IncomingMidiMessage?.Invoke(ev);

            CreateOUTEvent(ev, null);
        }

        private void MidiDevice_OnLogAdded(string sDevice, bool bIn, string sLog)
        {
            NewLog?.Invoke(sDevice, bIn, sLog);
        }

        private void CreateOUTEvent(MidiEvent ev, MatrixItem routingOUT = null)
        {
            InputMidiMessage?.Invoke(ev); //pour envoyer à l'UI l'info et éventuellement appeller une recall box si paramétré

            //attention : c'est bien un message du device IN qui arrive !
            List<MatrixItem> matrix = new List<MatrixItem>();

            if (routingOUT == null)
            {
                matrix = MidiMatrix.Where(i => i.DeviceOut != null && i.Options.Active && i.DeviceIn != null && i.DeviceIn.Name == ev.Device && Tools.GetChannel(i.ChannelIn) == ev.Channel).ToList();
            }
            else //c'est un message déjà OUT (ne venant pas d'un MIDI IN mais d'une invocation UI)
            {
                matrix.Add(routingOUT);
            }

            foreach (MatrixItem routing in matrix.OrderBy(r => r.Options.PlayMode))
            {
                OutputMidiMessage?.Invoke(true, routing.RoutingGuid);

                List<MidiEvent> EventsToProcess = EventPreProcessor(routing, ev, routingOUT == null ? false : true);

                if (routingOUT != null)
                {
                    foreach (var evToProcess in EventsToProcess)
                    {
                        _eventsProcessedOUT += 1;

                        if (evToProcess.WaitTimeBeforeNextEvent > 0)
                        {
                            Thread.Sleep(evToProcess.WaitTimeBeforeNextEvent);
                        }

                        routing.DeviceOut.SendMidiEvent(evToProcess);
                    }
                    OutputMidiMessage?.Invoke(false, routing.RoutingGuid);
                }
                else
                {
                    Task t = Task.Factory.StartNew(() =>
                    {
                        //lecture de tous les events qui ont été ajoutés par les options
                        foreach (var evToProcess in EventsToProcess)
                        {
                            _eventsProcessedOUT += 1;

                            if (evToProcess.WaitTimeBeforeNextEvent > 0)
                            {
                                Thread.Sleep(evToProcess.WaitTimeBeforeNextEvent);
                            }

                            routing.DeviceOut.SendMidiEvent(evToProcess);
                        }
                        OutputMidiMessage?.Invoke(false, routing.RoutingGuid);
                    });
                }
            }
        }

        private List<MidiEvent> EventPreProcessor(MatrixItem routing, MidiEvent evIN, bool bOutEvent)
        {
            bool bTranslated = false;
            if (!bOutEvent) { bTranslated = MidiTranslator(routing, evIN); } //TRANSLATEUR DE MESSAGES

            if (!bTranslated)
            {
                MidiEvent _eventsOUT = null;

                //opérations de filtrage 
                switch (evIN.Type)
                {
                    case TypeEvent.CC:
                        if (routing.Options.AllowAllCC || bOutEvent)
                        {
                            if (routing.Options.CC_ToVolume == evIN.Values[0])
                            {
                                _eventsOUT = new MidiEvent(evIN.Type, new List<int> { 7, evIN.Values[1] }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name);
                            }
                            else
                            {
                                var convertedCC = routing.Options.CC_Converters.FirstOrDefault(i => i[0] == evIN.Values[0]);
                                _eventsOUT = new MidiEvent(evIN.Type, new List<int> { convertedCC != null ? convertedCC[1] : evIN.Values[0], evIN.Values[1] }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name);
                            }
                        }
                        break;
                    case TypeEvent.CH_PRES:
                        if ((routing.Options.PlayMode != PlayModes.AFTERTOUCH && routing.Options.AllowAftertouch) || bOutEvent)
                        {
                            _eventsOUT = new MidiEvent(evIN.Type, new List<int> { evIN.Values[0] }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name);
                        }
                        else if (routing.Options.PlayMode == PlayModes.AFTERTOUCH)
                        {
                            routing.CurrentATValue = evIN.Values[0];

                            _eventsOUT = new MidiEvent(TypeEvent.CC, new List<int> { routing.DeviceOut.CC_Volume, evIN.Values[0] }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name);
                        }
                        break;
                    case TypeEvent.NOTE_OFF:
                        if (routing.Options.AllowNotes || bOutEvent)
                        {
                            int iNote = Tools.GetNoteIndex(evIN.Values[0], evIN.Values[1], routing.Options);
                            var convertedNote = routing.Options.Note_Converters.FirstOrDefault(i => i[0] == evIN.Values[0]);
                            if (convertedNote != null) { iNote = convertedNote[1]; } //TODO DOUTE

                            if (iNote > -1)
                            {
                                routing.CurrentATValue = 0;

                                var eventout = new MidiEvent(evIN.Type, new List<int> { iNote, routing.Options.PlayMode == PlayModes.AFTERTOUCH ? 0 : evIN.Values[1] }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name);
                                _eventsOUT = eventout;
                            }
                        }
                        break;
                    case TypeEvent.NOTE_ON:
                        if (routing.Options.AllowNotes || bOutEvent)
                        {
                            int iNote = Tools.GetNoteIndex(evIN.Values[0], evIN.Values[1], routing.Options);
                            var convertedNote = routing.Options.Note_Converters.FirstOrDefault(i => i[0] == evIN.Values[0]);
                            if (convertedNote != null) { iNote = convertedNote[1]; } //TODO DOUTE

                            if (iNote > -1) //NOTE : il faut systématiquement mettre au moins une véolcité de 1 pour que la note se déclenche
                            {
                                //int iVelocity = item.Options.AftertouchVolume && ev.Values[1] > 0 ? 1 : ev.Values[1];
                                int iVelocity = evIN.Values[1];

                                var eventout = new MidiEvent(evIN.Type, new List<int> { iNote, iVelocity }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name);

                                if (routing.NotesSentForPanic[eventout.Values[0]])
                                {
                                    //on n'envoie aucun nouvel évènement
                                }
                                else
                                {
                                    _eventsOUT = eventout;
                                }
                            }
                        }
                        break;
                    case TypeEvent.NRPN:
                        if (routing.Options.AllowNrpn || bOutEvent)
                        {
                            _eventsOUT = new MidiEvent(evIN.Type, new List<int> { evIN.Values[0], evIN.Values[1] }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name);
                        }
                        break;
                    case TypeEvent.PB:
                        if (routing.Options.AllowPitchBend || bOutEvent)
                        {
                            _eventsOUT = new MidiEvent(evIN.Type, new List<int> { evIN.Values[0] }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name);
                        }
                        break;
                    case TypeEvent.PC:
                        if (routing.Options.AllowProgramChange || bOutEvent)
                        {
                            _eventsOUT = new MidiEvent(evIN.Type, new List<int> { evIN.Values[0] }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name);
                        }
                        break;
                    case TypeEvent.POLY_PRES:
                        if (routing.Options.AllowAftertouch || bOutEvent)
                        {
                            _eventsOUT = new MidiEvent(evIN.Type, new List<int> { evIN.Values[0], evIN.Values[1] }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name);
                        }
                        break;
                    case TypeEvent.SYSEX:
                        if (routing.Options.AllowSysex || bOutEvent)
                        {
                            _eventsOUT = new MidiEvent(evIN.Type, SysExTranscoder(evIN.SysExData), routing.DeviceOut.Name);
                        }
                        break;
                }

                if (_eventsOUT != null)
                {
                    return EventProcessor(routing, _eventsOUT, bOutEvent);
                }
                else { return new List<MidiEvent>(); }
            }
            else { return new List<MidiEvent>(); }
        }

        private List<MidiEvent> EventProcessor(MatrixItem routing, MidiEvent eventOUT, bool bOutEvent)
        {
            Guid routingGuid = routing.RoutingGuid;

            List<MidiEvent> EventsToProcess = new List<MidiEvent>();

            bool bAvoidCCToSmooth = false;

            switch (eventOUT.Type)
            {
                case TypeEvent.PC: //
                    routing.SetCurrentProgram(eventOUT.Values[0]);
                    EventsToProcess.Add(eventOUT);
                    break;

                case TypeEvent.CC: //smooth CC
                    if (!bOutEvent) //!bOutevent parce qu'on ne veut pas que les évènements forcés (type changement preset) créent un smooth
                    {
                        //si l'item est bloqué à cause du smooth, on interdit les nouveaux entrants
                        if (routing.IsBlocked(eventOUT.Values[0], eventOUT.Values[1], eventOUT.Channel, eventOUT.Device))
                        {
                            bAvoidCCToSmooth = true;
                        }
                        else
                        {
                            //sinon on enregistre et on crée une liste d'events additionnels
                            var newitems = routing.SetCC(eventOUT.Values[0], eventOUT.Values[1], eventOUT.Channel, eventOUT.Device, routing.Options.SmoothCC);
                            int count = newitems.Count;

                            if (routing.Options.SmoothCC && count > 0)
                            {
                                bAvoidCCToSmooth = true;

                                foreach (var cc in newitems)
                                {
                                    cc.WaitTimeBeforeNextEvent = (int)(routing.Options.SmoothCCLength / count); //le smoooth doit durer 1sec
                                    EventsToProcess.Add(cc);
                                }
                                routing.UnblockCC(eventOUT.Values[0], eventOUT.Channel, eventOUT.Device);
                            }
                        }
                    }
                    else //on mémorise juste la valeur et on débloque le CC si il est bloqué
                    {
                        routing.SetCC(eventOUT.Values[0], eventOUT.Values[1], eventOUT.Channel, eventOUT.Device, false);
                        routing.UnblockCC(eventOUT.Values[0], eventOUT.Channel, eventOUT.Device);
                    }

                    if (!bAvoidCCToSmooth) //ne pas jouer le CC  trop rapide, il a été joué par le Smooth quelques lignes plus haut
                    {
                        EventsToProcess.Add(eventOUT);
                    }
                    break;

                case TypeEvent.NOTE_ON:
                case TypeEvent.NOTE_OFF: //delayer
                    if (!bOutEvent) // ne doit fonctionner qu'avec les input du routing et pas avec les évènements forcés
                    {
                        bool bMono = false;

                        switch (routing.Options.PlayMode)
                        {
                            case PlayModes.AFTERTOUCH:
                                EventsToProcess.Add(new MidiEvent(MidiDevice.TypeEvent.CC, new List<int> { 7, routing.CurrentATValue }, eventOUT.Channel, eventOUT.Device));
                                routing.MemorizeNotesPlayed(eventOUT);
                                break;
                            case PlayModes.MONO_HIGH:
                                EventsToProcess.AddRange(routing.SetPlayMono(1, eventOUT));
                                bMono = EventsToProcess.Count > 0 ? true : false;
                                break;
                            case PlayModes.MONO_LOW:
                                EventsToProcess.AddRange(routing.SetPlayMono(2, eventOUT));
                                bMono = EventsToProcess.Count > 0 ? true : false;
                                break;
                            case PlayModes.MONO_INTERMEDIATE_HIGH:
                                EventsToProcess.AddRange(routing.SetPlayMono(3, eventOUT));
                                bMono = EventsToProcess.Count > 0 ? true : false;
                                break;
                            case PlayModes.MONO_INTERMEDIATE_LOW:
                                EventsToProcess.AddRange(routing.SetPlayMono(4, eventOUT));
                                bMono = EventsToProcess.Count > 0 ? true : false;
                                break;
                            case PlayModes.MONO_IN_BETWEEN:
                                EventsToProcess.AddRange(routing.SetPlayMono(5, eventOUT));
                                bMono = true;
                                break;
                            case PlayModes.HARMONY:
                                var newev = routing.SetHarmony(eventOUT);
                                bMono = true;
                                EventsToProcess.AddRange(newev);
                                break;
                            default:
                                routing.MemorizeNotesPlayed(eventOUT);
                                break;
                        }

                        if (!bMono) //si la recherche de la low note ou high note a ramené un résultat (par SetPlayMono) ça signifie que la note entrante a été bloquée
                        {
                            if (GiveLife) //donne de la vie au projet en ajoutant un peu de pitch bend et de delay
                            {
                                Random random = new Random();

                                if (eventOUT.Type == TypeEvent.NOTE_ON)
                                {
                                    int randomWaitON = random.Next(1, 40);
                                    int randomPB = random.Next(8192 - 400, 8192 + 400);

                                    int iNewVol = routing.RandomizeCCValue(7, eventOUT.Channel, eventOUT.Device, 5);
                                    int iNewMod = routing.RandomizeCCValue(1, eventOUT.Channel, eventOUT.Device, 5);
                                    //int iNewPan = 0; // routing.RandomizeCCValue(10, copiedevent.Channel, copiedevent.Device, 5);

                                    MidiEvent pbRandom = new MidiEvent(TypeEvent.PB, new List<int> { randomPB }, eventOUT.Channel, eventOUT.Device);

                                    if (iNewVol > 0)
                                    {
                                        EventsToProcess.Add(new MidiEvent(TypeEvent.CC, new List<int> { 7, iNewVol }, eventOUT.Channel, eventOUT.Device));
                                    }
                                    if (iNewMod > 0)
                                    {
                                        EventsToProcess.Add(new MidiEvent(TypeEvent.CC, new List<int> { 1, iNewMod }, eventOUT.Channel, eventOUT.Device));
                                    }
                                    //if (iNewPan > 0)
                                    //{
                                    //    routing.DeviceOut.SendMidiEvent(new MidiEvent(TypeEvent.CC, new List<int> { 10, iNewPan }, copiedevent.Channel, copiedevent.Device));
                                    //}

                                    EventsToProcess.Add(pbRandom);

                                    eventOUT.WaitTimeBeforeNextEvent = randomWaitON + routing.Options.DelayNotesLength > 0 ? routing.Options.DelayNotesLength : 0;
                                    EventsToProcess.Add(eventOUT);
                                }
                                else
                                {
                                    int randomWaitOFF = random.Next(41, 60); //trick pour éviter que les note off se déclenchent avant les note on

                                    eventOUT.WaitTimeBeforeNextEvent = randomWaitOFF + routing.Options.DelayNotesLength > 0 ? routing.Options.DelayNotesLength : 0;
                                    routing.DeviceOut.SendMidiEvent(eventOUT);

                                }
                            }
                            else if (routing.Options.DelayNotesLength > 0)
                            {
                                eventOUT.WaitTimeBeforeNextEvent = routing.Options.DelayNotesLength;
                                EventsToProcess.Add(eventOUT);
                            }
                            else
                            {
                                EventsToProcess.Add(eventOUT);
                            }
                        }
                    }
                    else { EventsToProcess.Add(eventOUT); }
                    break;

                default:
                    EventsToProcess.Add(eventOUT);
                    break;
            }

            return EventsToProcess;

        }

        void ChangeOptions(MatrixItem routing, MidiOptions newop, bool bInit)
        {
            bool bChanges = false;

            if (routing.DeviceOut != null)
            {
                if (newop == null) //tout charger
                {
                    if (routing.Options.CC_Attack_Value > -1 || (bInit && routing.Options.CC_Attack_Value > -1))
                    {
                        CreateOUTEvent(new MidiEvent(TypeEvent.CC, new List<int> { routing.DeviceOut.CC_Attack, routing.Options.CC_Attack_Value }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name), routing);
                        bChanges = true;
                    }
                    if (routing.Options.CC_Timbre_Value > -1 || (bInit && routing.Options.CC_Timbre_Value > -1))
                    {
                        CreateOUTEvent(new MidiEvent(TypeEvent.CC, new List<int> { routing.DeviceOut.CC_Timbre, routing.Options.CC_Timbre_Value }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name), routing);
                        bChanges = true;
                    }
                    if (routing.Options.CC_FilterCutOff_Value > -1 || (bInit && routing.Options.CC_FilterCutOff_Value > -1))
                    {
                        CreateOUTEvent(new MidiEvent(TypeEvent.CC, new List<int> { routing.DeviceOut.CC_FilterCutOff, routing.Options.CC_FilterCutOff_Value }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name), routing);
                        bChanges = true;
                    }
                    if (routing.Options.CC_Chorus_Value > -1 || (bInit && routing.Options.CC_Chorus_Value > -1))
                    {
                        CreateOUTEvent(new MidiEvent(TypeEvent.CC, new List<int> { routing.DeviceOut.CC_Chorus, routing.Options.CC_Chorus_Value }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name), routing);
                        bChanges = true;
                    }
                    if (routing.Options.CC_Decay_Value > -1 || (bInit && routing.Options.CC_Decay_Value > -1))
                    {
                        CreateOUTEvent(new MidiEvent(TypeEvent.CC, new List<int> { routing.DeviceOut.CC_Decay, routing.Options.CC_Decay_Value }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name), routing);
                        bChanges = true;
                    }
                    if (routing.Options.CC_Pan_Value > -1 || (bInit && routing.Options.CC_Pan_Value > -1))
                    {
                        CreateOUTEvent(new MidiEvent(TypeEvent.CC, new List<int> { routing.DeviceOut.CC_Pan, routing.Options.CC_Pan_Value }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name), routing);
                        bChanges = true;
                    }
                    if (routing.Options.CC_Release_Value > -1 || (bInit && routing.Options.CC_Release_Value > -1))
                    {
                        CreateOUTEvent(new MidiEvent(TypeEvent.CC, new List<int> { routing.DeviceOut.CC_Release, routing.Options.CC_Release_Value }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name), routing);
                        bChanges = true;
                    }
                    if (routing.Options.CC_Reverb_Value > -1 || (bInit && routing.Options.CC_Reverb_Value > -1))
                    {
                        CreateOUTEvent(new MidiEvent(TypeEvent.CC, new List<int> { routing.DeviceOut.CC_Reverb, routing.Options.CC_Reverb_Value }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name), routing);
                        bChanges = true;
                    }
                    if (routing.Options.CC_Volume_Value > -1 || (bInit && routing.Options.CC_Volume_Value > -1))
                    {
                        CreateOUTEvent(new MidiEvent(TypeEvent.CC, new List<int> { routing.DeviceOut.CC_Volume, routing.Options.CC_Volume_Value }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name), routing);
                        bChanges = true;
                    }
                }
                else
                {
                    if (newop.TranspositionOffset != routing.Options.TranspositionOffset) //midi panic
                    {
                        foreach (var item in MidiMatrix)
                        {
                            RemovePendingNotes(item);
                        }
                    }

                    //comparer
                    if (newop.CC_Attack_Value != routing.Options.CC_Attack_Value || (bInit && routing.Options.CC_Attack_Value > -1))
                    {
                        CreateOUTEvent(new MidiEvent(TypeEvent.CC, new List<int> { routing.DeviceOut.CC_Attack, newop.CC_Attack_Value }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name), routing);
                        bChanges = true;
                    }
                    if (newop.CC_Timbre_Value != routing.Options.CC_Timbre_Value || (bInit && routing.Options.CC_Timbre_Value > -1))
                    {
                        CreateOUTEvent(new MidiEvent(TypeEvent.CC, new List<int> { routing.DeviceOut.CC_Timbre, newop.CC_Timbre_Value }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name), routing);
                        bChanges = true;
                    }
                    if (newop.CC_FilterCutOff_Value != routing.Options.CC_FilterCutOff_Value || (bInit && routing.Options.CC_FilterCutOff_Value > -1))
                    {
                        CreateOUTEvent(new MidiEvent(TypeEvent.CC, new List<int> { routing.DeviceOut.CC_FilterCutOff, newop.CC_FilterCutOff_Value }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name), routing);
                        bChanges = true;
                    }
                    if (newop.CC_Chorus_Value != routing.Options.CC_Chorus_Value || (bInit && routing.Options.CC_Chorus_Value > -1))
                    {
                        CreateOUTEvent(new MidiEvent(TypeEvent.CC, new List<int> { routing.DeviceOut.CC_Chorus, newop.CC_Chorus_Value }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name), routing);
                        bChanges = true;
                    }
                    if (newop.CC_Decay_Value != routing.Options.CC_Decay_Value || (bInit && routing.Options.CC_Decay_Value > -1))
                    {
                        CreateOUTEvent(new MidiEvent(TypeEvent.CC, new List<int> { routing.DeviceOut.CC_Decay, newop.CC_Decay_Value }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name), routing);
                        bChanges = true;
                    }
                    if (newop.CC_Pan_Value != routing.Options.CC_Pan_Value || (bInit && routing.Options.CC_Pan_Value > -1))
                    {
                        CreateOUTEvent(new MidiEvent(TypeEvent.CC, new List<int> { routing.DeviceOut.CC_Pan, newop.CC_Pan_Value }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name), routing);
                        bChanges = true;
                    }
                    if (newop.CC_Release_Value != routing.Options.CC_Release_Value || (bInit && routing.Options.CC_Release_Value > -1))
                    {
                        CreateOUTEvent(new MidiEvent(TypeEvent.CC, new List<int> { routing.DeviceOut.CC_Release, newop.CC_Release_Value }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name), routing);
                        bChanges = true;
                    }
                    if (newop.CC_Reverb_Value != routing.Options.CC_Reverb_Value || (bInit && routing.Options.CC_Reverb_Value > -1))
                    {
                        CreateOUTEvent(new MidiEvent(TypeEvent.CC, new List<int> { routing.DeviceOut.CC_Reverb, newop.CC_Reverb_Value }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name), routing);
                        bChanges = true;
                    }
                    if (newop.CC_Volume_Value != routing.Options.CC_Volume_Value || (bInit && routing.Options.CC_Volume_Value > -1))
                    {
                        CreateOUTEvent(new MidiEvent(TypeEvent.CC, new List<int> { routing.DeviceOut.CC_Volume, newop.CC_Volume_Value }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name), routing);
                        bChanges = true;
                    }
                }
                //if (bChanges) { routing.UnblockAllCC(); }
            }

            if (newop != null) { routing.Options = newop; }
        }

        internal void ChangeDefaultCC(List<InstrumentData> instruments)
        {
            foreach (var item in MidiMatrix)
            {
                if (item.DeviceOut != null)
                {
                    var instr = instruments.FirstOrDefault(i => i.Device.Equals(item.DeviceOut.Name));
                    if (instr != null && instr.DefaultCC.Count > 0)
                    {
                        item.DeviceOut.CC_Volume = instr.GetCCParameter(InstrumentData.CC_Parameters.CC_Volume);
                        item.DeviceOut.CC_Attack = instr.GetCCParameter(InstrumentData.CC_Parameters.CC_Attack);
                        item.DeviceOut.CC_Chorus = instr.GetCCParameter(InstrumentData.CC_Parameters.CC_Chorus);
                        item.DeviceOut.CC_Decay = instr.GetCCParameter(InstrumentData.CC_Parameters.CC_Decay);
                        item.DeviceOut.CC_FilterCutOff = instr.GetCCParameter(InstrumentData.CC_Parameters.CC_FilterCutOff);
                        item.DeviceOut.CC_Pan = instr.GetCCParameter(InstrumentData.CC_Parameters.CC_Pan);
                        item.DeviceOut.CC_Release = instr.GetCCParameter(InstrumentData.CC_Parameters.CC_Release);
                        item.DeviceOut.CC_Reverb = instr.GetCCParameter(InstrumentData.CC_Parameters.CC_Reverb);
                        item.DeviceOut.CC_Timbre = instr.GetCCParameter(InstrumentData.CC_Parameters.CC_Timbre);
                    }
                }
            }
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
                    CreateOUTEvent(new MidiEvent(TypeEvent.CC, new List<int> { pc00.Control, pc00.Value }, pc00.Channel, routing.DeviceOut.Name), routing);
                    CreateOUTEvent(new MidiEvent(TypeEvent.CC, new List<int> { pc32.Control, pc32.Value }, pc32.Channel, routing.DeviceOut.Name), routing);
                    CreateOUTEvent(new MidiEvent(TypeEvent.PC, new List<int> { prg.Program }, prg.Channel, routing.DeviceOut.Name), routing);
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
            int iPbDirection = -1;

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
                        case TypeEvent.PB:
                            matchIN = Regex.Match(translate[0], "(\\[)(IN)=(PB#)(\\d{1})(:)(-?\\d{1,4})((-)(-?\\d{1,4}))(\\])");
                            if (matchIN.Success) //le message arrive entre 0 et 16384
                            {
                                string sPB = matchIN.Groups[4].Value;
                                string sVal1 = matchIN.Groups[6].Value;
                                string sVal2 = matchIN.Groups[9].Value;
                                if (sPB.Equals("0")) //up only
                                {
                                    if (ev.Values[0] > 0 && ev.Values[0] <= Convert.ToInt32(sVal2 + 8192) && ev.Values[0] >= Convert.ToInt32(sVal1 + 8192))
                                    { iPbDirection = 0; bMustTranslate = true; }
                                }
                                else if (sPB.Equals("1")) //down only
                                {
                                    if (ev.Values[0] > 0 && ev.Values[0] <= Convert.ToInt32(sVal2 + 8192) && ev.Values[0] >= Convert.ToInt32(sVal1 + 8192))
                                    { iPbDirection = 1; bMustTranslate = true; }
                                }
                                else //both directions
                                {
                                    if (ev.Values[0] <= Convert.ToInt32(sVal2 + 8192) && ev.Values[0] >= Convert.ToInt32(sVal1 + 8192))
                                    { iPbDirection = 2; bMustTranslate = true; }
                                }
                            }
                            break;
                    }

                    if (bMustTranslate)
                    {
                        Match matchOUT = null;
                        //lecture du message OUT qui doit être construit
                        Match mType = Regex.Match(translate[0], "(\\[)(OUT)=(SYS#|PC#|CC#|KEY#|PB#)");
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
                                        if (ev.Type == TypeEvent.NOTE_ON) { iPrgValue = ev.Values[0]; }
                                        else if (ev.Type == TypeEvent.CC) { iPrgValue = ev.Values[1]; }
                                        else if (ev.Type == TypeEvent.PC) { iPrgValue = ev.Values[0]; }
                                        else if (ev.Type == TypeEvent.CH_PRES) { iPrgValue = ev.Values[0]; }
                                        else if (ev.Type == TypeEvent.SYSEX) { iPrgValue = 0; } //absurde.
                                        else if (ev.Type == TypeEvent.PB)
                                        {
                                            if (iPbDirection == 0)
                                            { iPrgValue = ev.Values[0] / 128; }
                                            else if (iPbDirection == 1)
                                            { iPrgValue = ev.Values[0] / 64; }
                                            else //0=64, 8192=128, -9192=0
                                            { iPrgValue = (ev.Values[0] + 8192) / 256; }
                                        }

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
                                        if (ev.Type == TypeEvent.NOTE_ON) { iCCValue = ev.Values[0]; }
                                        else if (ev.Type == TypeEvent.CC) { iCCValue = ev.Values[1]; }
                                        else if (ev.Type == TypeEvent.PC) { iCCValue = ev.Values[0]; }
                                        else if (ev.Type == TypeEvent.CH_PRES) { iCCValue = ev.Values[0]; }
                                        else if (ev.Type == TypeEvent.SYSEX) { iCCValue = 0; } //absurde.
                                        else if (ev.Type == TypeEvent.PB)
                                        {
                                            if (iPbDirection == 0)
                                            { iCCValue = ev.Values[0] / 128; }
                                            else if (iPbDirection == 1)
                                            { iCCValue = ev.Values[0] / 64; }
                                            else //0=64, 8192=128, -9192=0
                                            { iCCValue = (ev.Values[0] + 8192) / 256; }
                                        }

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
                                            if (ev.Type == TypeEvent.NOTE_ON) { iVelo = ev.Values[0]; }
                                            else if (ev.Type == TypeEvent.CC) { iVelo = ev.Values[1]; }
                                            else if (ev.Type == TypeEvent.PC) { iVelo = ev.Values[0]; }
                                            else if (ev.Type == TypeEvent.CH_PRES) { iVelo = ev.Values[0]; }
                                            else if (ev.Type == TypeEvent.SYSEX) { iVelo = 64; } //absurde.
                                            else if (ev.Type == TypeEvent.PB)
                                            {
                                                if (iPbDirection == 0)
                                                { iVelo = ev.Values[0] / 128; }
                                                else if (iPbDirection == 1)
                                                { iVelo = ev.Values[0] / 64; }
                                                else //0=64, 8192=128, -9192=0
                                                { iVelo = (ev.Values[0] + 8192) / 256; }
                                            }

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

                                            if (ev.Type == TypeEvent.NOTE_ON) { iNote = ev.Values[0]; }
                                            else if (ev.Type == TypeEvent.CC) { iNote = ev.Values[1]; }
                                            else if (ev.Type == TypeEvent.PC) { iNote = ev.Values[0]; }
                                            else if (ev.Type == TypeEvent.CH_PRES) { iNote = ev.Values[0]; }
                                            else if (ev.Type == TypeEvent.SYSEX) { iNote = 64; } //absurde.
                                            else if (ev.Type == TypeEvent.PB)
                                            {
                                                if (iPbDirection == 0)
                                                { iNote = ev.Values[0] / 128; }
                                                else if (iPbDirection == 1)
                                                { iNote = ev.Values[0] / 64; }
                                                else //0=64, 8192=128, -9192=0
                                                { iNote = (ev.Values[0] + 8192) / 256; }
                                            }

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
                                            if (ev.Type == TypeEvent.NOTE_ON) { iNote = ev.Values[0]; iVelo = ev.Values[0]; }
                                            else if (ev.Type == TypeEvent.CC) { iNote = ev.Values[1]; iVelo = ev.Values[1]; }
                                            else if (ev.Type == TypeEvent.PC) { iNote = ev.Values[0]; iVelo = ev.Values[0]; }
                                            else if (ev.Type == TypeEvent.CH_PRES) { iNote = ev.Values[0]; iVelo = ev.Values[0]; } //absurde.
                                            else if (ev.Type == TypeEvent.SYSEX) { iNote = 64; iVelo = 64; } //absurde.
                                            else if (ev.Type == TypeEvent.PB)
                                            {
                                                if (iPbDirection == 0)
                                                { iNote = ev.Values[0] / 128; iVelo = ev.Values[0] / 128; }
                                                else if (iPbDirection == 1)
                                                { iNote = ev.Values[0] / 64; iVelo = ev.Values[0] / 64; }
                                                else //0=64, 8192=128, -9192=0
                                                { iNote = (ev.Values[0] + 8192) / 256; iVelo = (ev.Values[0] + 8192) / 256; }
                                            }

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
                                        if (ev.Type == TypeEvent.NOTE_ON) { iATValue = ev.Values[0]; }
                                        else if (ev.Type == TypeEvent.CC) { iATValue = ev.Values[1]; }
                                        else if (ev.Type == TypeEvent.PC) { iATValue = ev.Values[0]; }
                                        else if (ev.Type == TypeEvent.CH_PRES) { iATValue = ev.Values[0]; }
                                        else if (ev.Type == TypeEvent.SYSEX) { iATValue = 0; } //absurde.
                                        else if (ev.Type == TypeEvent.PB)
                                        {
                                            if (iPbDirection == 0)
                                            { iATValue = ev.Values[0] / 128; }
                                            else if (iPbDirection == 1)
                                            { iATValue = ev.Values[0] / 64; }
                                            else //0=64, 8192=128, -9192=0
                                            { iATValue = (ev.Values[0] + 8192) / 256; }
                                        }

                                        routing.DeviceOut.SendMidiEvent(new MidiEvent(TypeEvent.CH_PRES, new List<int> { iATValue }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name));
                                        _eventsProcessedOUT += 1;
                                    }
                                    break;
                                case "PB#":
                                    //[OUT=PB#1:0:8192]
                                    //[OUT= PB#0:0:8192]
                                    matchOUT = Regex.Match(translate[0], "(\\[)(OUT)=(PB#)(\\d{1})((:)(\\d{1,4})-(\\d{1,4}))(\\])");
                                    string sDirection = matchOUT.Groups[2].Value;
                                    string sPB1 = matchOUT.Groups[4].Value;
                                    string sPB2 = matchOUT.Groups[7].Value;

                                    int iPBValue = 0;
                                    if (ev.Type == TypeEvent.NOTE_ON) { iPBValue = ev.Values[0]; }
                                    else if (ev.Type == TypeEvent.CC) { iPBValue = ev.Values[1]; }
                                    else if (ev.Type == TypeEvent.PC) { iPBValue = ev.Values[0]; }
                                    else if (ev.Type == TypeEvent.CH_PRES) { iPBValue = ev.Values[0]; }
                                    else if (ev.Type == TypeEvent.SYSEX) { iPBValue = 0; } //absurde.
                                    else if (ev.Type == TypeEvent.PB)
                                    {
                                        if (iPbDirection == 0)
                                        { iPBValue = ev.Values[0] / 128; }
                                        else if (iPbDirection == 1)
                                        { iPBValue = ev.Values[0] / 64; }
                                        else //0=64, 8192=128, -9192=0
                                        { iPBValue = (ev.Values[0] + 8192) / 256; }
                                    }
                                    if (sDirection.Equals("0"))
                                    {
                                        iPBValue = iPBValue * 128;
                                    }
                                    else if (sDirection.Equals("1"))
                                    {
                                        iPBValue = iPBValue * 64;
                                    }
                                    else
                                    {
                                        iPBValue = iPBValue >= 64 ? iPBValue * 128 : iPBValue * 64;
                                    }

                                    routing.DeviceOut.SendMidiEvent(new MidiEvent(TypeEvent.PB, new List<int> { iPBValue }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name));
                                    _eventsProcessedOUT += 1;

                                    break;
                            }
                        }
                    }
                }
            }

            return bMustTranslate;
        }

        internal void SendGenericMidiEvent(MidiEvent ev)
        {
            CreateOUTEvent(ev, null);
        }

        internal void InitRouting(List<LiveData> data)
        {
            foreach (var live in data)
            {
                var deviceout = UsedDevicesOUT.FirstOrDefault(d => d.Name.Equals(live.DeviceOUT));
                if (deviceout == null)
                {
                    var devOUT = OutputDevices.FirstOrDefault(d => d.Name.Equals(live.DeviceOUT));
                    var devINIT = new MidiDevice(devOUT, CubaseInstrumentData.Instruments.FirstOrDefault(i => i.Device.Equals(live.DeviceOUT)));
                    devINIT.OnMidiEvent += DeviceOut_OnMidiEvent;
                    UsedDevicesOUT.Add(devINIT);
                    devINIT.OpenDevice();
                }
                else
                {
                    deviceout.OpenDevice();
                }

                var devicein = UsedDevicesIN.FirstOrDefault(d => d.Name.Equals(live.DeviceIN));
                if (devicein == null)
                {
                    var devIN = InputDevices.FirstOrDefault(d => d.Name.Equals(live.DeviceIN));
                    var devINIT = new MidiDevice(devIN);
                    devINIT.OnMidiEvent += DeviceIn_OnMidiEvent;
                    UsedDevicesIN.Add(devINIT);
                    devINIT.OpenDevice();
                }
                else
                {
                    devicein.OpenDevice();
                }
            }

            foreach (var d in data)
            {
                foreach (var cc in d.StartCC.Where(c => c.CC != 0 && c.CC != 32))
                {
                    SendCC(d.RoutingGuid, cc.CC, cc.CCValue, Tools.GetChannelInt(cc.Channel), cc.Device);
                }

                var routing = MidiMatrix.FirstOrDefault(m => m.RoutingGuid == d.RoutingGuid);
                if (routing != null)
                {
                    ChangeOptions(routing, d.StartOptions, true);

                    MidiPreset mp = new MidiPreset();
                    mp.Channel = Tools.GetChannelInt(d.Channel);
                    mp.PresetName = "Sequencer";
                    mp.Prg = d.Program;

                    foreach (var cc in d.StartCC.Where(c => c.CC == 0 || c.CC == 32).OrderBy(c => c.CC))
                    {
                        if (cc.CC == 00) { mp.Msb = cc.CCValue; }
                        else if (cc.CC == 32) { mp.Lsb = cc.CCValue; }
                    }
                    ChangeProgram(routing, mp, true);
                }
            }
        }

        #endregion

        #region PUBLIC

        public Guid AddRouting(string sDeviceIn, string sDeviceOut, int iChIn, int iChOut, MidiOptions options, MidiPreset preset = null)
        {
            var devIN = InputDevices.FirstOrDefault(d => d.Name.Equals(sDeviceIn));
            var devOUT = OutputDevices.FirstOrDefault(d => d.Name.Equals(sDeviceOut));

            if (devIN != null && UsedDevicesIN.Count(d => d.Name.Equals(sDeviceIn)) == 0)
            {
                if (!sDeviceIn.Equals(Tools.INTERNAL_GENERATOR))
                {
                    var device = new MidiDevice(devIN);
                    device.UsedForRouting = true;
                    device.OnMidiEvent += DeviceIn_OnMidiEvent;
                    UsedDevicesIN.Add(device);
                }
            }
            if (devOUT != null && UsedDevicesOUT.Count(d => d.Name.Equals(sDeviceOut)) == 0)
            {
                var device = new MidiDevice(devOUT, CubaseInstrumentData.Instruments.FirstOrDefault(i => i.Device.Equals(devOUT.Name)));
                device.OnMidiEvent += DeviceOut_OnMidiEvent;
                UsedDevicesOUT.Add(device);
            }
            //iAction : 0 = delete, 1 = adds

            //devIN != null && devOUT != null && 
            if (iChIn >= 0 && iChIn <= 16 && iChOut >= 0 && iChOut <= 16)
            {
                MidiMatrix.Add(new MatrixItem(UsedDevicesIN.FirstOrDefault(d => d.Name.Equals(sDeviceIn)), UsedDevicesOUT.FirstOrDefault(d => d.Name.Equals(sDeviceOut)), iChIn, iChOut, options, preset));

                if (sDeviceIn.Equals(Tools.INTERNAL_GENERATOR))
                {
                    MidiMatrix.Last().Options.Active = false;
                }

                CheckAndCloseUnusedDevices();
                //hyper important d'être à la fin !

                if (iChOut > 0 && iChOut <= 16)
                {
                    ChangeDefaultCC(CubaseInstrumentData.Instruments);
                    ChangeOptions(MidiMatrix.Last(), options, true);
                    ChangeProgram(MidiMatrix.Last(), preset, true);
                }

                var guid = MidiMatrix.Last().RoutingGuid;

                return guid;

            }
            else { return Guid.Empty; }
        }

        public bool ModifyRouting(Guid routingGuid, string sDeviceIn, string sDeviceOut, int iChIn, int iChOut, MidiOptions options, MidiPreset preset = null)
        {
            bool bDeviceOutChanged = false;

            var routing = MidiMatrix.FirstOrDefault(m => m.RoutingGuid == routingGuid);

            if (routing != null)
            {
                if ((routing.DeviceOut == null && sDeviceOut.Length > 0) || (routing.DeviceOut != null && !routing.DeviceOut.Name.Equals(sDeviceOut)))
                { bDeviceOutChanged = true; }

                routing.ChannelIn = iChIn;
                routing.ChannelOut = iChOut;
                bool bINChanged = routing.CheckDeviceIn(sDeviceIn);
                bool bOUTChanged = routing.CheckDeviceOut(sDeviceOut);

                if (bINChanged)
                {
                    bool active = routing.Options.Active;
                    routing.Options.Active = false;

                    if (!sDeviceIn.Equals(Tools.INTERNAL_GENERATOR))
                    {
                        if (sDeviceIn.Length > 0 && UsedDevicesIN.Count(d => d.Name.Equals(sDeviceIn)) == 0)
                        {
                            var device = new MidiDevice(InputDevices.FirstOrDefault(d => d.Name.Equals(sDeviceIn)));
                            device.UsedForRouting = true;
                            device.OnMidiEvent += DeviceIn_OnMidiEvent;
                            UsedDevicesIN.Add(device);
                        }
                        routing.DeviceIn = UsedDevicesIN.FirstOrDefault(d => d.Name.Equals(sDeviceIn));
                        routing.Options.Active = active;
                    }
                    else
                    {
                        routing.DeviceIn = null;
                    }

                    routing.Options.Active = active;

                    CheckAndCloseUnusedDevices();
                }
                if (bOUTChanged)
                {
                    bool active = routing.Options.Active;
                    routing.Options.Active = false;

                    if (sDeviceOut.Length > 0 && UsedDevicesOUT.Count(d => d.Name.Equals(sDeviceOut)) == 0)
                    {
                        var outdev = OutputDevices.FirstOrDefault(d => d.Name.Equals(sDeviceOut));
                        var device = new MidiDevice(outdev, CubaseInstrumentData.Instruments.FirstOrDefault(i => i.Device.Equals(outdev.Name)));
                        device.OnMidiEvent += DeviceOut_OnMidiEvent;
                        UsedDevicesOUT.Add(device);

                        routing.DeviceOut = UsedDevicesOUT.FirstOrDefault(d => d.Name.Equals(sDeviceOut));
                        routing.Options.Active = active;
                    }
                    else
                    {
                        routing.Options.Active = active;
                        routing.DeviceOut = UsedDevicesOUT.FirstOrDefault(d => d.Name.Equals(sDeviceOut));
                    }


                    CheckAndCloseUnusedDevices();
                }

                if (iChOut > 0 && iChOut <= 16)
                {
                    //hyper important d'être à la fin !
                    ChangeDefaultCC(CubaseInstrumentData.Instruments);
                    ChangeOptions(routing, options, bDeviceOutChanged ? true : false);
                    ChangeProgram(routing, preset, bDeviceOutChanged ? true : false);
                }

                return true;
            }
            else //en mode ouverture de projet on a déjà un routing guid
            {
                AddRouting(sDeviceIn, sDeviceOut, iChIn, iChOut, options, preset);
                MidiMatrix.Last().SetRoutingGuid(routingGuid);
                return true;
            }
        }

        public void SetSolo(Guid routingGuid)
        {
            var routingOn = MidiMatrix.FirstOrDefault(m => m.RoutingGuid == routingGuid);

            if (routingOn != null)
            {
                routingOn.Options.Active = true;
            }

            var routingOff = MidiMatrix.Where(m => m.RoutingGuid != routingGuid);
            foreach (var r in routingOff)
            {
                RemovePendingNotes(r);
                r.Options.Active = false;
            }
        }

        public void MuteRouting(Guid routingGuid)
        {
            var routingOn = MidiMatrix.FirstOrDefault(m => m.RoutingGuid == routingGuid);
            if (routingOn != null)
            {
                RemovePendingNotes(routingOn);
                routingOn.Options.Active = false;
            }
        }

        public void UnmuteRouting(Guid routingGuid)
        {
            var routingOff = MidiMatrix.FirstOrDefault(m => m.RoutingGuid == routingGuid);
            if (routingOff != null)
            {
                routingOff.Options.Active = true;
            }
        }

        public void UnmuteAllRouting()
        {
            foreach (var r in MidiMatrix)
            {
                r.Options.Active = true;
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

        public int GetFreeChannelForDevice(string sDevice, int iWanted, bool bIn)
        {
            int[] Channels = new int[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

            if (iWanted == 0) { iWanted += 1; }

            Channels[iWanted - 1] += 1;

            var devicerouting = MidiMatrix.Where(m => m.DeviceOut != null && m.DeviceOut.Name.Equals(sDevice));

            if (devicerouting != null && devicerouting.Count() > 0)
            {
                foreach (var v in devicerouting)
                {
                    if (v.ChannelOut > 0) //NA
                    {
                        Channels[v.ChannelOut - 1] += 1;
                    }
                }

                if (Channels[iWanted - 1] > 1) //déja affecté
                {
                    for (int i = 0; i < Channels.Length; i++)
                    {
                        if (Channels[i] == 0)
                        {
                            return i + 1;
                        }
                    }
                    return 0;
                }
                else { return iWanted; }

            }
            else { return iWanted; }
        }

        public void SendCC(Guid routingGuid, int iCC, int iValue, int iChannel, string sDevice)
        {
            var routing = MidiMatrix.FirstOrDefault(d => d.RoutingGuid == routingGuid && d.DeviceOut != null);
            if (routing != null)
            {
                CreateOUTEvent(new MidiEvent(TypeEvent.CC, new List<int> { iCC, iValue }, Tools.GetChannel(iChannel), sDevice), routing);
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

                    Thread.Sleep(10); //juste pour laisse au synthé le temps de faire son program change
                    CreateOUTEvent(new MidiEvent(TypeEvent.NOTE_ON, new List<int> { iNote, note.Velocity }, Tools.GetChannel(note.Channel), routing.DeviceOut.Name), routing);
                    Thread.Sleep((int)(note.Length));
                    CreateOUTEvent(new MidiEvent(TypeEvent.NOTE_OFF, new List<int> { iNote, note.Velocity }, Tools.GetChannel(note.Channel), routing.DeviceOut.Name), routing);
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

        public void OpenUsedPorts(bool bIn)
        {
            CheckAndCloseUnusedDevices();

            if (bIn)
            {
                foreach (var dev in UsedDevicesIN)
                {
                    dev.OpenDevice();
                    dev.OnMidiEvent += DeviceIn_OnMidiEvent;
                }
            }
            else
            {
                foreach (var dev in UsedDevicesOUT)
                {
                    dev.OpenDevice();
                    dev.OnMidiEvent += DeviceOut_OnMidiEvent;
                }
            }
        }

        public void CloseUsedPorts(bool bIn)
        {
            CheckAndCloseUnusedDevices();

            if (bIn)
            {
                foreach (var dev in UsedDevicesIN)
                {
                    dev.CloseDevice();
                    dev.OnMidiEvent -= DeviceIn_OnMidiEvent;
                }
            }
            else
            {
                foreach (var dev in UsedDevicesOUT)
                {
                    dev.CloseDevice();
                    dev.OnMidiEvent -= DeviceOut_OnMidiEvent;
                }
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
                    masterdevice.EnableClock();
                    masterdevice.OnMidiClockEvent += MidiClock_OnEvent;
                }
            }

            ClockRunning = bActivated;
            ClockBPM = iBPM;
            ClockDevice = sDevice;
        }

        internal List<LiveData> GetLiveCCData()
        {
            List<LiveData> listdata = new List<LiveData>();

            foreach (var item in MidiMatrix)
            {
                var CurrentOptions = item.Options.Clone();
                List<LiveCC> CurrentCC = new List<LiveCC>();

                foreach (var cc in item.LiveData)
                {
                    CurrentCC.Add(new LiveCC(cc.Device, cc.Channel, cc.CC, cc.CCValue));
                }

                LiveData CurrentData = new LiveData();

                CurrentData.StartCC = CurrentCC;
                CurrentData.StartOptions = CurrentOptions;
                CurrentData.RoutingGuid = item.RoutingGuid;
                CurrentData.Program = item.LiveProgram;
                if (item.DeviceOut != null) { CurrentData.DeviceOUT = item.DeviceOut.Name; }
                if (item.DeviceIn != null) { CurrentData.DeviceIN = item.DeviceIn.Name; }

                listdata.Add(CurrentData);
            }
            return listdata;
        }

        public void AddLifeToProject(bool value)
        {
            GiveLife = value;
        }

        public static bool CheckAndOpenINPort(string sDevice)
        {
            var exists = UsedDevicesIN.FirstOrDefault(d => d.Name.Equals(sDevice));

            if (exists == null)
            {
                var devIN = InputDevices.FirstOrDefault(d => d.Name.Equals(sDevice));
                if (devIN != null)
                {
                    var device = new MidiDevice(devIN);
                    device.IsReserverdForInternalPurposes = true;
                    UsedDevicesIN.Add(device);
                    return true;
                }
                else { return false; }
            }
            else { return false; }
        }

        public static void CheckAndCloseINPort(string sDevice)
        {
            var exists = UsedDevicesIN.FirstOrDefault(d => d.Name.Equals(sDevice) && !d.UsedForRouting);

            if (exists != null)
            {
                exists.CloseDevice();
                UsedDevicesIN.Remove(exists);
            }
        }

        #endregion
    }
}
