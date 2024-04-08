using MessagePack;
using MidiTools;
using RtMidi.Core.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using VSTHost;

namespace MidiLiveSystem
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static string APP_NAME = "Midi Live System";

        internal delegate void CCMixEventHandler(Guid RoutingGuid, int[] sValues);
        internal static event CCMixEventHandler CCMixData;

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
        public CCMixer ControlChangeMixer;

        private SequencerData SeqData = new SequencerData();

        private bool ViewOnConfig = false;

        private MidiRouting Routing = new MidiRouting();
        private List<RoutingBox> Boxes = new List<RoutingBox>();
        private List<Frame> GridFrames = new List<Frame>();
        public static BoxPreset CopiedPreset = new BoxPreset();
        public ProjectConfiguration Project = new ProjectConfiguration();
        public SQLiteDatabaseManager Database;
        public MidiSequence RecordedSequence;

        public MainWindow()
        {
            InitializeComponent();

            Task.Run(() => InitFrames(CurrentHorizontalGrid, CurrentVerticalGrid));

            UIRefreshRate = new System.Timers.Timer();
            UIRefreshRate.Elapsed += UIRefreshRate_Elapsed;
            UIRefreshRate.Interval = 1000;
            UIRefreshRate.Start();

            if (!File.Exists(SQLiteDatabaseManager.Database))
            {
                MessageBox.Show("This is the first start. " + Environment.NewLine + "Add your first Routing Box. If you need some help, there's a menu on the top-left of each Routing Box that opens a menu in which you can get help regarding the features." + Environment.NewLine + Environment.NewLine + "Enjoy !");
            }

            try
            {
                Database = new SQLiteDatabaseManager();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                Close();
            }

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
                Title = string.Concat(APP_NAME, " [", sMessage, "]");
            });
        }

        private async void MidiRouting_InputMidiMessage(MidiEvent ev)
        {
            if (Project.TriggerRecallDevice.Equals(ev.Device))
            {
                if (ev.Type == MidiDevice.TypeEvent.NOTE_ON && Project.TriggerRecallButtons.Equals("NOTE") && ev.Values[0] >= Project.TriggerRecallButtonsValue && ev.Values[0] <= Project.TriggerRecallButtonsValue + 8)
                {
                    if (RecallWindow == null)
                    {
                        await Dispatcher.InvokeAsync(() =>
                        {
                            RecallWindow = new RecallButtons(Boxes, Project);
                            RecallWindow.Closed += RecallWindow_Closed;
                        });
                    }
                    await RecallWindow.SetButton(true, Project.TriggerRecallButtonsValue, ev.Values[0]);
                }
                else if (ev.Type == MidiDevice.TypeEvent.NOTE_ON && Project.TriggerRecallButtons.Equals("CC") && ev.Values[1] < 8)
                {
                    if (RecallWindow == null)
                    {
                        await Dispatcher.InvokeAsync(() =>
                        {
                            RecallWindow = new RecallButtons(Boxes, Project);
                            RecallWindow.Closed += RecallWindow_Closed;
                        });
                    }
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

        private async void Window_Closed(object sender, EventArgs e)
        {
            MidiRouting.InputStaticMidiMessage -= MidiRouting_InputMidiMessage;
            NewMessage -= MainWindow_NewMessage;
            UIRefreshRate.Elapsed -= UIRefreshRate_Elapsed;
            UIRefreshRate.Stop();
            UIRefreshRate.Enabled = false;

            while (UIEventPool.TasksRunning > 0)
            {
                await Task.Delay(1000);
            }

            if (SequencerWindow != null)
            {
                await SequencerWindow.StopPlay(false);
                SequencerWindow.Close();
            }
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
            if (ControlChangeMixer != null)
            {
                ControlChangeMixer.Close();
            }

            foreach (var detached in DetachedWindows)
            {
                detached.Close();
            }

            foreach (var box in Boxes)
            {
                box.CloseVSTWindow();
            }

            await Database.SaveInstruments(CubaseInstrumentData.Instruments);

            await Routing.DeleteAllRouting();
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

                await Routing.ChangeAllInputsMidiIn(Project.AllInputs);

                await SaveTemplate();

                //rename des box
                if (config.BoxNames != null)
                {
                    foreach (string[] boxname in config.BoxNames)
                    {
                        var b = Boxes.FirstOrDefault(b => b.BoxGuid.ToString().Equals(boxname[1]));
                        if (b != null)
                        {
                            b.SetBoxName(boxname[0]);
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

        private void ControlChangeMixer_Closed(object sender, EventArgs e)
        {
            ControlChangeMixer.OnUIEvent -= RoutingBox_UIEvent;
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

        private async void RecordedSequence_RecordCounter(string sInfo)
        {
            await tbRecord.Dispatcher.InvokeAsync(() =>
            {
                tbRecord.Text = sInfo;
            });
        }

        private async void PlayedSequence_RecordCounter(string sInfo)
        {
            await tbPlay.Dispatcher.InvokeAsync(() =>
            {
                tbPlay.Text = sInfo;
            });
        }

        private async void RoutingBox_UIEvent(Guid gBox, string sControl, object sValue)
        {
            lock (UIRefreshRate)
            {
                UIRefreshRate.Enabled = false;
            }

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
                        await InitFrames(1, 1);
                        await AddRoutingBoxToFrame(box, false);
                        break;
                    case "MINIMIZE":
                        await InitFrames(Project.HorizontalGrid, Project.VerticalGrid);
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
                            bool bDontRemovePlugin = CheckVSTUsage(box.BoxGuid, box.GetVST());

                            await box.CloseVSTHost(bDontRemovePlugin);
                            await Routing.DeleteRouting(box.RoutingGuid);
                            Boxes.Remove(box);
                            await AddAllRoutingBoxes();
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
                        }
                        break;
                    case "OPEN_CC_MIX":
                        if (box.RoutingGuid != Guid.Empty)
                        {
                            if (ControlChangeMixer != null)
                            {
                                ControlChangeMixer.Close();
                                ControlChangeMixer.OnUIEvent -= RoutingBox_UIEvent;
                                ControlChangeMixer.Closed -= ControlChangeMixer_Closed;
                            }

                            var options = await box.GetOptions();
                            ControlChangeMixer = new CCMixer(box.BoxGuid, box.RoutingGuid, box.BoxName, box.GetCurrentPreset().PresetName, options.CCMixDefaultParameters);
                            ControlChangeMixer.OnUIEvent += ControlChange_UIEvent;
                            ControlChangeMixer.Closed += ControlChangeMixer_Closed;
                            ControlChangeMixer.Show();
                            await ControlChangeMixer.InitMixer();
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

                    case "INITIALIZE_AUDIO":
                        bool bOK = await Routing.InitializeAudio((VSTPlugin)sValue);
                        if (bOK) { await ProcessBoxData(box, false); }
                        break;

                    case "CHECK_VST_HOST":
                        VSTPlugin vst = await Routing.CheckVSTSlot((string)sValue);
                        await box.SetVST(vst, Convert.ToInt32(box.cbVSTSlot.SelectedValue.ToString()), Convert.ToInt32(box.cbChannelMidiOut.SelectedValue.ToString()));
                        break;

                    case "REMOVE_VST":
                        bool bRemoved = await Routing.RemoveVST((string)sValue);
                        foreach (var b in Boxes)
                        {
                            await b.CheckAndRemoveVST((string)sValue);
                        }
                        await box.SetVST(null, Convert.ToInt32(box.cbVSTSlot.SelectedValue.ToString()), Convert.ToInt32(box.cbChannelMidiOut.SelectedValue.ToString()));
                        break;

                    case "PLAY_NOTE":

                        BoxPreset bp1 = (BoxPreset)sValue;

                        string sDevIn1 = bp1.DeviceIn;
                        string sDevOut1 = bp1.DeviceOut;
                        int iChIn1 = bp1.ChannelIn;
                        int iChOut1 = bp1.ChannelOut;

                        if (bp1.RoutingGuid != Guid.Empty)
                        {
                            await Routing.ModifyRouting(bp1.RoutingGuid, sDevIn1, sDevOut1, box.GetVST(), iChIn1, iChOut1, bp1.MidiOptions, bp1.MidiPreset, sDevIn1.Equals(Tools.INTERNAL_SEQUENCER) && iChIn1 > 0 && SeqData.Sequencer.Length >= iChIn1 - 1 ? SeqData.Sequencer[iChIn1 - 1] : null);
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
                            await Routing.ModifyRouting(bp2.RoutingGuid, sDevIn2, sDevOut2, box.GetVST(), iChIn2, iChOut2, bp2.MidiOptions, bp2.MidiPreset, sDevIn2.Equals(Tools.INTERNAL_SEQUENCER) && iChIn2 > 0 && SeqData.Sequencer.Length >= iChIn2 - 1 ? SeqData.Sequencer[iChIn2 - 1] : null);
                        }

                        if (ConductorWindow != null) //rafraichir le conducteur selon le preset qui a changé
                        {
                            await ConductorWindow.InitStage();
                        }

                        if (ControlChangeMixer != null)
                        {
                            await ControlChangeMixer.InitPage(box.BoxName, box.GetCurrentPreset().PresetName);
                            MidiOptions presetopt = await box.GetOptions();
                            await ControlChangeMixer.InitMixer();
                        }

                        break;

                    case "SEND_CC_DATA":
                        int[] cc = (int[])sValue;
                        await Routing.SendCC(box.RoutingGuid, cc[0], cc[1]);
                        break;

                    case "COPY_PRESET":
                        CopiedPreset = (BoxPreset)sValue;
                        break;

                    case "CHECK_OUT_CHANNEL":
                        string sDevice = ((string)sValue).Split("#|#")[0];
                        int iChannelWanted = Convert.ToInt32(((string)sValue).Split("#|#")[1]);

                        //int iAvailable = Routing.GetFreeChannelForDevice(sDevice, iChannelWanted, false);
                        int iAvailable = GetFreeChannelForDevice(box.BoxGuid, sDevice, iChannelWanted);
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

            lock (UIRefreshRate)
            {
                UIRefreshRate.Enabled = true;
            }
        }

        private async void ControlChange_UIEvent(Guid gBox, string sControl, object sValue)
        {
            var box = Boxes.FirstOrDefault(b => b.BoxGuid == gBox);
            if (box != null)
            {
                switch (sControl)
                {
                    case "CC_MIX_DATA":
                        if (box.RoutingGuid != Guid.Empty)
                        {
                            MidiOptions presetopt = await box.GetOptions();
                            CCMixData?.Invoke(box.RoutingGuid, presetopt.DefaultRoutingCC);
                        }
                        break;
                    case "CC_SEND_MIX_DATA":
                        if (box.RoutingGuid != Guid.Empty)
                        {
                            int[] iCC = (int[])sValue;
                            if (iCC[1] > -1) //on n'envoie rien si on fait un INIT à -1
                            {
                                await Routing.SendCC(box.RoutingGuid, iCC[0], iCC[1]);
                            }
                            box.SetInitCC(iCC);
                        }
                        break;
                    case "CC_SAVE_MIX_DEFAULT":
                        if (box.RoutingGuid != Guid.Empty)
                        {
                            await box.InitDefaultCCMixer((int[])sValue);
                        }
                        break;
                }
            }
        }

        private async void UIRefreshRate_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            await tbNoteName.Dispatcher.InvokeAsync(() => tbNoteName.Text = "");

            if (Routing.Events <= 32) //pour éviter de saturer les process avec des appels UI inutiles
            {
                await Dispatcher.InvokeAsync(() => Title = string.Concat(APP_NAME + " [", Routing.CyclesInfo, "]"));
            }
            else
            {
                await Dispatcher.InvokeAsync(() => Title = string.Concat(APP_NAME + " [", Routing.CyclesInfo, ". UI events disabled]"));
            }

            if (Routing.Events <= 32) //pour éviter de saturer les process avec des appels UI inutiles
            {
                await SaveTemplate();
            }
        }

        private async void MidiRouting_NewLog(string sDevice, bool bIn, string sLog)
        {
            await LogWindow.AddLog(sDevice, bIn, sLog);
        }

        private async void btnAddBox_Click(object sender, RoutedEventArgs e)
        {
            RoutingBox rtb = new RoutingBox(Project, MidiRouting.InputDevices, MidiRouting.OutputDevices, Boxes.Count);
            Boxes.Add(rtb);
            await AddRoutingBoxToFrame(rtb, true);

        }

        private async void btnSaveProject_Click(object sender, RoutedEventArgs e)
        {
            if (Boxes.Count > 0)
            {
                await btnSaveProject.Dispatcher.InvokeAsync(() => tbSaveProject.Text = "Saving...");

                await SaveTemplate();
                await Routing.SaveVSTParameters();

                if (RecallWindow != null) { RecallWindow.SaveRecallsToProject(); }

                try
                {
                    await Database.SaveProjectV2(Boxes, Project, RecordedSequence, SeqData);
                    await Database.SaveInstruments(CubaseInstrumentData.Instruments);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Unable to Save Project : " + ex.Message);
                }

                await btnSaveProject.Dispatcher.InvokeAsync(() => tbSaveProject.Text = "Save Project");
            }
            else { MessageBox.Show("Nothing to Save."); }
        }

        private async void btnOpenProject_Click(object sender, RoutedEventArgs e)
        {
            UIRefreshRate.Enabled = false;

            await Dispatcher.InvokeAsync(() => IsEnabled = false);

            try
            {
                if (RecallWindow != null) //pour forcer le rafraichissement suite au rechargement de la config
                {
                    RecallWindow.Close();
                    RecallWindow = null;
                }

                if (LogWindow != null && LogWindow.IsVisible)
                {
                    LogWindow.Close();
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
                        foreach (var box in Boxes)
                        {
                            if (box.VSTWindow != null)
                            { box.CloseVSTWindow(); }
                        }

                        await Routing.DeleteAllRouting();

                        Project = project.Item2;
                        NewMessage?.Invoke("Project Loaded");


                        await Routing.ChangeAllInputsMidiIn(Project.AllInputs);

                        //ouverture des ports techniques
                        if (Project.ClockDevice.Length > 0)
                        {
                            MidiRouting.CheckAndOpenINPort(Project.ClockDevice);
                        }
                        if (Project.TriggerRecallDevice.Length > 0)
                        {
                            MidiRouting.CheckAndOpenINPort(Project.TriggerRecallDevice);
                        }
                        NewMessage?.Invoke("IN Ports Opened");

                        await SearchBoxesForAudioInitialization(project.Item3); //initialisation de l'audio si besoin (usage de VST)

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

                            await UpdateDevicesUsage();

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

            await Dispatcher.InvokeAsync(() => IsEnabled = true);

            UIRefreshRate.Enabled = true;
        }

        private async void btnSwitchView_Click(object sender, RoutedEventArgs e)
        {
            if (Boxes.Count > 0)
            {
                if (ViewOnConfig)
                {
                    foreach (var box in Boxes)
                    {
                        await box.Dispatcher.InvokeAsync(() =>
                        {
                            box.tabSwitch.SelectedIndex = 0;
                        });
                    }
                    ViewOnConfig = false;
                }
                else
                {
                    foreach (var box in Boxes)
                    {
                        await box.Dispatcher.InvokeAsync(() =>
                        {
                            box.tabSwitch.SelectedIndex = 1;
                        });
                    }
                    ViewOnConfig = true;
                }
            }
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
            if (RecallWindow != null && RecallWindow.IsVisible)
            {
                RecallWindow.Closed -= RecallWindow_Closed;
                RecallWindow.Close();
            }
            else
            {
                if (RecallWindow != null)
                {
                    RecallWindow.Close();
                    RecallWindow.Closed -= RecallWindow_Closed;
                }
                RecallWindow = new RecallButtons(Boxes, Project);
                RecallWindow.Show();
                RecallWindow.Closed += RecallWindow_Closed;
            }
        }

        private async void btnRecordSequence_Click(object sender, RoutedEventArgs e)
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
                await tbRecord.Dispatcher.InvokeAsync(() => tbRecord.Text = "LOADING...");

                RecordedSequence = new MidiSequence();

                RecordedSequence.RecordCounter += RecordedSequence_RecordCounter;
                RecordedSequence.SequenceFinished += Routing_SequenceFinished;
                await RecordedSequence.StartRecording(true, true, Routing);
                await tbRecord.Dispatcher.InvokeAsync(() => tbRecord.Text = "GO !");
            }
        }

        private async void btnPlaySequence_Click(object sender, RoutedEventArgs e)
        {
            if (RecordedSequence != null)
            {
                await tbPlay.Dispatcher.InvokeAsync(() => tbPlay.Text = "LOADING...");
                RecordedSequence.SequenceFinished -= Routing_SequenceFinished;
                RecordedSequence.SequenceFinished += Routing_SequenceFinished;

                RecordedSequence.RecordCounter -= PlayedSequence_RecordCounter;

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

                        RecordedSequence.PlayRecordingAsync(Routing);
                    }
                }
                else
                {
                    await tbPlay.Dispatcher.InvokeAsync(() => tbPlay.Text = "PLAY");
                    MessageBox.Show("Nothing to Play.");
                }
            }
        }

        private async Task InitFrames(int iRows, int iCols)
        {
            GridFrames.Clear();

            await gridBoxes.Dispatcher.InvokeAsync(() =>
            {
                gridBoxes.Children.Clear();
                gridBoxes.RowDefinitions.Clear();
                gridBoxes.ColumnDefinitions.Clear();
            });

            if (iRows == 1 && iCols == 1) //option de maximisation
            {
                await gridBoxes.Dispatcher.InvokeAsync(() =>
                {
                    gridBoxes.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(100, GridUnitType.Star) });
                    gridBoxes.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(100, GridUnitType.Star) });
                });
            }
            else
            {
                for (int row = 0; row < iRows; row++)
                {
                    await gridBoxes.Dispatcher.InvokeAsync(() =>
                    {
                        gridBoxes.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(iCols, GridUnitType.Star) });
                    });
                }
                for (int col = 0; col < iCols; col++)
                {
                    await gridBoxes.Dispatcher.InvokeAsync(() =>
                    {
                        gridBoxes.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(iRows, GridUnitType.Star) });
                    });
                }
            }

            for (int row = 0; row < iRows; row++)
            {
                for (int col = 0; col < iCols; col++)
                {
                    Border border = new Border();
                    border.BorderBrush = Brushes.Gray;
                    border.BorderThickness = new Thickness(1);

                    await Dispatcher.InvokeAsync(() =>
                    {
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
                    });
                };
            }
        }

        private async Task RemoveAllBoxes(int iRows, int iCols)
        {
            if (Boxes != null)
            {
                foreach (var box in Boxes)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        box.OnUIEvent -= RoutingBox_UIEvent;
                        var frame = GridFrames.FirstOrDefault(frame => frame.Tag.ToString().Equals(box.BoxGuid.ToString()));
                        if (frame != null)
                        {
                            frame.Content = null;
                        }
                    });
                }
            }

            await InitFrames(iRows, iCols);
        }

        private async Task AddAllRoutingBoxes()
        {
            await RemoveAllBoxes(CurrentHorizontalGrid, CurrentVerticalGrid);

            foreach (var box in Boxes.OrderBy(b => b.GridPosition))
            {
                await AddRoutingBoxToFrame(box, true);
            }
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

        private async Task UpdateDevicesUsage()
        {
            List<string> UsedDevices = new List<string>();

            for (int iB = 0; iB < Boxes.Count; iB++) //mise à jour des pérophériques MIDI utilisés
            {
                var devices = await Boxes[iB].GetAllDevices();
                UsedDevices.AddRange(devices);
            }
            await Routing.UpdateUsedDevices(UsedDevices);
        }

        private async Task SaveTemplate()
        {
            List<Task> tasks = new List<Task>();

            await UpdateDevicesUsage();

            for (int i = 0; i < Boxes.Count; i++)
            {
                int index = i;
                tasks.Add(ProcessBoxData(Boxes[index], false));
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

            BoxPreset snapshot = await box.Snapshot();

            sDevIn = snapshot.DeviceIn;
            sDevOut = snapshot.DeviceOut;
            iChIn = snapshot.ChannelIn;
            iChOut = snapshot.ChannelOut;
            options = snapshot.MidiOptions;
            preset = snapshot.MidiPreset;
            VSTPlugin vst = box.GetVST();

            if (box.RoutingGuid == Guid.Empty || bFromSave)
            {
                if (bFromSave)
                {
                    var autogeneratedguid = await Routing.AddRouting(sDevIn, sDevOut, vst, iChIn, iChOut, options, preset, sDevIn.Equals(Tools.INTERNAL_SEQUENCER) && SeqData.Sequencer.Length >= iChIn - 1 ? SeqData.Sequencer[iChIn - 1] : null);
                    if (vst != null)
                    {
                        vst = await Routing.CheckVSTSlot(sDevOut);
                        await box.SetVST(vst, Convert.ToInt32(box.cbVSTSlot.SelectedValue.ToString()), Convert.ToInt32(box.cbChannelMidiOut.SelectedValue.ToString()));
                    }

                    Routing.SetRoutingGuid(autogeneratedguid, box.RoutingGuid);
                }
                else
                {
                    var routingguid = await Routing.AddRouting(sDevIn, sDevOut, vst, iChIn, iChOut, options, preset, sDevIn.Equals(Tools.INTERNAL_SEQUENCER) && iChIn > 0 && SeqData.Sequencer.Length >= iChIn - 1 ? SeqData.Sequencer[iChIn - 1] : null);
                    box.SetRoutingGuid(routingguid);
                }
            }
            else
            {
                await Routing.ModifyRouting(snapshot.RoutingGuid, sDevIn, sDevOut, vst, iChIn, iChOut, options, preset, sDevIn.Equals(Tools.INTERNAL_SEQUENCER) && iChIn > 0 && SeqData.Sequencer.Length >= iChIn - 1 ? SeqData.Sequencer[iChIn - 1] : null);
            }
        }

        private async Task SearchBoxesForAudioInitialization(RoutingBoxes rtb)
        {
            for (int iB = 0; iB < rtb.AllPresets.Length; iB++)
            {
                if (rtb.AllPresets[iB].DeviceOut.StartsWith(Tools.VST_HOST)) //initialization de l'audio si ce n'est pas fait
                {
                    VSTHostInfo info = rtb.AllPresets[iB].VSTData; //c'est juste pour récupérer n'importe quelle info de plugin pour obtenir le driver audio et le sample rate
                    if (info != null)
                    {
                        bool b = await Routing.InitializeAudio(new VSTPlugin { VSTHostInfo = info });
                        if (!b)
                        {
                            MessageBox.Show("Unable to initialize Audio Device !");
                        }
                    }
                }
            }
        }

        private int GetFreeChannelForDevice(Guid boxguid, string sDevice, int iChannelWanted)
        {
            if (iChannelWanted == 0) { iChannelWanted = 1; }

            bool[] channels = new bool[16];

            foreach (var Box in Boxes)
            {
                if (Box.BoxGuid == boxguid)
                {
                    var mem = Box.GetRoutingBoxMemory();
                    bool bHasMorphing = mem.Any(p => p.MidiOptions.PresetMorphing > 0);
                    foreach (var preset in mem)
                    {
                        if (bHasMorphing && preset.DeviceOut.Equals(sDevice) && preset.ChannelOut > 0)//si on a demandé un morphing de preset, on doit absolument attribuer un nouveau canal MIDI car on ne peut pas morpher sur le même
                        {
                            channels[preset.ChannelOut - 1] = true;
                        }
                    }
                }
                else
                {
                    foreach (var preset in Box.GetRoutingBoxMemory())
                    {
                        if (preset.DeviceOut.Equals(sDevice) && preset.ChannelOut > 0)
                        {
                            channels[preset.ChannelOut - 1] = true;
                        }
                    }
                }
            }

            if (!channels[iChannelWanted - 1])
            {
                return iChannelWanted;
            }
            else
            {
                for (int iC = 0; iC < channels.Length; iC++)
                {
                    if (!channels[iC])
                    { return iC + 1; }
                }
            }
            return -1;
        }

        private bool CheckVSTUsage(Guid intialboxguid, VSTPlugin vst)
        {
            foreach (var b in Boxes.Where(bb => bb.BoxGuid != intialboxguid))
            {
                if (b.HasVSTAttached && b.GetVST().Slot == vst.Slot)
                {
                    return true;
                }
            }
            return false;
        }

    }

    [MessagePackObject]
    [Serializable]
    public class RoutingBoxes
    {
        [Key("AllPresets")]
        public BoxPreset[] AllPresets;

        public RoutingBoxes()
        {

        }

        internal List<RoutingBox> GetBoxes(ProjectConfiguration project, MidiRouting routing)
        {
            List<RoutingBox> boxes = new List<RoutingBox>();

            IEnumerable<Guid> distinctValues = AllPresets.Select(arr => arr.BoxGuid).Distinct();

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

                    var box = new RoutingBox(project, MidiRouting.InputDevices, MidiRouting.OutputDevices, iGridPosition, g, presetsample.BoxName, presetsample.RoutingGuid, AllPresets.Where(p => p.BoxGuid == g).ToArray());
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
