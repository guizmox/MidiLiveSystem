using Jacobi.Vst.Core.Host;
using Jacobi.Vst.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jacobi.Vst.Plugin.Framework.Plugin;

namespace MidiTools
{
    internal sealed class PluginCommandStub : IVstPluginCommandStub
    {
        public IVstPluginContext PluginContext { get; set; }

        public IVstPluginCommands24 Commands { get; private set; }

        public PluginCommandStub(VstPluginContext context)
        {
            Commands = new VstPluginCommands(context);
        }


        public static int ProcessEvents(VstEvent[] events)
        {
            // Gérer les événements MIDI ici
            foreach (VstEvent vstEvent in events)
            {
                if (vstEvent.EventType == VstEventTypes.MidiEvent)
                {
                    VstMidiEvent midiEvent = vstEvent as VstMidiEvent;
                    // Traiter l'événement MIDI selon vos besoins
                }
            }

            return 0; // Indiquer que les événements ont été traités avec succès
        }
    }
}
