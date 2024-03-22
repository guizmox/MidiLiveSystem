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
using System.Windows.Shapes;
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
                        device = Plugin.VSTSynth;
                    }
                    catch (Exception ex)
                    {
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

        public void DisposePlugin()
        {
            if (Plugin != null)
            {
                Plugin.DisposeVST();
            }
        }
    }
}