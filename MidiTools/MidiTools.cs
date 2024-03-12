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
        public static string SYSEX_CHECK = @"^(F0)([A-f0-9]*)(F7)$";
        public static string INTERNAL_GENERATOR = "Internal Generator";

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
            return Convert.ToInt32(key.ToString()[3..]);
        }

        internal static int GetChannelInt(Channel ch)
        {
            return Convert.ToInt32(ch.ToString()[7..]);
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

        internal static int[] GetNoteIndex(int key, int vel, MidiOptions options)
        {
            int iNote = -1;

            if (options != null)
            {
                if (options.CompressVelocityRange)
                {
                    if (vel > options.VelocityFilterHigh)
                    { vel = options.VelocityFilterHigh; }
                    else if (vel < options.VelocityFilterLow)
                    { vel = options.VelocityFilterLow; }
                }

                if (options.TransposeNoteRange)
                {
                    if (key < options.NoteFilterLow)
                    {
                        while (key < options.NoteFilterLow)
                        {
                            key += 12;
                        }
                    }
                    else if (key > options.NoteFilterHigh)
                    {
                        while (key > options.NoteFilterHigh)
                        {
                            key -= 12;
                        }
                    }
                }

                if (key >= options.NoteFilterLow && key <= options.NoteFilterHigh && (vel == 0 || (vel >= options.VelocityFilterLow && vel <= options.VelocityFilterHigh))) //attention, la vélocité d'un noteoff est souvent à 0 (dépend des devices mais généralement)
                {
                    iNote = key + options.TranspositionOffset;
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

        internal static int GetMidiClockInterval(int iTempo)
        {

            // Nombre de MIDI Clocks par quart de note
            int midiClocksPerQuarterNote = 24;

            // Calcul du délai entre chaque MIDI Clock en microsecondes
            double microsecondsPerQuarterNote = 60000000.0 / iTempo;
            double microsecondsPerMIDIClock = microsecondsPerQuarterNote / midiClocksPerQuarterNote;

            // Conversion du délai en millisecondes
            double millisecondsPerMIDIClock = microsecondsPerMIDIClock / 1000;

            return (int)Math.Round(millisecondsPerMIDIClock);
        }
    }

}

