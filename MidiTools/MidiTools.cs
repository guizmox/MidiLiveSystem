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
    public static class Tools
    {
        public readonly static int[] CCToNotBlock = new int[16] { 0, 6, 32, 64, 65, 66, 67, 68, 120, 121, 122, 123, 124, 125, 126, 127 };

        public static string SYSEX_CHECK = @"^(F0)([A-f0-9]*)(F7)$";
        public static string INTERNAL_GENERATOR = "Internal Note Generator";
        public static string INTERNAL_SEQUENCER = "Internal Sequencer";
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
            List<byte[]> list = new List<byte[]>();
            List<string> sMessages = new List<string>();
            //F0 ... F7 to byte[]
            //on va envoyer les différents messages par paquets séparés
            StringBuilder sbSysEx = new StringBuilder();
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
                var hex = sysex.Substring(2);

                if (hex.Length > 0)
                {
                    var bytes = Enumerable.Range(0, hex.Length / 2)
                      .Select(x => Convert.ToByte(hex.Substring(x * 2, 2), 16))
                      .ToArray();

                    list.Add(bytes);
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
            int iQt = 4;

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

