using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Odyssey.Director.Sim
{
    /// <summary>Results for one (director, skill level) pair.</summary>
    public sealed class EvalResult
    {
        public string Director, Skill;
        public int Runs, Segments;
        public float InBandRate;      // share of segments where HP lost was 15-50% with no death
        public float CompletionRate;  // share of runs finished without losing all lives
        public float DeathsPerRun;
    }

    /// <summary>
    /// Plays any <see cref="IDirector"/> against the simulated players with fixed seeds, so every
    /// director faces exactly the same random outcomes. Used by tests and by the Play-mode evaluator.
    /// </summary>
    public static class DirectorEvaluation
    {
        public static EvalResult Run(IDirector director, SkillProfile skill, int runs, int baseSeed)
        {
            int completed = 0, segments = 0, inBand = 0, deaths = 0;
            for (int r = 0; r < runs; r++)
            {
                var sim = new RunSimulator(baseSeed + r);
                sim.Reset(skill);
                director.ResetRun();
                var last = sim.Neutral();
                while (!sim.Finished)
                {
                    last = sim.Step(director.Decide(last));
                    segments++;
                    if (ChallengeBand.InBand(last)) inBand++;
                    deaths += last.Deaths;
                }
                if (sim.Completed) completed++;
            }
            return new EvalResult
            {
                Director = director.Name, Skill = skill.Name, Runs = runs, Segments = segments,
                InBandRate = segments > 0 ? (float)inBand / segments : 0f,
                CompletionRate = (float)completed / runs,
                DeathsPerRun = (float)deaths / runs
            };
        }

        public static string ToMarkdownTable(IEnumerable<EvalResult> results)
        {
            var sb = new StringBuilder();
            sb.AppendLine("| Director | Skill | Runs | In-band segments | Runs completed | Deaths per run |");
            sb.AppendLine("|---|---|---|---|---|---|");
            foreach (var r in results)
                sb.AppendLine($"| {r.Director} | {r.Skill} | {r.Runs} | {r.InBandRate:P1} | {r.CompletionRate:P1} | {r.DeathsPerRun:0.00} |");
            return sb.ToString();
        }

        public static string ToCsv(IEnumerable<EvalResult> results)
        {
            var sb = new StringBuilder("director,skill,runs,in_band_rate,completion_rate,deaths_per_run\n");
            foreach (var r in results)
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3:0.0000},{4:0.0000},{5:0.0000}",
                    r.Director, r.Skill, r.Runs, r.InBandRate, r.CompletionRate, r.DeathsPerRun));
            return sb.ToString();
        }
    }
}
