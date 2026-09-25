#!/usr/bin/env python3
"""Convert the cahya dsn wilayah.sql dataset into validated, normalized CSV files."""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import re
from datetime import datetime, timezone
from pathlib import Path

ROW_PATTERN = re.compile(r"\('(?P<code>[0-9.]+)'\s*,\s*'(?P<name>(?:''|[^'])*)'\)")
CODE_PATTERNS = {
    0: re.compile(r"^\d{2}$"),
    1: re.compile(r"^\d{2}\.\d{2}$"),
    2: re.compile(r"^\d{2}\.\d{2}\.\d{2}$"),
    3: re.compile(r"^\d{2}\.\d{2}\.\d{2}\.\d{4}$"),
}
FILE_NAMES = {
    0: "provinces.csv",
    1: "regencies.csv",
    2: "districts.csv",
    3: "villages.csv",
}
HEADERS = {
    0: ["id", "code", "name"],
    1: ["id", "code", "province_id", "name"],
    2: ["id", "code", "regency_id", "name"],
    3: ["id", "code", "district_id", "name"],
}


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def numeric_id(code: str) -> int:
    return int(code.replace(".", ""))


def parse_rows(source: Path) -> dict[int, list[tuple[str, str]]]:
    levels: dict[int, list[tuple[str, str]]] = {level: [] for level in range(4)}
    seen: set[str] = set()
    with source.open(encoding="utf-8-sig") as stream:
        for line_number, line in enumerate(stream, 1):
            match = ROW_PATTERN.search(line)
            if match is None:
                continue
            code = match.group("code")
            name = match.group("name").replace("''", "'").strip()
            level = code.count(".")
            if level not in CODE_PATTERNS or CODE_PATTERNS[level].fullmatch(code) is None:
                raise ValueError(f"Unexpected code at line {line_number}: {code}")
            if not name:
                raise ValueError(f"Empty region name at line {line_number}: {code}")
            if code in seen:
                raise ValueError(f"Duplicate region code at line {line_number}: {code}")
            seen.add(code)
            levels[level].append((code, name))

    if not seen:
        raise ValueError("No region rows were found in the SQL file.")
    for level in range(1, 4):
        for code, _ in levels[level]:
            parent = code.rsplit(".", 1)[0]
            if parent not in seen:
                raise ValueError(f"Missing parent {parent} for {code}")
    return levels


def write_csv(path: Path, level: int, rows: list[tuple[str, str]]) -> None:
    with path.open("w", encoding="utf-8", newline="") as stream:
        writer = csv.writer(stream, lineterminator="\n")
        writer.writerow(HEADERS[level])
        for code, name in rows:
            row: list[object] = [numeric_id(code), code]
            if level > 0:
                row.append(numeric_id(code.rsplit(".", 1)[0]))
            row.append(name)
            writer.writerow(row)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path, help="Path to wilayah.sql")
    parser.add_argument("output", type=Path, help="Output directory")
    parser.add_argument("--version", default="kepmendagri-300.2.2-2138-2025")
    parser.add_argument(
        "--source-name",
        default="cahya dsn wilayah.sql (Kepmendagri No. 300.2.2-2138 Tahun 2025)",
    )
    args = parser.parse_args()

    source = args.source.resolve()
    output = args.output.resolve()
    if not source.is_file():
        raise SystemExit(f"Source file not found: {source}")
    output.mkdir(parents=True, exist_ok=True)
    levels = parse_rows(source)

    files: dict[str, dict[str, object]] = {}
    level_names = ["provinces", "regencies", "districts", "villages"]
    for level, key in enumerate(level_names):
        path = output / FILE_NAMES[level]
        write_csv(path, level, levels[level])
        files[key] = {
            "path": path.name,
            "count": len(levels[level]),
            "sha256": sha256(path),
        }

    fingerprint_input = "".join(f'{files[key]["sha256"]}\n' for key in level_names)
    manifest = {
        "schemaVersion": 1,
        "version": args.version,
        "source": args.source_name,
        "generatedAt": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
        "sourceFile": source.name,
        "sourceSha256": sha256(source),
        "datasetSha256": hashlib.sha256(fingerprint_input.encode("utf-8")).hexdigest(),
        "files": files,
    }
    manifest_path = output / "manifest.json"
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"manifest": str(manifest_path), "counts": {key: value["count"] for key, value in files.items()}}, indent=2))


if __name__ == "__main__":
    main()
