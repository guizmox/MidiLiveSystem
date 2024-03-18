using MidiTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MidiLiveSystem
{
    /// <summary>
    /// Logique d'interaction pour SequencerBox.xaml
    /// </summary>
    public partial class SequencerBox : Page
    {
        private System.Timers.Timer BufferProcessor;
        internal bool IsRecording { get; private set; } = false;
        internal int SequencerIndex = 0;
        internal Sequencer InternalSequence;
        private List<MidiEvent> Buffer = new List<MidiEvent>();
        private int ActualStep = 0;
        private Button[] ButtonSteps = new Button[32];
        private bool AlternateButtonColor = false;

        private List<SequenceStep> StepsRecorded = new List<SequenceStep>();

        public SequencerBox(int iSequencer, Sequencer seq)
        {
            InitializeComponent();

            BufferProcessor = new System.Timers.Timer();
            BufferProcessor.Elapsed += BufferProcessor_Elapsed;
            BufferProcessor.Interval = 250;
            BufferProcessor.Start();

            SequencerIndex = iSequencer;
            InternalSequence = seq;

            InitPage();
        }

        private void InitPage()
        {
            for (int i = 1; i < 33; i++)
            {
                cbQteSteps.Items.Add(new ComboBoxItem { Content = i.ToString(), Tag = i.ToString() });
            }

            for (int i = 1; i < 33; i++)
            {
                Button btn = new Button();
                btn.Name = "btnStep_" + i.ToString();
                btn.Background = Brushes.White;
                btn.Margin = new Thickness(2);
                btn.VerticalAlignment = VerticalAlignment.Bottom;
                btn.Click += BtnStep_Click;
                Grid.SetColumn(btn, i - 1);
                gdSteps.Children.Add(btn);
                ButtonSteps[i - 1] = btn;
            }

            if (InternalSequence != null)
            {
                FillUI();
            }
        }

        private async Task FillUI()
        {
            await Dispatcher.InvokeAsync(async () =>
            {
                tbChannel.Text = string.Concat("Channel : ", InternalSequence.Channel.ToString());
                cbQuantization.SelectedValue = InternalSequence.Quantization;
                tbTempo.Text = InternalSequence.Tempo.ToString();
                cbQteSteps.SelectedValue = InternalSequence.Steps;
                ckTranspose.IsChecked = InternalSequence.Transpose;
                slGate.Value = InternalSequence.Sequence != null && InternalSequence.Sequence.Length > 0 ? InternalSequence.Sequence[0].GatePercent : 25;

                if (InternalSequence.Sequence != null)
                {
                    for (int i = 0; i < InternalSequence.Sequence.Length; i++)
                    {
                        await ChangeButtonColor(i, Brushes.IndianRed);
                    }
                }
            });
        }

        private async void cbQteSteps_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int iMax = ((ComboBox)sender).SelectedIndex;
            await ClearButtons(iMax);
        }

        private async Task ClearButtons(int iMax)
        {
            ActualStep = 0;
            StepsRecorded.Clear();

            for (int i = 0; i < 32; i++)
            {
                if (i <= iMax)
                {
                    await ChangeButtonColor(i, Brushes.White);
                }
                else
                {
                    await ChangeButtonColor(i, Brushes.Black);
                }
            }
        }

        private async void BufferProcessor_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (IsRecording)
            {                
                AlternateButtonColor = AlternateButtonColor ? false : true;
                await ChangeButtonColor(ActualStep, AlternateButtonColor ? Brushes.Gray : Brushes.GreenYellow);
            }
        }

        private async Task ChangeButtonColor(int iStep, SolidColorBrush color)
        {
            await Dispatcher.InvokeAsync(() => 
            {
                ButtonSteps[iStep].Background = color;
            });
        }

        private void BtnStep_Click(object sender, RoutedEventArgs e)
        {

        }

        private async void btnRecordSequence_Click(object sender, RoutedEventArgs e)
        {
            IsRecording = true;

            int iMax = await cbQteSteps.Dispatcher.InvokeAsync(() => cbQteSteps.SelectedIndex);

            InternalSequence.Clear();
            await ClearButtons(iMax);

            await btnRecordSequence.Dispatcher.InvokeAsync(() => btnRecordSequence.Background = Brushes.IndianRed);
            await btnStopSequence.Dispatcher.InvokeAsync(() => btnStopSequence.Background = Brushes.DarkGray);

        }

        private async void btnStopSequence_Click(object sender, RoutedEventArgs e)
        {
            ActualStep = 0;
            IsRecording = false;

            await btnRecordSequence.Dispatcher.InvokeAsync(() => btnRecordSequence.Background = Brushes.DarkGray);
            await btnStopSequence.Dispatcher.InvokeAsync(() => btnStopSequence.Background = Brushes.DarkGray);

            await ProcessRecordedData(StepsRecorded);
        }

        private async void btnIncrementStep_Click(object sender, RoutedEventArgs e)
        {
            await ChangeButtonColor(ActualStep, Brushes.DarkRed);
            ActualStep++;
        }

        internal async Task AddMidiEvent(MidiEvent ev)
        {
            if (ev.Type == MidiDevice.TypeEvent.NOTE_ON)
            {
                lock (Buffer)
                {
                    Buffer.Add(ev);
                }
            }
            else if (ev.Type == MidiDevice.TypeEvent.NOTE_OFF)
            {
                int iSteps = await cbQteSteps.Dispatcher.InvokeAsync(() => cbQteSteps.SelectedIndex);
                int iGatePercent = await slGate.Dispatcher.InvokeAsync(() => (int)slGate.Value);

                if (ActualStep >= iSteps)
                {
                    await ProcessEvent(iGatePercent);

                    await btnRecordSequence.Dispatcher.InvokeAsync(() => btnRecordSequence.Background = Brushes.DarkGray);
                    await btnStopSequence.Dispatcher.InvokeAsync(() => btnStopSequence.Background = Brushes.DarkGray);

                    IsRecording = false;

                    await ProcessRecordedData(StepsRecorded);
                }
                else
                {
                    await ProcessEvent(iGatePercent);
                }
            }
        }

        private async Task ProcessEvent(int iGatePercent)
        {
            lock (Buffer)
            {
                if (Buffer.Count >= 1)
                {
                    int iCount = Buffer.Count;
                    List<int[]> notes = new List<int[]>();
                    foreach (var buf in Buffer.Where(b => b.Type == MidiDevice.TypeEvent.NOTE_ON))
                    {
                        notes.Add(new int[] { buf.Values[0], buf.Values[1] });
                    }
                    SequenceStep step = new SequenceStep(ActualStep, iGatePercent, iCount, notes);
                    StepsRecorded.Add(step);
                    ActualStep++;
                }
                Buffer.Clear();
            }

            if (ActualStep > 0)
            {
                await ChangeButtonColor(ActualStep - 1, Brushes.IndianRed);
            }
        }

        private async Task ProcessRecordedData(List<SequenceStep> stepsRecorded)
        {
            int iSteps = await cbQteSteps.Dispatcher.InvokeAsync(() => Convert.ToInt32(cbQteSteps.SelectedValue));
            string sTempo = await tbTempo.Dispatcher.InvokeAsync(() => tbTempo.Text.Trim());
            bool bTranspose = await ckTranspose.Dispatcher.InvokeAsync(() => ckTranspose.IsChecked.Value);
            string sQuantize = await cbQuantization.Dispatcher.InvokeAsync(() => cbQuantization.SelectedValue.ToString());
            int iTempo = 120;
            if (int.TryParse(sTempo, out iTempo))
            {
                if (iTempo >= 40 && iTempo <= 300)
                {

                }
                else
                {
                    MessageBox.Show("Tempo value out of bounds. Set to default (120)");
                    await tbTempo.Dispatcher.InvokeAsync(() => tbTempo.Text = "120");
                }
            }

            InternalSequence.Channel = SequencerIndex;
            InternalSequence.Quantization = sQuantize;
            InternalSequence.Steps = iSteps;
            InternalSequence.Sequence = stepsRecorded.ToArray();
            InternalSequence.Transpose = bTranspose;
        }
    }
}
