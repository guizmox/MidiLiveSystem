using MidiTools;
using RtMidi.Core.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
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
        private System.Timers.Timer UIRefreshRate;
        private int RefreshCounter = 0;

        private int CurrentVerticalGrid = 0;
        private int CurrentHorizontalGrid = 0;

        private MidiConfiguration ConfigWindow;
        private MidiLog LogWindow;
        private Keyboard KeysWindow;
        private List<DetachedBox> DetachedWindows = new List<DetachedBox>();
        private bool ViewOnConfig = false;

        private MidiRouting Routing = new MidiRouting();
        private List<RoutingBox> Boxes = new List<RoutingBox>();
        private List<Frame> GridFrames = new List<Frame>();
        public static BoxPreset CopiedPreset = new BoxPreset();
        public ProjectConfiguration Project = new ProjectConfiguration();
        public SQLiteDatabaseManager Database = new SQLiteDatabaseManager();
        public MidiSequence RecordedSequence = new MidiSequence();

        public MainWindow()
        {
            InitializeComponent();
            InitFrames(Project.HorizontalGrid, Project.VerticalGrid);

            UIRefreshRate = new System.Timers.Timer();
            UIRefreshRate.Elapsed += UIRefreshRate_Elapsed;
            UIRefreshRate.Interval = 1000;
            UIRefreshRate.Start();

            RecordedSequence.SequenceFinished += Routing_SequenceFinished;

            //chargement des template instruments
            CubaseInstrumentData.Instruments = Database.LoadInstruments();

            Routing.Debug();
        }

        private void Keyboard_KeyPressed(string sKey)
        {
            Dispatcher.Invoke(() =>
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
            });
        }

        private void Routing_IncomingMidiMessage(MidiDevice.MidiEvent ev)
        {
            Dispatcher.Invoke(() =>
            {
                if (FocusManager.GetFocusedElement(this) is TextBox textBox)
                {
                    switch (ev.Type)
                    {
                        case MidiDevice.TypeEvent.NOTE_ON:
                            textBox.Text = ev.Values[0].ToString();
                            break;
                        case MidiDevice.TypeEvent.CC:
                            textBox.Text = ev.Values[1].ToString();
                            break;
                        case MidiDevice.TypeEvent.SYSEX:
                            textBox.Text = ev.SysExData;
                            break;
                    }
                }
            });
        }

        private void InitFrames(int iRows, int iCols)
        {
            GridFrames.Clear();
            gridBoxes.Children.Clear();
            gridBoxes.RowDefinitions.Clear();
            gridBoxes.ColumnDefinitions.Clear();

            if (iRows == 1 && iCols == 1) //option de maximisation
            {
                gridBoxes.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(100, GridUnitType.Star) });
                gridBoxes.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(100, GridUnitType.Star) });
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
            Database.SaveInstruments(CubaseInstrumentData.Instruments);
            //Database.SaveProject(Boxes, Project, RecordedSequence);

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
                if (config.VerticalGrid > -1)
                {
                    CurrentVerticalGrid = config.VerticalGrid;
                }
                if (config.HorizontalGrid > -1) 
                {
                    CurrentHorizontalGrid = config.HorizontalGrid;
                }

                //réinit de la fenêtre
                if (Project.VerticalGrid != CurrentVerticalGrid || Project.HorizontalGrid != CurrentHorizontalGrid)
                {
                     Project = config;
                     AddAllRoutingBoxes();
                }
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
            Routing.OpenUsedPorts();
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
                        InitFrames(Project.HorizontalGrid, Project.VerticalGrid);
                        AddAllRoutingBoxes();
                        break;
                    case "DETACH":
                        box.Detached = true;
                        AddAllRoutingBoxes();
                        break;
                    case "REMOVE":
                        Routing.DeleteRouting(box.RoutingGuid);
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
                    case "MUTE":
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
                    case "PLAY_NOTE":
                        if (box.RoutingGuid != Guid.Empty)
                        {
                            string sDevIn = box.cbMidiIn.SelectedItem != null ? ((ComboBoxItem)box.cbMidiIn.SelectedItem).Tag.ToString() : "";
                            string sDevOut = box.cbMidiOut.SelectedItem != null ? ((ComboBoxItem)box.cbMidiOut.SelectedItem).Tag.ToString() : "";

                            Routing.ModifyRouting(box.RoutingGuid, sDevIn, sDevOut,
                                                   Convert.ToInt32(((ComboBoxItem)box.cbChannelMidiIn.SelectedItem).Tag.ToString()),
                                                   Convert.ToInt32(((ComboBoxItem)box.cbChannelMidiOut.SelectedItem).Tag.ToString()),
                                                   box.GetOptions(), box.GetPreset());
                            Routing.SendNote(box.RoutingGuid, ((MidiOptions)sValue).PlayNote);
                        }
                        break;
                    case "PRESET_CHANGE":
                        if (box.RoutingGuid != Guid.Empty)
                        {
                            string sDevIn = box.cbMidiIn.SelectedItem != null ? ((ComboBoxItem)box.cbMidiIn.SelectedItem).Tag.ToString() : "";
                            string sDevOut = box.cbMidiOut.SelectedItem != null ? ((ComboBoxItem)box.cbMidiOut.SelectedItem).Tag.ToString() : "";

                            Routing.ModifyRouting(box.RoutingGuid, sDevIn, sDevOut,
                                                   Convert.ToInt32(((ComboBoxItem)box.cbChannelMidiIn.SelectedItem).Tag.ToString()),
                                                   Convert.ToInt32(((ComboBoxItem)box.cbChannelMidiOut.SelectedItem).Tag.ToString()),
                                                   box.GetOptions(), box.GetPreset());
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

        private void UIRefreshRate_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            RefreshCounter += 1;
            int i = Routing.AdjustUIRefreshRate(); //renvoit une quantité en ms

            Routing.IncomingMidiMessage -= Routing_IncomingMidiMessage;
            if (Routing.Events <= 2) //pour éviter de saturer les process avec des appels UI inutiles
            {
                Routing.IncomingMidiMessage += Routing_IncomingMidiMessage;
            }

            //if (LogWindow != null)
            //{
            //    LogWindow.AddLog("UI", true, Routing.Events.ToString());
            //}

            Dispatcher.Invoke(() =>
            {
                this.Title = string.Concat("Midi Live System [", Routing.CyclesInfo, " - UI Refresh Rate : ", i, " Sec.]");

                if (RefreshCounter >= i)
                {
                    //sauvegarde temporaire
                    SaveTemplate();
                    RefreshCounter = 0;
                }
            });


        }

        private void MidiRouting_NewLog(string sDevice, bool bIn, string sLog)
        {
            LogWindow.AddLog(sDevice, bIn, sLog);
        }

        private void btnSettings_Click(object sender, RoutedEventArgs e)
        {
            if (ConfigWindow != null)
            {        
                if (!ConfigWindow.IsActive)
                {
                    ConfigWindow.Closed -= MainConfiguration_Closed;
                    ConfigWindow = new MidiConfiguration(Project, Boxes);
                    ConfigWindow.Show();
                    ConfigWindow.Closed += MainConfiguration_Closed;
                }
                else if (!ConfigWindow.IsFocused)
                {
                    ConfigWindow.Focus();
                }
            }
            else
            {
                ConfigWindow = new MidiConfiguration(Project, Boxes);

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

            try
            {
                Database.SaveProject(Boxes, Project, RecordedSequence);
                Database.SaveInstruments(CubaseInstrumentData.Instruments);
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

                        RecordedSequence = project.Item4;

                        if (Boxes != null)
                        {
                            AddAllRoutingBoxes();
                            SaveTemplate();
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
                    RecordedSequence.StopRecording(true, true);
                    if (Project == null)
                    {
                        Project = new ProjectConfiguration();
                    }
                    Project.RecordedSequence = RecordedSequence;
                    MessageBox.Show(RecordedSequence.GetSequenceInfo());
                }
                else
                {
                    btnRecordSequence.Background = Brushes.Red;

                    if (RecordedSequence.Events.Count > 0)
                    {
                        var confirm = MessageBox.Show("Would you like to erase the last recording ?", "Erase ?", MessageBoxButton.YesNo);
                        if (confirm == MessageBoxResult.Yes)
                        {
                            RecordedSequence.StopRecording(true, true);
                            RecordedSequence.Clear();
                            RecordedSequence.StartRecording(false, true);
                        }
                        else
                        {
                            btnRecordSequence.Background = Brushes.DarkGray;
                        }
                    }
                    else
                    {
                        RecordedSequence.StartRecording(false, true);
                    }
                }
            }
        }

        private void btnPlaySequence_Click(object sender, RoutedEventArgs e)
        {
            if (RecordedSequence.Events.Count > 0)
            {
                if (btnPlaySequence.Background == Brushes.Green)
                {
                    btnPlaySequence.Background = Brushes.DarkGray;
                    RecordedSequence.StopSequence(); //risque très important de NOTE OFF pending
                }
                else
                {
                    btnPlaySequence.Background = Brushes.Green;
                    Routing.CloseUsedPorts();
                    RecordedSequence.PlaySequenceAsync(RecordedSequence.Events);
                }
            }
            else
            {
                MessageBox.Show("Nothing to Play.");
            }
        }

        private void btnSwitchView_Click(object sender, RoutedEventArgs e)
        {
            if (Boxes.Count > 0)
            {
                if (ViewOnConfig)
                {
                    foreach (var box in Boxes)
                    {
                        box.tabSwitch.SelectedIndex = 0;
                    }
                    ViewOnConfig = false;
                }
                else
                {
                    foreach (var box in Boxes)
                    {
                        box.tabSwitch.SelectedIndex = 1;
                    }
                    ViewOnConfig = true;
                }
            }
        }

        private void AddRoutingBoxToFrame(RoutingBox rtb, bool bCreate)
        {
            if (!GridFrames.Any(g => g.Tag.ToString().Equals("")))
            {
                if (CurrentVerticalGrid + CurrentHorizontalGrid >= 20)
                {
                    MessageBox.Show("You can't add more Routing Boxes.");
                }
                else
                {
                    if (CurrentHorizontalGrid >= CurrentVerticalGrid)
                    {
                        CurrentVerticalGrid += 1;
                        AddAllRoutingBoxes();
                    }
                    else
                    {
                        CurrentHorizontalGrid += 1;
                        AddAllRoutingBoxes();
                    }
                }
            }
            else
            {
                var frame = GridFrames.FirstOrDefault(g => g.Tag.ToString().Equals(""));

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
            RemoveAllBoxes(CurrentHorizontalGrid, CurrentVerticalGrid);

            foreach (var box in Boxes.OrderBy(b => b.GridPosition))
            {
                AddRoutingBoxToFrame(box, true);
            }
        }

        private void RemoveAllBoxes(int iRows, int iCols)
        {
            if (Boxes != null)
            {
                foreach (var box in Boxes)
                {
                    box.OnUIEvent -= RoutingBox_UIEvent;
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
                                       box.GetOptions(), box.GetPreset());
                    if (sDevOut.Length > 0)
                    {
                        var instr = CubaseInstrumentData.Instruments.FirstOrDefault(i => i.Device.Equals(sDevOut));
                        if (instr != null && instr.SysExInitializer.Length > 0)
                        {
                            Routing.SendSysEx(box.RoutingGuid, instr);
                        }
                    }
                }
                else
                {
                    string sDevIn = box.cbMidiIn.SelectedItem != null ? ((ComboBoxItem)box.cbMidiIn.SelectedItem).Tag.ToString() : "";
                    string sDevOut = box.cbMidiOut.SelectedItem != null ? ((ComboBoxItem)box.cbMidiOut.SelectedItem).Tag.ToString() : "";

                    Routing.ModifyRouting(box.RoutingGuid, sDevIn, sDevOut,
                                           Convert.ToInt32(((ComboBoxItem)box.cbChannelMidiIn.SelectedItem).Tag.ToString()),
                                           Convert.ToInt32(((ComboBoxItem)box.cbChannelMidiOut.SelectedItem).Tag.ToString()),
                                           box.GetOptions(), box.GetPreset());
                }
                Routing.SetClock(Project.ClockActivated, Project.BPM, Project.ClockDevice);
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

            int iGridPosition = -1;

            foreach (var g in distinctValues)
            {
                var presetsample = AllPresets.FirstOrDefault(p => p.BoxGuid == g);
                if (presetsample != null)
                {
                    try { iGridPosition = Convert.ToInt32(project.BoxNames.FirstOrDefault(b => b[1].Equals(g.ToString()))[2]); } catch { iGridPosition++; }
                    var box = new RoutingBox(project, MidiTools.MidiRouting.InputDevices, MidiTools.MidiRouting.OutputDevices, iGridPosition);
                    box.BoxGuid = g;
                    //box.RoutingGuid = presetsample.RoutingGuid;
                    box.LoadMemory(AllPresets.Where(p => p.BoxGuid == g).ToArray());
                    boxes.Add(box);
                }
            }

            return boxes;
        }
    }
}
