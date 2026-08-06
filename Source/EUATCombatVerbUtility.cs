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

        private static bool TryEvaluateApparelVerb(Pawn pawn, Thing enemyTarget, Verb verb, out ApparelVerbEvaluation evaluation)
        {
            evaluation = ApparelVerbEvaluation.Invalid;

            if (!TryGetAbility(verb, out Ability ability) || !TryGetExplosionRadius(ability, out float radius))
            {
                return false;
            }

            if (!TryGetVanillaAoeTarget(ability, verb, out LocalTargetInfo castTarget, out Thing affectedTarget))
            {
                return false;
            }

            float opportunityScore = GetAoeOpportunityScore(pawn, ability, castTarget, radius);
            if (opportunityScore < MinimumOpportunityScore)
            {
                return false;
            }

            IAttackTarget attackTarget = affectedTarget as IAttackTarget ?? enemyTarget as IAttackTarget;
            float score = GetVanillaShootingTargetScore(attackTarget, pawn, verb, radius) + opportunityScore;
            evaluation = new ApparelVerbEvaluation(ability, verb, castTarget, score);
            return true;
        }

        private static bool TryGetVanillaAoeTarget(Ability ability, Verb verb, out LocalTargetInfo castTarget, out Thing affectedTarget)
        {
            affectedTarget = null;
            LocalTargetInfo vanillaTarget = ability.AIGetAOETarget();
            if (!vanillaTarget.IsValid)
            {
                castTarget = LocalTargetInfo.Invalid;
                return false;
            }

            affectedTarget = vanillaTarget.Thing;
            if (CanUseTarget(ability, verb, vanillaTarget))
            {
                castTarget = vanillaTarget;
                return true;
            }

            if (vanillaTarget.Thing == null)
            {
                castTarget = LocalTargetInfo.Invalid;
                return false;
            }

            LocalTargetInfo cellTarget = vanillaTarget.Thing.Position;
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

        private static float GetAoeOpportunityScore(Pawn pawn, Ability ability, LocalTargetInfo castTarget, float radius)
        {
            int hostileTargets = CountVanillaAffectedHostiles(pawn, ability, castTarget);
            if (hostileTargets == 0)
            {
                hostileTargets = CountHostilesInRadius(pawn, ability, castTarget.Cell, radius);
            }

            int explosives = CountExplosivesInRadius(pawn, castTarget.Cell, radius);
            int extraHostileTargets = Math.Max(0, hostileTargets - 1);
            return extraHostileTargets * ExtraHostileTargetScore + explosives * ExplosiveTargetScore;
        }

        private static int CountVanillaAffectedHostiles(Pawn pawn, Ability ability, LocalTargetInfo castTarget)
        {
            int hostileTargets = 0;
            foreach (LocalTargetInfo affectedTarget in ability.GetAffectedTargets(castTarget))
            {
                if (affectedTarget.Thing is Pawn targetPawn && IsHostileAoePawn(pawn, targetPawn))
                {
                    hostileTargets++;
                }
            }

            return hostileTargets;
        }

        private static int CountHostilesInRadius(Pawn pawn, Ability ability, IntVec3 center, float radius)
        {
            int hostileTargets = 0;
            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(center, pawn.Map, radius, useCenter: true))
            {
                if (thing is Pawn targetPawn && IsHostileAoePawn(pawn, targetPawn) && IsValidAoeAffectedTarget(ability, targetPawn))
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
            if (target == null || GetShootingTargetScoreMethod == null)
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
