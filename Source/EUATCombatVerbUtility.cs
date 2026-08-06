using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace EnemiesUseApparelToo
{
    public static class EUATCombatVerbUtility
    {
        private const float ExtraHostileTargetScore = 20f;
        private const float ExplosiveTargetScore = 30f;
        private const float MinimumOpportunityScore = 20f;
        private const float MinimumTargetSwitchOpportunityScore = 60f;

        private static readonly MethodInfo GetShootingTargetScoreMethod = AccessTools.Method(
            typeof(AttackTargetFinder),
            "GetShootingTargetScore",
            new[] { typeof(IAttackTarget), typeof(IAttackTargetSearcher), typeof(Verb) });

        public static bool TryMakeSmartApparelJob(Pawn pawn, Thing enemyTarget, out Job job)
        {
            job = null;

            if (pawn?.apparel == null || enemyTarget == null || pawn.Map == null)
            {
                return false;
            }

            ApparelVerbEvaluation bestSmartEvaluation = ApparelVerbEvaluation.Invalid;
            ApparelVerbEvaluation originalEvaluation = ApparelVerbEvaluation.Invalid;
            foreach (Verb verb in pawn.apparel.AllApparelVerbs)
            {
                if (!CanConsiderApparelVerb(verb))
                {
                    continue;
                }

                if (!originalEvaluation.IsValid && TryEvaluateOriginalApparelVerb(verb, enemyTarget, out ApparelVerbEvaluation fallbackEvaluation))
                {
                    originalEvaluation = fallbackEvaluation;
                }

                if (TryEvaluateAoeApparelVerb(pawn, enemyTarget, verb, out ApparelVerbEvaluation smartEvaluation) &&
                    smartEvaluation.Score > bestSmartEvaluation.Score)
                {
                    bestSmartEvaluation = smartEvaluation;
                }
            }

            ApparelVerbEvaluation bestEvaluation = bestSmartEvaluation.IsValid ? bestSmartEvaluation : originalEvaluation;
            if (!bestEvaluation.IsValid)
            {
                return false;
            }

            job = MakeApparelVerbJob(bestEvaluation);
            return job != null;
        }

        private static bool CanConsiderApparelVerb(Verb verb)
        {
            return verb != null &&
                verb.Available() &&
                verb.verbProps?.violent == true;
        }

        private static bool TryEvaluateOriginalApparelVerb(Verb verb, Thing enemyTarget, out ApparelVerbEvaluation evaluation)
        {
            evaluation = ApparelVerbEvaluation.Invalid;

            if (!verb.CanHitTarget(enemyTarget))
            {
                return false;
            }

            evaluation = new ApparelVerbEvaluation(verb, enemyTarget, 0f);
            return true;
        }

        private static bool TryEvaluateAoeApparelVerb(Pawn pawn, Thing enemyTarget, Verb verb, out ApparelVerbEvaluation bestEvaluation)
        {
            bestEvaluation = ApparelVerbEvaluation.Invalid;

            if (!TryGetExplosionRadius(verb, out float radius))
            {
                return false;
            }

            IAttackTarget currentTarget = enemyTarget as IAttackTarget;
            if (currentTarget != null)
            {
                TryEvaluateTarget(pawn, verb, currentTarget, radius, MinimumOpportunityScore, ref bestEvaluation);
            }

            foreach (IAttackTarget attackTarget in pawn.Map.attackTargetsCache.GetPotentialTargetsFor(pawn))
            {
                if (attackTarget == currentTarget)
                {
                    continue;
                }

                TryEvaluateTarget(pawn, verb, attackTarget, radius, MinimumTargetSwitchOpportunityScore, ref bestEvaluation);
            }

            return bestEvaluation.IsValid;
        }

        private static void TryEvaluateTarget(
            Pawn pawn,
            Verb verb,
            IAttackTarget attackTarget,
            float radius,
            float minimumOpportunityScore,
            ref ApparelVerbEvaluation bestEvaluation)
        {
            Thing targetThing = attackTarget.Thing;
            if (targetThing == null || targetThing.Destroyed || targetThing.Map != pawn.Map || attackTarget.ThreatDisabled(pawn))
            {
                return;
            }

            if (!TryGetCastTarget(verb, targetThing, out LocalTargetInfo castTarget))
            {
                return;
            }

            float opportunityScore = GetAoeOpportunityScore(pawn, castTarget.Cell, radius);
            if (opportunityScore < minimumOpportunityScore)
            {
                return;
            }

            float score = GetVanillaShootingTargetScore(attackTarget, pawn, verb) + opportunityScore;
            if (score > bestEvaluation.Score)
            {
                bestEvaluation = new ApparelVerbEvaluation(verb, castTarget, score);
            }
        }

        private static bool TryGetCastTarget(Verb verb, Thing targetThing, out LocalTargetInfo castTarget)
        {
            LocalTargetInfo thingTarget = targetThing;
            if (CanUseTarget(verb, thingTarget))
            {
                castTarget = thingTarget;
                return true;
            }

            LocalTargetInfo cellTarget = targetThing.Position;
            if (CanUseTarget(verb, cellTarget))
            {
                castTarget = cellTarget;
                return true;
            }

            castTarget = LocalTargetInfo.Invalid;
            return false;
        }

        private static bool CanUseTarget(Verb verb, LocalTargetInfo target)
        {
            return target.IsValid &&
                verb.CanHitTarget(target) &&
                verb.ValidateTarget(target, showMessages: false);
        }

        private static float GetAoeOpportunityScore(Pawn pawn, IntVec3 center, float radius)
        {
            int hostileTargets = CountHostilesInRadius(pawn, center, radius);
            int explosives = CountExplosivesInRadius(pawn, center, radius);
            int extraHostileTargets = Math.Max(0, hostileTargets - 1);
            return extraHostileTargets * ExtraHostileTargetScore + explosives * ExplosiveTargetScore;
        }

        private static int CountHostilesInRadius(Pawn pawn, IntVec3 center, float radius)
        {
            int hostileTargets = 0;
            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(center, pawn.Map, radius, useCenter: true))
            {
                if (thing is Pawn targetPawn && IsHostileAoePawn(pawn, targetPawn))
                {
                    hostileTargets++;
                }
            }

            return hostileTargets;
        }

        private static int CountExplosivesInRadius(Pawn pawn, IntVec3 center, float radius)
        {
            int explosives = 0;
            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(center, pawn.Map, radius, useCenter: true))
            {
                if (IsExplosiveOpportunity(pawn, thing))
                {
                    explosives++;
                }
            }

            return explosives;
        }

        private static bool IsHostileAoePawn(Pawn pawn, Pawn targetPawn)
        {
            return targetPawn.Spawned &&
                !targetPawn.Downed &&
                targetPawn.HostileTo(pawn);
        }

        private static bool IsExplosiveOpportunity(Pawn pawn, Thing thing)
        {
            if (thing == null || thing.Destroyed || thing.Faction == pawn.Faction)
            {
                return false;
            }

            if (thing is ThingWithComps thingWithComps && thingWithComps.GetComp<CompExplosive>() != null)
            {
                return true;
            }

            return thing.def?.comps?.Any(comp => comp is CompProperties_Explosive) == true;
        }

        private static float GetVanillaShootingTargetScore(IAttackTarget target, Pawn pawn, Verb verb)
        {
            if (target == null || GetShootingTargetScoreMethod == null)
            {
                return 0f;
            }

            try
            {
                return (float)GetShootingTargetScoreMethod.Invoke(null, new object[] { target, pawn, verb });
            }
            catch
            {
                return 0f;
            }
        }

        private static bool TryGetExplosionRadius(Verb verb, out float radius)
        {
            radius = verb.verbProps?.defaultProjectile?.projectile?.explosionRadius ?? 0f;
            if (radius > 0f)
            {
                return true;
            }

            radius = verb.verbProps?.ai_AvoidFriendlyFireRadius ?? 0f;
            return radius > 0f;
        }

        private static Job MakeApparelVerbJob(ApparelVerbEvaluation evaluation)
        {
            Job job = JobMaker.MakeJob(JobDefOf.UseVerbOnThing, evaluation.Target);
            job.verbToUse = evaluation.Verb;
            job.maxNumStaticAttacks = 1;
            job.expiryInterval = 2000;
            job.endIfCantShootTargetFromCurPos = true;
            job.endIfCantShootInMelee = true;
            return job;
        }

        private readonly struct ApparelVerbEvaluation
        {
            public static readonly ApparelVerbEvaluation Invalid = new ApparelVerbEvaluation(null, LocalTargetInfo.Invalid, float.MinValue);

            public ApparelVerbEvaluation(Verb verb, LocalTargetInfo target, float score)
            {
                Verb = verb;
                Target = target;
                Score = score;
            }

            public Verb Verb { get; }
            public LocalTargetInfo Target { get; }
            public float Score { get; }
            public bool IsValid => Verb != null && Target.IsValid;
        }
    }
}
