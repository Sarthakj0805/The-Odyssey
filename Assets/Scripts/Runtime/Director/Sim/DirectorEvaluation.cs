using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Odyssey.Telemetry;

namespace Odyssey.Director.Sim
{
    /// <summary>Aggregated results for one (director, skill profile) pair.</summary>
    public sealed class EvalResult
    {
        public string Director, Skill;
        public int Runs, Segments;
        public float CompletionRate;
        public float TimeInBand;          // fraction of segments with HP loss AND time in the target band
        public float TimeInBandStd;       // std-dev of the per-run in-band fraction (variance across runs)
        public float MeanHpLost;
        public float DeathsPerRun;
        public float TrivialRate;
        public float MeanReturn;          // mean summed reward per run
    }

    /// <summary>
    /// Runs any <see cref="IDirector"/> against the simulated players with fixed seeds.
    /// Used by edit-mode tests (Fixed/Heuristic) and by the Play-mode evaluator (adds RL).
    /// </summary>
    public static class DirectorEvaluation
    {
        public static EvalResult Run(IDirector director, SkillProfile skill, int runs, int baseSeed)
        {
            var perRunBand = new List<float>(runs);
            int completed = 0, segs = 0, deaths = 0, trivial = 0, inBandTotal = 0;
            float hpLost = 0f, returns = 0f;

            for (int r = 0; r < runs; r++)
            {
                var sim = new RunSimulator(baseSeed + r);
                sim.Reset(skill);
                director.ResetRun();
                var last = sim.Neutral();
                int inBand = 0, runSegs = 0;
                while (!sim.Finished)
                {
                    var d = director.Decide(last);
                    last = sim.Step(d);
                    runSegs++;
                    if (ChallengeBand.InBand(last)) inBand++;
                    if (ChallengeBand.IsTrivial(last)) trivial++;
                    hpLost += last.HpLostFrac;
                    deaths += last.Deaths;
                    returns += ChallengeBand.Reward(last, d.StrongBoons && last.SegmentIndex > 0);
                }
                if (sim.Completed) completed++;
                else returns -= 1f;   // same terminal penalty as training
                segs += runSegs; inBandTotal += inBand;
                perRunBand.Add(runSegs > 0 ? (float)inBand / runSegs : 0f);
            }

            float mean = perRunBand.Average();
            float std = (float)Math.Sqrt(perRunBand.Select(x => (x - mean) * (x - mean)).Average());
            return new EvalResult
            {
                Director = director.Name, Skill = skill.Name, Runs = runs, Segments = segs,
                CompletionRate = (float)completed / runs,
                TimeInBand = segs > 0 ? (float)inBandTotal / segs : 0f,
                TimeInBandStd = std,
                MeanHpLost = segs > 0 ? hpLost / segs : 0f,
                DeathsPerRun = (float)deaths / runs,
                TrivialRate = segs > 0 ? (float)trivial / segs : 0f,
                MeanReturn = returns / runs
            };
        }

        public static string ToMarkdownTable(IEnumerable<EvalResult> results)
        {
            var sb = new StringBuilder();
            sb.AppendLine("| Director | Skill | Runs | Completion | Time-in-band | Std (per-run) | Mean HP lost/seg | Deaths/run | Trivial segs | Mean return |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
            foreach (var r in results)
                sb.AppendLine($"| {r.Director} | {r.Skill} | {r.Runs} | {r.CompletionRate:P1} | {r.TimeInBand:P1} | {r.TimeInBandStd:0.000} | {r.MeanHpLost:P1} | {r.DeathsPerRun:0.00} | {r.TrivialRate:P1} | {r.MeanReturn:0.00} |");
            return sb.ToString();
        }

        public static string ToCsv(IEnumerable<EvalResult> results)
        {
            var sb = new StringBuilder("director,skill,runs,completion,time_in_band,time_in_band_std,mean_hp_lost,deaths_per_run,trivial_rate,mean_return\n");
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            foreach (var r in results)
                sb.AppendLine(string.Format(inv, "{0},{1},{2},{3:0.0000},{4:0.0000},{5:0.0000},{6:0.0000},{7:0.0000},{8:0.0000},{9:0.0000}",
                    r.Director, r.Skill, r.Runs, r.CompletionRate, r.TimeInBand, r.TimeInBandStd, r.MeanHpLost, r.DeathsPerRun, r.TrivialRate, r.MeanReturn));
            return sb.ToString();
        }
    }
}
