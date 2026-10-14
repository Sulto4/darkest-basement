using System;

namespace DarkestBasement.QA
{
    // Puncte de agățare pentru QA-ul automat, disponibile pentru toate echipele.
    // Moment("tag") cere o captură de ecran (dacă rulează -qa); altfel nu face nimic, deci se poate chema oricând.
    //
    // Echipa C, de exemplu:  QaHooks.Moment("combat_crit");  QaHooks.Moment("combat_hand");
    public static class QaHooks
    {
        public static bool Active { get; internal set; }
        public static event Action<string> MomentRequested;

        public static void Moment(string tag)
        {
            if (Active) MomentRequested?.Invoke(tag);
        }
    }
}
