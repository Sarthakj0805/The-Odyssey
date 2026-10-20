"""Export ML-Agents TensorBoard scalars to CSV (+ PNG if matplotlib is installed).

Usage (from the repo root, inside the `odyssey_ai` conda env):
    python ml/scripts/export_tensorboard.py --run ml/results/director_v1 --out results/training
"""
import argparse
import csv
import glob
import os

from tensorboard.backend.event_processing.event_accumulator import EventAccumulator

TAGS = [
    "Environment/Cumulative Reward",
    "Environment/Episode Length",
    "Director/InBand",
    "Director/Deaths",
    "Director/Difficulty",
    "Policy/Entropy",
]


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--run", required=True, help="ML-Agents run folder, e.g. ml/results/director_v1")
    ap.add_argument("--out", default="results/training")
    args = ap.parse_args()

    event_files = glob.glob(os.path.join(args.run, "**", "events.out.tfevents.*"), recursive=True)
    if not event_files:
        raise SystemExit(f"No TensorBoard event files under {args.run}")
    os.makedirs(args.out, exist_ok=True)

    rows = {}
    for f in event_files:
        acc = EventAccumulator(f, size_guidance={"scalars": 0})
        acc.Reload()
        for tag in acc.Tags().get("scalars", []):
            for ev in acc.Scalars(tag):
                rows.setdefault(tag, []).append((ev.step, ev.value))

    csv_path = os.path.join(args.out, "training_curves.csv")
    with open(csv_path, "w", newline="") as fh:
        w = csv.writer(fh)
        w.writerow(["tag", "step", "value"])
        for tag in sorted(rows):
            for step, value in sorted(rows[tag]):
                w.writerow([tag, step, f"{value:.6f}"])
    print(f"wrote {csv_path} ({sum(len(v) for v in rows.values())} points, {len(rows)} tags)")

    try:
        import matplotlib
        matplotlib.use("Agg")
        import matplotlib.pyplot as plt
    except ImportError:
        print("matplotlib not installed - skipping PNG (pip install matplotlib to enable)")
        return

    wanted = [t for t in TAGS if t in rows]
    fig, axes = plt.subplots(len(wanted), 1, figsize=(8, 2.4 * len(wanted)), sharex=True)
    if len(wanted) == 1:
        axes = [axes]
    for ax, tag in zip(axes, wanted):
        pts = sorted(rows[tag])
        ax.plot([p[0] for p in pts], [p[1] for p in pts], linewidth=1.5)
        ax.set_title(tag, fontsize=10)
        ax.grid(alpha=0.3)
    axes[-1].set_xlabel("step")
    fig.tight_layout()
    png = os.path.join(args.out, "training_curves.png")
    fig.savefig(png, dpi=120)
    print(f"wrote {png}")


if __name__ == "__main__":
    main()
