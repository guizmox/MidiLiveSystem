﻿using Microsoft.Win32;
using MidiTools;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;

namespace VSTHost
{


    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public delegate void VSTHostEventHandler(int boxpreset, int iAction);
        public event VSTHostEventHandler OnVSTHostEvent;
        public VSTPlugin Plugin;

        private System.Timers.Timer VSTParametersCheck;

        private int BoxPreset = 0;
        private Guid RoutingGuid = Guid.Empty;

        public MainWindow(Guid routingguid, string boxname, int preset, VSTPlugin plugin)
        {
            InitializeComponent();
            BoxPreset = preset;
            RoutingGuid = routingguid;
            Title = string.Concat(Title, " - ", boxname);
            Plugin = plugin;
            UtilityAudio.AudioEvent += UtilityAudio_AudioEvent;
            plugin.VSTEvent += Plugin_VSTEvent;

            InitPage();

            if (plugin.Loaded)
            {
                OpenPlugin();
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

            if (UtilityAudio.AudioInitialized)
            {
                cbAudioOut.SelectedValue = UtilityAudio.DeviceName;
                cbSampleRate.SelectedValue = UtilityAudio.DeviceSampleRate.ToString();
                cbAudioOut.IsEnabled = false;
                cbSampleRate.IsEnabled = false;
            }
            else
            {
                if (cbAudioOut.Items.Count > 1 && asioDriverNames.Length > 0)
                {
                    cbAudioOut.SelectedIndex = 1;
                }
            }
        }

        private async void Plugin_VSTEvent(string sMessage)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                int iLast = Title.LastIndexOf("[");
                if (iLast > 0)
                {
                    Title = string.Concat(Title[0..iLast].Trim(), " [", sMessage, "]");
                }
                else
                {
                    Title = string.Concat(Title.Trim(), " [", sMessage, "]");
                }
            });
        }

        private async void UtilityAudio_AudioEvent(string sMessage, string sDevice, int iSampleRate)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                int iLast = Title.LastIndexOf("[");
                if (iLast > 0)
                {
                    Title = string.Concat(Title[0..iLast].Trim(), " [", sMessage, " - ", sDevice, "]");
                }
                else
                {
                    Title = string.Concat(Title.Trim(), " [", sMessage, " - ", sDevice, "]");
                }
            });
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            Plugin.VSTEvent -= Plugin_VSTEvent;
            UtilityAudio.AudioEvent -= UtilityAudio_AudioEvent;

            if (VSTParametersCheck != null)
            {
                VSTParametersCheck.Stop();
                VSTParametersCheck.Enabled = false;
            }

            Plugin.CloseEditor();

            OnVSTHostEvent?.Invoke(BoxPreset, 0);
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
                    OnVSTHostEvent?.Invoke(BoxPreset, 1); //pour initialiser l'ASIO. Pas d'autre usage
                }
                else
                {
                    Plugin.VSTHostInfo.SampleRate = SampleRate;
                    Plugin.VSTHostInfo.AsioDevice = AsioDevice;
                    Plugin.VSTHostInfo.VSTPath = sVST;
                    OnVSTHostEvent?.Invoke(BoxPreset, 2); //pour initialiser le plugin. Pas d'autre usage
                }
            }
        }

        public async Task LoadPlugin()
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            while (!UtilityAudio.AudioInitialized && stopwatch.ElapsedMilliseconds < 10000)
            {
                await Task.Delay(100);
            }

            stopwatch.Stop();

            if (stopwatch.ElapsedMilliseconds >= 10000)
            {
                UtilityAudio.StopAudio();
                UtilityAudio.Dispose();
                MessageBox.Show("Unable to initialize Audio !");
                Close();
            }
            else
            {
                await Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    try
                    {
                        if (!Plugin.Loaded)
                        {
                            string sInfo = Plugin.LoadVST();
                            if (sInfo.Length > 0)
                            {
                                MessageBox.Show(sInfo);
                            }
                        }

                        OpenPlugin();

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
                        MessageBox.Show("Unable to load VST : " + ex.Message);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Unable to load VST plugin : " + ex.Message);
                }
            });
            }
        }

        private async void OpenPlugin()
        {
            Mouse.OverrideCursor = Cursors.Wait;

            await Dispatcher.InvokeAsync(() =>
            {
                var result = Plugin.OpenEditor(new WindowInteropHelper(this).EnsureHandle());
                if (result)
                {
                    System.Drawing.Rectangle rect = new System.Drawing.Rectangle();
                    Plugin.GetWindowSize(out rect);
                    Width = rect.Width + 20;
                    Height = rect.Height + 50;
                    ResizeMode = ResizeMode.NoResize;
                    this.Title = Plugin.VSTHostInfo.VSTName;
                }
                else { MessageBox.Show("Unable to open plugin editor. Not initialized."); }

                Mouse.OverrideCursor = null;
            });
        }

        private void VSTParametersCheck_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            Plugin.GetParameters();

            Dispatcher.Invoke(() =>
            {
                string sInfo = Plugin.GetInfo();

                int iLast = Title.LastIndexOf("[");
                if (iLast > 0)
                {
                    Title = string.Concat(Title[0..iLast].Trim(), " [", sInfo, "]");
                }
                else
                {
                    Title = string.Concat(Title.Trim(), " [", sInfo, "]");
                }
            });
        }

    }
}