using System.Xml;
using Verse;

namespace EnemiesUseApparelToo
{
    public class EUAT_PatchInIdeologyAbilities : PatchOperationSequence
    {
        public bool invert = false;

        protected override bool ApplyWorker(XmlDocument xml) {
            
            if (EnemiesUseApparelTooModSettings.IdeologyAdditionsOn != invert)
            {
                return base.ApplyWorker(xml);
            }
            return true;
        }
    }
}