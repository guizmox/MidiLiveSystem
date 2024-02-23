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
using static System.Net.Mime.MediaTypeNames;

namespace MidiLiveSystem
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static int DefaultHorizontal = 4;
        private static int DefaultVertical = 4;

        private System.Timers.Timer Clock;

        private MidiConfiguration ConfigWindow;
        private MidiLog LogWindow;
        private Keyboard KeysWindow;
        private List<DetachedBox> DetachedWindows = new List<DetachedBox>();

        private MidiRouting Routing = new MidiRouting();
        private List<RoutingBox> Boxes = new List<RoutingBox>();
        private List<Frame> GridFrames = new List<Frame>();
        public static BoxPreset CopiedPreset = new BoxPreset();
        public ProjectConfiguration Project;
        public SQLiteDatabaseManager Database = new SQLiteDatabaseManager();
        public MidiSequence SequenceRecorder = new MidiSequence();

        public MainWindow()
        {
            InitializeComponent();
            InitFrames(DefaultHorizontal, DefaultVertical);

            Clock = new System.Timers.Timer();
            Clock.Elapsed += Clock_Elapsed;
            Clock.Interval = 10000;
            Clock.Start();

            Routing.Debug();
        }

        private void Keyboard_KeyPressed(string sKey)
        {
            if (FocusManager.GetFocusedElement(this) is TextBox textBox)
            {
                if (sKey.Equals("BACK", StringComparison.OrdinalIgnoreCase))
                {
                    string sNew = textBox.Text.Length == 0 ? "" : textBox.Text.Substring(0, textBox.Text.Length - 1);
                    textBox.Text = sNew;
                }
                else
                {
                    textBox.Text += sKey;
                }
            }
        }

        private void InitFrames(int iRows, int iCols)
        {
            GridFrames.Clear();
            gridBoxes.Children.Clear();
            gridBoxes.RowDefinitions.Clear();
            gridBoxes.ColumnDefinitions.Clear();

            if (iRows == 1 && iCols == 1) //option de maximisation
            {
                gridBoxes.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(80, GridUnitType.Star) });
                gridBoxes.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(20, GridUnitType.Star) });

                gridBoxes.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(80, GridUnitType.Star) });
                gridBoxes.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(20, GridUnitType.Star) });
            }
            else
            {
                for (int row = 0; row < iRows; row++)
                {
                    gridBoxes.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(iCols, GridUnitType.Star) });
                }
                for (int col = 0; col < iCols; col++)
                {
                    gridBoxes.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(iRows, GridUnitType.Star) });
                }
            }

            for (int row = 0; row < iRows; row++)
            {
                for (int col = 0; col < iCols; col++)
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

        private void Window_Closed(object sender, EventArgs e)
        {
            if (LogWindow != null)
            {
                LogWindow.Close();
            }
            if (KeysWindow != null)
            {
                KeysWindow.Close();
            }
            if (ConfigWindow != null)
            {
                ConfigWindow.Close();
            }
            foreach (var detached in DetachedWindows)
            {
                detached.Close();
            }
        }

        private void Log_Closed(object sender, EventArgs e)
        {
            MidiRouting.NewLog -= MidiRouting_NewLog;
            Routing.StopLog();

            btnLog.Background = Brushes.IndianRed;
            LogWindow.Closed -= Log_Closed;
            LogWindow = null;
        }

        private void MainConfiguration_Closed(object sender, EventArgs e)
        {
            var config = ConfigWindow.Configuration;
            if (config != null)
            {
                Project = config;
                //rename des box
                if (config.BoxNames != null)
                {
                    foreach (string[] boxname in config.BoxNames)
                    {
                        var b = Boxes.FirstOrDefault(b => b.BoxGuid.ToString().Equals(boxname[1]));
                        if (b != null)
                        {
                            b.BoxName = boxname[0];
                            b.GridPosition = Convert.ToInt32(boxname[2]);
                            Dispatcher.Invoke(() =>
                            {
                                b.tbRoutingName.Text = boxname[0];
                            });
                        }
                    }
                }
            }
            else
            {
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show("Unable to get Project Configuration");
                });
            }
        }

        private void DetachedBox_Closed(object sender, EventArgs e)
        {
            if (this.IsActive)
            {
                DetachedBox db = (DetachedBox)sender;
                var box = db.RoutingBox;
                box.Detached = false;
                db.Closed -= DetachedBox_Closed;
                DetachedWindows.Remove(db);

                AddAllRoutingBoxes();
            }
        }

        private void Routing_SequenceFinished(string sInfo)
        {
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show(sInfo);
                btnPlaySequence.Background = Brushes.DarkGray;
            });
        }

        private void RoutingBox_UIEvent(Guid gBox, string sControl, object sValue)
        {
            var box = Boxes.FirstOrDefault(b => b.BoxGuid == gBox);
            if (box != null)
            {
                switch (sControl)
                {
                    case "MAXIMIZE":
                        InitFrames(1, 1);
                        AddRoutingBoxToFrame(box, false);
                        break;
                    case "MINIMIZE":
                        if (Project != null)
                        {
                            InitFrames(Project.HorizontalGrid, Project.VerticalGrid);
                        }
                        else
                        {
                            InitFrames(DefaultHorizontal, DefaultVertical); 
                        }
                        AddRoutingBoxToFrame(box, false);
                        break;
                    case "DETACH":
                        box.Detached = true;
                        AddAllRoutingBoxes();
                        break;
                    case "REMOVE":
                        Boxes.Remove(box);
                        AddAllRoutingBoxes();
                        break;
                    case "MOVE_NEXT":
                        if (box.GridPosition == Boxes.Count - 1) //déjà en dernière position
                        {

                        }
                        else
                        {
                            var next = Boxes.FirstOrDefault(b => b.GridPosition == box.GridPosition + 1);
                            box.GridPosition++;
                            next.GridPosition--;
                            AddAllRoutingBoxes();
                        }
                        break;
                    case "MOVE_PREVIOUS":
                        if (box.GridPosition == 0) //déjà en premiète position
                        {

                        }
                        else
                        {
                            var previous = Boxes.FirstOrDefault(b => b.GridPosition == box.GridPosition - 1);
                            box.GridPosition--;
                            previous.GridPosition++;
                            AddAllRoutingBoxes();
                        }
                        break;
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
                    case "CHECK_OUT_CHANNEL":
                        int iChannel = (int)sValue;
                        SaveTemplate(); //pour obtenir une version propre de ce qui a été saisi et enregistré sur les box
                        if (Routing.CheckChannelUsage(box.RoutingGuid, iChannel, false))
                        {
                            Dispatcher.Invoke(() =>
                            {
                                //MessageBox.Show("Warning : The selected OUT Channel is already in use ! (" + iChannel.ToString() + ")");
                                box.cbChannelMidiOut.SelectedIndex += 1;
                            });
                        }
                        break;
                }
            }
        }

        private void Clock_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                this.Title = string.Concat("Midi Live System [", Routing.CyclesInfo, "]");

                //sauvegarde temporaire
                SaveTemplate();
            });


        }

        private void MidiRouting_NewLog(string sDevice, bool bIn, string sLog)
        {
            LogWindow.AddLog(sDevice, bIn, sLog);
        }

        private void btnSettings_Click(object sender, RoutedEventArgs e)
        {
            if (ConfigWindow != null && !ConfigWindow.IsFocused)
            {
                ConfigWindow.Focus();
            }
            else
            {   
                if (ConfigWindow != null)
                {
                    ConfigWindow.Closed -= MainConfiguration_Closed;
                }

                if (Project != null)
                {
                    ConfigWindow = new MidiConfiguration(Project, Boxes);
                }
                else
                {
                    ConfigWindow = new MidiConfiguration(Boxes);
                }

                ConfigWindow.Show();
                ConfigWindow.Closed += MainConfiguration_Closed;
            }
        }

        private void btnAddBox_Click(object sender, RoutedEventArgs e)
        {
            RoutingBox rtb = new RoutingBox(Project, MidiRouting.InputDevices, MidiRouting.OutputDevices, Boxes.Count);
            Boxes.Add(rtb);
            AddRoutingBoxToFrame(rtb, true);
        }

        private void btnSaveProject_Click(object sender, RoutedEventArgs e)
        {
            SaveTemplate();

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


            List<BoxPreset> allpresets = new List<BoxPreset>();
            //sauvegarde des box
            foreach (RoutingBox box in Boxes)
            {
                allpresets.AddRange(box.GetRoutingBoxMemory());
            }

            XmlSerializer serializerRouting = new XmlSerializer(typeof(RoutingBoxes));
            using (StringWriter textWriter = new StringWriter())
            {
                serializerRouting.Serialize(textWriter, new RoutingBoxes() { AllPresets = allpresets.ToArray() });
                sRoutingConfig = textWriter.ToString();
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
                        Project = project.Item2;

                        Boxes = project.Item3.GetBoxes(Project);

                        if (Boxes != null)
                        {
                            AddAllRoutingBoxes();
                        }
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

        private void btnLog_Click(object sender, RoutedEventArgs e)
        {
            if (btnLog.Background == Brushes.IndianRed) //log était inactif
            {
                if (LogWindow == null)
                {
                    LogWindow = new MidiLog();
                }
                LogWindow.Closed += Log_Closed;
                MidiRouting.NewLog += MidiRouting_NewLog;
                Routing.StartLog();
                btnLog.Background = Brushes.DarkGray;
                LogWindow.Show();
            }
            else
            {
                if (LogWindow != null)
                {
                    btnLog.Background = Brushes.IndianRed;
                    LogWindow.Closed -= Log_Closed;
                    LogWindow.Close();
                    LogWindow = null;
                    MidiRouting.NewLog -= MidiRouting_NewLog;
                    Routing.StopLog();
                }
            }
        }

        private void btnKeyboard_Click(object sender, RoutedEventArgs e)
        {
            if (KeysWindow == null)
            {
                KeysWindow = new Keyboard();
                Keyboard.KeyPressed += Keyboard_KeyPressed;
                KeysWindow.Show();
            }
            else
            {
                Keyboard.KeyPressed -= Keyboard_KeyPressed;
                KeysWindow.Close();
                KeysWindow = null;
            }
        }

        private void btnRecordSequence_Click(object sender, RoutedEventArgs e)
        {
            if (Boxes.Count == 0)
            {
                MessageBox.Show("Routing must be initialized");
            }
            else
            {
                if (btnRecordSequence.Background == Brushes.Red)
                {
                    btnRecordSequence.Background = Brushes.DarkGray;
                    SequenceRecorder.StopRecording(true, true);
                    MessageBox.Show(SequenceRecorder.GetSequenceInfo());
                }
                else
                {
                    btnRecordSequence.Background = Brushes.Red;

                    if (SequenceRecorder.Events.Count > 0)
                    {
                        var confirm = MessageBox.Show("Would you like to erase the last recording ?", "Erase ?", MessageBoxButton.YesNo);
                        if (confirm == MessageBoxResult.Yes)
                        {
                            SequenceRecorder.StopRecording(true, true);
                            SequenceRecorder.Clear();
                            SequenceRecorder.StartRecording(false, true);
                        }
                        else
                        {
                            btnRecordSequence.Background = Brushes.DarkGray;
                        }
                    }
                    else
                    {
                        SequenceRecorder.StartRecording(false, true);
                    }
                }
            }
        }

        private void btnPlaySequence_Click(object sender, RoutedEventArgs e)
        {
            if (SequenceRecorder.Events.Count > 0)
            {
                if (btnPlaySequence.Background == Brushes.Green)
                {
                    btnPlaySequence.Background = Brushes.DarkGray;
                    SequenceRecorder.StopSequence(); //risque très important de NOTE OFF pending
                }
                else
                {
                    btnPlaySequence.Background = Brushes.Green;
                    SequenceRecorder.PlaySequenceAsync(SequenceRecorder.Events);
                    SequenceRecorder.SequenceFinished += Routing_SequenceFinished;
                }
            }
            else
            {
                MessageBox.Show("Nothing to Play.");
            }
        }

        private void AddRoutingBoxToFrame(RoutingBox rtb, bool bCreate)
        {
            var frame = GridFrames.FirstOrDefault(g => g.Tag.ToString().Equals(""));

            if (frame == null)
            {
                MessageBox.Show("You can't add more Routing Box");
            }
            else
            {
                if (rtb.Detached)
                {
                    var detached = DetachedWindows.FirstOrDefault(d => d.RoutingBox.BoxGuid == rtb.BoxGuid);
                    if (detached != null)
                    {
                        detached.Focus();
                    }
                    else
                    {
                        DetachedBox db = new DetachedBox(rtb);
                        db.Show();
                        if (bCreate)
                        {
                            rtb.OnUIEvent += RoutingBox_UIEvent;
                        }
                        DetachedWindows.Add(db);
                        db.Closed += DetachedBox_Closed;
                    }
                }
                else
                {
                    frame.Tag = rtb.BoxGuid.ToString();
                    frame.Navigate(rtb);
                    if (bCreate)
                    {
                        rtb.OnUIEvent += RoutingBox_UIEvent;
                    }
                }
            }
        }

        private void AddAllRoutingBoxes()
        {
            if (Project != null)
            {
                RemoveAllBoxes(Project.HorizontalGrid, Project.VerticalGrid);
            }
            else
            {
                RemoveAllBoxes(DefaultHorizontal, DefaultVertical);
            }

            foreach (var box in Boxes.OrderBy(b => b.GridPosition))
            {
                AddRoutingBoxToFrame(box, false);
            }
        }

        private void RemoveAllBoxes(int iRows, int iCols)
        {
            if (Boxes != null)
            {
                foreach (var box in Boxes)
                {
                    var frame = GridFrames.FirstOrDefault(frame => frame.Tag.ToString().Equals(box.BoxGuid.ToString()));
                    if (frame != null)
                    {
                        frame.Content = null;
                    }
                }
            }

            InitFrames(iRows, iCols);
        }

        private void SaveTemplate()
        {
            foreach (RoutingBox box in Boxes)
            {
                box.Snapshot(); //enregistrement du preset en cours
                if (box.RoutingGuid == Guid.Empty)
                {
                    string sDevIn = box.cbMidiIn.SelectedItem != null ? ((ComboBoxItem)box.cbMidiIn.SelectedItem).Tag.ToString() : "";
                    string sDevOut = box.cbMidiOut.SelectedItem != null ? ((ComboBoxItem)box.cbMidiOut.SelectedItem).Tag.ToString() : "";

                    box.RoutingGuid = Routing.AddRouting(sDevIn, sDevOut,
                                       Convert.ToInt32(((ComboBoxItem)box.cbChannelMidiIn.SelectedItem).Tag.ToString()),
                                       Convert.ToInt32(((ComboBoxItem)box.cbChannelMidiOut.SelectedItem).Tag.ToString()),
                                       box.LoadOptions(), box.GetPreset());
                }
                else
                {
                    string sDevIn = box.cbMidiIn.SelectedItem != null ? ((ComboBoxItem)box.cbMidiIn.SelectedItem).Tag.ToString() : "";
                    string sDevOut = box.cbMidiOut.SelectedItem != null ? ((ComboBoxItem)box.cbMidiOut.SelectedItem).Tag.ToString() : "";

                    Routing.ModifyRouting(box.RoutingGuid, sDevIn, sDevOut,
                                           Convert.ToInt32(((ComboBoxItem)box.cbChannelMidiIn.SelectedItem).Tag.ToString()),
                                           Convert.ToInt32(((ComboBoxItem)box.cbChannelMidiOut.SelectedItem).Tag.ToString()),
                                           box.LoadOptions(), box.GetPreset());
                }
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

        internal List<RoutingBox> GetBoxes(ProjectConfiguration project)
        {
            IEnumerable<Guid> distinctValues = AllPresets.Select(arr => arr.BoxGuid).Distinct();
            List<RoutingBox> boxes = new List<RoutingBox>();

            foreach (var g in distinctValues)
            {
                var presetsample = AllPresets.FirstOrDefault(p => p.BoxGuid == g);
                if (presetsample != null)
                {
                    var box = new RoutingBox(project, MidiTools.MidiRouting.InputDevices, MidiTools.MidiRouting.OutputDevices, Convert.ToInt32(project.BoxNames[2]));
                    box.BoxGuid = g;
                    box.LoadMemory(AllPresets.Where(p => p.BoxGuid == g).ToArray());
                    boxes.Add(box);
                }
            }

            return boxes;
        }
    }
}
