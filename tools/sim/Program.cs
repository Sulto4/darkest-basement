using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using DarkestBasement.Model.Data;
using DarkestBasement.Model.Sim;

namespace NUnit.Framework
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class TestAttribute : Attribute { }
}
namespace DarkestBasement.Simulator
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            try
            {
                if (args.Length == 0 || args[0] == "tests") return Tests();
                if (args[0] != "sim") throw new ArgumentException("Use tests or sim --runs N --seed N.");
                int count = 500, seed = 1;
                for (int i = 1; i < args.Length; i += 2) { if (args[i] == "--runs") count = int.Parse(args[i + 1]); else if (args[i] == "--seed") seed = int.Parse(args[i + 1]); else throw new ArgumentException(args[i]); }
                if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
                return Simulate(count, seed);
            }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        private static int Tests()
        {
            int passed = 0, failed = 0;
            foreach (var method in typeof(Tests.ModelRulesTests).GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (method.GetCustomAttribute<NUnit.Framework.TestAttribute>() == null) continue;
                try { method.Invoke(null, null); Console.ForegroundColor = ConsoleColor.Green; Console.WriteLine("PASS " + method.Name); passed++; }
                catch (Exception e) { Console.ForegroundColor = ConsoleColor.Red; Console.WriteLine("FAIL " + method.Name + ": " + (e.InnerException ?? e)); failed++; }
                finally { Console.ResetColor(); }
            }
            Console.WriteLine("Tests: " + passed + " passed, " + failed + " failed."); return failed == 0 && passed > 0 ? 0 : 1;
        }
        private static int Simulate(int count, int seed)
        {
            int wins = 0, normal = 0, normalRounds = 0, bosses = 0, bossRounds = 0, combats = 0, winningCombats = 0;
            int concessions = 0; int[] bossCounts = new int[3], bossRoundCounts = new int[3];
            int[] floorClears = new int[3]; var cards = new SortedDictionary<string, CardMetrics>(StringComparer.Ordinal);
            foreach (var def in CardDatabase.All.Values) cards.Add(def.Id, new CardMetrics());
            var sim = new RunSimulator();
            for (int i = 0; i < count; i++)
            {
                SimulationResult result = sim.Play(seed + i); if (result.Victory) { wins++; winningCombats += result.Combats; }
                concessions += result.Concessions;
                foreach (var sample in result.Samples) if (sample.IsBoss) { bossCounts[sample.Floor - 1]++; bossRoundCounts[sample.Floor - 1] += sample.Rounds; }
                normal += result.NormalCombats; normalRounds += result.NormalRounds; bosses += result.BossCombats; bossRounds += result.BossRounds; combats += result.Combats;
                for (int f = 0; f < result.FloorsCleared; f++) floorClears[f]++;
                foreach (var pair in result.Cards) { var m = cards[pair.Key]; m.Offered += pair.Value.Offered; m.Picked += pair.Value.Picked; m.Played += pair.Value.Played; m.Damage += pair.Value.Damage; }
            }
            Console.WriteLine("Runs=" + count + " seeds=" + seed + ".." + (seed + count - 1));
            Console.WriteLine("Wins=" + wins + " (" + (100.0 * wins / count).ToString("F1") + "%) floor clears=" + string.Join("/", floorClears));
            Console.WriteLine("Normal rounds=" + (normalRounds / (double)Math.Max(1, normal)).ToString("F2") + " (n=" + normal + ") Boss rounds=" + (bossRounds / (double)Math.Max(1, bosses)).ToString("F2") + " (n=" + bosses + ")");
            Console.WriteLine("Combats/run all=" + (combats / (double)count).ToString("F2") + " winning=" + (winningCombats / (double)Math.Max(1, wins)).ToString("F2"));
            long damage = 0; int plays = 0; foreach (var metric in cards.Values) { damage += metric.Damage; plays += metric.Played; }
            Console.WriteLine("Boss rounds by floor=" + (bossRoundCounts[0] / (double)Math.Max(1, bossCounts[0])).ToString("F2") + "/" + (bossRoundCounts[1] / (double)Math.Max(1, bossCounts[1])).ToString("F2") + "/" + (bossRoundCounts[2] / (double)Math.Max(1, bossCounts[2])).ToString("F2") + " concessions=" + concessions);
            Console.WriteLine("card                       offers picks pick% plays play% damage damage%");
            double maxDamageShare = 0, maxPlayShare = 0;
            foreach (var pair in cards)
            {
                var m = pair.Value; maxPlayShare = Math.Max(maxPlayShare, m.Played / (double)Math.Max(1, plays)); maxDamageShare = Math.Max(maxDamageShare, m.Damage / (double)Math.Max(1, damage));
                Console.WriteLine("{0,-26} {1,6} {2,5} {3,5:F1} {4,6} {5,6:F2} {6,7} {7,6:F2}", pair.Key, m.Offered, m.Picked, 100.0 * m.Picked / Math.Max(1, m.Offered), m.Played, 100.0 * m.Played / Math.Max(1, plays), m.Damage, 100.0 * m.Damage / Math.Max(1, damage));
            }
            bool balanced = wins / (double)count >= 0.35 && wins / (double)count <= 0.55 && normalRounds / (double)normal >= 3 && normalRounds / (double)normal <= 4 && bossRounds / (double)bosses >= 6 && bossRounds / (double)bosses <= 8 && winningCombats / (double)Math.Max(1, wins) >= 16 && winningCombats / (double)Math.Max(1, wins) <= 20 && maxDamageShare < 0.25 && maxPlayShare < 0.25;
            for (int i = 0; i < 3; i++) balanced &= bossRoundCounts[i] / (double)Math.Max(1, bossCounts[i]) >= 6 && bossRoundCounts[i] / (double)Math.Max(1, bossCounts[i]) <= 8;
            Console.WriteLine("Balance targets: " + (balanced ? "PASS" : "OUTSIDE TARGET") + " (card dominance threshold: 25% damage or plays)");
            return count == 500 && seed == 1 && !balanced ? 1 : 0;
        }
    }
}
