using System;
using System.Collections.Generic;
using NAudio.Wave;
using Jacobi.Vst.Interop;
using CommonUtils.VSTPlugin;
using Jacobi.Vst.Host.Interop;
using System.Threading;
using System.Net.Security;

// Copied from the microDRUM project
// https://github.com/microDRUM
// I think it is created by massimo.bernava@gmail.com
// Modified by perivar@nerseth.com
namespace VSTHost
{
    public class VSTPlugin
    {
        public VST VSTSynth;
        private VSTStream vstStream;

        public VSTPlugin()
        {

        }

        public VST LoadVST(string VSTFile, int iSampleRate)
        {
            DisposeVST();

            VSTSynth = new VST();

            var hcs = new HostCommandStub();

            try
            {
                string sVSTPath = System.IO.Path.GetDirectoryName(VSTFile);

                VSTSynth.pluginContext = VstPluginContext.Create(VSTFile, hcs);
                VSTSynth.pluginContext.PluginCommandStub.Commands.Open();
                //pluginContext.PluginCommandStub.SetProgram(0);
                //GeneralVST.pluginContext.PluginCommandStub.Commands.Open(hWnd);
                //GeneralVST.pluginContext.PluginCommandStub.Commands.(true);

                vstStream = new VSTStream();
                vstStream.ProcessCalled += VSTSynth.Stream_ProcessCalled;
                vstStream.pluginContext = VSTSynth.pluginContext;
                vstStream.SetWaveFormat(iSampleRate, 2);

                UtilityAudio.AudioMixer.AddInputStream(vstStream);

                return VSTSynth;
            }
            catch
            {
                throw;
            }
        }

        public static string GetVSTDirectory()
        {
            try
            {
                Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey("SOFTWARE").OpenSubKey("VST");

                if (key != null) return key.GetValue("VstPluginsPath").ToString();
                else { return ""; }
            }
            catch { return ""; }
        }

        public void DisposeVST()
        {
            if (UtilityAudio.AudioMixer != null) UtilityAudio.AudioMixer.RemoveInputStream(vstStream);

            if (vstStream != null) vstStream.Dispose();
            vstStream = null;

            if (VSTSynth != null) VSTSynth.Dispose();
            VSTSynth = null;
        }

    }

    public static class UtilityAudio
    {
        private static Thread AudioThread;

        private static IWavePlayer AudioDevice;
        internal static RecordableMixerStream32 AudioMixer;
        public static bool AudioInitialized = false;

        public static bool OpenAudio(string asioDevice, int iSampleRate)
        {
            int iOK = 0;

            if (AudioDevice == null) //asio pas encore initialisé
            {
                AudioThread = new Thread(() =>
                {
                    StartSTA(asioDevice, iSampleRate, ref iOK);
                });

                AudioThread.SetApartmentState(ApartmentState.STA);
                AudioThread.Start();

                while (iOK == 0)
                {
                    Thread.Sleep(10);
                }
            }
            else { iOK = 1; }

            return iOK == 1 ? true : false;
        }

        private static void StartSTA(string asioDevice, int iSampleRate, ref int iOK)
        {
            try
            {
                if (AudioDevice != null && AudioDevice.PlaybackState == PlaybackState.Playing)
                {
                    iOK = 2;
                }

                if (iOK == 0)
                {
                    AudioMixer = new RecordableMixerStream32(iSampleRate);
                    AudioMixer.AutoStop = false;

                    if (!string.IsNullOrEmpty(asioDevice))
                    {
                        AudioDevice = new AsioOut(asioDevice);
                    }

                    if (AudioDevice == null)
                    {
                        AudioDevice = new WaveOut();
                    }

                    if (AudioDevice == null)
                    {
                        iOK = 2;
                    }

                    if (iOK == 0)
                    {
                        AudioDevice.Init(AudioMixer);
                        iOK = 1;
                    }
                }
            }
            catch
            {
                iOK = 2;
            }
        }

        public static void StartAudio()
        {
            if (AudioDevice != null)
            { 
                AudioDevice.Play(); 
            }
        }

        public static void StopAudio()
        {
            if (AudioDevice != null) { AudioDevice.Stop(); }
            AudioThread = null;
        }

        public static void Dispose()
        {
            if (AudioDevice != null)
            {
                AudioDevice.Stop();
                try
                {
                    AudioDevice.Dispose();
                }
                catch 
                { 
                }
                AudioDevice = null;
            }

            if (AudioMixer != null) { AudioMixer.Dispose(); AudioMixer = null; }

        }

    }
}
