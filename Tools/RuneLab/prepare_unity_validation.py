"""Prepare an optional local-only Rune Casting holdout for the Unity EditMode test."""
import argparse
import json
from pathlib import Path

from runes import RUNES

CLASSES = [rune.id for rune in RUNES]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("experiment", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    local = (Path(__file__).parent / ".local").resolve()
    target = args.output.resolve()
    if not target.is_relative_to(local):
        parser.error("Write raw validation strokes only under the ignored .local directory.")
    split = json.loads((args.experiment / "split.json").read_text(encoding="utf-8"))
    records = {
        row["id"]: row
        for line in (args.experiment / "dataset_snapshot.jsonl").read_text(encoding="utf-8").splitlines()
        if line.strip()
        for row in (json.loads(line),)
    }
    cases = []
    for ident in split["test"]:
        row = records[ident]
        label = -1 if row["expected"] == "invalid" else CLASSES.index(row["expected"])
        cases.append({"label": label, "points": [{"x": x, "y": y} for x, y in row["points"]]})
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(json.dumps({"cases": cases}, separators=(",", ":")), encoding="utf-8")
    print(f"Prepared {len(cases)} held-out strokes for local Unity validation.")


if __name__ == "__main__":
    main()
