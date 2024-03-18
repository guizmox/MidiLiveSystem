using MidiTools;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace MidiLiveSystem
{
    /// <summary>
    /// Logique d'interaction pour InternalSequencer.xaml
    /// </summary>
    public partial class InternalSequencer : Window
    {
        public static int MaxSequences = 4;

        private bool DeviceAdded = false;
        private string DeviceListener = "";
        private MidiRouting Routing;
        private Sequencer[] InternalSequences;
        private List<SequencerBox> SequencerBoxes = new List<SequencerBox>();
        private List<Frame> GridFrames = new List<Frame>();

        public InternalSequencer(ProjectConfiguration project, MidiRouting routing, Sequencer[] intSeq)
        {
            InitializeComponent();

            InternalSequences = intSeq;

            Routing = routing;
            InitPage();
        }

        private void InitPage()
        {
            //gdSequencer.Children.Clear();
            foreach (var d in MidiRouting.InputDevices)
            {
                cbMidiIn.Items.Add(new ComboBoxItem() { Tag = "I-" + d.Name, Content = d.Name });
            }

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

                var box = new SequencerBox(row, InternalSequences[row - 1]);
                SequencerBoxes.Add(box);
                frame.Navigate(box);
            };

            MidiRouting.StaticIncomingMidiMessage += Routing_IncomingMidiMessage;
        }

        private void cbMidiIn_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBoxItem cbNew = (ComboBoxItem)e.AddedItems[0];
            ComboBoxItem cbOld = null;
            if (e.RemovedItems != null && e.RemovedItems.Count > 0) { cbOld = (ComboBoxItem)e.RemovedItems[0]; }

            if (cbOld != null && DeviceAdded && cbOld.Tag.ToString().StartsWith("I-"))
            {
                var oldDevice = cbOld.Tag.ToString().Substring(2);
                MidiRouting.CheckAndCloseINPort(oldDevice);
            }

            if (cbNew != null && cbNew.Tag.ToString().StartsWith("I-"))
            {
                DeviceListener = cbNew.Tag.ToString().Substring(2);
                DeviceAdded = MidiRouting.CheckAndOpenINPort(DeviceListener);
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
                    }
                }
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if (DeviceAdded && cbMidiIn.SelectedValue != null)
            {
                MidiRouting.CheckAndCloseINPort(cbMidiIn.SelectedValue.ToString().Substring(2));
            }

            MidiRouting.StaticIncomingMidiMessage -= Routing_IncomingMidiMessage;
        }

        private async void btnPlaySequences_Click(object sender, RoutedEventArgs e)
        {
            await btnPlaySequences.Dispatcher.InvokeAsync(() => btnPlaySequences.Background = Brushes.IndianRed);
            await btnStopSequences.Dispatcher.InvokeAsync(() => btnStopSequences.Background = Brushes.DarkGray);

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
                for (int i = 0; i < MaxSequences; i++)
                {
                    await InternalSequences[i].StartSequence();
                }
            }
        }

        private async void btnStopSequences_Click(object sender, RoutedEventArgs e)
        {
            await btnPlaySequences.Dispatcher.InvokeAsync(() => btnPlaySequences.Background = Brushes.DarkGray);
            await btnStopSequences.Dispatcher.InvokeAsync(() => btnStopSequences.Background = Brushes.DarkGray);

            for (int i = 0; i < MaxSequences; i++)
            {
                await InternalSequences[i].StopSequence();
            }
        }
    }

    [Serializable]
    public class SequencerData
    {
        public Sequencer[] Sequencer;

        public SequencerData()
        {

        }
    }
}
