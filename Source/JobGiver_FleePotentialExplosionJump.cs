
using Verse;
using RimWorld;
using EnemiesUseApparelToo.Utility;
using Verse.AI;


namespace EnemiesUseApparelToo
{
    public class JobGiver_FleePotentialExplosionJump : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if ((int)pawn.RaceProps.intelligence < 2)
            {
                return null;
            }
            if (pawn.mindState.knownExploder == null)
            {
                return null;
            }
            if (!pawn.mindState.knownExploder.Spawned)
            {
                pawn.mindState.knownExploder = null;
                return null;
            }
            if (pawn.Downed && !pawn.health.CanCrawl)
            {
                return null;
            }
            Ability ability = null;
            if (EnemiesUseApparelTooUtility.PawnHasJumpAbility(pawn, out ability) != true)
            {
                return null;
            }
            if (PawnUtility.PlayerForcedJobNowOrSoon(pawn))
            {
                return null;
            }
            Thing knownExploder = pawn.mindState.knownExploder;
            if ((float)(pawn.Position - knownExploder.Position).LengthHorizontalSquared > 81f)
            {
                return null;
            }
            if (!EnemiesUseApparelTooUtility.TryFindRelocatePosition(ability, pawn, out var result, ability.verb.EffectiveRange))
            {
                return null;
            }
            Job job = ability.GetJob(result, result);
            return job;
        }
    }
}