using Verse;
using RimWorld;

namespace ProgressionPacing
{
    [StaticConstructorOnStartup]
    public static class Startup
    {
        static Startup()
        {
            ProgressionPacingModSettings.UpdateResearchProjectCosts();
            ProgressionPacingModSettings.UpdateQuestPacing();
        }
    }
}
