using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using static MidiTools.MidiDevice;
using System.Threading.Tasks;
using System.Threading;
using System.Timers;
using MessagePack;
using Microsoft.Extensions.Logging;

namespace MidiTools
{
    [MessagePackObject]
    [Serializable]
    public class MidiSequence
    {
        [IgnoreMember] // Ignorer cette propriété privée lors de la sérialisation
        private readonly EventPool Tasks = new("MidiSequence");

        [Key("SequencerDefault")]
        public List<LiveData> SequencerDefault { get; set; } = new List<LiveData>();

        public delegate void SequenceFinishedHandler(string sInfo);
        public event SequenceFinishedHandler SequenceFinished;

        public delegate void RecorderLengthCounter(string sInfo);
        public event RecorderLengthCounter RecordCounter;

        [IgnoreMember] // Ignorer cette propriété privée lors de la sérialisation
        private readonly List<MidiEvent> _eventsIN = new();

        [IgnoreMember] // Ignorer cette propriété privée lors de la sérialisation
        private readonly List<MidiEvent> _eventsOUT = new();

        [Key("EventsOUT")]
        public List<MidiEvent> EventsOUT { get { return _eventsOUT; } }

        [Key("SequenceLength")]
        public TimeSpan SequenceLength { get; set; } = TimeSpan.Zero;

        [Key("IsStopped")]
        public bool IsStopped { get; set; } = true;

        [IgnoreMember]
        private System.Timers.Timer Recorder;

        [IgnoreMember]
        private DateTime RecorderStart;

        [IgnoreMember]
        private System.Timers.Timer Player;

        [IgnoreMember]
        private DateTime PlayerStart;

        private bool StopSequenceRequested = false;

        public MidiSequence()
        {

        }

        public string GetSequenceInfo()
        {
            int iTracks = 0;
            StringBuilder sbInfo = new();
            sbInfo.AppendLine("Sequence Status :");
            sbInfo.AppendLine(Environment.NewLine);
            var devices = _eventsOUT.Select(e => e.Device).Distinct();

            foreach (var s in devices)
            {
                string sChannels = " [Ch : ";
                for (int i = 1; i <= 16; i++)
                {
                    if (_eventsOUT.Any(e => e.Device.Equals(s) && e.Channel == Tools.GetChannel(i)))
                    {
                        iTracks++;
                        sChannels = string.Concat(sChannels, i.ToString() + ", ");
                        break;
                    }
                }
                sChannels = string.Concat(sChannels[0..^2], "]");
                sbInfo.AppendLine(string.Concat(s, " > ", sChannels));
            }
            sbInfo.AppendLine(Environment.NewLine);
            if (_eventsIN.Count > 0)
            {
                sbInfo.AppendLine(string.Concat("MIDI IN event(s) : ", _eventsIN.Count));
                sbInfo.AppendLine(Environment.NewLine);
            }
            if (_eventsOUT.Count > 0)
            {
                sbInfo.AppendLine(string.Concat("MIDI OUT event(s) : " + _eventsOUT.Count, " (Tracks : ", iTracks.ToString(), ")"));
            }

            return sbInfo.ToString();
        }

        public async Task StartRecording(bool bIn, bool bOut, MidiRouting routing)
        {
            if (MidiRouting.HasOutDevices > 0)
            {
                //on doit absolument garder les valeurs actuelles des données pour figer tous les paramètres MIDI pour pouvoir reproduire la séquence fidèlement
                SequencerDefault = await routing.GetLiveCCData();

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
            else
            {
                StartStopPlayerCounter(false);
                SequenceFinished?.Invoke("No Output Device.");
            }
        }

        public void StopRecording(bool bIn, bool bOut)
        {
            StartStopRecordCounter(false);

            if (bIn)
            {
                OnMidiSequenceEventIN -= MidiEventSequenceHandler_OnMidiEventIN;
            }
            if (bOut)
            {
                OnMidiSequenceEventOUT -= MidiEventSequenceHandler_OnMidiEventOUT;
            }
        }

        private void StartStopRecordCounter(bool bStart)
        {
            if (bStart)
            {
                RecorderStart = DateTime.Now;
                Recorder = new System.Timers.Timer();
                Recorder.Elapsed += Recorder_Elapsed;
                Recorder.Interval = 1000;
                Recorder.Start();
            }
            else
            {
                SequenceLength = DateTime.Now - RecorderStart;
                if (Recorder != null) //si on a enregistré du vide, il est NULL
                {
                    Recorder.Stop();
                    Recorder.Close();
                    Recorder = null;
                }
            }
        }

        private void StartStopPlayerCounter(bool bStart)
        {
            if (bStart)
            {
                PlayerStart = DateTime.Now + SequenceLength;
                Player = new System.Timers.Timer();
                Player.Elapsed += Player_Elapsed;
                Player.Interval = 1000;
                Player.Start();
            }
            else
            {
                if (Player != null) //si on a enregistré du vide, il est NULL
                {
                    Player.Stop();
                    Player.Close();
                    Player = null;
                }
            }
        }

        private void Player_Elapsed(object sender, ElapsedEventArgs e)
        {
            TimeSpan elapsed = PlayerStart - e.SignalTime;
            string elapsedTime = elapsed.ToString(@"mm\:ss");
            RecordCounter?.Invoke(elapsedTime);
        }

        private void Recorder_Elapsed(object sender, ElapsedEventArgs e)
        {
            TimeSpan elapsed = e.SignalTime - RecorderStart;
            string elapsedTime = elapsed.ToString(@"mm\:ss");
            RecordCounter?.Invoke(elapsedTime);
        }

        private void MidiEventSequenceHandler_OnMidiEventIN(MidiEvent ev)
        {
            if (_eventsIN.Count == 0)
            {
                StartStopRecordCounter(true);
            }
            lock (_eventsIN) { _eventsIN.Add(ev); }
        }

        private void MidiEventSequenceHandler_OnMidiEventOUT(MidiEvent ev)
        {
            lock (_eventsOUT) { _eventsOUT.Add(ev); }
        }

        public void Clear()
        {
            _eventsIN.Clear();
            _eventsOUT.Clear();
        }

        public void PlayRecordingAsync()
        {
            _ = Tasks.AddTask(() =>
            {
                StopSequenceRequested = false;

                Stopwatch stopwatch = new(); // Créer un chronomètre

                MidiRouting.InitDevicesForSequencePlay(SequencerDefault);

                StartStopPlayerCounter(true);
                Thread.Sleep(1000);

                for (int i = 0; i < _eventsOUT.Count; i++)
                {
                    MidiEvent eventtoplay = new(_eventsOUT[i].Type, _eventsOUT[i].Values, _eventsOUT[i].Channel, _eventsOUT[i].Device);

                    long elapsedTicks = 0;
                    double waitingTime = 0;

                    if (i > 0)
                    {
                        elapsedTicks = _eventsOUT[i].EventDate.Ticks - _eventsOUT[i - 1].EventDate.Ticks;
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

                    MidiRouting.SendRecordedEvent(eventtoplay);

                    if (StopSequenceRequested)
                    {
                        break;
                    }
                }

                StartStopPlayerCounter(false);

                MidiRouting.Panic();

                SequenceFinished?.Invoke(_eventsOUT.Count.ToString() + " event(s) have been played.");
            });
        }

        public void StopSequence()
        {
            StopSequenceRequested = true;
        }
    }
}
