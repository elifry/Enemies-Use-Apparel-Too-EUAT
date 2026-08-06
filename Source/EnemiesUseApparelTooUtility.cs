using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;


using RimWorld;

namespace EnemiesUseApparelToo.Utility
{

    public static class EnemiesUseApparelTooUtility
    {
        public static Apparel GetAbilityApparelSource(Ability ability)
        {
            Apparel apparelwithability = null;
            if (ability.pawn.apparel != null)
            {
                foreach (Apparel apparelitem in ability.pawn.apparel.WornApparel)
                {
                    foreach (Ability abilityitem in apparelitem.AllAbilitiesForReading)
                    {
                        if (ability == abilityitem)
                        {
                            apparelwithability = apparelitem;
                        }
                    }
                }
            }
            return apparelwithability;
        }

        public static bool PawnHasApparelwithAbility(AbilityDef abilitydef, Pawn pawn, out Ability ability)
        {
            ability = null;
            if (pawn.apparel.WornApparel != null)
            {
                foreach (Apparel apparelitem in pawn.apparel.WornApparel)
                {
                    if (apparelitem.AllAbilitiesForReading != null)
                    {
                        foreach (Ability abilityitem in apparelitem.AllAbilitiesForReading)
                        {
                            if (abilityitem.def == abilitydef)
                            {
                                ability = abilityitem;
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }

        public static bool PawnHasAbilitywithComp(Pawn pawn, AbilityComp comp, out Ability ability)
        {
            ability = null;
            if (pawn.abilities.AllAbilitiesForReading != null)
            {
                foreach (Ability abilityitem in pawn.abilities.AllAbilitiesForReading)
                {
                    foreach (AbilityComp abilityComp in abilityitem.comps)
                    {
                        if (abilityComp == comp)
                        {
                            ability = abilityitem;
                            return true;
                        }
                    }
                }

            }
            return false;
        }

        public static bool PawnHasJumpAbility(Pawn pawn, out Ability ability)
        {
            ability = null;
            if (pawn.abilities.AllAbilitiesForReading != null)
            {
                foreach (Ability abilityitem in pawn.abilities.AllAbilitiesForReading)
                {
                    if (abilityitem.def.jobDef == JobDefOf.CastJump)
                    {
                        ability = abilityitem;
                        return true;
                    }
                }

            }
            return false;
        }

        public static bool IsHealthy(Pawn pawn, float thresholdPercent)
        {
            HediffSet hediffSet = pawn.health.hediffSet;
            float num = 0f;
            for (int i = 0; i < hediffSet.hediffs.Count; i++)
            {
                if (hediffSet.hediffs[i] is Hediff_Injury)
                {
                    num += hediffSet.hediffs[i].Severity;
                }
            }
            return num / pawn.health.LethalDamageThreshold < thresholdPercent;
        }

        public static bool IsThreatened(Pawn pawn, float maxThreatDistance = 2f, int minCloseTargets = 2)
        {
            if (pawn.Spawned && !pawn.Downed)
            {
                return PawnUtility.EnemiesAreNearby(pawn, 9, passDoors: true, maxThreatDistance, minCloseTargets);
            }
            return false;
        }

        public static bool AiFindJumpCell(Ability abilityjump, TargetInfo target, float healththreshold, float minDistToTarget, out IntVec3 dest)
        {
            dest = IntVec3.Invalid;
            Pawn pawn = abilityjump.pawn;
            float effectiveJumpRange = abilityjump.verb.EffectiveRange;

            if (abilityjump == null)
            {
                return false;
            }
            if (!abilityjump.CanCast || abilityjump.Casting || target == null)
            {
                return false;
            }
            if (pawn.equipment?.Primary?.def.IsMeleeWeapon == true && IsHealthy(pawn, healththreshold))
            {
                var destination = RCellFinder.BestOrderedGotoDestNear(target.Cell, pawn, (c) => JumpUtility.ValidJumpTarget(pawn, pawn.Map, c) && JumpUtility.CanHitTargetFrom(pawn, pawn.Position, c, effectiveJumpRange));
                if (IsValidAiJump(pawn, destination, effectiveJumpRange, minDistToTarget))
                {
                    dest = destination;
                }
            }
            else if (!IsHealthy(pawn, healththreshold) && IsThreatened(pawn))
            {
                if (TryFindRelocatePosition(abilityjump, pawn, out var destination2, effectiveJumpRange))
                {
                    dest = destination2;
                }

            }
            else if (TryFindShootingPosition(pawn, target, out var destination3))
            {
                if (IsValidAiJump(pawn, destination3, effectiveJumpRange, minDistToTarget))
                {
                    dest = destination3;
                }
            }
            else if (!IsHealthy(pawn, 0.25f) && IsThreatened(pawn, 30f, 0))
            {
                if (TryFindRelocatePosition(abilityjump, pawn, out var destination4, effectiveJumpRange))
                {
                    dest = destination4;
                }

            }

            return dest.IsValid;
        }

        public static bool TryFindRelocatePosition(Ability jump, Pawn pawn, out IntVec3 relocatePosition, float maxDistance)
        {
            List<Thing> tmpHostileSpots = new List<Thing>();
            tmpHostileSpots.Clear();
            tmpHostileSpots.AddRange(from a in pawn.Map.attackTargetsCache.GetPotentialTargetsFor(pawn)
                                     where !a.ThreatDisabled(pawn)
                                     select a.Thing);
            relocatePosition = CellFinderLoose.GetFallbackDest(pawn, tmpHostileSpots, maxDistance, 5f, 5f, 20, (IntVec3 c) => jump.verb.ValidateTarget(c, showMessages: false));
            tmpHostileSpots.Clear();
            return relocatePosition.IsValid;
        }

        private static bool TryFindShootingPosition(Pawn pawn, TargetInfo target, out IntVec3 dest, Verb verbToUse = null)
        {
            Verb verb = verbToUse ?? pawn.TryGetAttackVerb(null, !pawn.IsColonist);
            if (verb == null)
            {
                dest = IntVec3.Invalid;
                return false;
            }
            return CastPositionFinder.TryFindCastPosition(new CastPositionRequest
            {
                caster = pawn,
                target = target.Thing,
                verb = verb,
                maxRangeFromTarget = verb.EffectiveRange,
                wantCoverFromTarget = (verb.EffectiveRange > 5f)
            }, out dest);
        }

        private static bool IsValidAiJump(Pawn pawn, IntVec3 dest, float effectiverange, float minDistToTarget)
        {
            if (dest == null)
            {
                return false;
            }
            float num = pawn.Position.DistanceTo(dest);
            if (num < minDistToTarget || num > effectiverange || !GenSight.LineOfSight(pawn.Position, dest, pawn.Map))
            {
                return false;
            }
            return true;
        }

    }
}