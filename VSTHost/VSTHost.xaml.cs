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
        public delegate void VSTHostEventHandler(VSTHostInfo vst, int boxpreset, bool bClose);
        public event VSTHostEventHandler OnVSTHostEvent;

        private System.Timers.Timer VSTParametersCheck;

        int BoxPreset = 0;
        Guid RoutingGuid = Guid.Empty;
        VSTHostInfo VSTInfo;
        VSTPlugin Plugin;

        public MainWindow(Guid routingguid, string boxname, int preset, VSTHostInfo vst = null)
        {
            InitializeComponent();
            BoxPreset = preset;
            RoutingGuid = routingguid;
            Title = string.Concat(Title, " - ", boxname);
            VSTInfo = vst;

            InitPage();

            if (VSTInfo != null)
            {
                Dispatcher.Invoke(() => LoadPlugin());
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            OnVSTHostEvent?.Invoke(VSTInfo, BoxPreset, true);
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

                if (VSTInfo == null)
                {
                    VSTInfo = new VSTHostInfo();
                    VSTInfo.SampleRate = SampleRate;
                    VSTInfo.AsioDevice = AsioDevice;
                    VSTInfo.VSTPath = sVST;
                    VSTInfo.VSTHostGuid = Guid.NewGuid();
                    OnVSTHostEvent?.Invoke(VSTInfo, BoxPreset, false);
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

        public async Task<VST> LoadPlugin()
        {
            VST device = null;

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
                        Plugin.VSTSynth = Plugin.LoadVST(VSTInfo.VSTPath, VSTInfo.SampleRate);

                        IntPtr windowHandle = new WindowInteropHelper(this).Handle;

                        Plugin.VSTSynth.pluginContext.PluginCommandStub.Commands.EditorOpen(windowHandle);
                        System.Drawing.Rectangle rect = new System.Drawing.Rectangle();
                        Plugin.VSTSynth.pluginContext.PluginCommandStub.Commands.EditorGetRect(out rect);
                        Width = rect.Width;
                        Height = rect.Height;
                        ResizeMode = ResizeMode.NoResize;
                        device = Plugin.VSTSynth;

                        try
                        {
                            VSTInfo.VSTName = Plugin.VSTSynth.pluginContext.PluginCommandStub.Commands.GetProductString();
                            if (VSTInfo.VSTName.Length == 0)
                            {
                                VSTInfo.VSTName = Plugin.VSTSynth.pluginContext.PluginCommandStub.Commands.GetInputProperties(0).Label;
                            }
                        }
                        catch
                        {
                            VSTInfo.VSTName = Path.GetFileName(VSTInfo.VSTPath);
                        }

                        if (VSTInfo.Program > -1)
                        {
                            try
                            {
                                Plugin.VSTSynth.pluginContext.PluginCommandStub.Commands.SetProgram(VSTInfo.Program);
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show("VST Program can't be set : " + ex.Message);
                            }
                        }

                        if (VSTInfo.ParameterNames.Count > 0)
                        {
                            try
                            {
                                for (int iP = 0; iP < VSTInfo.ParameterNames.Count; iP++)
                                {
                                    Plugin.VSTSynth.pluginContext.PluginCommandStub.Commands.SetParameter(iP, VSTInfo.ParameterValues[iP]);
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
                    }
                    catch (Exception ex)
                    {
                        if (Plugin != null) { Plugin.DisposeVST(); }
                        VSTInfo = null;
                        MessageBox.Show("Unable to load VST : " + ex.Message);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Unable to load VST plugin : " + ex.Message);
                }
            });

            return device;
        }

        private async void VSTParametersCheck_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            await UIEventPool.AddTask(() =>
            {
                var props = Plugin.VSTSynth.pluginContext.PluginCommandStub.Commands.GetProgram();
                if (props != null)
                {
                    VSTInfo.Program = props;
                }

                VSTInfo.ParameterValues.Clear();
                VSTInfo.ParameterNames.Clear();
                for (int i = 0; i < VSTInfo.ParameterCount; i++)
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
                                VSTInfo.ParameterNames.Add(propName);
                                VSTInfo.ParameterValues.Add(propData);
                            }
                            else { VSTInfo.ParameterCount = i; break; }
                        }
                        else { VSTInfo.ParameterCount = i; break; }
                    }
                    catch { VSTInfo.ParameterCount = i; break; }
                }
            });
        }

        public void DisposePlugin()
        {
            if (VSTParametersCheck != null)
            {
                VSTParametersCheck.Stop();
                VSTParametersCheck.Enabled = false;
                VSTParametersCheck = null;
            }

            if (Plugin != null)
            {
                Plugin.DisposeVST();
                Plugin.VSTSynth.pluginContext.PluginCommandStub.Commands.Close();
                Plugin.VSTSynth.pluginContext.Dispose();
            }
        }
    }
}