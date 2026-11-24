using UnityEngine;

namespace PaperWings.Gameplay
{
    /// <summary>
    /// How hard Accounting fights back (Stage 35). Picked in Options, saved with the other options.
    ///
    ///                      EASY        NORMAL   HARD
    ///   Damage you take    60 %        100 %    135 %   (staples, cans, boss attacks, bumps)
    ///   Rival fire range   80 %        100 %    115 %
    ///   Rival aim cone     70 %        100 %    120 %   (how far off-nose they'll still shoot)
    ///   Rival burst pause  x1.6        x1       x0.7    (time between bursts)
    ///   Rival burst length -1 shot     -        +1 shot
    ///   Rival tenacity     75 %        100 %    120 %   (how hard they stick to your tail)
    ///   Boss health        70 %        100 %    135 %   (and their breakable parts)
    ///
    /// Normal is exactly the tuning in the settings assets, so balancing stays in one place.
    /// Campaign star times don't change with difficulty.
    /// </summary>
    public static class Difficulty
    {
        public enum Level { Easy, Normal, Hard }

        const string Key = "PaperWings.Opt.Difficulty";

        static Level? current;
        public static Level Current
        {
            get
            {
                if (!current.HasValue) current = (Level)Mathf.Clamp(PlayerPrefs.GetInt(Key, (int)Level.Normal), 0, 2);
                return current.Value;
            }
            set
            {
                current = value;
                PlayerPrefs.SetInt(Key, (int)value);
                PlayerPrefs.Save();
            }
        }

        public static string Name(Level l) => l switch { Level.Easy => "Easy", Level.Hard => "Hard", _ => "Normal" };
        public static string Describe(Level l) => l switch
        {
            Level.Easy => "Tougher paper, rivals shoot less and wander off, bosses fold sooner.",
            Level.Hard => "Flimsier paper, sharp-shooting rivals that hang on, tougher bosses.",
            _ => "The game as designed."
        };

        static float Pick(float easy, float normal, float hard) => Current switch { Level.Easy => easy, Level.Hard => hard, _ => normal };

        /// <summary>Multiplies all damage the player's paper takes.</summary>
        public static float DamageToPlayer => Pick(0.6f, 1f, 1.35f);
        public static float RivalFireRange => Pick(0.8f, 1f, 1.15f);
        public static float RivalFireAngle => Pick(0.7f, 1f, 1.2f);
        public static float RivalBurstPause => Pick(1.6f, 1f, 0.7f);
        public static int RivalBurstExtra => Current == Level.Easy ? -1 : Current == Level.Hard ? 1 : 0;
        public static float RivalTenacity => Pick(0.75f, 1f, 1.2f);
        public static float BossHealth => Pick(0.7f, 1f, 1.35f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => current = null;
    }
}
