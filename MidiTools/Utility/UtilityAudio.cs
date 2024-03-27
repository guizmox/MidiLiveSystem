using Jacobi.Vst.Core;
using Jacobi.Vst.Host.Interop;
using Jacobi.Vst.Samples.Host;
using MidiTools;
using NAudio.Wave;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;

// Copied from the microDRUM project
// https://github.com/microDRUM
// I think it is created by massimo.bernava@gmail.com
// Modified by perivar@nerseth.com
namespace VSTHost
{
    [Serializable]
    public class VSTParameter
    {
        public int Index;
        public float Data;
        public string Name;
    }

    [Serializable]
    public class VSTHostInfo
    {
        public Guid VSTHostGuid = Guid.Empty;
        public int SynthID = 0;
        public string AsioDevice = "";
        public int SampleRate = 48000;
        public string VSTPath = "";
        public string Error = "";
        public string VSTName = "";
        public int Program = -1;
        public List<VSTParameter> Parameters = new List<VSTParameter>();
        public int PluginID = 0;
        public int AudioOutputs = 0;
        public int Slot = 1;
    }

    internal class VSTStreamEventArgs : EventArgs
    {
        internal float MaxL = float.MinValue;
        internal float MaxR = float.MaxValue;

        internal VSTStreamEventArgs(float maxL, float maxR)
        {
            MaxL = maxL;
            MaxR = maxR;
        }
    }

    internal class VSTStream : WaveStream
    {
        private VSTMidi VSTSynth;

        internal VstPluginContext pluginContext = null;
        internal event EventHandler<VSTStreamEventArgs> ProcessCalled;

        private WaveFormat waveFormat;

        internal VstAudioBuffer[] inputBuffers;
        internal VstAudioBuffer[] outputBuffers;

        private float[] output;
        private int BlockSize = 0;

        //private WaveChannel32 wavStream;
        internal VSTStream(VSTMidi vstsynth)
        {
            VSTSynth = vstsynth;
        }

        internal new void Dispose()
        {
            base.Dispose();
        }

        private void RaiseProcessCalled(float maxL, float maxR)
        {
            EventHandler<VSTStreamEventArgs> handler = ProcessCalled;

            if (handler != null)
            {
                handler(this, new VSTStreamEventArgs(maxL, maxR));
            }
        }

        private void UpdateBlockSize(int blockSize)
        {
            BlockSize = blockSize;

            int inputCount = pluginContext.PluginInfo.AudioInputCount;
            int outputCount = pluginContext.PluginInfo.AudioOutputCount;

            var inputMgr = new VstAudioBufferManager(inputCount, blockSize);
            var outputMgr = new VstAudioBufferManager(outputCount, blockSize);

            inputBuffers = inputMgr.Buffers.ToArray();
            outputBuffers = outputMgr.Buffers.ToArray();

            pluginContext.PluginCommandStub.Commands.SetBlockSize(blockSize);
            pluginContext.PluginCommandStub.Commands.SetSampleRate(WaveFormat.SampleRate);
            pluginContext.PluginCommandStub.Commands.SetProcessPrecision(VstProcessPrecision.Process64);


            output = new float[WaveFormat.Channels * blockSize];
        }

        private float[] ProcessReplace(int blockSize)
        {
            if (blockSize != BlockSize) UpdateBlockSize(blockSize);

            // check if we are processing a wavestream (VST) or if this is audio outputting only (VSTi)
            //if (wavStream != null)
            //{
            //    int sampleCount = blockSize * 2;
            //    int sampleCountx4 = sampleCount * 4;
            //    int loopSize = sampleCount / WaveFormat.Channels;

            //    // Convert byte array into float array and store in Vst Buffers
            //    // naudio reads an buffer of interlaced float's
            //    // must take every 4th byte and convert to float
            //    // Vst.Net audio buffer format (-1 to 1 floats).
            //    var naudioBuf = new byte[blockSize * WaveFormat.Channels * 4];
            //    int bytesRead = wavStream.Read(naudioBuf, 0, sampleCountx4);

            //    // populate the inputbuffers with the incoming wave stream
            //    // TODO: do not use unsafe - but like this http://vstnet.codeplex.com/discussions/246206 ?
            //    // this whole section is modelled after http://vstnet.codeplex.com/discussions/228692
            //    unsafe
            //    {
            //        fixed (byte* byteBuf = &naudioBuf[0])
            //        {
            //            float* floatBuf = (float*)byteBuf;
            //            int j = 0;
            //            for (int i = 0; i < loopSize; i++)
            //            {
            //                inputBuffers[0][i] = *(floatBuf + j);
            //                j++;
            //                inputBuffers[1][i] = *(floatBuf + j);
            //                j++;
            //            }
            //        }
            //    }
            //}

            try
            {
                //pluginContext.PluginCommandStub.MainsChanged(true);
                pluginContext.PluginCommandStub.Commands.StartProcess();
                pluginContext.PluginCommandStub.Commands.ProcessReplacing(inputBuffers, outputBuffers);

                lock (VSTSynth.MidiStack)
                {
                    if (VSTSynth.MidiStack.Count > 0)
                    {
                        pluginContext.PluginCommandStub.Commands.ProcessEvents(VSTSynth.MidiStack.ToArray());
                        VSTSynth.MidiStack.Clear();
                    }
                }

                pluginContext.PluginCommandStub.Commands.StopProcess();
                //pluginContext.PluginCommandStub.MainsChanged(false);
            }
            catch
            {
                throw;
            }

            int indexOutput = 0;

            float maxL = float.MinValue;
            float maxR = float.MinValue;

            for (int j = 0; j < BlockSize; j++)
            {
                output[indexOutput] = outputBuffers[0][j];
                output[indexOutput + 1] = outputBuffers[1][j];

                maxL = Math.Max(maxL, output[indexOutput]);
                maxR = Math.Max(maxR, output[indexOutput + 1]);
                indexOutput += 2;
            }
            RaiseProcessCalled(maxL, maxR);
            return output;
        }

        internal int Read(float[] buffer, int offset, int sampleCount)
        {
            // CALL VST PROCESS HERE WITH BLOCK SIZE OF sampleCount
            float[] tempBuffer = ProcessReplace(sampleCount / 2);

            // Copying Vst buffer inside Audio buffer, no conversion needed for WaveProvider32
            for (int i = 0; i < sampleCount; i++)
                buffer[i + offset] = tempBuffer[i];

            return sampleCount;
        }

        internal void SetWaveFormat(int sampleRate, int channels)
        {
            this.waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var waveBuffer = new WaveBuffer(buffer);
            int samplesRequired = count / 4;
            int samplesRead = Read(waveBuffer.FloatBuffer, offset / 4, samplesRequired);
            return samplesRead * 4;
        }

        public override WaveFormat WaveFormat
        {
            get { return waveFormat; }
        }

        public override long Length
        {
            get { return long.MaxValue; }
        }

        public override long Position
        {
            get
            {
                return 0;
            }
            set
            {
                long x = value;
            }
        }
    }

    internal class VSTMidi
    {
        internal VstPluginContext PluginContext = null;
        internal List<VstEvent> MidiStack = new List<VstEvent>();
        internal event EventHandler<VSTStreamEventArgs> StreamCall = null;

        internal VSTMidi()
        {

        }

        public void MIDI_NoteOn(byte Note, byte Velocity)
        {
            byte Cmd = 0x90;
            MIDI(Cmd, Note, Velocity);
        }

        public void MIDI_NoteOff(byte Note, byte Velocity)
        {
            byte Cmd = 0x80;
            MIDI(Cmd, Note, Velocity);
        }

        public void MIDI_ProgramChange(byte programNumber)
        {
            byte Cmd = 0xC0; // Code de commande MIDI pour le changement de programme
            MIDI(Cmd, programNumber, 0); // La vélocité est généralement 0 pour un changement de programme
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
            MIDI(cmd, lsb, msb);
        }

        public void MIDI_Aftertouch(byte pressureValue)
        {
            // Assurez-vous que la valeur de la pression est dans la plage valide (0 à 127 inclus)
            if (pressureValue < 0 || pressureValue > 127)
            {
                throw new ArgumentOutOfRangeException("pressureValue", "La valeur de la pression doit être comprise entre 0 et 127.");
            }

            byte cmd = 0xD0; // Code de commande MIDI pour l'aftertouch (pression du canal)
            MIDI(cmd, pressureValue, 0); // Le deuxième paramètre est la valeur de pression, le troisième est souvent ignoré dans le cas de l'aftertouch
        }

        public void MIDI_PolyphonicAftertouch(byte note, byte pressureValue)
        {
            // Assurez-vous que la note et la valeur de la pression sont dans les plages valides (0 à 127 inclus)
            if (note < 0 || note > 127 || pressureValue < 0 || pressureValue > 127)
            {
                throw new ArgumentOutOfRangeException("note", "La note doit être comprise entre 0 et 127, et la valeur de la pression doit être comprise entre 0 et 127.");
            }

            byte cmd = 0xA0; // Code de commande MIDI pour le polyphonic aftertouch
            MIDI(cmd, note, pressureValue);
        }

        public void MIDI_CC(byte Number, byte Value)
        {
            byte Cmd = 0xB0;
            MIDI(Cmd, Number, Value);
        }

        private void MIDI(byte Cmd, byte Val1, byte Val2)
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

            lock (MidiStack) { MidiStack.Add(vse); }
        }

        internal void Stream_ProcessCalled(object sender, VSTStreamEventArgs e)
        {
            if (StreamCall != null) StreamCall(sender, e);
        }
    }

    public class VSTPlugin
    {
        public VSTHostInfo VSTHostInfo;
        internal VSTMidi VSTSynth;
        private VSTStream vstStream;
        public bool Loaded = false;
        public int Slot = 0;

        public VSTPlugin(int slot)
        {
            Slot = slot;
        }

        public VSTPlugin()
        {
            Slot = 1;
        }

        public string GetInfo()
        {
            if (vstStream != null && vstStream.outputBuffers != null)
            {
                return string.Concat(VSTHostInfo.VSTName + " : OUT Buffers : ", vstStream.outputBuffers.Length, " - Stream Length : ", vstStream.Length);
            }
            else
            {
                return string.Concat("VST Stream not initialized for " + VSTHostInfo.VSTName);
            }
        }

        public string LoadVST()
        {
            string sInfo = "";

            bool bDisposed = DisposeVST();

            if (bDisposed)
            {
                VSTSynth = new VSTMidi();

                var hcs = new HostCommandStub();

                try
                {
                    VSTSynth.PluginContext = VstPluginContext.Create(VSTHostInfo.VSTPath, hcs);
                    VSTSynth.PluginContext.PluginCommandStub.Commands.Open();
                    //VSTHostInfo.ParameterCount = VSTSynth.PluginContext.PluginInfo.ParameterCount;
                    
                    VSTHostInfo.PluginID = VSTSynth.PluginContext.PluginInfo.PluginID;
                    //VSTHostInfo.AudioInputs = VSTSynth.PluginContext.PluginInfo.AudioInputCount;
                    VSTHostInfo.AudioOutputs = VSTSynth.PluginContext.PluginInfo.AudioOutputCount;
                    VSTHostInfo.Slot = Slot;

                    vstStream = new VSTStream(VSTSynth);
                    vstStream.ProcessCalled += VSTSynth.Stream_ProcessCalled;
                    vstStream.pluginContext = VSTSynth.PluginContext;
                    vstStream.SetWaveFormat(VSTHostInfo.SampleRate, 2);

                    UtilityAudio.AudioMixer.AddInputStream(vstStream);

                    Loaded = true;

                    try
                    {
                        VSTHostInfo.VSTName = VSTSynth.PluginContext.PluginCommandStub.Commands.GetProductString();
                        if (VSTHostInfo.VSTName.Length == 0)
                        {
                            VSTHostInfo.VSTName = VSTSynth.PluginContext.PluginCommandStub.Commands.GetInputProperties(0).Label;
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
                            VSTSynth.PluginContext.PluginCommandStub.Commands.SetProgram(VSTHostInfo.Program);
                        }
                        catch (Exception ex)
                        {
                            sInfo = "VST Program can't be set : " + ex.Message;
                        }
                    }

                    if (VSTHostInfo.Parameters.Count > 0)
                    {
                        try
                        {
                            for (int iP = 0; iP < VSTHostInfo.Parameters.Count; iP++)
                            {
                                VSTSynth.PluginContext.PluginCommandStub.Commands.SetParameter(VSTHostInfo.Parameters[iP].Index, VSTHostInfo.Parameters[iP].Data);
                            }

                        }
                        catch (Exception ex)
                        {
                            sInfo = "VST Parameters can't be set : " + ex.Message;
                        }
                    }
                }
                catch (Exception ex)
                {
                    sInfo = "Unable to init VST : " + ex.Message;
                }
            }
            else { return "Unable to remove last VST"; }

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

        internal bool DisposeVST()
        {
            try
            {
                if (UtilityAudio.AudioMixer != null)
                {
                    UtilityAudio.AudioMixer.RemoveInputStream(vstStream);
                }

                if (VSTSynth != null && VSTSynth.PluginContext != null)
                {
                    VSTSynth.PluginContext.PluginCommandStub.Commands.StopProcess();
                    VSTSynth.PluginContext.PluginCommandStub.Commands.Close();
                    VSTSynth = null;
                }

                if (vstStream != null)
                {
                    vstStream.Dispose();
                    vstStream = null;
                }
                Loaded = false;
            }
            catch
            {
                Loaded = false;
                return false; 
            }

            return true;
        }

        public void GetParameters()
        {
            if (VSTSynth.PluginContext.PluginCommandStub != null)
            {
                var props = VSTSynth.PluginContext.PluginCommandStub.Commands.GetProgram();
                if (props != null)
                {
                    VSTHostInfo.Program = props;
                }

                VSTHostInfo.Parameters.Clear();

                int iParameters = VSTSynth.PluginContext.PluginInfo.ParameterCount;
                for (int i = 0; i < iParameters; i++)
                {
                    try
                    {
                        string propName = propName = VSTSynth.PluginContext.PluginCommandStub.Commands.GetParameterName(i);
                        float propData = VSTSynth.PluginContext.PluginCommandStub.Commands.GetParameter(i);
                        VSTHostInfo.Parameters.Add(new VSTParameter { Index = i, Name = propName, Data = propData });
                        //var properties = VSTSynth.PluginContext.PluginCommandStub.Commands.GetParameterProperties(i);

                        //if (properties != null)
                        //{
                        //    string propName = propName = VSTSynth.PluginContext.PluginCommandStub.Commands.GetParameterName(i);
                        //    var propData = VSTSynth.PluginContext.PluginCommandStub.Commands.GetParameter(i);
                        //    if (propName != null)
                        //    {
                        //        VSTHostInfo.ParameterNames.Add(propName);
                        //        VSTHostInfo.ParameterValues.Add(propData);
                        //    }
                        //    else {break; }
                        //}
                    }
                    catch { break; }
                }
            }
        }

        public bool OpenEditor(IntPtr EditorHandle)
        {
            if (VSTSynth != null && VSTSynth.PluginContext != null)
            {
                CloseEditor();
                return VSTSynth.PluginContext.PluginCommandStub.Commands.EditorOpen(EditorHandle);
            }
            else { return false; }
        }

        public void CloseEditor()
        {
            if (VSTSynth != null && VSTSynth.PluginContext != null)
            {
                VSTSynth.PluginContext.PluginCommandStub.Commands.EditorIdle();
                VSTSynth.PluginContext.PluginCommandStub.Commands.EditorClose();
            }
        }

        public void GetWindowSize(out Rectangle rect)
        {
            VSTSynth.PluginContext.PluginCommandStub.Commands.EditorGetRect(out rect);
        }

        public void SetSlot(int iSlot)
        {
            Slot = iSlot;
            if (VSTHostInfo != null) { VSTHostInfo.Slot = iSlot; }
        }
    }

    public static class UtilityAudio
    {
        public delegate void AudioEventHandler(string sMessage, string sDevice, int iSampleRate);
        public static event AudioEventHandler AudioEvent;

        private static Thread AudioThread;

        private static IWavePlayer AudioDevice;
        internal static RecordableMixerStream32 AudioMixer;
        public static bool AudioInitialized = false;
        public static string DeviceName = "";
        public static int DeviceSampleRate = 0;

        public static bool OpenAudio(string asioDevice, int iSampleRate)
        {
            DeviceName = asioDevice;
            DeviceSampleRate = iSampleRate;

            int iOK = 0;

            if (AudioDevice == null) //asio pas encore initialisé
            {
                AudioEvent?.Invoke("Opening Audio Driver", asioDevice, iSampleRate);
                AudioThread = new Thread(() =>
                {
                    StartSTA(asioDevice, iSampleRate, ref iOK);
                });

                AudioThread.SetApartmentState(ApartmentState.STA);
                AudioThread.Start();

                while (iOK == 0)
                {
                    Thread.Sleep(100);
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
                        var device = new AsioOut(asioDevice);
                        AudioDevice = device;
                        AudioEvent?.Invoke("ASIO Device Inputs : " + device.NumberOfInputChannels + " / Outputs : " + device.NumberOfOutputChannels + " / Latency : " + device.PlaybackLatency, asioDevice, iSampleRate);
                    }

                    if (AudioDevice == null)
                    {
                        var device = new WaveOut();
                        AudioDevice = device;
                        AudioEvent?.Invoke("WaveOut Device Latency : " + device.DesiredLatency + " / Buffers : " + device.NumberOfBuffers, asioDevice, iSampleRate);
                    }
                    if (AudioDevice == null)
                    {
                        iOK = 2;
                    }

                    if (iOK == 0)
                    {
                        AudioEvent?.Invoke("Mixer Info : Wave Format : " + AudioMixer.WaveFormat + " / Inputs : " + AudioMixer.InputCount, asioDevice, iSampleRate);
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
                AudioEvent?.Invoke("Starting Audio Driver", DeviceName, DeviceSampleRate);
                AudioDevice.Play();
                Thread.Sleep(100);
                AudioEvent?.Invoke("Audio Driver Initialized", DeviceName, DeviceSampleRate);
            }
        }

        public static void StopAudio()
        {
            AudioEvent?.Invoke("Stopping Audio Driver", DeviceName, DeviceSampleRate);
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
                    AudioEvent?.Invoke("Dispose Audio Driver", DeviceName, DeviceSampleRate);
                    AudioDevice.Dispose();
                }
                catch
                {
                }
                AudioDevice = null;
            }

            if (AudioMixer != null)
            {
                AudioEvent?.Invoke("Dispose Audio Mixer", DeviceName, DeviceSampleRate);
                AudioMixer.Dispose();
                AudioMixer = null;
            }

        }

    }
}
