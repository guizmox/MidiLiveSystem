using MidiTools;
using System;
using System.Collections.Generic;
using System.Drawing;
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
        private InternalSequencer MainWindow;

        private System.Timers.Timer BufferProcessor;
        internal bool IsRecording { get; private set; } = false;

        internal int SequencerIndex = 0;
        internal Sequencer InternalSequence;
        private List<MidiEvent> Buffer = new List<MidiEvent>();
        private int ActualStep = 0;
        private Button[] ButtonSteps = new Button[32];
        private bool AlternateButtonColor = false;
        private bool IsPlaying = false;

        private List<SequenceStep> StepsRecorded = new List<SequenceStep>();

        public SequencerBox(int iSequencer, Sequencer seq, InternalSequencer mainW)
        {
            InitializeComponent();

            MainWindow = mainW;

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
                btn.Tag = i - 1;
                btn.Background = Brushes.White;
                btn.Margin = new Thickness(1);
                btn.VerticalAlignment = VerticalAlignment.Bottom;
                btn.FontSize = 9;
                btn.Width = 40;
                btn.Padding = new Thickness(1);
                btn.Content = "--";
                btn.Foreground = Brushes.Black;
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
                slTempo.Value = InternalSequence.Tempo;
                cbQteSteps.SelectedValue = InternalSequence.Steps == 0 ? 4 : InternalSequence.Steps;
                ckTranspose.IsChecked = InternalSequence.Transpose;
                slGate.Value = InternalSequence.Sequence[0] != null ? InternalSequence.Sequence[0].GatePercent : 25;
                btnMuted.Background = InternalSequence.Muted ? Brushes.IndianRed : Brushes.DarkGray;

                for (int iStep = 0; iStep < InternalSequence.Sequence.Length; iStep++)
                {
                    SequenceStep seq = InternalSequence.Sequence[iStep];

                    if (seq == null) //c'est un intermédiaire
                    {
                        await ChangeButtonText(iStep, null);
                        await ChangeButtonColor(iStep, Brushes.DarkOrange);
                    }
                    else if (seq.StepCount <= 0)
                    {
                        await ChangeButtonText(iStep, null);
                        await ChangeButtonColor(iStep, Brushes.White);
                    }
                    else
                    {
                        await ChangeButtonText(iStep, seq);
                        await ChangeButtonColor(iStep, Brushes.IndianRed);
                    }
                }

                for (int iStep = InternalSequence.Steps; iStep < 32; iStep++)
                {
                    await ChangeButtonColor(iStep, Brushes.Black);
                }
            });
        }

        private async void slTempo_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            double tempo = await slTempo.Dispatcher.InvokeAsync(() => slTempo.Value);
            await lbTempo.Dispatcher.InvokeAsync(() => lbTempo.Content = string.Concat("Tempo (", tempo.ToString(), ")"));
            await InternalSequence.ChangeTempo((int)tempo);
        }

        private async void ckTranspose_Click(object sender, RoutedEventArgs e)
        {
            InternalSequence.Transpose = await (ckTranspose.Dispatcher.InvokeAsync(() => ckTranspose.IsChecked.Value));
        }

        private async void slGate_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            double gate = await (slGate.Dispatcher.InvokeAsync(() => slGate.Value));
            await lbGate.Dispatcher.InvokeAsync(() => lbGate.Content = string.Concat("Gate (", gate.ToString(), ")"));

            lock (InternalSequence.Sequence)
            {
                for (int i = 0; i < InternalSequence.Sequence.Length; i++)
                {
                    if (InternalSequence.Sequence[i] != null)
                    {
                        InternalSequence.Sequence[i].GatePercent = (int)gate;
                    }
                }
            }
        }

        private async void cbQteSteps_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBoxItem cb = ((ComboBoxItem)e.AddedItems[0]);
            if (cb.IsFocused)
            {
                int iMax = Convert.ToInt32(cb.Tag) - 1;
                await ClearButtons(iMax + 1);
            }
        }

        private async void cbQuantization_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBoxItem cb = ((ComboBoxItem)e.AddedItems[0]);
            if (cb.IsFocused)
            {
                string sQuantize = cb.Tag.ToString();
                await ChangeQuantization(sQuantize);
            }
        }

        private async void btnMuted_Click(object sender, RoutedEventArgs e)
        {
            await btnMuted.Dispatcher.InvokeAsync(() =>
            {
                if (btnMuted.Background == Brushes.DarkGray)
                { btnMuted.Background = Brushes.IndianRed; InternalSequence.Muted = true; }
                else { btnMuted.Background = Brushes.DarkGray; InternalSequence.Muted = false; }
            });
        }

        private async Task ChangeQuantization(string sQuantize)
        {
            await UIEventPool.AddTask(() =>
            {
                InternalSequence.Quantization = sQuantize;
            });

            ActualStep = 0;
            StepsRecorded.Clear();
        }

        private async Task ClearButtons(int iMax)
        {
            await UIEventPool.AddTask(() =>
            {
                InternalSequence.InitSequence(iMax);

                ActualStep = 0;
                StepsRecorded.Clear();
            });

            for (int i = 0; i < 32; i++)
            {
                await ButtonSteps[i].Dispatcher.InvokeAsync(() => ButtonSteps[i].Content = "--");
                if (i < iMax)
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
                await ChangeButtonColor(ActualStep, AlternateButtonColor ? Brushes.White : Brushes.IndianRed);
            }
        }

        private async Task ChangeButtonColor(int iStep, SolidColorBrush color)
        {
            if (iStep >= 0)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    ButtonSteps[iStep].Background = color;
                });
            }
        }

        private async Task ChangeButtonText(int iStep, SequenceStep stepdata)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                if (stepdata != null)
                {
                    string sText = "";
                    if (stepdata.NotesAndVelocity != null && stepdata.NotesAndVelocity.Count > 0)
                    {
                        sText = Tools.MidiNoteNumberToNoteName(stepdata.NotesAndVelocity[0][0]);
                        if (stepdata.NotesAndVelocity.Count > 1) { sText = string.Concat(sText, Environment.NewLine, "+", (stepdata.NotesAndVelocity.Count - 1)); }
                    }
                    ButtonSteps[iStep].Content = sText;
                }
            });
        }

        private async Task ChangeButtonTextPlay(int iStep)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                ButtonSteps[iStep].Content = ">";
            });
        }

        private void BtnStep_Click(object sender, RoutedEventArgs e)
        {
            if (!IsPlaying)
            {
                int iTag = Convert.ToInt32(((Button)sender).Tag.ToString());

                if (InternalSequence.Sequence[iTag].NotesAndVelocity != null && InternalSequence.Sequence[iTag].NotesAndVelocity.Count > 0)
                {
                    EditValue editor = new EditValue(InternalSequence.Sequence[iTag], "Edit Step Values");
                    editor.ShowDialog();
                    InternalSequence.Sequence[iTag] = editor.GetStep();
                }
            }
        }

        private async void btnRecordSequence_Click(object sender, RoutedEventArgs e)
        {
            IsRecording = true;

            int iMax = await cbQteSteps.Dispatcher.InvokeAsync(() => Convert.ToInt32(cbQteSteps.SelectedValue));

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
            if (IsRecording)
            {
                int iLength = await cbQteSteps.Dispatcher.InvokeAsync(() => Convert.ToInt32(cbQteSteps.SelectedValue));

                if (InternalSequence.Sequence != null)
                {
                    await ChangeButtonText(ActualStep, InternalSequence.Sequence.Last());
                }

                if (StepsRecorded.Count > 0)
                {
                    StepsRecorded.Last().StepCount += 1;
                }

                if (ActualStep < iLength - 1)
                {
                    await ChangeButtonColor(ActualStep, Brushes.DarkOrange);

                    ActualStep++;
                }
                else
                {
                    await ChangeButtonColor(ActualStep, Brushes.DarkRed);

                    ActualStep = 0;
                    IsRecording = false;

                    await btnRecordSequence.Dispatcher.InvokeAsync(() => btnRecordSequence.Background = Brushes.DarkGray);
                    await btnStopSequence.Dispatcher.InvokeAsync(() => btnStopSequence.Background = Brushes.DarkGray);

                    await ProcessRecordedData(StepsRecorded);
                }
            }
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
            else if (ev.Type == MidiDevice.TypeEvent.NOTE_OFF && Buffer.Count > 0)
            {
                int iSteps = await cbQteSteps.Dispatcher.InvokeAsync(() => cbQteSteps.SelectedIndex);
                double dGatePercent = await slGate.Dispatcher.InvokeAsync(() => slGate.Value);

                if (ActualStep >= iSteps)
                {
                    await ProcessEvent(dGatePercent);

                    await btnRecordSequence.Dispatcher.InvokeAsync(() => btnRecordSequence.Background = Brushes.DarkGray);
                    await btnStopSequence.Dispatcher.InvokeAsync(() => btnStopSequence.Background = Brushes.DarkGray);

                    IsRecording = false;

                    await ProcessRecordedData(StepsRecorded);
                }
                else
                {
                    await ProcessEvent(dGatePercent);
                }
            }
        }

        private async Task ProcessEvent(double dGatePercent)
        {
            lock (Buffer)
            {
                if (Buffer.Count >= 1)
                {
                    List<int[]> notes = new List<int[]>();
                    foreach (var buf in Buffer.Where(b => b.Type == MidiDevice.TypeEvent.NOTE_ON))
                    {
                        notes.Add(new int[] { buf.Values[0], buf.Values[1] });
                    }
                    SequenceStep step = new SequenceStep(ActualStep, dGatePercent, 1, notes);
                    StepsRecorded.Add(step);
                    ActualStep++;
                }
                Buffer.Clear();
            }

            if (ActualStep > 0)
            {
                await ChangeButtonColor(ActualStep - 1, Brushes.IndianRed);
                await ChangeButtonText(ActualStep - 1, StepsRecorded.Last());
            }
        }

        private async Task ProcessRecordedData(List<SequenceStep> stepsRecorded)
        {
            int iSteps = await cbQteSteps.Dispatcher.InvokeAsync(() => Convert.ToInt32(cbQteSteps.SelectedValue));
            double dTempo = await slTempo.Dispatcher.InvokeAsync(() => slTempo.Value);
            bool bTranspose = await ckTranspose.Dispatcher.InvokeAsync(() => ckTranspose.IsChecked.Value);
            string sQuantize = await cbQuantization.Dispatcher.InvokeAsync(() => cbQuantization.SelectedValue.ToString());

            InternalSequence.Channel = SequencerIndex;
            InternalSequence.Quantization = sQuantize;
            InternalSequence.Steps = iSteps;
            InternalSequence.SetSequence(stepsRecorded);
            InternalSequence.Transpose = bTranspose;
            await InternalSequence.ChangeTempo((int)dTempo);

            await FillUI();
        }

        internal async Task Listener(bool bOK)
        {
            if (await Dispatcher.InvokeAsync(() => MainWindow.IsVisible))
            {
                if (!InternalSequence.Muted)
                {
                    await FillUI();

                    if (bOK)
                    {
                        IsPlaying = true;
                        await btnMuted.Dispatcher.InvokeAsync(() => btnMuted.IsEnabled = false);
                        await btnRecordSequence.Dispatcher.InvokeAsync(() => btnRecordSequence.IsEnabled = false);
                        await btnIncrementStep.Dispatcher.InvokeAsync(() => btnIncrementStep.IsEnabled = false);
                        await btnStopSequence.Dispatcher.InvokeAsync(() => btnStopSequence.IsEnabled = false);
                        await cbQteSteps.Dispatcher.InvokeAsync(() => cbQteSteps.IsEnabled = false);
                        await cbQuantization.Dispatcher.InvokeAsync(() => cbQuantization.IsEnabled = false);
                        //await slGate.Dispatcher.InvokeAsync(() => slGate.IsEnabled = false);
                        //await slTempo.Dispatcher.InvokeAsync(() => slTempo.IsEnabled = false);
                        await ckTranspose.Dispatcher.InvokeAsync(() => ckTranspose.IsEnabled = false);

                        InternalSequence.OnInternalSequencerStep += InternalSequence_OnInternalSequencerStep;
                    }
                    else
                    {
                        IsPlaying = false;
                        await btnMuted.Dispatcher.InvokeAsync(() => btnMuted.IsEnabled = true);
                        await btnRecordSequence.Dispatcher.InvokeAsync(() => btnRecordSequence.IsEnabled = true);
                        await btnIncrementStep.Dispatcher.InvokeAsync(() => btnIncrementStep.IsEnabled = true);
                        await btnStopSequence.Dispatcher.InvokeAsync(() => btnStopSequence.IsEnabled = true);
                        await cbQteSteps.Dispatcher.InvokeAsync(() => cbQteSteps.IsEnabled = true);
                        await cbQuantization.Dispatcher.InvokeAsync(() => cbQuantization.IsEnabled = true);
                        //await slGate.Dispatcher.InvokeAsync(() => slGate.IsEnabled = true);
                        //await slTempo.Dispatcher.InvokeAsync(() => slTempo.IsEnabled = true);
                        await ckTranspose.Dispatcher.InvokeAsync(() => ckTranspose.IsEnabled = true);

                        InternalSequence.OnInternalSequencerStep -= InternalSequence_OnInternalSequencerStep;
                    }
                }
            }
        }

        private async void InternalSequence_OnInternalSequencerStep(SequenceStep notes, SequenceStep lastnotes, double lengthInMs, int lastpositionInSequence, int positionInSequence)
        {
            if (await Dispatcher.InvokeAsync(() => MainWindow.IsVisible))
            {
                await ChangeButtonColor(lastpositionInSequence, Brushes.IndianRed);
                await ChangeButtonColor(positionInSequence, Brushes.Green);
                await ChangeButtonText(lastpositionInSequence, lastnotes);
                await ChangeButtonTextPlay(positionInSequence);
            }
        }
    }
}
