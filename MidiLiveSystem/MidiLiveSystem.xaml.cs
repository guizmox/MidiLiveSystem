using MidiTools;
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

        public MainWindow()
        {
            InitializeComponent();

            MidiRouting.NewLog += MidiRouting_NewLog;
            Log.Show();

            Clock = new System.Timers.Timer();
            Clock.Elapsed += Clock_Elapsed;
            Clock.Interval = 10;
            Clock.Start();

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

        private void LoadPage(Page page)
        {
            // Assigner la page au ContentControl
            BoxContainer.Content = page;
        }

        private void btnAddBox_Click(object sender, RoutedEventArgs e)
        {
            RoutingBox rtb = new RoutingBox(MidiRouting.InputDevices, MidiRouting.OutputDevices);
            rtb.OnRoutingBoxChange += Rtb_OnRoutingBoxChange;
            Boxes.Add(rtb);
            LoadPage(rtb);
        }

        private void Rtb_OnRoutingBoxChange(Guid boxId, RoutingBox.RoutingBoxEvent ev, MidiOptions options, string DeviceIn, string DeviceOut, int ChannelIn, int ChannelOut, string Preset)
        {
            var box = Boxes.FirstOrDefault(b => b.BoxGuid == boxId);

            if (box != null)
            {
                switch (ev)
                {
                    case RoutingBox.RoutingBoxEvent.ADD_ROUTING:
                        Guid guid = Routing.AddRouting(DeviceIn, DeviceOut, ChannelIn, ChannelOut, options);
                        box.RoutingGuid = guid;
                        break;
                    case RoutingBox.RoutingBoxEvent.MODIFY_ROUTING:
                        Routing.ModifyRouting(box.RoutingGuid, options);
                        break;
                    case RoutingBox.RoutingBoxEvent.CHANGE_PRESET:

                        break;
                }
            }
        }
    }
}
