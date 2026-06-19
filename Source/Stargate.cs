// ==== Source/Stargate.cs ====
using Verse;

namespace BetterRimworlds.Stargate
{
    public class Stargate : Mod
    {
        private static ModContentPack contentPack;

        public Stargate(ModContentPack content) : base(content)
        {
            contentPack = content;
        }

        public static string ModRootPath()
        {
            return contentPack.RootDir;
        }
    }
}