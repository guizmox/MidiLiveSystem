using MidiTools;
using RtMidi.Core.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static MidiLiveSystem.RoutingBox;

namespace MidiLiveSystem
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private System.Timers.Timer Clock;

        MidiLog Log = new MidiLog();
        MidiRouting Routing = new MidiRouting();
        List<RoutingBox> Boxes = new List<RoutingBox>();
        List<Frame> GridFrames = new List<Frame>();

        public MainWindow()
        {
            InitializeComponent();
            InitPage();
            MidiRouting.NewLog += MidiRouting_NewLog;
            Log.Show();

            Clock = new System.Timers.Timer();
            Clock.Elapsed += Clock_Elapsed;
            Clock.Interval = 10;
            Clock.Start();

        }

        private void InitPage()
        {
            for (int row = 0; row < 4; row++)
            {
                for (int col = 0; col < 4; col++)
                {
                    Border border = new Border();
                    border.BorderBrush = Brushes.Gray;
                    border.BorderThickness = new Thickness(1);

                    Grid.SetRow(border, row);
                    Grid.SetColumn(border, col);
                    gridBoxes.Children.Add(border);

                    Frame frame = new Frame
                    {
                        Name = string.Concat("frmBox", row, "x", col),
                        Tag = ""
                    };

                    GridFrames.Add(frame);
                    gridBoxes.Children.Add(frame);
                    Grid.SetRow(frame, row);
                    Grid.SetColumn(frame, col);
                };
            }
        }

        private void Clock_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                lblCharge.Content = Routing.CyclesInfo;
            });
        }

        private void MidiRouting_NewLog(string sDevice, bool bIn, string sLog)
        {
            Log.AddLog(sLog);
        }

        private void AddRoutingBoxToFrame(RoutingBox rtb)
        {
            var frame = GridFrames.FirstOrDefault(g => g.Tag.ToString().Equals(""));

            if (frame == null)
            {
                MessageBox.Show("You can't add more Routing Box");
            }
            else
            {
                frame.Tag = rtb.BoxGuid.ToString();
                frame.Navigate(rtb);
            }
        }

        private void btnAddBox_Click(object sender, RoutedEventArgs e)
        {
            RoutingBox rtb = new RoutingBox(MidiRouting.InputDevices, MidiRouting.OutputDevices);
            Boxes.Add(rtb);
            AddRoutingBoxToFrame(rtb);
        }

        private void btnSaveRouting_Click(object sender, RoutedEventArgs e)
        {
            foreach (RoutingBox box in Boxes)
            {
                if (box.RoutingGuid == Guid.Empty)
                {
                    MidiOptions mo = box.GetOptions();
                    box.RoutingGuid = Routing.AddRouting(((ComboBoxItem)box.cbMidiIn.SelectedItem).Tag.ToString(),
                                       ((ComboBoxItem)box.cbMidiOut.SelectedItem).Tag.ToString(),
                                       Convert.ToInt32(((ComboBoxItem)box.cbChannelMidiIn.SelectedItem).Tag.ToString()),
                                       Convert.ToInt32(((ComboBoxItem)box.cbChannelMidiOut.SelectedItem).Tag.ToString()),
                                       mo);
                }
                else
                {
                    MidiOptions mo = box.GetOptions();
                    Routing.ModifyRouting(box.RoutingGuid, mo);
                }
            }
        }
    }
}
    