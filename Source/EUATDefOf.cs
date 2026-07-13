using RimWorld;
using Verse;

namespace EnemiesUseApparelToo
{
[DefOf]
public static class EUATDefOf
{
	[MayRequireAnomaly]
	public static AbilityDef EUAT_DeployTurret;

	[MayRequireRoyalty]
	public static AbilityDef EUAT_JetJump;

	[MayRequireRoyalty]
	public static AbilityDef EUAT_LaunchIncendiaryPheonixArmor;

	[MayRequireRoyalty]
	public static AbilityDef EUAT_LaunchFragGrenadeApparel;

	[MayRequireOdyssey]
	public static AbilityDef EUAT_DeployHunterDrone;
	
	[MayRequireAnomaly]
	public static ThingDef Gun_TacticalTurret;

	static EUATDefOf()
	{
		DefOfHelper.EnsureInitializedInCtor(typeof(EUATDefOf));
	}
}
}
