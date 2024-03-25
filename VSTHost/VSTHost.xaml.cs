using System.Text;
using MaterialDesignThemes.Wpf;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using NAudio.Wave;
using System.IO;
using Microsoft.Win32;
using CommonUtils.VSTPlugin;
using NAudio.Gui;
using System.Windows.Interop;
using MidiTools;

namespace VSTHost
{


    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public delegate void VSTHostEventHandler(VSTPlugin plugin, int boxpreset, bool bClose);
        public event VSTHostEventHandler OnVSTHostEvent;
        public VSTPlugin Plugin = new VSTPlugin();

        private System.Timers.Timer VSTParametersCheck;

        private int BoxPreset = 0;
        private Guid RoutingGuid = Guid.Empty;

        public MainWindow(Guid routingguid, string boxname, int preset, VSTHostInfo vst = null)
        {
            InitializeComponent();
            BoxPreset = preset;
            RoutingGuid = routingguid;
            Title = string.Concat(Title, " - ", boxname);
            Plugin.VSTHostInfo = vst;

            InitPage();

            if (vst != null)
            {
                Dispatcher.Invoke(() => LoadPlugin());
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if (VSTParametersCheck != null)
            {
                VSTParametersCheck.Stop();
                VSTParametersCheck.Enabled = false;
            }
            OnVSTHostEvent?.Invoke(Plugin, BoxPreset, true);
        }

        private void tbOpenVST_Click(object sender, RoutedEventArgs e)
        {
            string sVSTDefaultDir = VSTPlugin.GetVSTDirectory();

            OpenFileDialog fi = new OpenFileDialog();
            fi.Title = "Select VST Synth :";
            fi.Filter = "VST Files (*.dll)|*.dll";
            if (sVSTDefaultDir.Length > 0) { fi.InitialDirectory = sVSTDefaultDir; }
            fi.ShowDialog();

            string sVST = fi.FileName;

            if (File.Exists(sVST))
            {
                var AsioDevice = cbAudioOut.SelectedValue != null ? cbAudioOut.SelectedValue.ToString() : "";
                var SampleRate = Convert.ToInt32(cbSampleRate.SelectedValue);

                if (Plugin.VSTHostInfo == null)
                {
                    Plugin.VSTHostInfo = new VSTHostInfo();
                    Plugin.VSTHostInfo.SampleRate = SampleRate;
                    Plugin.VSTHostInfo.AsioDevice = AsioDevice;
                    Plugin.VSTHostInfo.VSTPath = sVST;
                    Plugin.VSTHostInfo.VSTHostGuid = Guid.NewGuid();
                    OnVSTHostEvent?.Invoke(Plugin, BoxPreset, false);
                }
            }
        }

        private void InitPage()
        {
            cbAudioOut.Items.Add(new ComboBoxItem { Content = "Default Windows Audio (Not recommanded !)", Tag = "" });

            var asioDriverNames = AsioOut.GetDriverNames();
            foreach (string driverName in asioDriverNames)
            {
                cbAudioOut.Items.Add(new ComboBoxItem { Content = "ASIO - " + driverName, Tag = driverName });
            }

            if (cbAudioOut.Items.Count > 1 && asioDriverNames.Length > 0)
            {
                cbAudioOut.SelectedIndex = 1;
            }
        }

        public async Task<VSTPlugin> LoadPlugin()
        {
            while (!UtilityAudio.AudioInitialized) //couper au bout de 10 secondes
            {
                await Task.Delay(100);
            }

            await Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    try
                    {
                        if (Plugin == null) { Plugin = new VSTPlugin(); }
                        Plugin.VSTSynth = Plugin.LoadVST();

                        IntPtr windowHandle = new WindowInteropHelper(this).Handle;

                        Plugin.VSTSynth.pluginContext.PluginCommandStub.Commands.EditorOpen(windowHandle);
                        System.Drawing.Rectangle rect = new System.Drawing.Rectangle();
                        Plugin.VSTSynth.pluginContext.PluginCommandStub.Commands.EditorGetRect(out rect);
                        Width = rect.Width + 20;
                        Height = rect.Height + 50;
                        ResizeMode = ResizeMode.NoResize;

                        try
                        {
                            Plugin.VSTHostInfo.VSTName = Plugin.VSTSynth.pluginContext.PluginCommandStub.Commands.GetProductString();
                            if (Plugin.VSTHostInfo.VSTName.Length == 0)
                            {
                                Plugin.VSTHostInfo.VSTName = Plugin.VSTSynth.pluginContext.PluginCommandStub.Commands.GetInputProperties(0).Label;
                            }
                        }
                        catch
                        {
                            Plugin.VSTHostInfo.VSTName = Path.GetFileName(Plugin.VSTHostInfo.VSTPath);
                        }

                        if (Plugin.VSTHostInfo.Program > -1)
                        {
                            try
                            {
                                Plugin.VSTSynth.pluginContext.PluginCommandStub.Commands.SetProgram(Plugin.VSTHostInfo.Program);
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show("VST Program can't be set : " + ex.Message);
                            }
                        }

                        if (Plugin.VSTHostInfo.ParameterNames.Count > 0)
                        {
                            try
                            {
                                for (int iP = 0; iP < Plugin.VSTHostInfo.ParameterNames.Count; iP++)
                                {
                                    Plugin.VSTSynth.pluginContext.PluginCommandStub.Commands.SetParameter(iP, Plugin.VSTHostInfo.ParameterValues[iP]);
                                }

                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show("VST Parameters can't be set : " + ex.Message);
                            }
                        }

                        if (VSTParametersCheck == null)
                        {
                            VSTParametersCheck = new System.Timers.Timer();
                            VSTParametersCheck.Elapsed += VSTParametersCheck_Elapsed;
                            VSTParametersCheck.Interval = 10000;
                            VSTParametersCheck.Start();
                        }
                        else
                        {
                            VSTParametersCheck.Enabled = true;
                            VSTParametersCheck.Start();
                        }
                    }
                    catch (Exception ex)
                    {
                        if (Plugin != null) { Plugin.DisposeVST(); }
                        Plugin.VSTHostInfo = null;
                        MessageBox.Show("Unable to load VST : " + ex.Message);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Unable to load VST plugin : " + ex.Message);
                }
            });

            return Plugin;
        }

        private async void VSTParametersCheck_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            await UIEventPool.AddTask(() =>
            {
                if (Plugin.VSTSynth.pluginContext.PluginCommandStub != null)
                {
                    var props = Plugin.VSTSynth.pluginContext.PluginCommandStub.Commands.GetProgram();
                    if (props != null)
                    {
                        Plugin.VSTHostInfo.Program = props;
                    }

                    Plugin.VSTHostInfo.ParameterValues.Clear();
                    Plugin.VSTHostInfo.ParameterNames.Clear();
                    for (int i = 0; i < Plugin.VSTHostInfo.ParameterCount; i++)
                    {
                        try
                        {
                            var properties = Plugin.VSTSynth.pluginContext.PluginCommandStub.Commands.GetParameterProperties(i);

                            if (properties != null)
                            {
                                string propName = propName = Plugin.VSTSynth.pluginContext.PluginCommandStub.Commands.GetParameterName(i);
                                var propData = Plugin.VSTSynth.pluginContext.PluginCommandStub.Commands.GetParameter(i);
                                if (propName != null)
                                {
                                    Plugin.VSTHostInfo.ParameterNames.Add(propName);
                                    Plugin.VSTHostInfo.ParameterValues.Add(propData);
                                }
                                else { Plugin.VSTHostInfo.ParameterCount = i; break; }
                            }
                            else { Plugin.VSTHostInfo.ParameterCount = i; break; }
                        }
                        catch { Plugin.VSTHostInfo.ParameterCount = i; break; }
                    }
                }
            });
        }

    }
}