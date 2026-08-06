using UnityEngine;
using Verse;

namespace EnemiesUseApparelToo
{

    public class EnemiesUseApparelTooModSettings : ModSettings
    {
        private static EnemiesUseApparelTooModSettings _instance;
        public bool EUAT_UseHarmonyPatch = true;
        public static bool UseHarmonyPatch => _instance.EUAT_UseHarmonyPatch;
        public bool EUAT_KeepModAbilities = true;
        public static bool KeepModAbilities => _instance.EUAT_KeepModAbilities;

        public bool EUAT_IdeologyAdditions = true;
        public static bool IdeologyAdditionsOn => _instance.EUAT_IdeologyAdditions;


        public EnemiesUseApparelTooModSettings()
        {
            _instance = this;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref EUAT_UseHarmonyPatch, "EUAT_UseHarmonyPatch", defaultValue: true);
            Scribe_Values.Look(ref EUAT_KeepModAbilities, "EUAT_KeepModAbilities", defaultValue: true);
            Scribe_Values.Look(ref EUAT_IdeologyAdditions, "EUAT_IdeologyAdditions", defaultValue: false);
            base.ExposeData();
        }
    }
    public class EnemiesUseApparelTooSettings : Mod
    {
        private EnemiesUseApparelTooModSettings settings;

        public EnemiesUseApparelTooSettings(ModContentPack content)
            : base(content)
        {
            settings = GetSettings<EnemiesUseApparelTooModSettings>();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);
            listingStandard.Label("Changes only apply after you restart the game.");
            listingStandard.Gap();
            listingStandard.CheckboxLabeled("Harmony Apparel Verb Targeting:", ref settings.EUAT_UseHarmonyPatch, "Enables a harmony patch that considers all violent apparel verbs for use. This may have unintended consequences with other mods, so use at your own risk. This will work for all vanilla items.");
            listingStandard.CheckboxLabeled("Keep this Mod's Abilities:", ref settings.EUAT_KeepModAbilities, "Items will keep the abilities (originally used to make items usable by enemies) from previous patches, this should help with backwards compatibility");
            listingStandard.CheckboxLabeled("BETA: Enable boss style enemies to use Ideology Abilities:", ref settings.EUAT_IdeologyAdditions, "Gives enemy bosses a chance to spawn with combat command or marksman aura, gives tribal berserkers a chance to spawn with berserk trance. It will make enemies that spawn with these unrecruitable, and the hediff will be removed on downed or death.");
            listingStandard.End();
            base.DoSettingsWindowContents(inRect);

        }

        public override string SettingsCategory() => "Enemies Use Apparel Too";
    }
}