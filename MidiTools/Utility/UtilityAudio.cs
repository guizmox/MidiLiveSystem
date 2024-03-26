using System;
using System.Collections.Generic;
using NAudio.Wave;
using Jacobi.Vst.Interop;
using CommonUtils.VSTPlugin;
using Jacobi.Vst.Host.Interop;
using System.Threading;
using System.Net.Security;
using Jacobi.Vst.Samples.Host;
using MidiTools;
using Jacobi.Vst.Core;
using System.Collections.Concurrent;
using System.IO;
using System.Windows.Forms;
using System.Drawing;

// Copied from the microDRUM project
// https://github.com/microDRUM
// I think it is created by massimo.bernava@gmail.com
// Modified by perivar@nerseth.com
namespace VSTHost
{
    internal class VST
    {
        private System.Timers.Timer StackTimer;
        internal VstPluginContext pluginContext = null;
        private ConcurrentBag<VstEvent> MidiStack = new ConcurrentBag<VstEvent>();
        private ConcurrentBag<VstEvent> MidiStackNoteOn = new ConcurrentBag<VstEvent>();

        internal event EventHandler<VSTStreamEventArgs> StreamCall = null;

        internal VST()
        {
            StackTimer = new System.Timers.Timer();
            StackTimer.Elapsed += StackTimer_Elapsed;
            StackTimer.Interval = 10;
            StackTimer.Start();
        }

        private void StackTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            //je force les évènements NOTE ON à être lancés avant pour éviter le risque de notes non relâchées (NOTE OFF avant NOTE ON)
            if (MidiStackNoteOn.Count > 0)
            {
                pluginContext.PluginCommandStub.Commands.ProcessEvents(MidiStackNoteOn.ToArray());
                MidiStackNoteOn.Clear();
            }

            if (MidiStack.Count > 0)
            {
                pluginContext.PluginCommandStub.Commands.ProcessEvents(MidiStack.ToArray());
                MidiStack.Clear();
            }
        }

        //EditParametersForm edit = new EditParametersForm();

        internal void StopTimer()
        {
            if (StackTimer != null)
            {
                StackTimer.Stop();
                StackTimer.Enabled = false;
                StackTimer = null;
            }
        }

        public void MIDI_NoteOn(byte Note, byte Velocity)
        {
            byte Cmd = 0x90;
            MIDI(true, Cmd, Note, Velocity);
        }

        public void MIDI_NoteOff(byte Note, byte Velocity)
        {
            byte Cmd = 0x80;
            MIDI(false, Cmd, Note, Velocity);
        }

        public void MIDI_ProgramChange(byte programNumber)
        {
            byte Cmd = 0xC0; // Code de commande MIDI pour le changement de programme
            MIDI(false, Cmd, programNumber, 0); // La vélocité est généralement 0 pour un changement de programme
        }

        public void MIDI_PitchBend(int pitchValue)
        {
            // Assurez-vous que la valeur du pitch bend est dans la plage valide (-8192 à 8191 inclus)
            if (pitchValue < -8192 || pitchValue > 8191)
            {
                throw new ArgumentOutOfRangeException("pitchValue", "La valeur du pitch bend doit être comprise entre -8192 et 8191.");
            }

            // Calculer les valeurs MSB et LSB à partir de la valeur du pitch bend
            byte lsb = (byte)(pitchValue & 0x7F);
            byte msb = (byte)((pitchValue >> 7) & 0x7F);

            byte cmd = 0xE0; // Code de commande MIDI pour le pitch bend
            MIDI(false, cmd, lsb, msb);
        }

        public void MIDI_Aftertouch(byte pressureValue)
        {
            // Assurez-vous que la valeur de la pression est dans la plage valide (0 à 127 inclus)
            if (pressureValue < 0 || pressureValue > 127)
            {
                throw new ArgumentOutOfRangeException("pressureValue", "La valeur de la pression doit être comprise entre 0 et 127.");
            }

            byte cmd = 0xD0; // Code de commande MIDI pour l'aftertouch (pression du canal)
            MIDI(false, cmd, pressureValue, 0); // Le deuxième paramètre est la valeur de pression, le troisième est souvent ignoré dans le cas de l'aftertouch
        }

        public void MIDI_PolyphonicAftertouch(byte note, byte pressureValue)
        {
            // Assurez-vous que la note et la valeur de la pression sont dans les plages valides (0 à 127 inclus)
            if (note < 0 || note > 127 || pressureValue < 0 || pressureValue > 127)
            {
                throw new ArgumentOutOfRangeException("note", "La note doit être comprise entre 0 et 127, et la valeur de la pression doit être comprise entre 0 et 127.");
            }

            byte cmd = 0xA0; // Code de commande MIDI pour le polyphonic aftertouch
            MIDI(false, cmd, note, pressureValue);
        }

        public void MIDI_CC(byte Number, byte Value)
        {
            byte Cmd = 0xB0;
            MIDI(false, Cmd, Number, Value);
        }

        private void MIDI(bool bNoteOn, byte Cmd, byte Val1, byte Val2)
        {
            var midiData = new byte[4];
            midiData[0] = Cmd;
            midiData[1] = Val1;
            midiData[2] = Val2;
            midiData[3] = 0;    // Reserved, unused

            var vse = new VstMidiEvent(/*DeltaFrames*/ 0,
                                       /*NoteLength*/ 0,
                                       /*NoteOffset*/  0,
                                       midiData,
                                       /*Detune*/        0,
                                       /*NoteOffVelocity*/ 0, true);

            if (bNoteOn)
            {
                MidiStackNoteOn.Add(vse);
            }
            else
            {
                MidiStack.Add(vse);
            }
        }

        //internal void ShowEditParameters()
        //{
        //	edit.AddParameters(pluginContext);
        //	edit.Show();
        //}

        internal void Stream_ProcessCalled(object sender, VSTStreamEventArgs e)
        {
            if (StreamCall != null) StreamCall(sender, e);
        }
    }

    public class VSTPlugin
    {
        public VSTHostInfo VSTHostInfo;
        internal VST VSTSynth;
        private VSTStream vstStream;

        public VSTPlugin()
        {

        }

        public string LoadVST()
        {
            string sInfo = "";

            DisposeVST();

            VSTSynth = new VST();

            var hcs = new HostCommandStub();

            try
            {
                string sVSTPath = System.IO.Path.GetDirectoryName(VSTHostInfo.VSTPath);

                VSTSynth.pluginContext = VstPluginContext.Create(VSTHostInfo.VSTPath, hcs);
                VSTSynth.pluginContext.PluginCommandStub.Commands.Open();
                //pluginContext.PluginCommandStub.SetProgram(0);
                //GeneralVST.pluginContext.PluginCommandStub.Commands.Open(hWnd);
                //GeneralVST.pluginContext.PluginCommandStub.Commands.(true);

                vstStream = new VSTStream();
                vstStream.ProcessCalled += VSTSynth.Stream_ProcessCalled;
                vstStream.pluginContext = VSTSynth.pluginContext;
                vstStream.SetWaveFormat(VSTHostInfo.SampleRate, 2);

                UtilityAudio.AudioMixer.AddInputStream(vstStream);

                try
                {
                    VSTHostInfo.VSTName = VSTSynth.pluginContext.PluginCommandStub.Commands.GetProductString();
                    if (VSTHostInfo.VSTName.Length == 0)
                    {
                        VSTHostInfo.VSTName = VSTSynth.pluginContext.PluginCommandStub.Commands.GetInputProperties(0).Label;
                    }
                }
                catch
                {
                    VSTHostInfo.VSTName = Path.GetFileName(VSTHostInfo.VSTPath);
                }

                if (VSTHostInfo.Program > -1)
                {
                    try
                    {
                        VSTSynth.pluginContext.PluginCommandStub.Commands.SetProgram(VSTHostInfo.Program);
                    }
                    catch (Exception ex)
                    {
                        sInfo = "VST Program can't be set : " + ex.Message;
                    }
                }

                if (VSTHostInfo.ParameterNames.Count > 0)
                {
                    try
                    {
                        for (int iP = 0; iP < VSTHostInfo.ParameterNames.Count; iP++)
                        {
                            VSTSynth.pluginContext.PluginCommandStub.Commands.SetParameter(iP, VSTHostInfo.ParameterValues[iP]);
                        }

                    }
                    catch (Exception ex)
                    {
                       sInfo = "VST Parameters can't be set : " + ex.Message;
                    }
                }
            }
            catch
            {
                throw;
            }

            return sInfo;
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

        internal void DisposeVST()
        {
            if (vstStream != null)
            {
                vstStream.ProcessCalled -= VSTSynth.Stream_ProcessCalled;
            }

            if (UtilityAudio.AudioMixer != null) UtilityAudio.AudioMixer.RemoveInputStream(vstStream);

            if (vstStream != null)
            {
                vstStream.Dispose();
                vstStream = null;
            }

            if (VSTSynth != null)
            {
                VSTSynth.pluginContext.PluginCommandStub.Commands.Close();
                //VSTSynth.pluginContext.Dispose();
            }
        }

        public void GetParameters()
        {
            if (VSTSynth.pluginContext.PluginCommandStub != null)
            {
                var props = VSTSynth.pluginContext.PluginCommandStub.Commands.GetProgram();
                if (props != null)
                {
                    VSTHostInfo.Program = props;
                }

                VSTHostInfo.ParameterValues.Clear();
                VSTHostInfo.ParameterNames.Clear();
                for (int i = 0; i < VSTHostInfo.ParameterCount; i++)
                {
                    try
                    {
                        var properties = VSTSynth.pluginContext.PluginCommandStub.Commands.GetParameterProperties(i);

                        if (properties != null)
                        {
                            string propName = propName = VSTSynth.pluginContext.PluginCommandStub.Commands.GetParameterName(i);
                            var propData = VSTSynth.pluginContext.PluginCommandStub.Commands.GetParameter(i);
                            if (propName != null)
                            {
                                VSTHostInfo.ParameterNames.Add(propName);
                                VSTHostInfo.ParameterValues.Add(propData);
                            }
                            else { VSTHostInfo.ParameterCount = i; break; }
                        }
                        else { VSTHostInfo.ParameterCount = i; break; }
                    }
                    catch { VSTHostInfo.ParameterCount = i; break; }
                }
            }
        }

        public void OpenEditor(IntPtr windowHandle)
        {
            VSTSynth.pluginContext.PluginCommandStub.Commands.EditorOpen(windowHandle);
        }

        public void GetWindowSize(out Rectangle rect)
        {
            VSTSynth.pluginContext.PluginCommandStub.Commands.EditorGetRect(out rect);
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
