using Jacobi.Vst.Core;
using Jacobi.Vst.Host.Interop;
using Jacobi.Vst.Plugin.Framework;
using Jacobi.Vst.Samples.Host;
using MessagePack;
using MidiTools;
using NAudio.CoreAudioApi;
using NAudio.Midi;
using NAudio.Wave;
using RtMidi.Core.Enums;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

// Copied from the microDRUM project
// https://github.com/microDRUM
// I think it is created by massimo.bernava@gmail.com
// Modified by perivar@nerseth.com
namespace VSTHost
{
    [MessagePackObject]
    [Serializable]
    public class VSTParameter
    {
        [Key("Index")]
        public int Index;
        [Key("Data")]
        public float Data;
        [Key("Name")]
        public string Name;
    }

    [MessagePackObject]
    [Serializable]
    public class VSTHostInfo
    {
        [Key("ChannelPrograms")]
        public string[] ChannelPrograms = new string[16] { "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "" };

        [Key("VSTHostGuid")]
        public Guid VSTHostGuid { get; set; } = Guid.Empty;

        [Key("SynthID")]
        public int SynthID { get; set; } = 0;

        [Key("AsioDevice")]
        public string AsioDevice { get; set; } = "";

        [Key("SampleRate")]
        public int SampleRate { get; set; } = 48000;

        [Key("VSTPath")]
        public string VSTPath { get; set; } = "";

        [Key("Error")]
        public string Error { get; set; } = "";

        [Key("VSTName")]
        public string VSTName { get; set; } = "";

        [Key("Program")]
        public int Program { get; set; } = -1;

        [Key("Parameters")]
        public List<VSTParameter> Parameters { get; set; } = new List<VSTParameter>();

        [Key("PluginID")]
        public int PluginID { get; set; } = 0;

        [Key("AudioOutputs")]
        public int AudioOutputs { get; set; } = 0;

        [Key("Slot")]
        public int Slot { get; set; } = 1;

        [Key("MidiInputs")]
        public int MidiInputs { get; set; } = 1;

        [Key("Dump")]
        public byte[] Dump { get; set; }

        public string GetInfo()
        {
            StringBuilder sbPlugin = new();
            sbPlugin.AppendLine("Plugin Name : " + VSTName);
            sbPlugin.AppendLine("Path : " + VSTPath);
            sbPlugin.AppendLine("Current Program : " + Program.ToString());
            sbPlugin.AppendLine("Parameters : " + Parameters.Count);
            sbPlugin.AppendLine("Audio OUT : " + AudioOutputs);
            sbPlugin.AppendLine("Midi IN : " + MidiInputs);
            return sbPlugin.ToString();
        }
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
        private readonly VSTMidi VSTSynth;

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
            ProcessCalled?.Invoke(this, new VSTStreamEventArgs(maxL, maxR));
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
            pluginContext.PluginCommandStub.Commands.MainsChanged(true);

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
                //pluginContext.PluginCommandStub.Commands.MainsChanged(true);
                pluginContext.PluginCommandStub.Commands.StartProcess();

                ProcessVSTMidiEvents();

                pluginContext.PluginCommandStub.Commands.ProcessReplacing(inputBuffers, outputBuffers);
                pluginContext.PluginCommandStub.Commands.StopProcess();
                //pluginContext.PluginCommandStub.Commands.MainsChanged(false);
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

        private bool ProcessVSTMidiEvents()
        {
            lock (VSTSynth.MidiStack)
            {
                if (VSTSynth.MidiStack.Count > 0)
                {
                    pluginContext.PluginCommandStub.Commands.ProcessEvents(VSTSynth.MidiStack.ToArray());
                    VSTSynth.MidiStack.Clear();
                    return true;
                }
            }
            return false;
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
        internal List<VstEvent> MidiStack = new();
        //internal event EventHandler<VSTStreamEventArgs> StreamCall = null;

        internal VSTMidi()
        {
        }

        public void MIDI_Clock()
        {           
            const int midiClockMessage = 0xF8; // MIDI Clock message type

            // Create MIDI Clock message
            MemoryStream message = new();
            BinaryWriter bw = new(message);
            bw.Write((byte)midiClockMessage);

            // Create VstMidiEvent
            VstMidiEvent vstMEvent = new(0, 0, 0, message.ToArray(), 0, 0, true);

            // Add MIDI event to stack or send directly to plugin
            lock (MidiStack)
            {
                MidiStack.Add(vstMEvent);
            }
        }

        public void MIDI_NoteOn(int Note, int Velocity, int iChannel)
        {
            MemoryStream message = new();
            BinaryWriter bw = new(message);
            bw.Write(MidiMessage.StartNote(Note, Velocity, iChannel).RawData);
            VstMidiEvent vstMEvent = new(0, 0, 0, message.ToArray(), 0, 0, true);
            lock (MidiStack) { MidiStack.Add(vstMEvent); }
        }

        public void MIDI_NoteOff(int Note, int Velocity, int iChannel)
        {
            MemoryStream message = new();
            BinaryWriter bw = new(message);
            bw.Write(MidiMessage.StopNote(Note, Velocity, iChannel).RawData);
            VstMidiEvent vstMEvent = new(0, 0, 0, message.ToArray(), 0, 0, true);
            lock (MidiStack) { MidiStack.Add(vstMEvent); }
        }

        public void MIDI_ProgramChange(int programNumber, int iChannel)
        {
            MemoryStream message = new();
            BinaryWriter bw = new(message);
            bw.Write(MidiMessage.ChangePatch(programNumber, iChannel).RawData);
            VstMidiEvent vstMEvent = new(0, 0, 0, message.ToArray(), 0, 0, true);
            lock (MidiStack) { MidiStack.Add(vstMEvent); }

            //byte Cmd = 0xC0; // Code de commande MIDI pour le changement de programme
            //MIDI(Cmd, programNumber, 0); // La vélocité est généralement 0 pour un changement de programme
        }

        public void MIDI_PitchBend(int pitchValue, int iChannel)
        {
            MemoryStream message = new();
            BinaryWriter bw = new(message);
            ushort pitchBendValue = (ushort)pitchValue;
            bw.Write((byte)(0xE0 | iChannel - 1)); // Status byte pour Pitch Bend (0xE0 est le status byte pour le message Pitch Bend)
            bw.Write((byte)(pitchBendValue & 0x7F)); // LSB (Least Significant Byte) du pitch bend value
            bw.Write((byte)((pitchBendValue >> 7) & 0x7F)); // MSB (Most Significant Byte) du pitch bend value
            VstMidiEvent vstMEvent = new(0, 0, 0, message.ToArray(), 0, 0, true);
            lock (MidiStack) { MidiStack.Add(vstMEvent); }
        }

        public void MIDI_Aftertouch(byte pressureValue, int iChannel)
        {
            MemoryStream message = new();
            BinaryWriter bw = new(message);
            bw.Write((byte)(0xD0 | (iChannel - 1)));
            bw.Write((byte)(pressureValue & 0x7F));
            VstMidiEvent vstMEvent = new(0, 0, 0, message.ToArray(), 0, 0, true);
            lock (MidiStack) { MidiStack.Add(vstMEvent); }
        }

        public void MIDI_PolyphonicAftertouch(byte note, byte pressureValue, int iChannel)
        {
            MemoryStream message = new();
            BinaryWriter bw = new(message);
            byte noteNumber = note;
            byte aftertouchValue = pressureValue;
            bw.Write((byte)(0xA0 | iChannel - 1)); // Status byte pour Polyphonic Aftertouch (0xA0 est le status byte pour le message Polyphonic Aftertouch)
            bw.Write(noteNumber); // Numéro de la note
            bw.Write(aftertouchValue); // Valeur de l'Aftertouch (Aftertouch Pressure)
            VstMidiEvent vstMEvent = new(0, 0, 0, message.ToArray(), 0, 0, true);
            lock (MidiStack) { MidiStack.Add(vstMEvent); }
        }

        public void MIDI_CC(int Number, int Value, int iChannel)
        {
            MemoryStream message = new();
            BinaryWriter bw = new(message);
            bw.Write(MidiMessage.ChangeControl(Number, Value, iChannel).RawData);
            VstMidiEvent vstMEvent = new(0, 0, 0, message.ToArray(), 0, 0, true);
            lock (MidiStack) { MidiStack.Add(vstMEvent); }
            //byte Cmd = 0xB0;
            //MIDI(Cmd, Number, Value);
        }

        public void MIDI_NRPN(int parameterNumber, int value)
        {
            const byte nrpnControlChange = 0xB0; // Control Change message type with channel
            const byte nrpnMSB = 0x63; // NRPN MSB (Most Significant Byte)
            const byte nrpnLSB = 0x62; // NRPN LSB (Least Significant Byte)

            // Create NRPN message
            MemoryStream message = new();
            BinaryWriter bw = new(message);
            bw.Write(nrpnControlChange); // Control Change message type
            bw.Write(nrpnMSB); // NRPN MSB
            bw.Write(parameterNumber >> 7); // MSB of parameter number
            bw.Write(nrpnLSB); // NRPN LSB
            bw.Write(parameterNumber & 0x7F); // LSB of parameter number
            bw.Write(value >> 7); // MSB of value
            bw.Write(value & 0x7F); // LSB of value

            // Create VstMidiEvent
            VstMidiEvent vstMEvent = new(0, 0, 0, message.ToArray(), 0, 0, true);

            // Add MIDI event to stack or send directly to plugin
            lock (MidiStack)
            {
                MidiStack.Add(vstMEvent);
            }
        }

        public void MIDI_Sysex(byte[] sysExData)
        {
            const byte sysExStart = 0xF0; // SysEx start byte
            const byte sysExEnd = 0xF7; // SysEx end byte

            // Create SysEx message
            MemoryStream message = new();
            BinaryWriter bw = new(message);
            bw.Write(sysExStart); // Start of SysEx message
            bw.Write(sysExData); // SysEx data
            bw.Write(sysExEnd); // End of SysEx message

            // Create VstMidiEvent
            VstMidiEvent vstMEvent = new(0, 0, 0, message.ToArray(), 0, 0, true);

            // Add MIDI event to stack or send directly to plugin
            lock (MidiStack)
            {
                MidiStack.Add(vstMEvent);
            }
        }

        //internal void Stream_ProcessCalled(object sender, VSTStreamEventArgs e)
        //{
        //    if (StreamCall != null) StreamCall(sender, e);
        //}
    }

    public class VSTPlugin
    {
        private System.Timers.Timer TimerProgramName;

        public delegate void VSTEventHandler(string sMessage);
        public event VSTEventHandler VSTEvent;

        public VSTHostInfo VSTHostInfo;
        internal VSTMidi VSTSynth;
        private VSTStream vstStream;
        public bool Loaded = false;
        public int Slot = 0;

        private bool EditorOpen = false;

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
            if (VSTSynth != null && vstStream != null && vstStream.outputBuffers != null)
            {
                return string.Concat(VSTHostInfo.VSTName + " : Audio Buffers : ", vstStream.outputBuffers.Length, " - Channels : ", (UtilityAudio.AudioMixer.InputCount * 2).ToString(), " - Latency : ", VSTSynth.PluginContext.HostCommandStub.Commands.GetOutputLatency());
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

                HostCommandStub hcs = new(VSTHostInfo.SampleRate);
                hcs.ChangeTempo(120);
                //hcs.PluginCalled += new EventHandler<PluginCalledEventArgs>(HostCmdStub_PluginCalled);
                //var timeInfo = hcs.Commands.GetTimeInfo(VstTimeInfoFlags.ClockValid);
                //timeInfo.

                try
                {
                    VSTEvent?.Invoke("Loading VST instrument " + Path.GetFileName(VSTHostInfo.VSTPath));
                    VSTSynth.PluginContext = VstPluginContext.Create(VSTHostInfo.VSTPath, hcs);
                    //var pcs = new PluginCommandStub(new Jacobi.Vst.Plugin.Framework.Plugin.VstPluginContext());
                    VSTSynth.PluginContext.PluginCommandStub.Commands.Open();
                    //VSTSynth.PluginContext.PluginCommandStub.Commands.ProcessEvents(null);
                    //VSTSynth.PluginContext.PluginCommandStub.Commands.SetBlockSize(1024);
                    //VSTSynth.PluginContext.AcceptPluginInfoData(true);
                    //VSTSynth.PluginContext.PluginCommandStub.Commands.MainsChanged(true);
                    VSTSynth.PluginContext.PluginCommandStub.Commands.SetBypass(false);
                    //VSTSynth.PluginContext.PluginCommandStub.Commands.
                    //VSTHostInfo.ParameterCount = VSTSynth.PluginContext.PluginInfo.ParameterCount;

                    VSTEvent?.Invoke("Loading VST information " + VSTHostInfo.VSTName);

                    VSTHostInfo.PluginID = VSTSynth.PluginContext.PluginInfo.PluginID;
                    //VSTHostInfo.AudioInputs = VSTSynth.PluginContext.PluginInfo.AudioInputCount;
                    VSTHostInfo.AudioOutputs = VSTSynth.PluginContext.PluginInfo.AudioOutputCount;
                    VSTHostInfo.MidiInputs = VSTSynth.PluginContext.PluginCommandStub.Commands.GetNumberOfMidiInputChannels();
                    if (VSTHostInfo.MidiInputs == 0) { VSTHostInfo.MidiInputs = 1; } //mono channel

                    Slot = VSTHostInfo.Slot;

                    vstStream = new VSTStream(VSTSynth)
                    {
                        //vstStream.ProcessCalled += VSTSynth.Stream_ProcessCalled;
                        pluginContext = VSTSynth.PluginContext
                    };
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

                    LoadVSTParameters();

                    sInfo = LoadVSTProgram();

                    TimerProgramName = new System.Timers.Timer
                    {
                        Interval = 5000,
                        Enabled = true
                    };
                    TimerProgramName.Elapsed += TimerProgramName_Elapsed;
                    TimerProgramName.Start();

                    VSTEvent?.Invoke("VST Loaded");
                }
                catch (Exception ex)
                {
                    sInfo += "Unable to init VST : " + ex.Message;
                }
            }
            else { return "Unable to remove last VST"; }

            return sInfo;
        }

        private void TimerProgramName_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            GetProgramName();
        }

        public string LoadVSTProgram()
        {
            if (VSTHostInfo.Program > -1)
            {
                try
                {
                    VSTSynth.PluginContext.PluginCommandStub.Commands.BeginSetProgram();
                    VSTSynth.PluginContext.PluginCommandStub.Commands.SetProgram(VSTHostInfo.Program);
                    VSTSynth.PluginContext.PluginCommandStub.Commands.EndSetProgram();
                    return "Program Set.";
                }
                catch (Exception ex)
                {
                    return "VST Program can't be set : " + ex.Message;
                }
            }
            else return "No Program";
        }

        public void LoadVSTParameters()
        {

            VSTEvent?.Invoke("Loading VST parameters (" + VSTHostInfo.Parameters.Count + ")");
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
                    VSTHostInfo.Error = "VST Parameters can't be set : " + ex.Message;
                }
            }

            VSTEvent?.Invoke("Loading Memory Dump (" + (VSTHostInfo.Dump != null ? VSTHostInfo.Dump.Length : 0) + " byte(s))");
            if (VSTHostInfo.Dump != null)
            {
                VSTSynth.PluginContext.PluginCommandStub.Commands.SetChunk(VSTHostInfo.Dump, true);
            }
        }

        //private void HostCmdStub_PluginCalled(object sender, PluginCalledEventArgs e)
        //{
        //    if (e.Message.StartsWith("GetTimeInfo"))
        //    {

        //    }
        //}

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
                if (TimerProgramName != null)
                {
                    TimerProgramName.Elapsed -= TimerProgramName_Elapsed;
                    TimerProgramName.Stop();
                    TimerProgramName.Enabled = false;
                    TimerProgramName = null;
                }

                if (VSTSynth != null && VSTSynth.PluginContext != null)
                {
                    VSTSynth.PluginContext.PluginCommandStub.Commands.MainsChanged(false);
                    VSTSynth.PluginContext.PluginCommandStub.Commands.StopProcess();
                }
                vstStream?.Close();

                UtilityAudio.AudioMixer?.RemoveInputStream(vstStream);

                if (vstStream != null)
                {
                    vstStream.Dispose();
                    vstStream = null;
                }

                if (VSTSynth != null && VSTSynth.PluginContext != null)
                {
                    GetParameters();
                    VSTSynth.PluginContext.PluginCommandStub.Commands.Close();
                    VSTSynth = null;
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

        internal void GetParameters()
        {
            if (VSTSynth.PluginContext.PluginCommandStub != null)
            {
                var props = VSTSynth.PluginContext.PluginCommandStub.Commands.GetProgram();
                VSTHostInfo.Program = props;

                VSTHostInfo.Parameters.Clear();

                var dump = VSTSynth.PluginContext.PluginCommandStub.Commands.GetChunk(true);
                if (dump != null)
                {
                    if (VSTHostInfo.Dump != null) { Array.Clear(VSTHostInfo.Dump); }
                    VSTHostInfo.Dump = dump;
                }

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

                        //}
                        //VSTSynth.PluginContext.PluginCommandStub.Commands.
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
            bool bOK = false;

            if (VSTSynth != null && VSTSynth.PluginContext != null)
            {
                if (EditorOpen)
                {
                    CloseEditor();
                }

                VSTEvent?.Invoke("Opening VST editor");
                bOK = VSTSynth.PluginContext.PluginCommandStub.Commands.EditorOpen(EditorHandle);
                EditorOpen = true;

            }
            else { bOK = false; }

            return bOK;
        }

        public void CloseEditor()
        {
            if (VSTSynth != null && VSTSynth.PluginContext != null)
            {
                VSTEvent?.Invoke("Closing VST editor");

                VSTSynth.PluginContext.PluginCommandStub.Commands.EditorIdle();
                VSTSynth.PluginContext.PluginCommandStub.Commands.EditorClose();
                EditorOpen = false;
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

        public void GetProgramName()
        {
            if (VSTSynth != null && VSTSynth.PluginContext != null && VSTHostInfo != null)
            {
                try
                {
                    int iChannels = VSTSynth.PluginContext.PluginCommandStub.Commands.GetNumberOfMidiInputChannels();
                    if (iChannels == 0)
                    {
                        VSTHostInfo.ChannelPrograms[0] = VSTSynth.PluginContext.PluginCommandStub.Commands.GetProgramName();
                    }
                    else
                    {
                        for (int i = 0; i < iChannels; i++)
                        {
                            VstMidiProgramName name = new();
                            int index = VSTSynth.PluginContext.PluginCommandStub.Commands.GetMidiProgramName(name, i);
                            if (name.Name.Length == 0)
                            {
                                VSTHostInfo.ChannelPrograms[i] = VSTSynth.PluginContext.PluginCommandStub.Commands.GetProgramNameIndexed(i);
                            }
                            else
                            {
                                VSTHostInfo.ChannelPrograms[i] = name.Name;
                            }
                        }
                    }
                }
                catch
                {
                }
            }
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

            return iOK == 1;
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
                    AudioMixer = new RecordableMixerStream32(iSampleRate)
                    {
                        AutoStop = false
                    };

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
            AudioDevice?.Stop();
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
