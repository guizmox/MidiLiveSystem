using MidiTools;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TrackBar;

namespace MidiLiveSystem
{
    /// <summary>
    /// Logique d'interaction pour InternalSequencer.xaml
    /// </summary>
    public partial class InternalSequencer : Window
    {
        public static int MaxSequences = 4;

        private bool Playing = false;
        private string DeviceListener = "";
        private MidiRouting Routing;
        private SequencerData SeqData;
        private List<SequencerBox> SequencerBoxes = new List<SequencerBox>();
        private List<Frame> GridFrames = new List<Frame>();

        public InternalSequencer(ProjectConfiguration project, MidiRouting routing, SequencerData seqdata)
        {
            InitializeComponent();

            SeqData = seqdata;

            Routing = routing;
            InitPage();
        }

        private void InitPage()
        {
            //gdSequencer.Children.Clear();
            foreach (var d in MidiRouting.InputDevices)
            {
                cbMidiIn.Items.Add(new ComboBoxItem() { Tag = d.Name, Content = d.Name });
            }

            cbMidiIn.SelectedValue = SeqData.StartStopListener;

            for (int row = 1; row < 5; row++)
            {
                Border border = new Border();
                border.BorderBrush = Brushes.Gray;
                border.BorderThickness = new Thickness(1);

                Grid.SetRow(border, row);
                gdSequencer.Children.Add(border);

                Frame frame = new Frame
                {
                    Name = string.Concat("frmBox", row, "x", row),
                    Tag = "",
                    Margin = new Thickness(5),
                };

                GridFrames.Add(frame);
                Grid.SetRow(frame, row);
                gdSequencer.Children.Add(frame);
                frame.Tag = row.ToString();

                var box = new SequencerBox(row, SeqData.Sequencer[row - 1], this);
                SequencerBoxes.Add(box);
                frame.Navigate(box);
            };

            tbLowKeyTranspose.Text = SequencerData.LowKeyTranspose.ToString();
            tbHighKeyTranspose.Text = SequencerData.HighKeyTranspose.ToString();

            MidiRouting.InputStaticMidiMessage += Routing_IncomingMidiMessage;
        }

        private async void cbMidiIn_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBoxItem cbNew = (ComboBoxItem)e.AddedItems[0];
            ComboBoxItem cbOld = null;
            if (e.RemovedItems != null && e.RemovedItems.Count > 0) { cbOld = (ComboBoxItem)e.RemovedItems[0]; }

            if (cbOld != null)
            {
                var oldDevice = cbOld.Tag.ToString();
                MidiRouting.CheckAndCloseINPort(oldDevice);
            }

            if (cbNew != null)
            {
                DeviceListener = cbNew.Tag.ToString();
                await Routing.SetSequencerListener(DeviceListener);
                SeqData.StartStopListener = DeviceListener;

            }
        }

        private async void Routing_IncomingMidiMessage(MidiEvent ev)
        {
            if (ev.Type == MidiDevice.TypeEvent.NOTE_ON || ev.Type == MidiDevice.TypeEvent.NOTE_OFF && ev.Device.Equals(DeviceListener))
            {
                for (int i = 0; i < SequencerBoxes.Count; i++)
                {
                    if (SequencerBoxes[i].IsRecording)
                    {
                        await SequencerBoxes[i].AddMidiEvent(ev);
                        await Routing.SendNoteToSequencerBoxes(ev, SequencerBoxes[i].SequencerChannel);
                    }
                }
            }
        }

        private async void Window_Closed(object sender, EventArgs e)
        {
            //if (cbMidiIn.SelectedValue != null)
            //{
            //    MidiRouting.CheckAndCloseINPort(DeviceListener);
            //}

            MidiRouting.InputStaticMidiMessage -= Routing_IncomingMidiMessage;

            await StopPlay(true);

            string sLowKey = await tbLowKeyTranspose.Dispatcher.InvokeAsync(() => tbLowKeyTranspose.Text);
            string sHighey = await tbHighKeyTranspose.Dispatcher.InvokeAsync(() => tbHighKeyTranspose.Text);
            var result = SeqData.SetTransposition(sLowKey, sHighey);
            await tbLowKeyTranspose.Dispatcher.InvokeAsync(() => tbLowKeyTranspose.Text = result[0].ToString());
            await tbHighKeyTranspose.Dispatcher.InvokeAsync(() => tbHighKeyTranspose.Text = result[1].ToString());
        }

        private async void btnPlaySequences_Click(object sender, RoutedEventArgs e)
        {
            await StartPlay(true);
        }

        private async void btnStopSequences_Click(object sender, RoutedEventArgs e)
        {
            await StopPlay(true);
        }

        //private async void Window_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        //{
        //    if (Playing)
        //    {
        //        for (int i = 0; i < MaxSequences; i++)
        //        {
        //            await SequencerBoxes[i].Listener(false);
        //        }
        //    }
        //}

        //private async void Window_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        //{
        //    if (Playing)
        //    {
        //        for (int i = 0; i < MaxSequences; i++)
        //        {
        //            await SequencerBoxes[i].Listener(true);
        //        }
        //    }
        //}

        public async Task StartPlay(bool bFromActualWindow)
        {
            if (Playing) //si on joue déjà, le deuxième message start éteint les séquences.
            {
                await StopPlay(bFromActualWindow);
            }
            else
            {
                string sLowKey = await tbLowKeyTranspose.Dispatcher.InvokeAsync(() => tbLowKeyTranspose.Text);
                string sHighey = await tbHighKeyTranspose.Dispatcher.InvokeAsync(() => tbHighKeyTranspose.Text);
                var result = SeqData.SetTransposition(sLowKey, sHighey);
                await tbLowKeyTranspose.Dispatcher.InvokeAsync(() => tbLowKeyTranspose.Text = result[0].ToString());
                await tbHighKeyTranspose.Dispatcher.InvokeAsync(() => tbHighKeyTranspose.Text = result[1].ToString());

                await btnPlaySequences.Dispatcher.InvokeAsync(() => btnPlaySequences.Background = Brushes.IndianRed);
                await btnStopSequences.Dispatcher.InvokeAsync(() => btnStopSequences.Background = Brushes.DarkGray);

                if (bFromActualWindow)
                {
                    bool bOK = true;

                    for (int i = 0; i < SequencerBoxes.Count; i++)
                    {
                        if (SequencerBoxes[i].IsRecording)
                        {
                            MessageBox.Show("You must stop the recordings");
                            bOK = false;
                            break;
                        }
                    }

                    if (bOK)
                    {
                        MidiRouting.InputStaticMidiMessage -= Routing_IncomingMidiMessage;

                        for (int i = 0; i < MaxSequences; i++)
                        {
                            if (SeqData.Sequencer[i].SequenceHasData()) { SequencerData.InitTimer(SeqData.Sequencer[i], i); }
                            SeqData.Sequencer[i].InitSequence();        
                            await SequencerBoxes[i].Listener(true);
                        }
                        SequencerData.StartTimers();
                    }
                }
                else
                {
                    MidiRouting.InputStaticMidiMessage -= Routing_IncomingMidiMessage;

                    for (int i = 0; i < MaxSequences; i++)
                    {
                        await SequencerBoxes[i].Listener(false);
                        if (SeqData.Sequencer[i].SequenceHasData()) { SequencerData.InitTimer(SeqData.Sequencer[i], i); }
                        SeqData.Sequencer[i].InitSequence();
                    }
                    SequencerData.StartTimers();
                }

                Playing = true;
            }
        }

        public async Task StopPlay(bool bFromActualWindow)
        {
            if (Playing)
            {
                await btnPlaySequences.Dispatcher.InvokeAsync(() => btnPlaySequences.Background = Brushes.DarkGray);
                await btnStopSequences.Dispatcher.InvokeAsync(() => btnStopSequences.Background = Brushes.DarkGray);

                if (bFromActualWindow)
                {
                    SequencerData.StopTimers();

                    for (int i = 0; i < MaxSequences; i++)
                    {
                        SeqData.Sequencer[i].StopSequence();
                        await SequencerBoxes[i].Listener(false);
                    }
                   
                    MidiRouting.InputStaticMidiMessage += Routing_IncomingMidiMessage;
                }
                else
                {
                    SequencerData.StopTimers();

                    for (int i = 0; i < MaxSequences; i++)
                    {
                        await SequencerBoxes[i].Listener(false);
                        SeqData.Sequencer[i].StopSequence();
                    }
                   
                }

                Routing.Panic(false);
                Playing = false;
            }
        }
    }
}
