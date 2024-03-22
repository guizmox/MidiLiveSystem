using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using TobiasErichsen.teVirtualMIDI;
using System.Xml;

namespace MidiTools
{
    public static class VirtualMidiCable
    {
        public delegate void VirtualMidiCableExceptionHandler(string sCableName, string sMessage);
        public static event VirtualMidiCableExceptionHandler VirtualMidiCableException;

        private static string PortName = "MIDI Live System Virtual Port";

        private static TeVirtualMIDI VirtualMidiPort;
        private static bool StopVirtualMidiPort = false;
        private static bool MidiProcessing = false;

        public static void InitVirtualMidiPort()
        {
            TeVirtualMIDI.logging(TeVirtualMIDI.TE_VM_LOGGING_MISC | TeVirtualMIDI.TE_VM_LOGGING_RX | TeVirtualMIDI.TE_VM_LOGGING_TX);

            Guid manufacturer = new Guid("aa4e075f-3504-4aab-9b06-9a4104a91cf0");
            Guid product = new Guid("bb4e075f-3504-4aab-9b06-9a4104a91cf0");

            VirtualMidiPort = new TeVirtualMIDI(PortName, 65535, TeVirtualMIDI.TE_VM_FLAGS_PARSE_RX, ref manufacturer, ref product);
            Thread thread = new Thread(new ThreadStart(VirtualMIDICableRouting));
            thread.Start();
        }

        private static void VirtualMIDICableRouting()
        {
            try
            {
                while (true)
                {
                    byte[] command = VirtualMidiPort.getCommand();

                    VirtualMidiPort.sendCommand(command);
                }
            }
            catch (Exception ex)
            {
                VirtualMidiCableException?.Invoke(PortName, ex.Message);
            }
        }


        public static async Task CloseVirtualMidiPort()
        {
            await UIEventPool.AddTask(() =>
            {
                VirtualMidiPort.shutdown();
            });
        }

    }
}
