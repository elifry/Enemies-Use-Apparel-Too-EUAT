using System;
using System.Collections.Generic;
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

        private static readonly MethodInfo GetShootingTargetScoreMethod = AccessTools.Method(
            typeof(AttackTargetFinder),
            "GetShootingTargetScore",
            new[] { typeof(IAttackTarget), typeof(IAttackTargetSearcher), typeof(Verb) });

        private static readonly MethodInfo ValidAoeAffectedTargetMethod = AccessTools.Method(
            typeof(Ability),
            "ValidAOEAffectedTarget",
            new[] { typeof(Thing) });

        public static bool TryMakeSmartApparelJob(Pawn pawn, Thing enemyTarget, out Job job)
        {
            job = null;

            if (pawn?.apparel == null || enemyTarget == null || pawn.Map == null)
            {
                return false;
            }

            ApparelVerbEvaluation bestEvaluation = ApparelVerbEvaluation.Invalid;
            foreach (Verb verb in pawn.apparel.AllApparelVerbs)
            {
                if (!CanConsiderApparelVerb(verb))
                {
                    continue;
                }

                if (TryEvaluateApparelVerb(pawn, enemyTarget, verb, out ApparelVerbEvaluation evaluation) &&
                    evaluation.Score > bestEvaluation.Score)
                {
                    bestEvaluation = evaluation;
                }
            }

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
                verb.verbProps?.violent == true &&
                TryGetAbility(verb, out Ability ability) &&
                ability.CanCast &&
                TryGetExplosionRadius(ability, out float radius) &&
                radius > 0f;
        }

        private static bool TryEvaluateApparelVerb(Pawn pawn, Thing enemyTarget, Verb verb, out ApparelVerbEvaluation bestEvaluation)
        {
            bestEvaluation = ApparelVerbEvaluation.Invalid;

            if (!TryGetAbility(verb, out Ability ability) || !TryGetExplosionRadius(ability, out float radius))
            {
                return false;
            }

            foreach (IAttackTarget attackTarget in GetPotentialTargets(pawn, enemyTarget))
            {
                Thing targetThing = attackTarget.Thing;
                if (targetThing == null || targetThing.Destroyed || targetThing.Map != pawn.Map)
                {
                    continue;
                }

                if (!TryGetCastTarget(ability, verb, targetThing, out LocalTargetInfo castTarget))
                {
                    continue;
                }

                float opportunityScore = GetAoeOpportunityScore(pawn, ability, targetThing.Position, radius);
                if (opportunityScore < MinimumOpportunityScore)
                {
                    continue;
                }

                float score = GetVanillaShootingTargetScore(attackTarget, pawn, verb, radius) + opportunityScore;
                if (score > bestEvaluation.Score)
                {
                    bestEvaluation = new ApparelVerbEvaluation(ability, verb, castTarget, score);
                }
            }

            return bestEvaluation.IsValid;
        }

        private static IEnumerable<IAttackTarget> GetPotentialTargets(Pawn pawn, Thing enemyTarget)
        {
            IAttackTarget currentTarget = enemyTarget as IAttackTarget;
            if (currentTarget != null)
            {
                yield return currentTarget;
            }

            foreach (IAttackTarget target in pawn.Map.attackTargetsCache.GetPotentialTargetsFor(pawn))
            {
                if (target == currentTarget || target.ThreatDisabled(pawn))
                {
                    continue;
                }

                yield return target;
            }
        }

        private static bool TryGetCastTarget(Ability ability, Verb verb, Thing targetThing, out LocalTargetInfo castTarget)
        {
            LocalTargetInfo thingTarget = targetThing;
            if (CanUseTarget(ability, verb, thingTarget))
            {
                castTarget = thingTarget;
                return true;
            }

            LocalTargetInfo cellTarget = targetThing.Position;
            if (CanUseTarget(ability, verb, cellTarget))
            {
                castTarget = cellTarget;
                return true;
            }

            castTarget = LocalTargetInfo.Invalid;
            return false;
        }

        private static bool CanUseTarget(Ability ability, Verb verb, LocalTargetInfo target)
        {
            return target.IsValid &&
                ability.AICanTargetNow(target) &&
                verb.CanHitTarget(target) &&
                verb.ValidateTarget(target, showMessages: false);
        }

        private static float GetAoeOpportunityScore(Pawn pawn, Ability ability, IntVec3 center, float radius)
        {
            int hostileTargets = 0;
            int explosives = 0;

            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(center, pawn.Map, radius, useCenter: true))
            {
                if (thing is Pawn targetPawn && targetPawn.Spawned && !targetPawn.Downed && targetPawn.HostileTo(pawn) &&
                    IsValidAoeAffectedTarget(ability, targetPawn))
                {
                    hostileTargets++;
                    continue;
                }

                if (IsExplosiveOpportunity(pawn, thing))
                {
                    explosives++;
                }
            }

            int extraHostileTargets = Math.Max(0, hostileTargets - 1);
            return extraHostileTargets * ExtraHostileTargetScore + explosives * ExplosiveTargetScore;
        }

        private static bool IsValidAoeAffectedTarget(Ability ability, Thing target)
        {
            if (ValidAoeAffectedTargetMethod == null)
            {
                return true;
            }

            try
            {
                return (bool)ValidAoeAffectedTargetMethod.Invoke(ability, new object[] { target });
            }
            catch
            {
                return true;
            }
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

        private static float GetVanillaShootingTargetScore(IAttackTarget target, Pawn pawn, Verb verb, float explosionRadius)
        {
            if (GetShootingTargetScoreMethod == null)
            {
                return 0f;
            }

            float originalAvoidFriendlyFireRadius = verb.verbProps.ai_AvoidFriendlyFireRadius;
            if (originalAvoidFriendlyFireRadius <= 0f)
            {
                verb.verbProps.ai_AvoidFriendlyFireRadius = explosionRadius;
            }

            try
            {
                return (float)GetShootingTargetScoreMethod.Invoke(null, new object[] { target, pawn, verb });
            }
            catch
            {
                return 0f;
            }
            finally
            {
                verb.verbProps.ai_AvoidFriendlyFireRadius = originalAvoidFriendlyFireRadius;
            }
        }

        private static bool TryGetAbility(Verb verb, out Ability ability)
        {
            ability = (verb as Verb_CastAbility)?.Ability;
            return ability != null;
        }

        private static bool TryGetExplosionRadius(Ability ability, out float radius)
        {
            radius = 0f;
            CompProperties_AbilityLaunchProjectile launchProjectile =
                ability.def.comps?.OfType<CompProperties_AbilityLaunchProjectile>().FirstOrDefault();

            ThingDef projectileDef = launchProjectile?.projectileDef;
            if (projectileDef?.projectile == null)
            {
                return false;
            }

            radius = projectileDef.projectile.explosionRadius;
            return radius > 0f;
        }

        private static Job MakeApparelVerbJob(ApparelVerbEvaluation evaluation)
        {
            Job job = evaluation.Ability.GetJob(evaluation.Target, evaluation.Target);
            if (job == null)
            {
                return null;
            }

            job.verbToUse = evaluation.Verb;
            job.maxNumStaticAttacks = 1;
            job.expiryInterval = 2000;
            job.endIfCantShootTargetFromCurPos = true;
            job.endIfCantShootInMelee = true;
            return job;
        }

        private readonly struct ApparelVerbEvaluation
        {
            public static readonly ApparelVerbEvaluation Invalid = new ApparelVerbEvaluation(null, null, LocalTargetInfo.Invalid, float.MinValue);

            public ApparelVerbEvaluation(Ability ability, Verb verb, LocalTargetInfo target, float score)
            {
                Ability = ability;
                Verb = verb;
                Target = target;
                Score = score;
            }

            public Ability Ability { get; }
            public Verb Verb { get; }
            public LocalTargetInfo Target { get; }
            public float Score { get; }
            public bool IsValid => Ability != null && Verb != null && Target.IsValid;
        }
    }
}
