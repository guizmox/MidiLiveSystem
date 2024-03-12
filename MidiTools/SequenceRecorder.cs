using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using static MidiTools.MidiDevice;
using System.Threading.Tasks;
using System.Threading;
using System.Timers;

namespace MidiTools
{

    [Serializable]
    public class MidiSequence
    {
        public List<LiveData> SequencerDefault = new List<LiveData>();

        public delegate void SequenceFinishedHandler(string sInfo);
        public event SequenceFinishedHandler SequenceFinished;

        public delegate void RecorderLengthCounter(string sInfo);
        public event RecorderLengthCounter RecordCounter;

        public bool StopSequenceRequested = false;
        private int PlayStatus = 0;

        private List<MidiEvent> _eventsIN = new List<MidiEvent>();
        private List<MidiEvent> _eventsOUT = new List<MidiEvent>();

        public List<MidiEvent> EventsOUT { get { return _eventsOUT; } }

        public TimeSpan SequenceLength = TimeSpan.Zero;
        public bool IsStopped = true;

        private System.Timers.Timer Recorder;
        private DateTime RecorderStart;

        private System.Timers.Timer Player;
        private DateTime PlayerStart;

        public MidiSequence()
        {

        }

        public string GetSequenceInfo()
        {
            int iTracks = 0;
            StringBuilder sbInfo = new StringBuilder();
            sbInfo.AppendLine("Sequence Status :");
            sbInfo.AppendLine(Environment.NewLine);
            var devices = _eventsOUT.Select(e => e.Device).Distinct();

            foreach (var s in devices)
            {
                string sChannels = " [Ch : ";
                for (int i = 1; i <= 16; i++)
                {
                    if (_eventsOUT.Count(e => e.Device.Equals(s) && e.Channel == Tools.GetChannel(i)) > 0)
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

        public void StartRecording(bool bIn, bool bOut, MidiRouting routing)
        {
            //on doit absolument garder les valeurs actuelles des données pour figer tous les paramètres MIDI pour pouvoir reproduire la séquence fidèlement
            SequencerDefault = routing.GetLiveCCData();

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

        public async void PlaySequenceAsync(MidiRouting routing)
        {
            StopSequenceRequested = false;

            await Task.Factory.StartNew(() =>
            {
                StartStopPlayerCounter(true);

                PlaySequence(_eventsOUT, routing);

                Thread.Sleep(2500);

                StartStopPlayerCounter(false);

                SequenceFinished?.Invoke(_eventsOUT.Count.ToString() + " event(s) have been played.");
            });
        }

        private void PlaySequence(List<MidiEvent> events, MidiRouting routing)
        {
            PlayStatus = 1;

            Stopwatch stopwatch = new Stopwatch(); // Créer un chronomètre

            routing.InitDevicesForSequencePlay(SequencerDefault);

            for (int i = 0; i < events.Count; i++)
            {
                MidiEvent eventtoplay = new MidiEvent(events[i].Type, events[i].Values, events[i].Channel, events[i].Device);

                long elapsedTicks = 0;
                double waitingTime = 0;

                if (i > 0)
                {
                    elapsedTicks = events[i].EventDate.Ticks - events[i - 1].EventDate.Ticks;
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

                routing.SendSequencedEvent(eventtoplay);

                if (StopSequenceRequested)
                {
                    routing.Panic();
                    break;
                }
            }

            PlayStatus = 0;
        }

        public void StopSequence()
        {
            StopSequenceRequested = true;
        }
    }
}
