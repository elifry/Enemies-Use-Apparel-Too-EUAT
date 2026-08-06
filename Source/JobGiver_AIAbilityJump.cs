
using UnityEngine;
using Verse;
using RimWorld;
using EnemiesUseApparelToo.Utility;
using Verse.AI;


namespace EnemiesUseApparelToo
{
    public class JobGiver_AIAbilityJump : JobGiver_AICastAbility
    {
        public float minDistToTarget = 10f;

        public float thresholdPercent = 0.50f;


        protected override Job TryGiveJob(Pawn pawn)
        {
            Ability ability = null;
            if (EnemiesUseApparelTooUtility.PawnHasJumpAbility(pawn, out ability) != true)
            {
                return null;
            }
            if (pawn.CurJob?.def == JobDefOf.CastJump)
            {
                return null;
            }
            if (ability == null || !ability.CanCast)
            {
                return null;
            }
            LocalTargetInfo target = GetTarget(pawn, ability);
            if (target.IsValid)
            {
                Job job = ability.GetJob(target, target);
                pawn.pather?.StopDead();
                return job;
            }

            return null;

        }

        public override ThinkNode DeepCopy(bool resolve = true)
        {
            JobGiver_AIAbilityJump obj = (JobGiver_AIAbilityJump)base.DeepCopy(resolve);
            obj.minDistToTarget = minDistToTarget;
            obj.thresholdPercent = thresholdPercent;
            return obj;
        }


        protected override LocalTargetInfo GetTarget(Pawn caster, Ability ability)
        {
            bool isMeleeAttack = caster.CurrentEffectiveVerb.IsMeleeAttack;
            float maxDist = minDistToTarget + ability.verb.EffectiveRange;
            if (!isMeleeAttack)
            {
                maxDist = maxDist + Mathf.Clamp(caster.CurrentEffectiveVerb.EffectiveRange * 0.66f, 2f, 20f);
            }

            Thing target = (Thing)AttackTargetFinder.BestAttackTarget(caster, TargetScanFlags.NeedLOSToAll | TargetScanFlags.NeedReachableIfCantHitFromMyPos | TargetScanFlags.NeedThreat | TargetScanFlags.NeedAutoTargetable, IsGoodTarget, 0f, maxDist, default(IntVec3), float.MaxValue, canBashDoors: true);

            if (target is Pawn && target != null)
            {
                caster.mindState.enemyTarget = target;
            }

            if (EnemiesUseApparelTooUtility.AiFindJumpCell(ability, target, thresholdPercent, minDistToTarget, out var destination))
            {
                return new LocalTargetInfo(destination);
            }

            return LocalTargetInfo.Invalid;
        }



        protected virtual bool IsGoodTarget(Thing thing)
        {
            if (thing is Pawn { Spawned: not false, Downed: false } pawn)
            {
                return !pawn.IsPsychologicallyInvisible();
            }
            return false;
        }


    }
}