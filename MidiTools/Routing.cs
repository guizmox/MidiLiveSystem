﻿using Jacobi.Vst.Plugin.Framework.Plugin;
using MessagePack;
using MicroLibrary;
using Microsoft.Extensions.Primitives;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using RtMidi.Core;
using RtMidi.Core.Enums;
using RtMidi.Core.Messages;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Runtime;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;
using System.Xml.Linq;
using VSTHost;
using static MidiTools.MidiDevice;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TrackBar;

namespace MidiTools
{
    internal class MatrixItem
    {
        internal EventPool Tasks = new("MatrixItem");

        public CancellationTokenSource cancellationTokenSource = new();
        public CancellationToken cancellationToken => cancellationTokenSource.Token;

        public delegate void SequencerPlayNote(List<MidiEvent> eventsON, List<MidiEvent> eventsOFF, MatrixItem matrix);
        public event SequencerPlayNote OnSequencerPlayNote;

        private readonly int[] MinorChordsIntervals = new int[32] { 9, -4, -3, -4, -4, -5, -4, 4, -5, 9, -5, -3, 5, 4, -9, 5, -7, -5, -2, -1, -2, 23, -5, 21, 21, -4, -4, 19, 19, -3, -100, -100 };
        private readonly int[] MajorChordsIntervals = new int[32] { -4, -3, -4, -6, 7, -4, -5, -4, -3, 7, -3, -5, -7, 3, 5, -9, -3, 19, -2, -2, -2, 2, 19, -4, 20, -3, -4, 21, 21, -5, -3, 21 };
        private readonly int[] OtherIntervals = new int[32] { -3, -3, -5, -2, -2, -4, -2, -5, -6, -3, -5, -5, 19, -5, -6, 21, -2, 20, -3, -6, 18, -3, -2, 19, -100, -100, -100, -100, -100, -100, -100, -100 };

        private Harmony _harmony = Harmony.MAJOR;
        private int _noteHarmony = 0;

        public int CurrentATValue = 0;
        public bool[] NotesSentForPanic = new bool[128];
        internal int[] RandomizedCCValues = new int[128];

        public List<int> CurrentNotesPlayed
        {
            get
            {
                List<int> list = new();
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

        internal MidiOptions Options { get; set; } = new MidiOptions();
        internal MidiDevice DeviceIn;
        internal string DeviceInInternalName = "";
        internal MidiDevice DeviceOut;
        internal Sequencer DeviceInSequencer;

        internal bool TempActive = false; //utilisé pour muter provisoirement le routing à partir de l'UI
        internal bool ChangeLocked = false; //utilisé pour empêcher de boucler dans le même ModifyRouting si il y a des actions en cours

        private int _dropmode = 0;
        internal int DropMode { get { return _dropmode; } set { _dropmode = value; DropModeDateTime = DateTime.Now; } } //permet de créer une instance temporaire de ce routing pour la dropper dès que toutes les notes + sustain sont relâchés
        internal DateTime DropModeDateTime;
        //0 = rien, 1 = en attente (reçoit que les note off), 2 = à supprimer

        internal MidiPreset Preset { get; set; }
        internal Guid RoutingGuid { get; private set; }

        internal MatrixItem(MidiDevice MidiIN, MidiDevice MidiOUT, int iChIN, int iChOUT, MidiOptions options, MidiPreset preset)
        {
            DeviceIn = MidiIN;
            DeviceOut = MidiOUT;
            ChannelIn = iChIN;
            ChannelOut = iChOUT;
            Options = options;
            RoutingGuid = Guid.NewGuid();
            Preset = preset;
            Tasks.TaskerGuid = RoutingGuid;
        }

        internal MatrixItem(Sequencer sequencer, int iChIN, MidiDevice MidiOUT, int iChOUT, MidiOptions options, MidiPreset preset)
        {
            DeviceOut = MidiOUT;
            ChannelIn = iChIN;
            ChannelOut = iChOUT;
            Options = options;
            RoutingGuid = Guid.NewGuid();
            Preset = preset;
            DeviceInSequencer = sequencer;
            DeviceInSequencer.OnInternalSequencerStep += Sequencer_OnInternalSequencerStep;
        }

        private void Sequencer_OnInternalSequencerStep(SequenceStep notes, SequenceStep lastnotes, double lengthInMs, int lastPositionInSequence, int positionInSequence)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                if (DeviceInSequencer != null && !DeviceInSequencer.Muted && DeviceOut != null && ChannelOut > 0 && Options.Active)
                {
                    List<MidiEvent> eventsON = new();
                    List<MidiEvent> eventsOFF = new();

                    foreach (var note in notes.NotesAndVelocity)
                    {
                        int iNote = note[0] - DeviceInSequencer.TransposeOffset;
                        int iVelocity = note[1];

                        MidiEvent mvON = new(TypeEvent.NOTE_ON, new List<int> { iNote, iVelocity }, Tools.GetChannel(ChannelOut), DeviceOut.Name);
                        eventsON.Add(mvON);
                    }

                    foreach (var note in notes.NotesAndVelocity)
                    {
                        int iNote = note[0] - DeviceInSequencer.TransposeOffset;
                        int iVelocity = note[1];
                        int iLength = (int)Math.Round(lengthInMs);

                        MidiEvent mvOFF = new(TypeEvent.NOTE_OFF, new List<int> { iNote, iVelocity }, Tools.GetChannel(ChannelOut), DeviceOut.Name)
                        {
                            Delay = iLength
                        };
                        eventsOFF.Add(mvOFF);
                    }

                    OnSequencerPlayNote?.Invoke(eventsON, eventsOFF, this);
                }
            }
        }

        internal async Task CreateNote(NoteGenerator note, int iLowestNotePlayed)
        {
            await Tasks.AddTask(() =>
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    if (Options.Active && ChannelOut > 0)
                    {
                        int iNote = note.Note;
                        if (Options.PlayNote_LowestNote)
                        {
                            iNote = iLowestNotePlayed;
                        }

                        var evON = new MidiEvent(TypeEvent.NOTE_ON, new List<int> { iNote, note.Velocity }, Tools.GetChannel(note.Channel), DeviceOut.Name);
                        var evOFF = new MidiEvent(TypeEvent.NOTE_OFF, new List<int> { iNote, note.Velocity }, Tools.GetChannel(note.Channel), DeviceOut.Name)
                        {
                            Delay = (int)note.Length
                        };

                        OnSequencerPlayNote?.Invoke(new List<MidiEvent> { evON }, new List<MidiEvent> { evOFF }, this);
                    }
                }
            });
        }

        internal MatrixItem()
        {

        }

        internal void SetRoutingGuid(Guid guid)
        {
            RoutingGuid = guid;
            Tasks.TaskerGuid = RoutingGuid;
        }

        internal bool CheckDeviceIn(string sDeviceIn, int iChannel)
        {
            if (DeviceIn != null && DeviceIn.Name != sDeviceIn)
            {
                return true;
            }
            else
            {
                if (DeviceIn == null)
                {
                    if (MidiRouting.InputDevices.Any(i => i.Name.Equals(sDeviceIn)))
                    {
                        return true;
                    }
                    else if (!sDeviceIn.Equals(DeviceInInternalName))
                    {
                        return true;
                    }
                }

                if (DeviceInSequencer == null && sDeviceIn.Equals(Tools.INTERNAL_SEQUENCER)) //on est passé d'un device midi au séquenceur
                {
                    return true;
                }
                else if (DeviceInSequencer != null && sDeviceIn.Equals(Tools.INTERNAL_SEQUENCER)) //on est toujours sur le séquenceur
                {
                    if (DeviceInSequencer.Channel != iChannel) //mais on a changé de canal midi
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                else if (DeviceInSequencer != null && !sDeviceIn.Equals(Tools.INTERNAL_SEQUENCER)) //on est passé du séquenceur à un device midi ou l'internal generator
                {
                    return true;
                }
                else { return false; }
            }
        }

        internal bool CheckDeviceOut(string sDeviceOut)
        {
            if (DeviceOut != null && !DeviceOut.Name.Equals(sDeviceOut))
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

        internal List<MidiEvent> SetPlayMono(int iHigh, MidiEvent incomingEV)
        {
            //NotesSentForPanic[incomingEV.Values[0]] = true;

            //1 = high, 2=low, 3=intermediate high, 4=intermediate low
            List<MidiEvent> eventsOUT = new();
            int iPlayedNotes = NotesSentForPanic.Count(n => n == true);

            //en partant du haut, recherche un groupe de 3 notes jouées sur moins de 2 octaves et isoler celle du milieu ?
            Random r = new();
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
            Dictionary<int, int> occurrenceCount = new();

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
                NotesSentForPanic[eventOUT.Values[0]] = true;
            }
            else
            {
                NotesSentForPanic[eventOUT.Values[0]] = false;
            }
        }

        internal List<MidiEvent> SetHarmony(MidiEvent eventOUT)
        {
            List<MidiEvent> newNotes = new();

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

                List<int> bChord = new();
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

        internal List<int> ClearPendingNotes()
        {
            List<int> NotesToRemove = new();

            for (int i = 0; i < 128; i++)
            {
                if (NotesSentForPanic[i])
                {
                    NotesToRemove.Add(i);
                    NotesSentForPanic[i] = false;
                }
            }

            if (_noteHarmony > 0)
            {
                NotesToRemove.Add(_noteHarmony);
                _noteHarmony = 0;
                _harmony = Harmony.MAJOR;
            }

            return NotesToRemove;
        }

        static List<int> FindClosestNumbers(List<int> numbers, int count)
        {
            if (count >= numbers.Count)
            {
                return numbers;
            }

            List<int> sortedNumbers = numbers.OrderBy(x => x).ToList();

            int minDifference = int.MaxValue;
            List<int> closestNumbers = new();

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
            Tuple<bool, int> Chord = new(false, 0);

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
                                iOffset = 6;
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

        internal void AddSequencer(Sequencer deviceInSequencer)
        {
            if (DeviceInSequencer != null)
            {
                DeviceInSequencer.OnInternalSequencerStep -= Sequencer_OnInternalSequencerStep;
                DeviceInSequencer = null;
            }

            DeviceInSequencer = deviceInSequencer;
            DeviceInSequencer.OnInternalSequencerStep += Sequencer_OnInternalSequencerStep;
        }

        internal void RemoveSequencer()
        {
            if (DeviceInSequencer != null)
            {
                DeviceInSequencer.OnInternalSequencerStep -= Sequencer_OnInternalSequencerStep;
                DeviceInSequencer = null;
            }
        }

        internal async Task CancelTask()
        {
            cancellationTokenSource.Cancel();

            while (Tasks.TasksRunning > 0)
            {
                await Task.Delay(5);
            }

            cancellationTokenSource = new CancellationTokenSource();
        }

        internal MatrixItem Clone()
        {
            var mi = (MatrixItem)this.MemberwiseClone();
            mi.SetRoutingGuid(Guid.NewGuid());
            mi.Options.Active = true;
            mi.ChangeLocked = true;
            cancellationTokenSource = new CancellationTokenSource();
            mi.DropMode = 1;

            return mi;
        }
    }

    public class MidiRouting
    {
        private List<string> _allinputs = new();

        private readonly EventPool Tasks = new("MidiRouting");

        private List<string> DevicesRoutingIN = new();
        private List<string> DevicesRoutingOUT = new();

        private static readonly int CLOCK_INTERVAL = 1000;

        private bool ClockRunning = false;
        private int ClockBPM = 120;
        private string ClockDevice = "";
        private bool GiveLife = false;

        public static List<RtMidi.Core.Devices.Infos.IMidiInputDeviceInfo> InputDevices
        {
            get
            {
                List<RtMidi.Core.Devices.Infos.IMidiInputDeviceInfo> list = new();
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
                List<RtMidi.Core.Devices.Infos.IMidiOutputDeviceInfo> list = new();
                foreach (var outputDeviceInfo in MidiDeviceManager.Default.OutputDevices)
                {
                    list.Add(outputDeviceInfo);
                }
                return list;
            }
        }

        public delegate void LogEventHandler(string sDevice, bool bIn, string sLog);
        public static event LogEventHandler NewLog;

        public delegate void OutputtingMidiEventHandler(bool b, Guid routingGuid);
        public static event OutputtingMidiEventHandler OutputMidiMessage;

        public delegate void InputMidiEventHandler(MidiEvent ev);
        public static event InputMidiEventHandler InputStaticMidiMessage;

        public delegate void OutputCCMidiEventHandler(Guid routingGuid, List<int> CC);
        public static event OutputCCMidiEventHandler OutputCCValues;

        public int Events { get { return _eventsProcessedINLast + _eventsProcessedOUTLast; } }

        public string CyclesInfo
        {
            get
            {
                StringBuilder sbMessage = new();
                sbMessage.Append("MIDI Avg. Messages / Sec. : ");
                sbMessage.Append(" [IN] : " + (_eventsProcessedINLast).ToString());
                sbMessage.Append(" / [OUT] : " + (_eventsProcessedOUTLast).ToString());
                sbMessage.Append(" / Latency : " + Math.Round(_processinglatency / _allinevents, 2).ToString() + " ms");
                int iMatrix = 0;
                int iSum = 0;
                lock (MidiMatrix) { iMatrix = MidiMatrix.Count; iSum = MidiMatrix.Sum(m => m.Tasks.LastMinuteProcessing); }
                sbMessage.Append(" (" + MidiMatrix.Count + " Routing(s))");
                sbMessage.Append(" - Tasks/Min : " + iSum);
                return sbMessage.ToString();
            }
        }

        public int LowestNoteRunning { get { return _lowestNotePlayed; } }
        private List<string> AllInputs { get { lock (_allinputs) { return _allinputs; } } set { lock (_allinputs) { _allinputs = value; } } }

        private readonly List<MatrixItem> MidiMatrix = new();
        internal static List<MidiDevice> UsedDevicesIN = new();
        internal static List<MidiDevice> UsedDevicesOUT = new();

        internal static int HasOutDevices { get { return UsedDevicesOUT.Count; } }

        private readonly System.Timers.Timer EventsCounter;
        private readonly System.Timers.Timer CloseDevicesTimer;
        private readonly MicroTimer MidiClock;

        //internal List<LiveCC> LiveData = new List<LiveCC>();

        private int _eventsProcessedIN = 0;
        private int _eventsProcessedOUT = 0;
        private int _eventsProcessedINLast = 0;
        private int _eventsProcessedOUTLast = 0;
        private double _processinglatency = 0;
        private int _allinevents = 1;

        private int _lowestNotePlayed = -1;
        private string AudioDevice = "";

        public MidiRouting()
        {
            //MidiDevice.OnLogAdded += MidiDevice_OnLogAdded;

            EventsCounter = new System.Timers.Timer();
            EventsCounter.Elapsed += QueueProcessor_OnEvent;
            EventsCounter.Interval = CLOCK_INTERVAL;
            EventsCounter.Start();

            MidiClock = new MicroTimer();
            MidiClock.MicroTimerElapsed += MidiClock_OnEvent;
            MidiClock.Interval = Tools.GetMidiClockInterval(ClockBPM); //valeur par défaut

            CloseDevicesTimer = new System.Timers.Timer();
            CloseDevicesTimer.Elapsed += CheckAndCloseUnusedDevices;
            CloseDevicesTimer.Interval = (10 * 1000); //valeur par défaut
            CloseDevicesTimer.Start();

            InstrumentData.OnSysExInitializerChanged += InstrumentData_OnSysExInitializerChanged;
        }

        #region PRIVATE

        private async void QueueProcessor_OnEvent(object sender, ElapsedEventArgs e)
        {
            await DropTransitionRouting();

            _eventsProcessedINLast = _eventsProcessedIN;
            _eventsProcessedOUTLast = _eventsProcessedOUT;
            _eventsProcessedIN = 0;
            _eventsProcessedOUT = 0;

            if (DateTime.Now.Second == 0) //réinit toutes les minutes
            { _processinglatency = 0; _allinevents = 1; }
        }

        private async void MatrixItem_OnSequencerPlayNote(List<MidiEvent> eventsON, List<MidiEvent> eventsOFF, MatrixItem matrix)
        {
            List<Task> tasks = new();

            foreach (var item in eventsON)
            {
                tasks.Add(matrix.Tasks.AddTask(async () => await GenerateOUTEvent(item, matrix, new TrackerGuid())));
            }

            foreach (var item in eventsOFF)
            {
                tasks.Add(matrix.Tasks.AddTask(async () => await GenerateOUTEvent(item, matrix, new TrackerGuid())));
            }

            await Task.WhenAll(tasks);
        }

        private async void MidiClock_OnEvent(object sender, MicroTimerEventArgs e)
        {
            var tasks = UsedDevicesOUT.Select(device =>
            {
                return Tasks.AddTask(() => { device.SendMidiEvent(new MidiEvent(TypeEvent.CLOCK, null, Channel.Channel1, device.Name)); });
            });

            await Task.WhenAll(tasks);
        }

        private void CheckAndCloseUnusedDevices(object sender, ElapsedEventArgs e)
        {
            List<string> ToRemoveIN = new();

            lock (UsedDevicesIN)
            {
                foreach (var devin in UsedDevicesIN.Where(d => !d.IsReserverdForInternalPurposes))
                {
                    if (!DevicesRoutingIN.Contains(devin.Name) && DateTime.Now.Subtract(devin.LastMessage).TotalSeconds > 60)
                    {
                        ToRemoveIN.Add(devin.Name);
                    }
                }

                foreach (string s in ToRemoveIN)
                {
                    var d = UsedDevicesIN.FirstOrDefault(d => d.Name.Equals(s));
                    d.CloseDevice();
                    d.OnMidiEvent -= DeviceIn_OnMidiEvent;

                    UsedDevicesIN.Remove(d);
                }
            }

            List<string> ToRemoveOUT = new();

            lock (UsedDevicesOUT)
            {
                foreach (var devout in UsedDevicesOUT.Where(d => DateTime.Now.Subtract(d.LastMessage).TotalSeconds > 60))
                {
                    if (!DevicesRoutingOUT.Contains(devout.Name) && DateTime.Now.Subtract(devout.LastMessage).TotalSeconds > 60)
                    {
                        if (!devout.Name.StartsWith(Tools.VST_HOST)) //on ne supprime pas les slots VST, potentiellement utilisés à plusieurs endroits
                        {
                            ToRemoveOUT.Add(devout.Name);
                        }
                    }

                }

                foreach (string s in ToRemoveOUT)
                {
                    var d = UsedDevicesOUT.FirstOrDefault(d => d.Name.Equals(s));
                    d.CloseDevice();

                    UsedDevicesOUT.Remove(d);
                }
            }
        }

        private async void InstrumentData_OnSysExInitializerChanged(InstrumentData instr)
        {
            var routing = MidiMatrix.FirstOrDefault(m => m.DeviceOut != null && m.DeviceOut.Name.Equals(instr.Device));
            if (routing != null && instr.SysExInitializer.Length > 0)
            {
                await SendGenericMidiEvent(new MidiEvent(TypeEvent.SYSEX, instr.SysExInitializer, instr.Device), routing);
            }
        }

        private async void DeviceIn_OnMidiEvent(bool bIn, MidiEvent ev)
        {
            _eventsProcessedIN += 1;

            //mémorisation de la note pressée la plus grave
            if (ev.Type == TypeEvent.NOTE_ON && ev.Values[0] < _lowestNotePlayed) { _lowestNotePlayed = ev.Values[0]; }
            else if (ev.Type == TypeEvent.NOTE_OFF && ev.Values[0] == _lowestNotePlayed) { _lowestNotePlayed = -1; }

            InputStaticMidiMessage?.Invoke(ev);

            await CreateOUTEventFromInput(ev);
        }

        private static void DeviceIn_StaticOnMidiEvent(bool bIn, MidiEvent ev)
        {
            InputStaticMidiMessage?.Invoke(ev);
        }

        private void MidiDevice_OnLogAdded(string sDevice, bool bIn, string sLog)
        {
            NewLog?.Invoke(sDevice, bIn, sLog);
        }

        private async Task GenerateOUTEvent(MidiEvent ev, MatrixItem routingOUT, TrackerGuid routingTracker)
        {
            _allinevents += 1;
            DateTime dtStart = DateTime.Now;

            MidiDevice deviceOut = UsedDevicesOUT.FirstOrDefault(d => d.Name.Equals(routingOUT.DeviceOut.Name));
            int iChannelOut = routingOUT.ChannelOut;

            if (routingOUT.DropMode < 2)
            {
                try
                {
                    OutputMidiMessage?.Invoke(true, routingOUT.RoutingGuid);

                    List<MidiEvent> EventsToProcess = await EventPreProcessor(routingOUT, deviceOut, iChannelOut, ev, true, routingOUT.cancellationToken);

                    await routingOUT.Tasks.AddTask(() =>
                    {

                        EventsToProcess = FilterEventsDropMode(EventsToProcess, routingOUT);

                        for (int i = 0; i < EventsToProcess.Count; i++)
                        {
                            if (EventsToProcess[i].Type == TypeEvent.CC) { OutputCCValues?.Invoke(routingOUT.RoutingGuid, EventsToProcess[i].Values); }

                            if (routingOUT.cancellationToken.IsCancellationRequested) { return; }

                            _eventsProcessedOUT += 1;

                            if (EventsToProcess[i].Delay > 0)
                            {
                                Thread.Sleep(EventsToProcess[i].Delay);
                            }

                            deviceOut.SendMidiEvent(EventsToProcess[i]);

                            if (EventsToProcess[i].ReleaseCC) //utilisé en mode Smooth CC pour débloquer le CC bloqué par le smooth
                            {
                                deviceOut.UnblockCC(EventsToProcess[i].Values[0], iChannelOut);
                            }
                        }

                        OutputMidiMessage?.Invoke(false, routingOUT.RoutingGuid);
                    });
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception)
                {
                    return;
                }
            }

            routingTracker.Clear();

            _processinglatency += (DateTime.Now - dtStart).TotalMilliseconds;
        }

        private async Task CreateOUTEventFromInput(MidiEvent ev)
        {
            _allinevents += 1;
            DateTime dtStart = DateTime.Now;
       
            //attention : c'est bien un message du device IN qui arrive !
            var matrix = MidiMatrix.Where(i => i.DeviceOut != null
                                                       && i.DropMode < 2
                                                       && i.Options.Active
                                                       && i.DeviceIn != null
                                                       && (i.DeviceIn.Name == ev.Device || i.DeviceIn.Name.Equals(Tools.ALL_INPUTS) && AllInputs.Contains(ev.Device))
                                                       && (Tools.GetChannel(i.ChannelIn) == ev.Channel) || i.ChannelIn == 0).OrderBy(r => r.Options.PlayMode);

            List<Task> tasksmatrix = new();

            foreach (MatrixItem routing in matrix)
            {
                if (routing.cancellationToken.IsCancellationRequested) { break; }

                tasksmatrix.Add(routing.Tasks.AddTask(async () =>
                {
                    MidiDevice deviceOut = UsedDevicesOUT.FirstOrDefault(d => d.Name.Equals(routing.DeviceOut.Name));
                    int iChannelOut = routing.ChannelOut;

                    try
                    {
                        OutputMidiMessage?.Invoke(true, routing.RoutingGuid);

                        List<MidiEvent> EventsToProcess = await EventPreProcessor(routing, deviceOut, iChannelOut, ev, false, routing.cancellationToken);

                        EventsToProcess = FilterEventsDropMode(EventsToProcess, routing);

                        for (int i = 0; i < EventsToProcess.Count; i++)
                        {
                            if (EventsToProcess[i].Type == TypeEvent.CC) { OutputCCValues?.Invoke(routing.RoutingGuid, EventsToProcess[i].Values); }

                            if (routing.cancellationToken.IsCancellationRequested) { return; }

                            _eventsProcessedOUT += 1;

                            if (EventsToProcess[i].Delay > 0)
                            {
                                Thread.Sleep(EventsToProcess[i].Delay);
                            }

                            deviceOut.SendMidiEvent(EventsToProcess[i]);

                            if (EventsToProcess[i].ReleaseCC) //utilisé en mode Smooth CC pour débloquer le CC bloqué par le smooth
                            {
                                deviceOut.UnblockCC(EventsToProcess[i].Values[0], iChannelOut);
                            }
                        }

                        OutputMidiMessage?.Invoke(false, routing.RoutingGuid);
                    }
                    catch (OperationCanceledException)
                    {

                    }
                    catch (Exception)
                    {

                    }
                }));
            }

            await Task.WhenAll(tasksmatrix);

            _processinglatency += (DateTime.Now - dtStart).TotalMilliseconds;
        }

        private static List<MidiEvent> FilterEventsDropMode(List<MidiEvent> eventsToProcess, MatrixItem routing)
        {
            if (routing.DropMode == 1)
            {
                if (!routing.DeviceOut.PendingNotesOrSustain(routing.ChannelOut))
                {
                    routing.DropMode = 2;
                }
                eventsToProcess = eventsToProcess.Where(e => !(e.Type == TypeEvent.CC && e.Values[0] == 64 && e.Values[1] >= 64)).ToList();
                eventsToProcess = eventsToProcess.Where(e => e.Type != TypeEvent.NOTE_ON).ToList();
            }
            return eventsToProcess;
        }

        private async Task DropTransitionRouting()
        {
            await Tasks.AddTask(() =>
            {
                lock (MidiMatrix)
                {
                    List<MatrixItem> todrop = MidiMatrix.Where(mm => mm.DropMode == 2).ToList();
                    int qte = todrop.Count;
                    for (int i = 0; i < qte; i++)
                    {
                        MidiMatrix.Remove(todrop[i]);
                    }

                    todrop = MidiMatrix.Where(mm => mm.DropMode == 1 && (DateTime.Now - mm.DropModeDateTime).TotalSeconds > 10).ToList();
                    {
                        int qte2 = todrop.Count;
                        for (int i = 0; i < qte2; i++)
                        {
                            todrop[i].DeviceOut.SendMidiEvent(new MidiEvent(TypeEvent.CC, new List<int> { 123, 127 }, Tools.GetChannel(todrop[i].ChannelOut), todrop[i].DeviceOut.Name));
                            todrop[i].DeviceOut.ClearNotes();
                            //for (int iKey = 0; iKey < 128; iKey++)
                            //{
                            //    while (todrop[i].DeviceOut.GetLiveNOTEValue(todrop[i].ChannelOut, iKey))
                            //    {
                            //        todrop[i].DeviceOut.SendMidiEvent(new MidiEvent(TypeEvent.NOTE_OFF, new List<int> { iKey, 0 }, Tools.GetChannel(todrop[i].ChannelOut), todrop[i].DeviceOut.Name));
                            //    }
                            //    MidiMatrix.Remove(todrop[i]);
                            //}
                        }
                        for (int i = 0; i < qte2; i++)
                        {
                            MidiMatrix.Remove(todrop[i]);
                        }
                    }
                    todrop.Clear();
                }
            });
        }

        private async Task<List<MidiEvent>> EventPreProcessor(MatrixItem routing, MidiDevice deviceout, int iChannelOut, MidiEvent evIN, bool bOutEvent, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!bOutEvent)
            {
                var translatedEvents = await MidiTranslator(routing, deviceout, iChannelOut, evIN, cancellationToken);
                if (translatedEvents.Count > 0) { return translatedEvents; }
            }
            //TRANSLATEUR DE MESSAGES

            MidiEvent _eventsOUT = null;

            //opérations de filtrage 
            switch (evIN.Type)
            {
                case TypeEvent.CC:
                    if (routing.Options.AllowAllCC || bOutEvent)
                    {
                        var convertedCC = routing.Options.CC_Converters.FirstOrDefault(i => i[0] == evIN.Values[0]);

                        if (routing.Options.AllowUndefinedCC)
                        {
                            _eventsOUT = new MidiEvent(evIN.Type, new List<int> { convertedCC != null ? convertedCC[1] : evIN.Values[0], evIN.Values[1] }, Tools.GetChannel(iChannelOut), deviceout.Name);
                        }
                        else
                        {
                            //si le CC entrant n'est pas présent dans la liste des CC non définis,
                            //ou bien qu'on a une transcodification du message, on laisse passer le CC
                            //sinon on ne fait rien
                            if (!routing.Options.UndefinedCC.Contains(evIN.Values[0]) || convertedCC != null)
                            {
                                _eventsOUT = new MidiEvent(evIN.Type, new List<int> { convertedCC != null ? convertedCC[1] : evIN.Values[0], evIN.Values[1] }, Tools.GetChannel(iChannelOut), deviceout.Name);
                            }
                        }
                    }
                    break;
                case TypeEvent.CH_PRES:
                    if ((routing.Options.PlayMode != PlayModes.AFTERTOUCH && routing.Options.AllowAftertouch) || bOutEvent)
                    {
                        _eventsOUT = new MidiEvent(evIN.Type, new List<int> { evIN.Values[0] }, Tools.GetChannel(iChannelOut), deviceout.Name);
                    }
                    else if (routing.Options.PlayMode == PlayModes.AFTERTOUCH)
                    {
                        routing.CurrentATValue = evIN.Values[0];

                        _eventsOUT = new MidiEvent(TypeEvent.CC, new List<int> { routing.DeviceOut.CC[7], evIN.Values[0] }, Tools.GetChannel(iChannelOut), deviceout.Name);
                    }
                    break;
                case TypeEvent.NOTE_OFF:
                    if (routing.Options.AllowNotes || bOutEvent)
                    {
                        int[] iNoteAndVel = Tools.GetNoteIndex(evIN.Values[0], evIN.Values[1], routing, true);
                        var convertedNote = routing.Options.Note_Converters.FirstOrDefault(i => i[0] == evIN.Values[0]);
                        if (convertedNote != null) { iNoteAndVel[0] = convertedNote[1]; } //TODO DOUTE

                        if (iNoteAndVel[0] > -1)
                        {
                            routing.CurrentATValue = 0;

                            var eventout = new MidiEvent(evIN.Type, new List<int> { iNoteAndVel[0], routing.Options.PlayMode == PlayModes.AFTERTOUCH ? 0 : evIN.Values[1] }, Tools.GetChannel(iChannelOut), deviceout.Name);
                            _eventsOUT = eventout;
                        }
                    }
                    break;
                case TypeEvent.NOTE_ON:
                    if (routing.Options.AllowNotes || bOutEvent)
                    {
                        int[] iNoteAndVel = Tools.GetNoteIndex(evIN.Values[0], evIN.Values[1], routing, false);
                        var convertedNote = routing.Options.Note_Converters.FirstOrDefault(i => i[0] == evIN.Values[0]);
                        if (convertedNote != null) { iNoteAndVel[0] = convertedNote[1]; } //TODO DOUTE

                        if (iNoteAndVel[0] > -1) //NOTE : il faut systématiquement mettre au moins une véolcité de 1 pour que la note se déclenche
                        {
                            //int iVelocity = item.Options.AftertouchVolume && ev.Values[1] > 0 ? 1 : ev.Values[1];
                            var eventout = new MidiEvent(evIN.Type, new List<int> { iNoteAndVel[0], iNoteAndVel[1] }, Tools.GetChannel(iChannelOut), deviceout.Name);

                            //problème du device comme le softstep qui peut envoyer à répétition plusieurs notes on sans le note off
                            //alors qu'en mode note generator on veut pouvoir lancer autant qu'on veut
                            //if (!bOutEvent && routing.DeviceOut.GetLiveNOTEValue(iChannelOut, iNoteAndVel[0]))
                            //{
                            //    //on n'envoie aucun nouvel évènement
                            //}
                            //else
                            //{
                            //    _eventsOUT = eventout;
                            //}
                            _eventsOUT = eventout;
                        }
                    }
                    break;
                case TypeEvent.NRPN:
                    if (routing.Options.AllowNrpn || bOutEvent)
                    {
                        _eventsOUT = new MidiEvent(evIN.Type, new List<int> { evIN.Values[0], evIN.Values[1] }, Tools.GetChannel(iChannelOut), deviceout.Name);
                    }
                    break;
                case TypeEvent.PB:
                    if (routing.Options.AllowPitchBend || bOutEvent)
                    {
                        _eventsOUT = new MidiEvent(evIN.Type, new List<int> { evIN.Values[0] }, Tools.GetChannel(iChannelOut), deviceout.Name);
                    }
                    break;
                case TypeEvent.PC:
                    if (routing.Options.AllowProgramChange || bOutEvent)
                    {
                        _eventsOUT = new MidiEvent(evIN.Type, new List<int> { evIN.Values[0] }, Tools.GetChannel(iChannelOut), deviceout.Name);
                    }
                    break;
                case TypeEvent.POLY_PRES:
                    if (routing.Options.AllowAftertouch || bOutEvent)
                    {
                        _eventsOUT = new MidiEvent(evIN.Type, new List<int> { evIN.Values[0], evIN.Values[1] }, Tools.GetChannel(iChannelOut), deviceout.Name);
                    }
                    break;
                case TypeEvent.SYSEX:
                    if (routing.Options.AllowSysex || bOutEvent)
                    {
                        _eventsOUT = new MidiEvent(evIN.Type, SysExTranscoder(evIN.SysExData), deviceout.Name);
                    }
                    break;
            }

            if (_eventsOUT != null)
            {
                _eventsOUT.Delay = evIN.Delay; //si on avait un délai positionné sur l'évènement entrant (cas du séquenceur notamment)

                return EventProcessor(routing, deviceout, iChannelOut, _eventsOUT, cancellationToken);
            }
            else { return new List<MidiEvent>(); }
        }

        private List<MidiEvent> EventProcessor(MatrixItem routing, MidiDevice deviceout, int iChannelOut, MidiEvent eventOUT, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            List<MidiEvent> EventsToProcess = new();

            bool bAvoidCCToSmooth = false;

            switch (eventOUT.Type)
            {
                case TypeEvent.PC: //
                    EventsToProcess.Add(eventOUT);
                    break;

                case TypeEvent.CC: //smooth CC
                    //si l'item est bloqué à cause du smooth, on interdit les nouveaux entrants
                    if (deviceout.IsBlocked(eventOUT.Values[0], iChannelOut))
                    {
                        bAvoidCCToSmooth = true;
                    }
                    else
                    {
                        //sinon on enregistre et on crée une liste d'events additionnels
                        //on bloque également les CC qui sont passés en Smooth (pour empêcher l'arrivée d'infos contradictoires)
                        var newitems = deviceout.SetCC(eventOUT.Values[0], eventOUT.Values[1], routing.Options, iChannelOut);

                        if (routing.Options.SmoothCC && newitems.Count > 0)
                        {
                            bAvoidCCToSmooth = true;

                            foreach (var cc in newitems)
                            {
                                cc.Delay = (int)(routing.Options.SmoothCCLength / newitems.Count); //le smoooth doit durer 1sec
                                EventsToProcess.Add(cc);
                            }
                            EventsToProcess.Last().ReleaseCC = true;
                        }
                    }

                    if (!bAvoidCCToSmooth) //ne pas jouer le CC  trop rapide, il a été joué par le Smooth quelques lignes plus haut
                    {
                        EventsToProcess.Add(eventOUT);
                    }
                    break;

                case TypeEvent.NOTE_ON:
                case TypeEvent.NOTE_OFF: //delayer
                                         //if (!bOutEvent) // ne doit fonctionner qu'avec les input du routing et pas avec les évènements forcés
                                         //{
                    bool bMono = false;
                    int iVelocity = 0;

                    switch (routing.Options.PlayMode)
                    {
                        case PlayModes.AFTERTOUCH:
                            EventsToProcess.Add(new MidiEvent(MidiDevice.TypeEvent.CC, new List<int> { 7, routing.CurrentATValue }, eventOUT.Channel, eventOUT.Device));
                            routing.MemorizeNotesPlayed(eventOUT);
                            break;
                        case PlayModes.MONO_HIGH:
                            EventsToProcess.AddRange(routing.SetPlayMono(1, eventOUT));
                            bMono = EventsToProcess.Count > 0;
                            break;
                        case PlayModes.MONO_LOW:
                            EventsToProcess.AddRange(routing.SetPlayMono(2, eventOUT));
                            bMono = EventsToProcess.Count > 0;
                            break;
                        case PlayModes.MONO_INTERMEDIATE_HIGH:
                            EventsToProcess.AddRange(routing.SetPlayMono(3, eventOUT));
                            bMono = EventsToProcess.Count > 0;
                            break;
                        case PlayModes.MONO_INTERMEDIATE_LOW:
                            EventsToProcess.AddRange(routing.SetPlayMono(4, eventOUT));
                            bMono = EventsToProcess.Count > 0;
                            break;
                        case PlayModes.MONO_IN_BETWEEN:
                            EventsToProcess.AddRange(routing.SetPlayMono(5, eventOUT));
                            bMono = true;
                            break;
                        case PlayModes.HARMONY:
                            var newev = routing.SetHarmony(eventOUT);
                            EventsToProcess.AddRange(newev);
                            bMono = true;
                            break;
                        case PlayModes.PIZZICATO_FAST:
                            var evON = new MidiEvent(TypeEvent.NOTE_ON, new List<int> { eventOUT.Values[0], eventOUT.Values[1] }, eventOUT.Channel, eventOUT.Device);
                            var evOFF = new MidiEvent(TypeEvent.NOTE_OFF, new List<int> { eventOUT.Values[0], 0 }, eventOUT.Channel, eventOUT.Device)
                            {
                                Delay = routing.Options.PlayModeOption
                            };
                            EventsToProcess.Add(evON);
                            EventsToProcess.Add(evOFF);
                            bMono = true;
                            break;
                        case PlayModes.PIZZICATO_SLOW:
                            var evON2 = new MidiEvent(TypeEvent.NOTE_ON, new List<int> { eventOUT.Values[0], eventOUT.Values[1] }, eventOUT.Channel, eventOUT.Device);
                            var evOFF2 = new MidiEvent(TypeEvent.NOTE_OFF, new List<int> { eventOUT.Values[0], 0 }, eventOUT.Channel, eventOUT.Device)
                            {
                                Delay = routing.Options.PlayModeOption
                            };
                            EventsToProcess.Add(evON2);
                            EventsToProcess.Add(evOFF2);
                            bMono = true;
                            break;
                        case PlayModes.REPEAT_NOTE_OFF_FAST:
                            if (eventOUT.Type == TypeEvent.NOTE_OFF)
                            {
                                iVelocity = deviceout.GetLiveVelocityValue(Tools.GetChannelInt(eventOUT.Channel), eventOUT.Values[0]);
                                iVelocity = (int)(iVelocity * (routing.Options.PlayModeOption / 100.0));
                                if (iVelocity <= 0) { iVelocity = 1; } //parti pris
                                else if (iVelocity > 127) { iVelocity = 127; }
                                var evONDouble = new MidiEvent(TypeEvent.NOTE_ON, new List<int> { eventOUT.Values[0], iVelocity }, eventOUT.Channel, eventOUT.Device);
                                var evOFFDouble = new MidiEvent(TypeEvent.NOTE_OFF, new List<int> { eventOUT.Values[0], 0 }, eventOUT.Channel, eventOUT.Device)
                                {
                                    Delay = 200
                                };
                                EventsToProcess.Add(eventOUT);
                                EventsToProcess.Add(evONDouble);
                                EventsToProcess.Add(evOFFDouble);
                                bMono = true;
                            }
                            break;
                        case PlayModes.REPEAT_NOTE_OFF_SLOW:
                            if (eventOUT.Type == TypeEvent.NOTE_OFF)
                            {
                                iVelocity = deviceout.GetLiveVelocityValue(Tools.GetChannelInt(eventOUT.Channel), eventOUT.Values[0]);
                                iVelocity = (int)(iVelocity * (routing.Options.PlayModeOption / 100.0));
                                if (iVelocity <= 0) { iVelocity = 1; } //parti pris
                                else if (iVelocity > 127) { iVelocity = 127; }
                                var evONDouble = new MidiEvent(TypeEvent.NOTE_ON, new List<int> { eventOUT.Values[0], iVelocity }, eventOUT.Channel, eventOUT.Device);
                                var evOFFDouble = new MidiEvent(TypeEvent.NOTE_OFF, new List<int> { eventOUT.Values[0], 0 }, eventOUT.Channel, eventOUT.Device)
                                {
                                    Delay = 600
                                };
                                EventsToProcess.Add(eventOUT);
                                EventsToProcess.Add(evONDouble);
                                EventsToProcess.Add(evOFFDouble);
                                bMono = true;
                            }
                            break;
                        case PlayModes.OCTAVE_UP:
                            EventsToProcess.Add(eventOUT);
                            iVelocity = (int)(eventOUT.Values[1] * (routing.Options.PlayModeOption / 100.0));
                            if (iVelocity <= 0) { iVelocity = 1; } //parti pris
                            else if (iVelocity > 127) { iVelocity = 127; }
                            EventsToProcess.Add(new MidiEvent(eventOUT.Type, new List<int> { eventOUT.Values[0] + 12, iVelocity }, eventOUT.Channel, eventOUT.Device));
                            bMono = true;
                            break;
                        case PlayModes.OCTAVE_DOWN:
                            EventsToProcess.Add(eventOUT);
                            iVelocity = (int)(eventOUT.Values[1] * (routing.Options.PlayModeOption / 100.0));
                            if (iVelocity <= 0) { iVelocity = 1; } //parti pris
                            else if (iVelocity > 127) { iVelocity = 127; }
                            EventsToProcess.Add(new MidiEvent(eventOUT.Type, new List<int> { eventOUT.Values[0] - 12, iVelocity }, eventOUT.Channel, eventOUT.Device));
                            bMono = true;
                            break;
                        default:
                            routing.MemorizeNotesPlayed(eventOUT);
                            break;
                    }

                    if (routing.Options.AddLife > 0) //donne de la vie au projet en ajoutant un peu de pitch bend et de delay
                    {
                        Random random = new();

                        if (eventOUT.Type == TypeEvent.NOTE_ON)
                        {
                            int randomWaitON = random.Next(1, (routing.Options.AddLife * 10));
                            int randomPB = random.Next(8192 - (routing.Options.AddLife * 250), 8192 + (routing.Options.AddLife * 250));

                            int iNewVol = RandomizeCCValue(7, (routing.Options.AddLife * 5), routing);
                            int iNewMod = RandomizeCCValue(1, (routing.Options.AddLife * 5), routing);
                            //int iNewPan = 0; // routing.RandomizeCCValue(10, copiedevent.Channel, copiedevent.Device, 5);

                            MidiEvent pbRandom = new(TypeEvent.PB, new List<int> { randomPB }, eventOUT.Channel, eventOUT.Device);

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
                            //    deviceout.SendMidiEvent(new MidiEvent(TypeEvent.CC, new List<int> { 10, iNewPan }, copiedevent.Channel, copiedevent.Device));
                            //}

                            EventsToProcess.Add(pbRandom);

                            if (!bMono)
                            {
                                eventOUT.Delay = randomWaitON + (routing.Options.DelayNotesLength > 0 ? routing.Options.DelayNotesLength : 0);
                                EventsToProcess.Add(eventOUT);
                            }
                        }
                        else
                        {
                            if (!bMono)
                            {
                                int randomWaitOFF = (routing.Options.AddLife * 10) + 10; //trick pour éviter que les note off se déclenchent avant les note on
                                eventOUT.Delay = randomWaitOFF + (routing.Options.DelayNotesLength > 0 ? routing.Options.DelayNotesLength : 0);
                                //deviceout.SendMidiEvent(eventOUT);
                                EventsToProcess.Add(eventOUT);
                            }
                        }
                    }
                    else if (routing.Options.DelayNotesLength > 0)
                    {
                        if (!bMono)
                        {
                            eventOUT.Delay = routing.Options.DelayNotesLength;
                            EventsToProcess.Add(eventOUT);
                        }
                    }
                    else
                    {
                        if (!bMono)
                        {
                            EventsToProcess.Add(eventOUT);
                        }
                    }
                    //}
                    //else { EventsToProcess.Add(eventOUT); }
                    break;

                default:
                    EventsToProcess.Add(eventOUT);
                    break;
            }

            return EventsToProcess.OrderBy(ev => ev.Type).ToList();
        }

        private async Task ChangeOptions(MatrixItem newrouting, MatrixItem oldrouting, MidiOptions newop, bool bInit)
        {
            if (newrouting.DeviceOut != null && newrouting.ChannelOut > 0)
            {
                if (newop == null) //tout charger
                {
                    for (int iCC = 0; iCC < 128; iCC++)
                    {
                        if (newrouting.Options.DefaultRoutingCC[iCC] > -1 || (bInit && newrouting.Options.DefaultRoutingCC[iCC] > -1))
                        {
                            await newrouting.Tasks.AddTask(() => newrouting.DeviceOut.SendMidiEvent(new MidiEvent(TypeEvent.CC, new List<int>() { iCC, newrouting.Options.DefaultRoutingCC[iCC] }, Tools.GetChannel(newrouting.ChannelOut), newrouting.DeviceOut.Name)));
                        }
                    }
                }
                else
                {
                    if (newop.TranspositionOffset != newrouting.Options.TranspositionOffset || newop.PlayMode != newrouting.Options.PlayMode) //midi panic
                    {
                        await SetDeviceMonoMode(newrouting, newrouting.DeviceOut, newrouting.ChannelOut, newop.PlayMode);

                        if (oldrouting == null)
                        {
                            bool bCreateTransition = bInit; // CheckIfRoutingCanMakeSmoothTransition(oldrouting.ChannelOut, newrouting.ChannelOut, oldrouting.Options, newop, bInit);
                            await RoutingTransition(newrouting, newrouting.DeviceOut, newrouting.ChannelOut, bCreateTransition);
                        }
                        else
                        {
                            bool bCreateTransition = CheckIfRoutingCanMakeSmoothTransition(oldrouting.ChannelOut, newrouting.ChannelOut, oldrouting.Options, newop);
                            await RoutingTransition(oldrouting, oldrouting.DeviceOut, oldrouting.ChannelOut, bCreateTransition);
                        }
                    }

                    for (int iCC = 0; iCC < 128; iCC++)
                    {
                        if (newop.DefaultRoutingCC[iCC] != newrouting.Options.DefaultRoutingCC[iCC] || (bInit && newrouting.Options.DefaultRoutingCC[iCC] > -1))
                        {
                            await newrouting.Tasks.AddTask(() => newrouting.DeviceOut.SendMidiEvent(new MidiEvent(TypeEvent.CC, new List<int>() { iCC, newop.DefaultRoutingCC[iCC] }, Tools.GetChannel(newrouting.ChannelOut), newrouting.DeviceOut.Name)));
                        }
                    }
                }
                //if (bChanges) { routing.UnblockAllCC(); }
            }

            if (newop != null) { newrouting.Options = newop; }
        }

        private static bool CheckIfRoutingCanMakeSmoothTransition(int ichannel, int inewchannel, MidiOptions options, MidiOptions newoptions)
        {
            if (ichannel == inewchannel)
            {
                return false;
            }
            else if (options.TranspositionOffset == newoptions.TranspositionOffset &&
                        (options.PlayMode == PlayModes.NORMAL ||
                         options.PlayMode == PlayModes.AFTERTOUCH ||
                         options.PlayMode == PlayModes.PIZZICATO_FAST ||
                         options.PlayMode == PlayModes.PIZZICATO_SLOW ||
                         options.PlayMode == PlayModes.REPEAT_NOTE_OFF_FAST ||
                         options.PlayMode == PlayModes.REPEAT_NOTE_OFF_SLOW))
            {
                return true;
            }
            else { return false; }
        }

        internal static void ChangeDefaultCC(MatrixItem routing, List<InstrumentData> instruments)
        {
            if (routing.DeviceOut != null)
            {
                var instr = instruments.FirstOrDefault(i => i.Device.Equals(routing.DeviceOut.Name));
                if (instr != null && instr.DefaultCC.Count > 0)
                {
                    routing.DeviceOut.CC[7] = instr.GetCCParameter(InstrumentData.CC_Parameters.CC_Volume);
                    routing.DeviceOut.CC[73] = instr.GetCCParameter(InstrumentData.CC_Parameters.CC_Attack);
                    routing.DeviceOut.CC[93] = instr.GetCCParameter(InstrumentData.CC_Parameters.CC_Chorus);
                    routing.DeviceOut.CC[75] = instr.GetCCParameter(InstrumentData.CC_Parameters.CC_Decay);
                    routing.DeviceOut.CC[74] = instr.GetCCParameter(InstrumentData.CC_Parameters.CC_FilterCutOff);
                    routing.DeviceOut.CC[10] = instr.GetCCParameter(InstrumentData.CC_Parameters.CC_Pan);
                    routing.DeviceOut.CC[72] = instr.GetCCParameter(InstrumentData.CC_Parameters.CC_Release);
                    routing.DeviceOut.CC[91] = instr.GetCCParameter(InstrumentData.CC_Parameters.CC_Reverb);
                    routing.DeviceOut.CC[71] = instr.GetCCParameter(InstrumentData.CC_Parameters.CC_Timbre);
                }
            }
        }

        private async Task SendProgramChange(MatrixItem routing, MidiPreset preset)
        {
            if (routing.DeviceOut != null && routing.ChannelOut > 0)
            {
                await routing.Tasks.AddTask(() =>
                {
                    ControlChangeMessage pc00 = new(Tools.GetChannel(preset.Channel), 0, preset.Msb);
                    ControlChangeMessage pc32 = new(Tools.GetChannel(preset.Channel), 32, preset.Lsb);
                    ProgramChangeMessage prg = new(Tools.GetChannel(preset.Channel), preset.Prg);

                    routing.DeviceOut.SendMidiEvent(new MidiEvent(TypeEvent.CC, new List<int> { pc00.Control, pc00.Value }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name));
                    routing.DeviceOut.SendMidiEvent(new MidiEvent(TypeEvent.CC, new List<int> { pc32.Control, pc32.Value }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name));
                    routing.DeviceOut.SendMidiEvent(new MidiEvent(TypeEvent.PC, new List<int> { preset.Prg }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name));
                });
            }
        }

        private async Task ChangeProgram(MatrixItem routing, MidiPreset newpres, bool bInit)
        {
            if (routing.DeviceOut != null)
            {
                if (routing.DeviceOut.Name.StartsWith(Tools.VST_HOST))
                {
                    routing.Preset = newpres;
                }
                else
                {
                    if (bInit || (newpres.Lsb != routing.Preset.Lsb || newpres.Msb != routing.Preset.Msb || newpres.Prg != routing.Preset.Prg || newpres.Channel != routing.Preset.Channel))
                    {
                        await SendProgramChange(routing, newpres);
                        routing.Preset = newpres;
                    }
                    else
                    {
                        routing.Preset = newpres;
                    }
                }
            }
            else
            {
                routing.Preset = newpres;
            }
        }

        private static string SysExTranscoder(string data)
        {
            //TODO SCRIPT TRANSCO
            return data;
        }

        private async Task<List<MidiEvent>> MidiTranslator(MatrixItem routing, MidiDevice deviceout, int iChannelOut, MidiEvent ev, CancellationToken cancellationToken)
        {
            List<MidiEvent> newEvents = new();

            await routing.Tasks.AddTask(() =>
            {
                foreach (var translate in routing.Options.Translators)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (translate.CanTranslate(ev))
                    {
                        newEvents.AddRange(translate.TranslatedMessages(ev, iChannelOut, deviceout));
                        //on peut transformer plusieurs fois le même évènements pour des effets chelous
                    }
                }
            });

            return newEvents;
        }

        private async Task SendGenericMidiEvent(MidiEvent ev, MatrixItem routing)
        {
            await GenerateOUTEvent(ev, routing, new TrackerGuid());
        }

        private static int RandomizeCCValue(int iCC, int iMax, MatrixItem routing)
        {
            int liveCC = routing.DeviceOut.GetLiveCCValue(routing.ChannelOut, iCC);

            if (routing.RandomizedCCValues[iCC] != 0)
            {
                int tmp = routing.RandomizedCCValues[iCC];
                routing.RandomizedCCValues[iCC] = 0;
                return liveCC - tmp;
            }
            else
            {
                Random random = new();
                int iVariation = random.Next(-iMax, iMax);
                int iNewValue = (liveCC - routing.RandomizedCCValues[iCC]) + iVariation;

                if (iNewValue < 0)
                {
                    int tmp = routing.RandomizedCCValues[iCC];
                    routing.RandomizedCCValues[iCC] = 0;
                    return liveCC - tmp;
                }
                else if (iNewValue > 127)
                {
                    int tmp = routing.RandomizedCCValues[iCC];
                    routing.RandomizedCCValues[iCC] = 0;
                    return liveCC - tmp;
                }
                else
                {
                    routing.RandomizedCCValues[iCC] = iVariation;
                    return iNewValue;
                }
            }
        }

        internal static void InitDevicesForSequencePlay(List<LiveData> initParams)
        {
            MidiDevice device = null;

            for (int i = 0; i < initParams.Count; i++)
            {
                int index = i;

                device = UsedDevicesOUT.FirstOrDefault(d => d.Name.Equals(initParams[index].DeviceOUT));

                if (device == null)
                {
                    var devOUT = OutputDevices.FirstOrDefault(d => d.Name.Equals(initParams[index].DeviceOUT));
                    if (devOUT != null)
                    {
                        var newdevice = new MidiDevice(devOUT, CubaseInstrumentData.Instruments.FirstOrDefault(i => i.Device.Equals(initParams[index].DeviceOUT)));
                        if (newdevice != null)
                        {
                            UsedDevicesOUT.Add(newdevice);
                        }
                    }
                }

                if (device != null)
                {
                    ////options d'init
                    //if (data.StartOptions.CC_Attack_Value > -1)
                    //{
                    //    device.SendMidiEvent(new MidiEvent(TypeEvent.CC, new List<int> { device.CC_Attack, data.StartOptions.CC_Attack_Value }, data.Channel, data.DeviceOUT));
                    //}
                    //if (data.StartOptions.CC_Timbre_Value > -1)
                    //{
                    //    device.SendMidiEvent(new MidiEvent(TypeEvent.CC, new List<int> { device.CC_Timbre, data.StartOptions.CC_Timbre_Value }, data.Channel, data.DeviceOUT));
                    //}
                    //if (data.StartOptions.CC_FilterCutOff_Value > -1)
                    //{
                    //    device.SendMidiEvent(new MidiEvent(TypeEvent.CC, new List<int> { device.CC_FilterCutOff, data.StartOptions.CC_FilterCutOff_Value }, data.Channel, data.DeviceOUT));
                    //}
                    //if (data.StartOptions.CC_Chorus_Value > -1)
                    //{
                    //    device.SendMidiEvent(new MidiEvent(TypeEvent.CC, new List<int> { device.CC_Chorus, data.StartOptions.CC_Chorus_Value }, data.Channel, data.DeviceOUT));
                    //}
                    //if (data.StartOptions.CC_Decay_Value > -1)
                    //{
                    //    device.SendMidiEvent(new MidiEvent(TypeEvent.CC, new List<int> { device.CC_Decay, data.StartOptions.CC_Decay_Value }, data.Channel, data.DeviceOUT));
                    //}
                    //if (data.StartOptions.CC_Pan_Value > -1)
                    //{
                    //    device.SendMidiEvent(new MidiEvent(TypeEvent.CC, new List<int> { device.CC_Pan, data.StartOptions.CC_Pan_Value }, data.Channel, data.DeviceOUT));
                    //}
                    //if (data.StartOptions.CC_Release_Value > -1)
                    //{
                    //    device.SendMidiEvent(new MidiEvent(TypeEvent.CC, new List<int> { device.CC_Release, data.StartOptions.CC_Release_Value }, data.Channel, data.DeviceOUT));
                    //}
                    //if (data.StartOptions.CC_Reverb_Value > -1)
                    //{
                    //    device.SendMidiEvent(new MidiEvent(TypeEvent.CC, new List<int> { device.CC_Reverb, data.StartOptions.CC_Reverb_Value }, data.Channel, data.DeviceOUT));
                    //}
                    //if (data.StartOptions.CC_Volume_Value > -1)
                    //{
                    //    device.SendMidiEvent(new MidiEvent(TypeEvent.CC, new List<int> { device.CC_Volume, data.StartOptions.CC_Volume_Value }, data.Channel, data.DeviceOUT));
                    //}
                    if (device.Plugin != null)
                    {
                        device.Plugin.VSTHostInfo.Dump = initParams[index].InitVSTState;
                        device.Plugin.VSTHostInfo.Parameters = initParams[index].InitVSTParameters;
                        device.Plugin.VSTHostInfo.Program = initParams[index].InitVSTProgram;
                        device.Plugin.LoadVSTParameters();
                        device.Plugin.LoadVSTProgram();
                    }


                    //envoi des valeurs CC mémorisées
                    foreach (var cc in initParams[index].StartCC)
                    {
                        device.SendMidiEvent(new MidiEvent(TypeEvent.CC, new List<int> { cc[0], cc[1] }, initParams[index].Channel, initParams[index].DeviceOUT));
                    }

                    if (device.Plugin == null) //on n'envoie pas de program change sur les VST, c'est géré dans le plugin
                    {
                        //envoi du program change
                        device.SendMidiEvent(new MidiEvent(TypeEvent.CC, new List<int> { 0, initParams[index].InitProgram.Msb }, Tools.GetChannel(initParams[index].InitProgram.Channel), initParams[index].DeviceOUT));
                        device.SendMidiEvent(new MidiEvent(TypeEvent.CC, new List<int> { 32, initParams[index].InitProgram.Lsb }, Tools.GetChannel(initParams[index].InitProgram.Channel), initParams[index].DeviceOUT));
                        device.SendMidiEvent(new MidiEvent(TypeEvent.PC, new List<int> { initParams[index].InitProgram.Prg }, Tools.GetChannel(initParams[index].InitProgram.Channel), initParams[index].DeviceOUT));
                    }
                }
            }
        }

        internal static void SendRecordedEvent(MidiEvent eventtoplay)
        {
            var device = UsedDevicesOUT.FirstOrDefault(d => d.Name.Equals(eventtoplay.Device));
            device?.SendMidiEvent(eventtoplay);
        }

        internal async Task<List<LiveData>> GetLiveCCData()
        {
            List<LiveData> listdata = new();

            for (int im = 0; im < MidiMatrix.Count; im++)
            {
                int index = im;
                await Tasks.AddTask(() =>
                {
                    if (MidiMatrix[index].DeviceOut != null)
                    {
                        var CurrentOptions = MidiMatrix[index].Options.Clone();

                        LiveData CurrentData = new()
                        {
                            StartCC = MidiMatrix[index].DeviceOut.GetLiveCCData(Tools.GetChannel(MidiMatrix[index].ChannelOut)),
                            StartOptions = CurrentOptions,
                            RoutingGuid = MidiMatrix[index].RoutingGuid,
                            InitProgram = MidiMatrix[index].Preset
                        };

                        if (MidiMatrix[index].DeviceOut != null && MidiMatrix[index].DeviceOut.Plugin != null)
                        {
                            MidiMatrix[index].DeviceOut.SaveVSTParameters();
                            CurrentData.InitVSTParameters = MidiMatrix[index].DeviceOut.Plugin.VSTHostInfo.Parameters;
                            CurrentData.InitVSTProgram = MidiMatrix[index].DeviceOut.Plugin.VSTHostInfo.Program;
                            CurrentData.InitVSTState = MidiMatrix[index].DeviceOut.Plugin.VSTHostInfo.Dump;
                        }

                        if (MidiMatrix[index].DeviceOut != null)
                        {
                            CurrentData.DeviceOUT = MidiMatrix[index].DeviceOut.Name;
                            CurrentData.Channel = Tools.GetChannel(MidiMatrix[index].ChannelOut);
                        }

                        listdata.Add(CurrentData);
                    }
                });
            }

            return listdata;
        }

        #endregion

        #region PUBLIC

        public void StopLog()
        {
            OnLogAdded -= MidiDevice_OnLogAdded;
        }

        public void StartLog()
        {
            OnLogAdded += MidiDevice_OnLogAdded;
        }

        public static void Debug()
        {
            //4,8,9
            //var regex = Regex.Match("[IN=KEY#64:1-127]", "(\\[)(IN)=(KEY#)(\\d{1,3})((-)(\\d{1,3}))*(:)(\\d{1,3})((-)(\\d{1,3}))*(\\])");
            //4,7,8,9
            //var regex2 = Regex.Match("[IN=KEY#64-127:64]", "(\\[)(IN)=(KEY#)(\\d{1,3})((-)(\\d{1,3}))*(:)(\\d{1,3})((-)(\\d{1,3}))*(\\])");

            //MidiTranslator(null, null);
        }

        public int GetFreeChannelForDevice(string sDevice, int iWanted)
        {
            if (iWanted == 0) { iWanted += 1; }


            var devicerouting = MidiMatrix.Where(m => m.DeviceOut != null && m.ChannelOut > 0 && m.DeviceOut.Name.Equals(sDevice)).ToList();

            if (devicerouting != null && devicerouting.Count > 0)
            {
                int iDev = devicerouting.Count;
                int[] Channels = new int[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
                foreach (var dev in devicerouting)
                {
                    Channels[dev.ChannelOut - 1] += 1;
                }

                if (Channels[iWanted - 1] > 0) //déja affecté
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

        public static bool CheckAndOpenINPort(string sDevice)
        {

            var exists = UsedDevicesIN.FirstOrDefault(d => d.Name.Equals(sDevice));

            if (exists == null)
            {
                var devIN = InputDevices.FirstOrDefault(d => d.Name.Equals(sDevice));
                if (devIN != null)
                {
                    var device = new MidiDevice(devIN)
                    {
                        IsReserverdForInternalPurposes = true
                    };
                    device.OnMidiEvent += DeviceIn_StaticOnMidiEvent;
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
                exists.OnMidiEvent -= DeviceIn_StaticOnMidiEvent;
                exists.CloseDevice();
                UsedDevicesIN.Remove(exists);
            }
        }

        public bool GetActiveStatus(Guid routingGuid)
        {
            var routing = MidiMatrix.FirstOrDefault(m => m.RoutingGuid == routingGuid);
            if (routing != null)
            {
                return routing.Options.Active;
            }
            else { return true; }
        }

        public static void Panic(bool bAll)
        {
            for (int iD = 0; iD < UsedDevicesOUT.Count; iD++)
            {
                for (int iCh = 1; iCh <= 16; iCh++)
                {
                    UsedDevicesOUT[iD].SendMidiEvent(new MidiEvent(TypeEvent.CC, new List<int> { 64, 0 }, Tools.GetChannel(iCh), UsedDevicesOUT[iD].Name));
                    //UsedDevicesOUT[iD].SendMidiEvent(new MidiEvent(TypeEvent.CC, new List<int> { 123, 127 }, Tools.GetChannel(iCh), UsedDevicesOUT[iD].Name));
                    if (!bAll)
                    {
                        for (int iKey = 0; iKey < 128; iKey++)
                        {
                            while (UsedDevicesOUT[iD].GetLiveNOTEValue(iCh, iKey))
                            {
                                UsedDevicesOUT[iD].SendMidiEvent(new MidiEvent(TypeEvent.NOTE_OFF, new List<int> { iKey, 0 }, Tools.GetChannel(iCh), UsedDevicesOUT[iD].Name));
                            }
                        }
                    }
                    else
                    {
                        for (int iKey = 0; iKey < 128; iKey++)
                        {
                            UsedDevicesOUT[iD].SendMidiEvent(new MidiEvent(TypeEvent.NOTE_OFF, new List<int> { iKey, 0 }, Tools.GetChannel(iCh), UsedDevicesOUT[iD].Name));
                        }
                    }
                    UsedDevicesOUT[iD].ClearNotes();
                }
            }
        }

        private static async Task SetDeviceMonoMode(MatrixItem routing, MidiDevice device, int channelout, PlayModes playmode)
        {
            await routing.Tasks.AddTask(() =>
            {
                if (device != null && channelout > 0)
                {
                    switch (playmode)
                    {
                        case PlayModes.MONO_LOW:
                        case PlayModes.MONO_HIGH:
                        case PlayModes.MONO_INTERMEDIATE_HIGH:
                        case PlayModes.MONO_INTERMEDIATE_LOW:
                        case PlayModes.MONO_IN_BETWEEN:
                        case PlayModes.HARMONY:
                            device.SendMidiEvent(new MidiEvent(TypeEvent.CC, new List<int> { 126, 127 }, Tools.GetChannel(channelout), device.Name));
                            break;
                        default:
                            device.SendMidiEvent(new MidiEvent(TypeEvent.CC, new List<int> { 127, 127 }, Tools.GetChannel(channelout), device.Name));
                            break;
                    }
                }
            });

        }

        private async Task RoutingTransition(MatrixItem routingCopy, MidiDevice device, int channelOut, bool bCreateTransitionRouting)
        {
            if (bCreateTransitionRouting)
            {
                //on ajoute un routing de transition uniquement si on a des notes en attente ou la pédale de sustain encore active
                if (device != null && device.PendingNotesOrSustain(channelOut) && !MidiMatrix.Any(mm => mm.RoutingGuid == routingCopy.RoutingGuid))
                {
                    //ajout d'un nouveau routing qui a pour vocation de ne recevoir que les NOTE_OFF + sustain OFF de façon à l'éteindre en douceur
                    await routingCopy.Tasks.AddTask(() => MidiMatrix.Add(routingCopy));
                }
            }
            else
            {
                //if (device != null && channelOut > 0)
                //{
                //    //await routing.Tasks.AddTask(() =>
                //    //{
                //    //    device.SendMidiEvent(new MidiEvent(TypeEvent.CC, new List<int> { 64, 0 }, Tools.GetChannel(channelOut), device.Name));

                //    //    List<int> pendingnotes = routing.ClearPendingNotes();

                //    //    foreach (var i in pendingnotes)
                //    //    {
                //    //        device.SendMidiEvent(new MidiEvent(TypeEvent.NOTE_OFF, new List<int> { i, 0 }, Tools.GetChannel(channelOut), device.Name));
                //    //    }
                //    //});

                if (device != null && channelOut > 0)
                {
                    await routingCopy.Tasks.AddTask(() =>
                    {
                        //device.SendMidiEvent(new MidiEvent(TypeEvent.CC, new List<int> { 123, 127 }, Tools.GetChannel(channelOut), device.Name));

                        for (int iKey = 0; iKey < 128; iKey++)
                        {
                            while (device.GetLiveNOTEValue(channelOut, iKey))
                            {
                                device.SendMidiEvent(new MidiEvent(TypeEvent.NOTE_OFF, new List<int> { iKey, 0 }, Tools.GetChannel(channelOut), device.Name));
                            }
                        }
                        device.ClearNotes();
                    });
                }
            }
        }

        public async Task<Guid> AddRouting(string sDeviceIn, string sDeviceOut, VSTPlugin vst, int iChIn, int iChOut, MidiOptions options, MidiPreset preset = null, Sequencer DeviceInSequencer = null)
        {
            Guid GUID = new();

            var devIN = InputDevices.FirstOrDefault(d => d.Name.Equals(sDeviceIn));
            var devOUT = OutputDevices.FirstOrDefault(d => d.Name.Equals(sDeviceOut));

            if (devIN != null && !UsedDevicesIN.Any(d => d.Name.Equals(sDeviceIn)))
            {
                if (sDeviceIn.Equals(Tools.INTERNAL_GENERATOR))
                {
                }
                else if (sDeviceIn.Equals(Tools.INTERNAL_SEQUENCER))
                {
                }
                else if (sDeviceIn.Equals(Tools.ALL_INPUTS))
                {
                    AddNewInDevice(AllInputs, true);
                }
                else
                {
                    AddNewInDevice(new List<string> { sDeviceIn }, false);
                }
            }

            if (sDeviceOut.StartsWith(Tools.VST_HOST) && !UsedDevicesOUT.Any(d => d.Name.Equals(sDeviceOut)))
            {
                if (vst.VSTHostInfo != null)
                {
                    AddNewOutDevice(sDeviceOut, vst);
                }
            }
            else if (sDeviceOut.StartsWith(Tools.VST_HOST) && UsedDevicesOUT.Any(d => d.Name.Equals(sDeviceOut)))
            {
                vst = UsedDevicesOUT.FirstOrDefault(d => d.Name.Equals(sDeviceOut)).Plugin;
            }
            else if (devOUT != null && !UsedDevicesOUT.Any(d => d.Name.Equals(sDeviceOut)))
            {
                AddNewOutDevice(devOUT.Name);
            }

            //iAction : 0 = delete, 1 = adds

            //devIN != null && devOUT != null && 
            if (iChIn >= 0 && iChIn <= 16 && iChOut >= 0 && iChOut <= 16)
            {
                MatrixItem newmatrix = null;

                //if (sDeviceIn.Equals(Tools.INTERNAL_GENERATOR))
                //{
                //    newmatrix.Options.Active = false;
                //}
                if (sDeviceIn.Equals(Tools.INTERNAL_SEQUENCER))
                {
                    if (DeviceInSequencer != null)
                    {
                        newmatrix = new MatrixItem(DeviceInSequencer, iChIn, UsedDevicesOUT.FirstOrDefault(d => d.Name.Equals(sDeviceOut)), iChOut, options, preset);
                        newmatrix.OnSequencerPlayNote += MatrixItem_OnSequencerPlayNote;
                        newmatrix.DeviceInInternalName = Tools.INTERNAL_SEQUENCER;
                    }
                }
                else
                {
                    newmatrix = new MatrixItem(UsedDevicesIN.FirstOrDefault(d => d.Name.Equals(sDeviceIn)), UsedDevicesOUT.FirstOrDefault(d => d.Name.Equals(sDeviceOut)), iChIn, iChOut, options, preset);

                    if (sDeviceIn.Equals(Tools.INTERNAL_GENERATOR))
                    {
                        newmatrix.OnSequencerPlayNote += MatrixItem_OnSequencerPlayNote;
                        newmatrix.DeviceInInternalName = Tools.INTERNAL_GENERATOR;
                    }
                }

                MidiMatrix.Add(newmatrix);

                if (iChOut > 0 && iChOut <= 16)
                {
                    ChangeDefaultCC(newmatrix, CubaseInstrumentData.Instruments);
                    await ChangeOptions(newmatrix, null, options, true);
                    await ChangeProgram(newmatrix, preset, true);
                }

                GUID = newmatrix.RoutingGuid;
            }
            else { GUID = Guid.Empty; }

            return GUID;
        }

        public async Task ModifyRouting(Guid routingGuid, string sDeviceIn, string sDeviceOut, VSTPlugin vst, int iChIn, int iChOut, MidiOptions options, MidiPreset preset = null, Sequencer DeviceInSequencer = null)
        {
            var newrouting = MidiMatrix.FirstOrDefault(m => m.RoutingGuid == routingGuid);

            if (newrouting != null && !newrouting.ChangeLocked)
            {
                //faire une copie pour l'utiliser en routing de transition dans le cas ou on change de device out ou d'options (transposition + play mode)
                //en gros, tout ce qui a des notes en suspend
                MatrixItem oldrouting = newrouting.Clone();

                newrouting.ChangeLocked = true;

                bool bINChanged = newrouting.CheckDeviceIn(sDeviceIn, iChIn);
                bool bOUTChanged = newrouting.CheckDeviceOut(sDeviceOut);

                if (iChIn != newrouting.ChannelIn) { bINChanged = true; }
                if (iChOut != newrouting.ChannelOut) { bOUTChanged = true; }

                if (bINChanged)
                {
                    bool active = newrouting.Options.Active;
                    newrouting.Options.Active = false;

                    newrouting.OnSequencerPlayNote -= MatrixItem_OnSequencerPlayNote;

                    if (sDeviceIn.Equals(Tools.INTERNAL_GENERATOR))
                    {
                        newrouting.RemoveSequencer();
                        newrouting.OnSequencerPlayNote += MatrixItem_OnSequencerPlayNote;
                        newrouting.DeviceIn = null;
                        newrouting.DeviceInInternalName = Tools.INTERNAL_GENERATOR;
                    }
                    else if (sDeviceIn.Equals(Tools.INTERNAL_SEQUENCER))
                    {
                        if (DeviceInSequencer != null)
                        {
                            newrouting.AddSequencer(DeviceInSequencer);
                            newrouting.OnSequencerPlayNote += MatrixItem_OnSequencerPlayNote;
                            newrouting.DeviceIn = null;
                            newrouting.DeviceInInternalName = Tools.INTERNAL_SEQUENCER;
                        }
                    }
                    else if (sDeviceIn.Equals(Tools.ALL_INPUTS))
                    {
                        newrouting.OnSequencerPlayNote -= MatrixItem_OnSequencerPlayNote;
                        newrouting.RemoveSequencer();
                        newrouting.DeviceInInternalName = "";

                        if (AllInputs.Count > 0)
                        {
                            newrouting.DeviceIn = AddNewInDevice(AllInputs, true);
                        }
                        else
                        {
                            newrouting.DeviceIn = null;
                        }
                    }
                    else
                    {
                        newrouting.OnSequencerPlayNote -= MatrixItem_OnSequencerPlayNote;
                        newrouting.RemoveSequencer();
                        newrouting.DeviceInInternalName = "";

                        if (sDeviceIn.Length > 0)
                        {
                            newrouting.DeviceIn = AddNewInDevice(new List<string> { sDeviceIn }, false);
                        }
                        else
                        {
                            newrouting.DeviceIn = null;
                        }
                    }
                    newrouting.ChannelIn = iChIn;
                    newrouting.Options.Active = active;
                }

                string oldDeviceOutName = newrouting.DeviceOut != null ? newrouting.DeviceOut.Name : "";
                int morphinglength = newrouting.Options.PresetMorphing;
                int newvolume = options.CC_Volume_Value;
                int oldChannelOut = newrouting.ChannelOut;

                if (bOUTChanged)
                {
                    bool active = newrouting.Options.Active;
                    newrouting.Options.Active = false;

                    await newrouting.CancelTask();

                    if (sDeviceOut.StartsWith(Tools.VST_HOST))
                    {
                        if (vst.VSTHostInfo != null)
                        {
                            newrouting.DeviceOut = AddNewOutDevice(sDeviceOut, vst);
                        }
                        //else { return; }
                    }
                    else if (sDeviceOut.Length > 0)
                    {
                        newrouting.DeviceOut = AddNewOutDevice(sDeviceOut, null);
                    }
                    else
                    {
                        newrouting.DeviceOut = null;
                    }
                    newrouting.ChannelOut = iChOut;
                    newrouting.Options.Active = active;
                }

                if (iChOut > 0)
                {
                    //hyper important d'être à la fin !
                    ChangeDefaultCC(newrouting, CubaseInstrumentData.Instruments);

                    //crée un routing de transition pour permettre aux notes encore en cours de ne pas se relâcher immédiatement mais de faire "vivre"
                    //les 2 routing jusqu'à temps que le premier n'ait plus de notes en cours d'exécution (ne garde que les note off)
                    await ChangeOptions(newrouting, oldrouting, options, bOUTChanged);

                    await ChangeProgram(newrouting, preset, bOUTChanged);
                }

                if (bOUTChanged)
                {
                    var oldDeviceOut = UsedDevicesOUT.FirstOrDefault(d => d.Name.Equals(oldDeviceOutName));

                    await RoutingTransition(oldrouting, oldDeviceOut, oldChannelOut, true);

                    if (oldrouting.Options.PresetMorphing > 0)
                    {
                        _ = PresetMorphing(oldrouting, 0, new List<int[]>(), false);
                    }

                    if (newrouting.Options.PresetMorphing > 0)
                    {
                        List<int[]> transfernotes = oldrouting.DeviceOut != null ? oldrouting.DeviceOut.GetLiveNotes(oldrouting.ChannelOut) : new List<int[]>();
                        int ioldCC64value = oldrouting.DeviceOut != null ? oldrouting.DeviceOut.GetLiveCCValue(oldrouting.ChannelOut, 64) : 0;

                        await PresetMorphing(newrouting, ioldCC64value, transfernotes, true);
                    }
                }

                newrouting.ChangeLocked = false;
            }
        }

        private async Task PresetMorphing(MatrixItem routing, int iOldCC64value, List<int[]> iOldNotes, bool bfadein)
        {
            TrackerGuid tracker = new();

            if (routing.DeviceInSequencer != null && !bfadein)
            {
                routing.AddSequencer(routing.DeviceInSequencer);
                //    //oldrouting.OnSequencerPlayNote += MatrixItem_OnSequencerPlayNote;
            }

            var deviceout = UsedDevicesOUT.FirstOrDefault(d => d.Name.Equals(routing.DeviceOut == null ? "" : routing.DeviceOut.Name));

            if (deviceout != null)
            {
                if (bfadein)
                {
                    await routing.Tasks.AddTask(async () =>
                    {
                        deviceout.SendMidiEvent(new MidiEvent(TypeEvent.CC, new List<int> { 7, 0 }, Tools.GetChannel(routing.ChannelOut), deviceout.Name));

                        if (iOldCC64value >= 64)
                        {
                            deviceout.SendMidiEvent(new MidiEvent(TypeEvent.CC, new List<int> { 64, 127 }, Tools.GetChannel(routing.ChannelOut), deviceout.Name));
                        }

                        foreach (var note in iOldNotes) //transfert des notes qui étaient sur l'ancien routing 
                        {
                            deviceout.SendMidiEvent(new MidiEvent(TypeEvent.NOTE_ON, new List<int> { note[0], note[1] }, Tools.GetChannel(routing.ChannelOut), deviceout.Name));
                        }

                        int memSmooth = routing.Options.SmoothCCLength;
                        routing.Options.SmoothCCLength = (int)Math.Ceiling(routing.Options.PresetMorphing * 0.75);
                        await GenerateOUTEvent(new MidiEvent(TypeEvent.CC, new List<int> { 7, routing.Options.CC_Volume_Value }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name), routing, tracker);
                        routing.Options.SmoothCCLength = memSmooth;
                    });

                    while (tracker.ToString().Length > 0)
                    {
                        await Task.Delay(10);
                    }
                }
                else
                {
                    routing.DropMode = 0;

                    routing.Options.SmoothCCLength = (int)Math.Ceiling(routing.Options.PresetMorphing * 1.25);
                    await routing.Tasks.AddTask(async () =>
                    {
                        await GenerateOUTEvent(new MidiEvent(TypeEvent.CC, new List<int> { 7, 0 }, Tools.GetChannel(routing.ChannelOut), deviceout.Name), routing, tracker);
                    });

                    while (tracker.ToString().Length > 0)
                    {
                        await Task.Delay(10);
                    }

                    if (routing.DeviceInSequencer != null)
                    {
                        routing.RemoveSequencer();
                        //oldrouting.OnSequencerPlayNote -= MatrixItem_OnSequencerPlayNote;
                    }

                    routing.DropMode = 1;
                }
            }
        }

        public async Task UpdateUsedDevices(List<string> devices)
        {
            await Tasks.AddTask(() =>
            {
                lock (DevicesRoutingIN) { DevicesRoutingIN.Clear(); }
                lock (DevicesRoutingOUT) { DevicesRoutingOUT.Clear(); }

                List<string> NewIN = new();
                List<string> NewOUT = new();

                foreach (var dev in devices.Distinct())
                {
                    if (dev.StartsWith("I-") && dev.Length > 2)
                    {
                        NewIN.Add(dev[2..]);
                    }
                    else if (dev.StartsWith("O-") && dev.Length > 2)
                    {
                        NewOUT.Add(dev[2..]);
                    }
                }

                lock (DevicesRoutingIN) { DevicesRoutingIN = NewIN; }
                lock (DevicesRoutingOUT) { DevicesRoutingOUT = NewOUT; }

            });
        }

        private static MidiDevice AddNewOutDevice(string sDeviceOut, VSTPlugin vst = null)
        {
            var dev = UsedDevicesOUT.FirstOrDefault(d => d.Name.Equals(sDeviceOut));
            if (dev == null)
            {
                var outdev = OutputDevices.FirstOrDefault(d => d.Name.Equals(sDeviceOut));
                MidiDevice newdev = null;
                if (vst != null && UsedDevicesOUT.FirstOrDefault(d => d.Name.Equals(sDeviceOut)) == null)
                {
                    newdev = new MidiDevice(vst, sDeviceOut);
                }
                else if (outdev != null)
                {
                    newdev = new MidiDevice(outdev, CubaseInstrumentData.Instruments.FirstOrDefault(i => i.Device.Equals(outdev.Name)));
                }

                if (newdev != null)
                {
                    newdev.UsedForRouting = true;
                    UsedDevicesOUT.Add(newdev);
                }

                return newdev;
            }
            else
            {
                dev.UsedForRouting = true;
                return dev;
            }
        }

        private MidiDevice AddNewInDevice(List<string> sDevicesIn, bool bAllInputs)
        {
            if (!bAllInputs)
            {
                var dev = UsedDevicesIN.FirstOrDefault(d => d.Name.Equals(sDevicesIn[0]));
                if (dev == null)
                {
                    var indev = InputDevices.FirstOrDefault(d => d.Name.Equals(sDevicesIn[0]));
                    if (indev != null)
                    {
                        var newdev = new MidiDevice(indev)
                        {
                            UsedForRouting = true
                        };
                        newdev.OnMidiEvent += DeviceIn_OnMidiEvent;
                        UsedDevicesIN.Add(newdev);
                        return newdev;
                    }
                    else { return null; }
                }
                else { return dev; }
            }
            else
            {
                var dev = UsedDevicesIN.FirstOrDefault(d => d.Name.Equals(Tools.ALL_INPUTS));
                if (dev == null)
                {
                    var newdev = new MidiDevice(sDevicesIn)
                    {
                        UsedForRouting = true
                    };
                    newdev.OnMidiEvent += DeviceIn_OnMidiEvent;
                    UsedDevicesIN.Add(newdev);
                    return newdev;
                }
                else { return dev; }
            }
        }

        public async Task ChangeAllInputsMidiIn(List<string> sDevicesIn)
        {
            await Tasks.AddTask(() =>
            {
                List<string> toremove = AllInputs.Except(sDevicesIn).ToList();
                List<string> toadd = sDevicesIn.Except(AllInputs).ToList();

                var device = UsedDevicesIN.FirstOrDefault(d => d.Name.Equals(Tools.ALL_INPUTS));
                if (device != null)
                {
                    foreach (var add in toadd)
                    {
                        device.AddAllInputsDevice(add);
                    }
                    foreach (var rem in toremove)
                    {
                        device.RemoveAllInputsDevice(rem);
                    }
                }

                AllInputs = sDevicesIn;
            });
        }

        public async Task SetSolo(Guid routingGuid)
        {
            var routingOn = MidiMatrix.FirstOrDefault(m => m.RoutingGuid == routingGuid);

            if (routingOn != null)
            {
                routingOn.TempActive = routingOn.Options.Active;
                routingOn.Options.Active = true;
            }

            var routingOff = MidiMatrix.Where(m => m.RoutingGuid != routingGuid).ToList();
            for (int i = 0; i < routingOff.Count; i++)
            {
                await RoutingTransition(routingOff[i], routingOff[i].DeviceOut, routingOff[i].ChannelOut, false);
                routingOff[i].TempActive = routingOff[i].Options.Active;
                routingOff[i].Options.Active = false;
            }
        }

        public async Task MuteRouting(Guid routingGuid)
        {
            var routingOn = MidiMatrix.FirstOrDefault(m => m.RoutingGuid == routingGuid);
            if (routingOn != null)
            {
                await routingOn.Tasks.AddTask(async () =>
                {
                    await RoutingTransition(routingOn, routingOn.DeviceOut, routingOn.ChannelOut, false);
                    routingOn.TempActive = true;
                    routingOn.Options.Active = false;
                });
            }
        }

        public async Task UnmuteRouting(Guid routingGuid)
        {
            var routingOff = MidiMatrix.FirstOrDefault(m => m.RoutingGuid == routingGuid);
            if (routingOff != null)
            {
                await routingOff.Tasks.AddTask(() =>
                {
                    routingOff.TempActive = false;
                    routingOff.Options.Active = true;
                });
            }

        }

        public async Task UnmuteAllRouting()
        {
            await Tasks.AddTask(() =>
            {
                foreach (var r in MidiMatrix)
                {
                    r.Options.Active = r.TempActive;
                }
            });
        }

        public async Task DeleteRouting(Guid routingGuid)
        {
            await Tasks.AddTask(() =>
            {
                var routing = MidiMatrix.FirstOrDefault(m => m.RoutingGuid == routingGuid);
                if (routing != null)
                {
                    MidiMatrix.Remove(routing);
                }
            });
        }

        public async Task DeleteAllRouting()
        {
            MidiClock.Stop();
            MidiClock.Enabled = false;
            EventsCounter.Stop();
            EventsCounter.Enabled = false;
            CloseDevicesTimer.Stop();
            CloseDevicesTimer.Enabled = false;

            List<Task> cancel = new();

            foreach (var routing in MidiMatrix)
            {
                cancel.Add(routing.CancelTask());
            }

            await Task.WhenAll(cancel);

            foreach (var device in UsedDevicesIN)
            {
                try
                {
                    device.OnMidiClockEvent -= MidiClock_OnEvent;
                    device.OnMidiEvent -= DeviceIn_OnMidiEvent;
                    device.CloseDevice();
                }
                catch { throw; }
            }


            foreach (var device in UsedDevicesOUT)
            {
                try
                {
                    device.OnMidiClockEvent -= MidiClock_OnEvent;
                    device.OnMidiEvent -= DeviceIn_OnMidiEvent;
                }
                catch { throw; }
            }

            Panic(true);

            foreach (var device in UsedDevicesOUT)
            {
                try
                {
                    device.CloseDevice();
                }
                catch { throw; }
            }

            UsedDevicesIN.Clear();

            UsedDevicesOUT.Clear();

            DeallocateAudio();

            MidiMatrix.Clear();
        }

        public async Task SendCC(Guid routingGuid, int iCC, int iValue)
        {
            var routing = MidiMatrix.FirstOrDefault(d => d.RoutingGuid == routingGuid && d.DeviceOut != null);
            if (routing != null)
            {
                await GenerateOUTEvent(new MidiEvent(TypeEvent.CC, new List<int> { iCC, iValue }, Tools.GetChannel(routing.ChannelOut), routing.DeviceOut.Name), routing, new TrackerGuid());
            }
        }

        public async Task SendNote(Guid routingGuid, NoteGenerator note)
        {
            var routing = MidiMatrix.FirstOrDefault(d => d.RoutingGuid == routingGuid && d.DeviceOut != null);
            if (routing != null)
            {
                await routing.CreateNote(note, LowestNoteRunning);
            }
        }

        public async Task SendProgramChange(Guid routingGuid, MidiPreset mp)
        {
            var routing = MidiMatrix.FirstOrDefault(d => d.RoutingGuid == routingGuid && d.DeviceOut != null);
            if (routing != null)
            {
                await ChangeProgram(routing, mp, false);
            }
        }

        public async Task OpenUsedPorts(bool bIn)
        {
            await Tasks.AddTask(() =>
            {
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
                    }
                }
            });
        }

        public async Task SetClock(bool bActivated, int iBPM, string sDevice)
        {
            await Tasks.AddTask(() =>
            {
                bool bChanged = false;

                if (ClockRunning != bActivated || ClockBPM != iBPM || !ClockDevice.Equals(sDevice))
                {
                    bChanged = true;
                }

                if (bChanged)
                {
                    MidiClock.Stop(); //coupure de l'horloge interne
                    MidiClock.Enabled = false;

                    foreach (var device in UsedDevicesIN)  //coupure des horloges externes
                    {
                        device.DisableClock();
                        device.OnMidiClockEvent -= MidiClock_OnEvent;
                    }

                    if (bActivated)
                    {
                        if (sDevice.Equals(Tools.INTERNAL_GENERATOR))
                        {
                            MidiClock.Interval = Tools.GetMidiClockInterval(iBPM); //valeur par défaut
                            MidiClock.Start(); //coupure de l'horloge interne
                            MidiClock.Enabled = true;
                        }
                        else
                        {
                            var masterdevice = UsedDevicesIN.FirstOrDefault(d => d.Name.Equals(sDevice));
                            masterdevice.EnableClock();
                            masterdevice.OnMidiClockEvent += MidiClock_OnEvent;
                        }
                    }
                }

                ClockRunning = bActivated;
                ClockBPM = iBPM;
                ClockDevice = sDevice;
            });
        }

        public async Task SetSequencerListener(string sDevice)
        {
            await Tasks.AddTask(() =>
            {
                if (sDevice.Length > 0)
                {
                    var exists = UsedDevicesIN.FirstOrDefault(d => d.Name.Equals(sDevice));

                    if (exists == null)
                    {
                        var newdev = AddNewInDevice(new List<string> { sDevice }, false);
                        newdev.IsReserverdForInternalPurposes = true;
                    }
                }
            });
        }

        public void ReactivateTimers()
        {
            EventsCounter.Enabled = true;
            EventsCounter.Start();
            CloseDevicesTimer.Enabled = true;
            CloseDevicesTimer.Start();
        }

        public void SetRoutingGuid(Guid oldGuid, Guid newGuid)
        {
            var routing = MidiMatrix.FirstOrDefault(d => d.RoutingGuid == oldGuid);
            routing?.SetRoutingGuid(newGuid);
        }

        public async Task<List<int>> GetCCData(Guid routingGuid, int[] sCC)
        {
            List<int> CCdata = new();
            await Tasks.AddTask(() =>
            {
                var routing = MidiMatrix.FirstOrDefault(d => d.RoutingGuid == routingGuid);
                if (routing != null && routing.DeviceOut != null)
                {
                    for (int i = 0; i < sCC.Length; i++)
                    {
                        CCdata.Add(routing.DeviceOut.GetLiveCCValue(routing.ChannelOut, sCC[i]));
                    }
                }
            });
            return CCdata;
        }

        public async Task<bool> InitializeAudio(VSTPlugin plugin)
        {
            bool bOK = false;

            await Tasks.AddTask(() =>
            {
                try
                {
                    if (!UtilityAudio.AudioInitialized)
                    {
                        bOK = UtilityAudio.OpenAudio(plugin.VSTHostInfo.AsioDevice, plugin.VSTHostInfo.SampleRate);

                        Stopwatch stopwatch = new();
                        stopwatch.Start();

                        while (!bOK && stopwatch.ElapsedMilliseconds < 10000)
                        {
                            Thread.Sleep(100);
                        }

                        stopwatch.Stop();

                        if (stopwatch.ElapsedMilliseconds >= 10000)
                        {
                            UtilityAudio.StopAudio();
                            UtilityAudio.Dispose();
                            bOK = false;
                        }
                        else
                        {
                            UtilityAudio.AudioInitialized = true;
                            AudioDevice = plugin.VSTHostInfo.AsioDevice;
                            AddLog(plugin.VSTHostInfo.AsioDevice, false, -1, "[OPEN]", "", "", "", "");
                        }

                        if (bOK)
                        {
                            UtilityAudio.StartAudio();
                        }
                        else { bOK = false; }
                    }
                    else { bOK = true; }
                }
                catch (Exception ex)
                {
                    plugin.VSTHostInfo.Error = "Unable to open audio device : " + ex.Message;
                    bOK = false;
                }
            });

            return bOK;
        }

        public bool DeallocateAudio()
        {
            bool bOK = false;

            if (UtilityAudio.AudioInitialized)
            {

                try
                {
                    UtilityAudio.StopAudio();
                    UtilityAudio.Dispose();
                    UtilityAudio.AudioInitialized = false;

                    try
                    {
                        AddLog(AudioDevice, false, -1, "[CLOSE]", "", "", "", "");
                    }
                    catch
                    { bOK = false; }

                    bOK = true; ;
                }
                catch { bOK = false; }
            }
            return bOK;
        }

        public async Task<VSTPlugin> CheckVSTSlot(string sDeviceAndSlot)
        {
            VSTPlugin vst = null;

            await Tasks.AddTask(() =>
            {
                var device = UsedDevicesOUT.FirstOrDefault(d => d.Name.Equals(sDeviceAndSlot));
                if (device != null)
                {
                    vst = device.Plugin;
                }
            });

            return vst;
        }

        public async Task<bool> RemoveVST(string sDeviceAndSlot)
        {
            bool bOK = false;

            await Tasks.AddTask(() =>
            {
                var device = UsedDevicesOUT.FirstOrDefault(d => d.Name.Equals(sDeviceAndSlot));
                if (device != null)
                {
                    device.CloseDevice();
                    UsedDevicesOUT.Remove(device);
                    bOK = true;

                    foreach (var r in MidiMatrix)
                    {
                        if (r.DeviceOut != null && r.DeviceOut.Name.Equals(sDeviceAndSlot))
                        {
                            r.DeviceOut.CloseDevice();
                            r.DeviceOut = null;
                        }
                    }
                }
            });

            return bOK;
        }

        public async Task SaveVSTParameters()
        {
            await Tasks.AddTask(() =>
            {
                var vsts = UsedDevicesOUT.Where(d => d.Name.StartsWith(Tools.VST_HOST));

                lock (vsts)
                {
                    foreach (var vst in vsts)
                    {
                        vst.SaveVSTParameters();
                    }
                }
            });
        }

        public async Task SendNoteToSequencerBoxes(MidiEvent ev, int iChannel)
        {
            await Tasks.AddTask(async () =>
            {
                var items = MidiMatrix.Where(mm => mm.DeviceInSequencer != null && mm.DropMode == 0 && mm.ChannelIn == iChannel && mm.DeviceOut != null && mm.ChannelOut > 0);
                foreach (var matrix in items)
                {
                    await GenerateOUTEvent(ev, matrix, new TrackerGuid());
                }
            });
        }



        #endregion
    }
}
