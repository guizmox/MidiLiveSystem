using MidiTools;
using RtMidi.Core.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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
using System.Xml.Linq;
using System.Xml.Serialization;
using static MidiLiveSystem.RoutingBox;

namespace MidiLiveSystem
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private System.Timers.Timer Clock;

        private MidiLog Log = new MidiLog();
        private MidiRouting Routing = new MidiRouting();
        private List<RoutingBox> Boxes = new List<RoutingBox>();
        private List<Frame> GridFrames = new List<Frame>();
        public static BoxPreset CopiedPreset = new BoxPreset();
        public ProjectConfiguration Project;
        public SQLiteDatabaseManager Database = new SQLiteDatabaseManager();

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

        private void RoutingBox_UIEvent(Guid gBox, string sControl, object sValue)
        {
            var box = Boxes.FirstOrDefault(b => b.BoxGuid == gBox);
            if (box != null)
            {
                switch (sControl)
                {
                    case "SOLO":
                        if (box.RoutingGuid != Guid.Empty)
                        {
                            if ((bool)sValue)
                            {
                                Routing.SetSolo(box.RoutingGuid);
                            }
                            else
                            {
                                Routing.UnmuteAllRouting();
                            }
                        }
                        break;
                    case "MUTE:":
                        if (box.RoutingGuid != Guid.Empty)
                        {
                            if ((bool)sValue)
                            {
                                Routing.MuteRouting(box.RoutingGuid);
                            }
                            else
                            {
                                Routing.UnmuteRouting(box.RoutingGuid);
                            }
                        }
                        break;
                    case "PROGRAM_CHANGE":
                        if (box.RoutingGuid != Guid.Empty)
                        {
                            Routing.SendPresetChange(box.RoutingGuid, (MidiPreset)sValue);
                        }
                        break;
                    case "PRESET_CHANGE":
                        if (box.RoutingGuid != Guid.Empty)
                        {
                            Routing.ModifyRoutingOptions(box.RoutingGuid, (MidiOptions)sValue);
                        }
                        break;
                    case "COPY_PRESET":
                        CopiedPreset = (BoxPreset)sValue;
                        break;
                }
            }
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

                rtb.OnUIEvent += RoutingBox_UIEvent;
            }
        }

        private void btnAddBox_Click(object sender, RoutedEventArgs e)
        {
            RoutingBox rtb = new RoutingBox(MidiRouting.InputDevices, MidiRouting.OutputDevices);
            Boxes.Add(rtb);
            AddRoutingBoxToFrame(rtb);
        }

        private void btnSaveProject_Click(object sender, RoutedEventArgs e)
        {
            foreach (RoutingBox box in Boxes)
            {
                if (box.RoutingGuid == Guid.Empty)
                {
                    if (box.cbMidiIn.SelectedItem != null && box.cbMidiOut.SelectedItem != null)
                    {
                        box.RoutingGuid = Routing.AddRouting(((ComboBoxItem)box.cbMidiIn.SelectedItem).Tag.ToString(),
                                           ((ComboBoxItem)box.cbMidiOut.SelectedItem).Tag.ToString(),
                                           Convert.ToInt32(((ComboBoxItem)box.cbChannelMidiIn.SelectedItem).Tag.ToString()),
                                           Convert.ToInt32(((ComboBoxItem)box.cbChannelMidiOut.SelectedItem).Tag.ToString()),
                                           box.LoadOptions(), box.GetPreset());
                    }
                }
                else
                {
                    Routing.ModifyRoutingOptions(box.RoutingGuid, box.LoadOptions());
                }
            }

            //sauvegarde de la config
            if (Project == null)
            {
                Project = new ProjectConfiguration(); //config par défaut
            }

            string sId = Project.ProjectId.ToString();
            string sProjectConfig = "";
            string sRoutingConfig = "";
            string sProjectName = Project.ProjectName;

            XmlSerializer serializerConfig = new XmlSerializer(typeof(ProjectConfiguration));
            using (StringWriter textWriter = new StringWriter())
            {
                serializerConfig.Serialize(textWriter, Project);
                sProjectConfig = textWriter.ToString();
            }

            //sauvegarde des box
            foreach (RoutingBox box in Boxes)
            {
                var memory = box.GetRoutingBoxMemory();
                XmlSerializer serializerRouting = new XmlSerializer(typeof(RoutingBoxes));
                using (StringWriter textWriter = new StringWriter())
                {
                    serializerRouting.Serialize(textWriter, memory);
                    sRoutingConfig = textWriter.ToString();
                }
            }

            try
            {
                Database.SaveProject(sId, sProjectName, sProjectConfig, sRoutingConfig, Environment.UserName);
            }
            catch (Exception ex) 
            {
                MessageBox.Show("Unable to Save Project : " + ex.Message);
            }
        }

        private void btnOpenProject_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                //Id, ProjectGuid, Name, DateProject, Author, Active
                List<string[]> projects = Database.GetProjects();
                if (projects.Count > 0) 
                {
                    Projects prj = new Projects(Database, projects);
                    prj.ShowDialog();
                    var project = prj.Project;
                    if (project != null)
                    {
                        //Configurer l'UI
                        //TODO
                    }
                    else
                    {
                        MessageBox.Show("Project not Loaded");
                    }
                }
                else
                {
                    MessageBox.Show("No project found.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unable to Get Project List : " + ex.Message);
            }
        }

        private void btnSettings_Click(object sender, RoutedEventArgs e)
        {

            MidiConfiguration mc;

            if (Project != null)
            {
                mc = new MidiConfiguration(Project);
            }
            else
            {
                mc = new MidiConfiguration();
            }

            mc.ShowDialog();
            var config = mc.Configuration;
            if (config != null ) 
            {
                Project = config;
            }
            else
            {
                MessageBox.Show("Unable to get Project Configuration");
            }
        }
    }

    [Serializable]
    public class RoutingBoxes
    {
        public BoxPreset[] AllPresets;

        public RoutingBoxes()
        {

        }
    }
}
    