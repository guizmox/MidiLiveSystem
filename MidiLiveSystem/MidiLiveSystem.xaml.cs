using MaterialDesignThemes.Wpf;
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
        private static string APP_NAME = "Midi Live System";

        private delegate void UIEventHandler(string sMessage);
        private static event UIEventHandler NewMessage;

        private System.Timers.Timer UIRefreshRate;

        private int CurrentVerticalGrid = 4;
        private int CurrentHorizontalGrid = 4;

        private MidiConfiguration ConfigWindow;
        private MidiLog LogWindow;
        private Keyboard KeysWindow;
        private List<DetachedBox> DetachedWindows = new List<DetachedBox>();
        public ProgramHelp HelpWindow;
        public Conductor ConductorWindow;
        public RecallButtons RecallWindow;
        public InternalSequencer SequencerWindow;

        private SequencerData SeqData = new SequencerData();

        private bool ViewOnConfig = false;

        private MidiRouting Routing = new MidiRouting();
        private List<RoutingBox> Boxes = new List<RoutingBox>();
        private List<Frame> GridFrames = new List<Frame>();
        public static BoxPreset CopiedPreset = new BoxPreset();
        public ProjectConfiguration Project = new ProjectConfiguration();
        public SQLiteDatabaseManager Database = new SQLiteDatabaseManager();
        public MidiSequence RecordedSequence;

        public MainWindow()
        {
            InitializeComponent();
            InitFrames(CurrentHorizontalGrid, CurrentVerticalGrid);

            UIRefreshRate = new System.Timers.Timer();
            UIRefreshRate.Elapsed += UIRefreshRate_Elapsed;
            UIRefreshRate.Interval = 1000;
            UIRefreshRate.Start();
            
            MidiRouting.InputStaticMidiMessage += MidiRouting_InputMidiMessage;

            //chargement des template instruments
            CubaseInstrumentData.Instruments = Database.LoadInstruments();


            RecallWindow = new RecallButtons(Boxes, Project);
            RecallWindow.Closed += RecallWindow_Closed;

            Sequencer[] seq = new Sequencer[InternalSequencer.MaxSequences];
            for (int iSeq = 0; iSeq < InternalSequencer.MaxSequences; iSeq++)
            {
                seq[iSeq] = new Sequencer(iSeq + 1, "4", 32, 120, null, false);
            }

            SeqData.Sequencer = seq;

            SequencerWindow = new InternalSequencer(Project, Routing, SeqData);

            NewMessage += MainWindow_NewMessage;
        }

        private async void MainWindow_NewMessage(string sMessage)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                Title = string.Concat(APP_NAME, " [", sMessage,  " -" + EventPool.TasksRunning + " task(s) running]");
            });
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

        private async void MidiRouting_InputMidiMessage(MidiEvent ev)
        {
            if (RecallWindow != null && Project.TriggerRecallDevice.Equals(ev.Device))
            {
                if (ev.Type == MidiDevice.TypeEvent.NOTE_ON && Project.TriggerRecallButtons.Equals("NOTE") && ev.Values[0] >= Project.TriggerRecallButtonsValue && ev.Values[0] <= Project.TriggerRecallButtonsValue + 8)
                {
                    await RecallWindow.SetButton(true, Project.TriggerRecallButtonsValue, ev.Values[0]);
                }
                else if (ev.Type == MidiDevice.TypeEvent.NOTE_ON && Project.TriggerRecallButtons.Equals("CC") && ev.Values[1] < 8)
                {
                    await RecallWindow.SetButton(false, Project.TriggerRecallButtonsValue, ev.Values[1]);
                }
            }

            if (ev.Type == MidiDevice.TypeEvent.START)
            {
                await SequencerWindow.StartPlay(false);
            }
            else if (ev.Type == MidiDevice.TypeEvent.STOP)
            {
                await SequencerWindow.StopPlay(false);
            }
            else
            {
                if (Routing.Events <= 16)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        if (ev.Type == MidiDevice.TypeEvent.CC)
                        {
                            tbNoteName.Text = "CC#" + ev.Values[0];
                        }
                        else if (ev.Type == MidiDevice.TypeEvent.NOTE_ON)
                        {
                            tbNoteName.Text = Tools.MidiNoteNumberToNoteName(ev.Values[0]);
                        }

                        if (FocusManager.GetFocusedElement(this) is TextBox textBox)
                        {
                            if (textBox.Name.Equals("tbPresetName") || textBox.Name.Equals("tbProjectName"))
                            {

                            }
                            else
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
                        }
                    });
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

        private async void Window_Closed(object sender, EventArgs e)
        {
            NewMessage -= MainWindow_NewMessage;
            UIRefreshRate.Enabled = false;
            UIRefreshRate.Elapsed -= UIRefreshRate_Elapsed;
            UIRefreshRate.Stop();

            MidiRouting.InputStaticMidiMessage -= MidiRouting_InputMidiMessage;

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
            if (HelpWindow != null)
            {
                HelpWindow.Close();
            }
            if (ConductorWindow != null)
            {
                ConductorWindow.Close();
            }
            if (RecallWindow != null)
            {
                RecallWindow.Close();
            }
            if (SequencerWindow != null)
            {
                SequencerWindow.Close();
            }

            foreach (var detached in DetachedWindows)
            {
                detached.Close();
            }

            Database.SaveInstruments(CubaseInstrumentData.Instruments);
            await Routing.DeleteAllRouting();
        }

        private void RecallWindow_Closed(object sender, EventArgs e)
        {
            RecallWindow.SaveRecallsToProject();
        }

        private void Log_Closed(object sender, EventArgs e)
        {
            MidiRouting.NewLog -= MidiRouting_NewLog;
            Routing.StopLog();

            btnLog.Background = Brushes.IndianRed;
            LogWindow.Closed -= Log_Closed;
            LogWindow = null;
        }

        private async void MainConfiguration_Closed(object sender, EventArgs e)
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
                    await AddAllRoutingBoxes();
                }
                Project = config;
                await SaveTemplate();

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

        private async void DetachedBox_Closed(object sender, EventArgs e)
        {
            if (this.IsActive)
            {
                DetachedBox db = (DetachedBox)sender;
                var box = db.RoutingBox;
                box.Detached = false;
                db.Closed -= DetachedBox_Closed;
                DetachedWindows.Remove(db);

                await AddAllRoutingBoxes();
            }
        }

        private async void Routing_SequenceFinished(string sInfo)
        {
            Dispatcher.Invoke(() =>
            {
                tbPlay.Text = "PLAY";
                tbRecord.Text = "REC";
                btnPlaySequence.Background = Brushes.DarkGray;
                btnRecordSequence.Background = Brushes.DarkGray;
                UIRefreshRate.Start();
            });

            await Routing.OpenUsedPorts(false);
        }

        private void RecordedSequence_RecordCounter(string sInfo)
        {
            Dispatcher.Invoke(() =>
            {
                tbRecord.Text = sInfo;
            });
        }

        private void PlayedSequence_RecordCounter(string sInfo)
        {
            Dispatcher.Invoke(() =>
            {
                tbPlay.Text = sInfo;
            });
        }

        private async void RoutingBox_UIEvent(Guid gBox, string sControl, object sValue)
        {
            var box = Boxes.FirstOrDefault(b => b.BoxGuid == gBox);
            if (box != null)
            {
                switch (sControl)
                {
                    case "HELP":
                        if (HelpWindow != null)
                        { HelpWindow.Close(); }
                        HelpWindow = new ProgramHelp();
                        HelpWindow.Show();
                        break;
                    case "MAXIMIZE":
                        InitFrames(1, 1);
                        await AddRoutingBoxToFrame(box, false);
                        break;
                    case "MINIMIZE":
                        InitFrames(Project.HorizontalGrid, Project.VerticalGrid);
                        await AddAllRoutingBoxes();
                        break;
                    case "DETACH":
                        box.Detached = true;
                        await AddAllRoutingBoxes();
                        break;
                    case "REMOVE":
                        var confirmation = MessageBox.Show("Are you sure ?", "Delete Box", MessageBoxButton.YesNo);
                        if (confirmation == MessageBoxResult.Yes)
                        {
                            await Routing.DeleteRouting(box.RoutingGuid);
                            Boxes.Remove(box);
                            await AddAllRoutingBoxes();
                            await RecallWindow.UpdateBoxes();
                        }
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
                            await AddAllRoutingBoxes();
                            await RecallWindow.UpdateBoxes();
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
                            await AddAllRoutingBoxes();
                            await RecallWindow.UpdateBoxes();
                        }
                        break;
                    case "SOLO":
                        if (box.RoutingGuid != Guid.Empty)
                        {
                            if ((bool)sValue)
                            {
                                await Routing.SetSolo(box.RoutingGuid);

                                foreach (var b in Boxes.Where(b => !b.BoxGuid.Equals(box.BoxGuid)))
                                {
                                    b.Dispatcher.Invoke(() =>
                                    {
                                        b.SetMute(true, false);
                                    });
                                }
                            }
                            else
                            {
                                await Routing.UnmuteAllRouting();

                                foreach (var b in Boxes.Where(b => !b.BoxGuid.Equals(box.BoxGuid)))
                                {
                                    b.Dispatcher.Invoke(() =>
                                    {
                                        b.SetMute(false, Routing.GetActiveStatus(b.RoutingGuid));
                                    });
                                }
                            }
                        }
                        break;
                    case "MUTE":
                        if (box.RoutingGuid != Guid.Empty)
                        {
                            if ((bool)sValue)
                            {
                                await Routing.MuteRouting(box.RoutingGuid);
                            }
                            else
                            {
                                await Routing.UnmuteRouting(box.RoutingGuid);
                            }
                        }
                        break;

                    case "PLAY_NOTE":

                        BoxPreset bp1 = (BoxPreset)sValue;

                        string sDevIn1 = bp1.DeviceIn;
                        string sDevOut1 = bp1.DeviceOut;
                        int iChIn1 = bp1.ChannelIn;
                        int iChOut1 = bp1.ChannelOut;

                        if (bp1.RoutingGuid != Guid.Empty)
                        {
                            await Routing.ModifyRouting(bp1.RoutingGuid, sDevIn1, sDevOut1, iChIn1, iChOut1, bp1.MidiOptions, bp1.MidiPreset, sDevIn1.Equals(Tools.INTERNAL_SEQUENCER) && SeqData.Sequencer.Length >= iChIn1 - 1 ? SeqData.Sequencer[iChIn1 - 1] : null);
                            await Routing.SendNote(box.RoutingGuid, bp1.MidiOptions.PlayNote);
                        }
                        break;

                    case "PRESET_CHANGE":

                        BoxPreset bp2 = (BoxPreset)sValue;

                        string sDevIn2 = bp2.DeviceIn;
                        string sDevOut2 = bp2.DeviceOut;
                        int iChIn2 = bp2.ChannelIn;
                        int iChOut2 = bp2.ChannelOut;

                        if (bp2.RoutingGuid != Guid.Empty)
                        {
                            await Routing.ModifyRouting(bp2.RoutingGuid, sDevIn2, sDevOut2, iChIn2, iChOut2, bp2.MidiOptions, bp2.MidiPreset, sDevIn2.Equals(Tools.INTERNAL_SEQUENCER) && SeqData.Sequencer.Length >= iChIn2 - 1 ? SeqData.Sequencer[iChIn2 - 1] : null);
                        }
                        break;

                    case "COPY_PRESET":
                        CopiedPreset = (BoxPreset)sValue;
                        break;

                    case "CHECK_OUT_CHANNEL":
                        string sDevice = ((string)sValue).Split("#|#")[0];
                        int iChannelWanted = Convert.ToInt32(((string)sValue).Split("#|#")[1]);

                        int iAvailable = Routing.GetFreeChannelForDevice(sDevice, iChannelWanted, false);
                        if (iAvailable > 0)
                        {
                            box.cbChannelMidiOut.SelectedValue = iAvailable;
                        }
                        else
                        {
                            MessageBox.Show("No more Channel available for this OUT device.");
                        }

                        //SaveTemplate(); //pour obtenir une version propre de ce qui a été saisi et enregistré sur les box
                        break;
                }
            }
        }

        private async void UIRefreshRate_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            await tbNoteName.Dispatcher.InvokeAsync(() =>
            {
                tbNoteName.Text = "";
            });

            if (Routing.Events <= 16) //pour éviter de saturer les process avec des appels UI inutiles
            {
                await SaveTemplate();
            }

            await Dispatcher.InvokeAsync(() =>
            {
                if (Routing.Events <= 16) //pour éviter de saturer les process avec des appels UI inutiles
                {
                    Title = string.Concat(APP_NAME + " [", Routing.CyclesInfo, " - UI Refresh Rate : ", UIRefreshRate.Interval / 1000, " Sec  - " + EventPool.TasksRunning + " task(s) running]");
                }
                else
                {
                    Title = string.Concat(APP_NAME + " [", Routing.CyclesInfo, " - UI events disabled - " + EventPool.TasksRunning + " task(s) running]");
                }
            });
        }

        private async void MidiRouting_NewLog(string sDevice, bool bIn, string sLog)
        {
            await LogWindow.AddLog(sDevice, bIn, sLog);
        }

        private void btnSettings_Click(object sender, RoutedEventArgs e)
        {
            if (ConfigWindow != null)
            {
                if (!ConfigWindow.IsActive)
                {
                    ConfigWindow.Closed -= MainConfiguration_Closed;
                    ConfigWindow = new MidiConfiguration(Project, Boxes, Routing);
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
                ConfigWindow = new MidiConfiguration(Project, Boxes, Routing);

                ConfigWindow.Show();
                ConfigWindow.Closed += MainConfiguration_Closed;
            }
        }

        private void btnSequencer_Click(object sender, RoutedEventArgs e)
        {
            if (SequencerWindow.IsVisible)
            {
                SequencerWindow.Close();
            }
            else
            {
                SequencerWindow.Close();
                SequencerWindow = new InternalSequencer(Project, Routing, SeqData);
                SequencerWindow.Show();
            }
        }

        private async void btnAddBox_Click(object sender, RoutedEventArgs e)
        {
            RoutingBox rtb = new RoutingBox(Project, MidiRouting.InputDevices, MidiRouting.OutputDevices, Boxes.Count);
            Boxes.Add(rtb);
            await AddRoutingBoxToFrame(rtb, true);
            await RecallWindow.UpdateBoxes();

        }

        private void btnConductor_Click(object sender, RoutedEventArgs e)
        {
            if (Boxes.Count > 0)
            {
                if (ConductorWindow != null)
                {
                    if (!ConductorWindow.IsActive)
                    {
                        ConductorWindow = new Conductor(Boxes, Routing);
                        ConductorWindow.Show();
                    }
                    else if (!ConductorWindow.IsFocused)
                    {
                        ConductorWindow.Focus();
                    }
                }
                else
                {
                    ConductorWindow = new Conductor(Boxes, Routing);
                    ConductorWindow.Show();
                }
            }
            else
            {
                MessageBox.Show("You need to add at least one Routing Box");
            }
        }

        private async void btnSaveProject_Click(object sender, RoutedEventArgs e)
        {
            if (Boxes.Count > 0)
            {
                await SaveTemplate();
                if (RecallWindow != null) { RecallWindow.SaveRecallsToProject(); }

                try
                {
                    Database.SaveProject(Boxes, Project, RecordedSequence, SeqData);
                    Database.SaveInstruments(CubaseInstrumentData.Instruments);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Unable to Save Project : " + ex.Message);
                }
            }
            else { MessageBox.Show("Nothing to Save."); }
        }

        private async void btnOpenProject_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (RecallWindow.IsVisible) //pour forcer le rafraichissement suite au rechargement de la config
                {
                    RecallWindow.Close();
                }

                SequencerWindow.Close();

                //Id, ProjectGuid, Name, DateProject, Author, Active
                List<string[]> projects = Database.GetProjects();
                if (projects.Count > 0)
                {
                    Projects prj = new Projects(Database, projects);
                    prj.ShowDialog();
                    var project = prj.Project;
                    if (project != null)
                    {
                        UIRefreshRate.Enabled = false;

                        await Routing.DeleteAllRouting();

                        Project = project.Item2;
                        NewMessage?.Invoke("Project Loaded");

                        Boxes = project.Item3.GetBoxes(Project, Routing);
                        NewMessage?.Invoke("Routing Boxes Loaded");

                        if (project.Item4 != null)
                        {
                            RecordedSequence = project.Item4;
                            NewMessage?.Invoke("Recording Loaded");
                        }

                        if (project.Item5 != null)
                        {
                            SeqData = project.Item5;
                            SequencerWindow = new InternalSequencer(Project, Routing, SeqData);
                            NewMessage?.Invoke("Sequencer Loaded");
                        }

                        if (Boxes != null)
                        {
                            if (Project.VerticalGrid > -1)
                            {
                                CurrentVerticalGrid = Project.VerticalGrid;
                            }
                            if (Project.HorizontalGrid > -1)
                            {
                                CurrentHorizontalGrid = Project.HorizontalGrid;
                            }

                            await AddAllRoutingBoxes();
                            NewMessage?.Invoke("Routing Boxes Added");

                            for (int iB = 0; iB < Boxes.Count; iB++)
                            {
                                try
                                {
                                    NewMessage?.Invoke("Initializing Routing Box (" + (iB + 1) + "/" + Boxes.Count + ") : " + Boxes[iB].BoxName);
                                    await ProcessBoxData(Boxes[iB], true);   
                                }
                                catch (Exception ex)
                                {
                                    NewMessage?.Invoke("Routing Box Initialization failed : " + Boxes[iB].BoxName + " (" + ex.Message + ")");
                                }
                            }
                        }

                        UIRefreshRate.Enabled = true;

                        Routing.ReactivateTimers();
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
                MessageBox.Show("Unable to Load Project : " + ex.Message);
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

        private void btnRecallButtons_Click(object sender, RoutedEventArgs e)
        {
            if (RecallWindow.IsVisible)
            {
                RecallWindow.Closed -= RecallWindow_Closed;
                RecallWindow.Close();
            }
            else
            {
                RecallWindow.Close();
                RecallWindow.Closed -= RecallWindow_Closed;
                RecallWindow = new RecallButtons(Boxes, Project);
                RecallWindow.Show();
                RecallWindow.Closed += RecallWindow_Closed;
            }
        }

        private void btnRecordSequence_Click(object sender, RoutedEventArgs e)
        {
            if (RecordedSequence == null)
            {
                RecordedSequence = new MidiSequence();
            }

            RecordedSequence.RecordCounter -= RecordedSequence_RecordCounter;
            RecordedSequence.SequenceFinished -= Routing_SequenceFinished;

            tbRecord.Text = "REC";

            bool bRecord = false;

            if (Boxes.Count == 0)
            {
                MessageBox.Show("Routing must be initialized");
            }
            else
            {
                if (btnRecordSequence.Background == Brushes.Red)
                {
                    btnRecordSequence.Background = Brushes.DarkGray;
                    RecordedSequence.StopRecording(false, true);

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

                    if (RecordedSequence.EventsOUT.Count > 0)
                    {
                        var confirm = MessageBox.Show("Would you like to erase the last recording ?", "Erase ?", MessageBoxButton.YesNo);
                        if (confirm == MessageBoxResult.Yes)
                        {
                            RecordedSequence.StopRecording(false, true);
                            RecordedSequence.Clear();

                            bRecord = true;
                        }
                        else
                        {
                            btnRecordSequence.Background = Brushes.DarkGray;
                        }
                    }
                    else
                    {
                        bRecord = true;
                    }
                }
            }

            if (bRecord)
            {
                RecordedSequence = new MidiSequence();

                tbRecord.Text = "GO !";
                RecordedSequence.RecordCounter += RecordedSequence_RecordCounter;
                RecordedSequence.SequenceFinished += Routing_SequenceFinished;
                RecordedSequence.StartRecording(true, true, Routing);
            }
        }

        private void btnPlaySequence_Click(object sender, RoutedEventArgs e)
        {
            if (RecordedSequence != null)
            {
                tbPlay.Text = "PLAY";
                RecordedSequence.SequenceFinished -= Routing_SequenceFinished;
                RecordedSequence.RecordCounter += PlayedSequence_RecordCounter;
                RecordedSequence.SequenceFinished += Routing_SequenceFinished;

                if (RecordedSequence.EventsOUT.Count > 0)
                {
                    if (btnPlaySequence.Background == Brushes.Green)
                    {
                        btnPlaySequence.Background = Brushes.DarkGray;
                        RecordedSequence.StopSequence(); //risque très important de NOTE OFF pending
                    }
                    else
                    {
                        btnPlaySequence.Background = Brushes.Green;

                        RecordedSequence.RecordCounter += PlayedSequence_RecordCounter;
                        UIRefreshRate.Stop(); //blocage de tout ce qui va potentiellement aller modifier le routing
                        RecordedSequence.PlaySequenceAsync(Routing);
                    }
                }
                else
                {
                    MessageBox.Show("Nothing to Play.");
                }
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

        private async Task AddAllRoutingBoxes()
        {
            RemoveAllBoxes(CurrentHorizontalGrid, CurrentVerticalGrid);

            List<Task> tasks = new List<Task>();

            foreach (var box in Boxes.OrderBy(b => b.GridPosition))
            {
                tasks.Add(EventPool.AddTask(async () => await AddRoutingBoxToFrame(box, true)));
            }

            await Task.WhenAll(tasks);

            await RecallWindow.UpdateBoxes();
        }

        private async Task AddRoutingBoxToFrame(RoutingBox rtb, bool bCreate)
        {
            bool bFrame = false;

            await Dispatcher.InvokeAsync(() =>
            {
                bFrame = GridFrames.Any(g => g.Tag.ToString().Equals(""));
            });

            if (!bFrame)
            {
                if (Boxes.Count >= 20)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show("You can't add more Routing Boxes.");
                    });
                }
                else
                {
                    if (CurrentHorizontalGrid + 1 >= CurrentVerticalGrid)
                    {
                        CurrentVerticalGrid += 1;
                        await AddAllRoutingBoxes();
                    }
                    else
                    {
                        CurrentHorizontalGrid += 1;
                        await AddAllRoutingBoxes();
                    }
                }
            }
            else
            {
                await Dispatcher.InvokeAsync(() =>
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
                });
            }
        }

        private async Task SaveTemplate()
        {
            List<Task> tasks = new List<Task>();

            for (int i = 0; i < Boxes.Count; i++)
            {
                int index = i;
                tasks.Add(EventPool.AddTask(async () => await ProcessBoxData(Boxes[index], false)));
            }

            await Task.WhenAll(tasks);

            await Routing.SetClock(Project.ClockActivated, Project.BPM, Project.ClockDevice);
        }

        private async Task ProcessBoxData(RoutingBox box, bool bFromSave)
        {
            string sDevIn = "";
            string sDevOut = "";
            int iChIn = 0;
            int iChOut = 0;
            MidiOptions options = null;
            MidiPreset preset = null;

            var snapshot = await box.Snapshot(); //enregistrement du preset en cours

            sDevIn = snapshot.DeviceIn;
            sDevOut = snapshot.DeviceOut;
            iChIn = snapshot.ChannelIn;
            iChOut = snapshot.ChannelOut;
            options = snapshot.MidiOptions;
            preset = snapshot.MidiPreset;

            if (box.RoutingGuid == Guid.Empty || bFromSave)
            {
                box.RoutingGuid = await Routing.AddRouting(sDevIn, sDevOut, iChIn, iChOut, options, preset, sDevIn.Equals(Tools.INTERNAL_SEQUENCER) && SeqData.Sequencer.Length >= iChIn - 1 ? SeqData.Sequencer[iChIn - 1] : null);
                var devices = await box.GetAllDevices();
                await Routing.UpdateUsedDevices(devices);
            }
            else
            {
                await Routing.ModifyRouting(snapshot.RoutingGuid, sDevIn, sDevOut, iChIn, iChOut, options, preset, sDevIn.Equals(Tools.INTERNAL_SEQUENCER) && SeqData.Sequencer.Length >= iChIn - 1 ? SeqData.Sequencer[iChIn - 1] : null);
                var devices = await box.GetAllDevices();
                await Routing.UpdateUsedDevices(devices);
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

        internal List<RoutingBox> GetBoxes(ProjectConfiguration project, MidiRouting routing)
        {
            IEnumerable<Guid> distinctValues = AllPresets.Select(arr => arr.BoxGuid).Distinct();
            List<RoutingBox> boxes = new List<RoutingBox>();

            int iGridPosition = -1;

            foreach (var g in distinctValues)
            {
                var presetsample = AllPresets.FirstOrDefault(p => p.BoxGuid == g);
                if (presetsample != null)
                {
                    var boxname = project.BoxNames.FirstOrDefault(b => b[1].Equals(g.ToString()));
                    if (boxname != null)
                    {
                        iGridPosition = Convert.ToInt32(boxname[2]);
                    }
                    else { iGridPosition++; }

                    var box = new RoutingBox(project, MidiTools.MidiRouting.InputDevices, MidiTools.MidiRouting.OutputDevices, iGridPosition);
                    box.BoxGuid = g;
                    box.BoxName = presetsample.BoxName;
                    box.RoutingGuid = presetsample.RoutingGuid;
                    box.LoadMemory(AllPresets.Where(p => p.BoxGuid == g).ToArray());
                    boxes.Add(box);
                }
            }
            //inscrire le nom des presets en force
            for (int i = 0; i < AllPresets.Count(); i += 8) //toujours 8 par box
            {
                var box = boxes.FirstOrDefault(b => b.BoxGuid == AllPresets[i].BoxGuid);
                box.btnPreset1.Content = AllPresets[i].PresetName;
                box.btnPreset2.Content = AllPresets[i + 1].PresetName;
                box.btnPreset3.Content = AllPresets[i + 2].PresetName;
                box.btnPreset4.Content = AllPresets[i + 3].PresetName;
                box.btnPreset5.Content = AllPresets[i + 4].PresetName;
                box.btnPreset6.Content = AllPresets[i + 5].PresetName;
                box.btnPreset7.Content = AllPresets[i + 6].PresetName;
                box.btnPreset8.Content = AllPresets[i + 7].PresetName;
            }

            return boxes;
        }
    }
}
