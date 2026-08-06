
using Verse;
using Verse.AI;
using RimWorld;

// *Uncomment for Harmony*
using HarmonyLib;

namespace EnemiesUseApparelToo
{

    [StaticConstructorOnStartup]
    public static class Start
    {
        static Start()
        {
            if (EnemiesUseApparelTooModSettings.UseHarmonyPatch)
            {
                new Harmony("EnemiesUseApparelToo").PatchAll();
            }

        }
    }

    [HarmonyPatch(typeof(JobGiver_AIFightEnemy), "GetAbilityJob")]
    public static class PatchGetAbilityJob
    {
        public static bool Prefix(ref Job __result, JobGiver_AIFightEnemy __instance, Pawn pawn, Thing enemyTarget)
        {
            if (EUATCombatVerbUtility.TryMakeSmartApparelJob(pawn, enemyTarget, out Job apparelJob))
            {
                __result = apparelJob;
                return false;
            }

            return true;
        }
    }

}