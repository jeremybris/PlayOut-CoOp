// Modified for PlayOut Co-Op, 2026-11-06 (GPL-3 s.5a): headless changes (dialogs/OSD through Headless.EngineUI,
// configurable install root). See README.md "Changes from Nucleus Co-op". Original: Nucleus Co-op Master/NucleusGaming/Forms/ShortcutsReminder/ReminderFormThread.cs
using Nucleus.Coop.Forms;
using System.Drawing;
using System.Threading;

namespace Nucleus.Gaming.Forms
{
    public static class ReminderFormThread
    {
        public static void StartReminderForms(Rectangle destBounds)
        {
            // PlayOut Co-Op: the shortcut reminder overlays are Nucleus UI (they need the gui theme assets); not shown headless.
            if (Nucleus.Gaming.Headless.EngineUI.Headless)
            {
                return;
            }

            Thread backgroundFormThread = new Thread(delegate ()
            {
                ShortcutsReminder reminder = new ShortcutsReminder(destBounds);
                GenericGameHandler.Instance.shortcutsReminders.Add(reminder);
                System.Windows.Threading.Dispatcher.Run();
            });

            backgroundFormThread.SetApartmentState(ApartmentState.STA); // needs to be STA or throws exception
            backgroundFormThread.Start();
        }
    }
}
