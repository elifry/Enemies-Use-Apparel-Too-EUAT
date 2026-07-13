using Verse;


namespace EnemiesUseApparelToo
{
    
public class HediffComp_RemovedOnDowned : HediffComp
{
	public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
	{
		base.Notify_PawnDied(dinfo, culprit);
		base.Pawn.health.RemoveHediff(parent);
	}

	public override void CompPostPostAdd(DamageInfo? dinfo)
	{
		base.CompPostPostAdd(dinfo);
        base.Pawn.guest.Recruitable = false;
	}

    
	public override void CompPostTickInterval(ref float severityAdjustment, int delta)
	{
		if (base.Pawn.Downed == true)
		{
			base.Pawn.health.RemoveHediff(parent);
		}
	}
}
}