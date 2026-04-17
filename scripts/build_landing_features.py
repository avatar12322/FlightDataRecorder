#!/usr/bin/env python3
"""
Agregacja surowych logów CSV (FlightDataRecorder) do jednego wiersza na lądowanie — cechy FOQA/FDM.

Użycie:
  python build_landing_features.py --input "Lot_A320neo_20260411_021331.csv" --output landings_features.csv
  python build_landing_features.py --input-dir . --glob "Lot_*.csv" --output landings_features.csv

Kolumna Ocena (0–3) pozostaje pusta — uzupełnij ręcznie po ocenie lotów.
"""

from __future__ import annotations

import argparse
import glob
import math
import os
import numpy as np
import pandas as pd


REQUIRED_BASE = [
    "Timestamp_ms",
    "RadioAlt_ft",
    "VerticalSpeed_fpm",
    "Pitch_deg",
    "Airspeed_kts",
    "OnGround",
    "Heading_deg",
    "Wind_kts",
    "Wind_dir_deg",
]


def _load_csv(path: str) -> pd.DataFrame:
    return pd.read_csv(path, dtype=np.float64, na_values=["", "nan", "NaN"])


def find_touchdown_indices(df: pd.DataFrame) -> list[int]:
    og = df["OnGround"].to_numpy(dtype=float)
    out: list[int] = []
    for i in range(1, len(og)):
        prev_v = og[i - 1]
        cur_v = og[i]
        prev_air = (prev_v < 0.5) or (not np.isfinite(prev_v))
        cur_gr = np.isfinite(cur_v) and (cur_v >= 0.5)
        if prev_air and cur_gr:
            out.append(i)
    return out


def rollout_end_index(df: pd.DataFrame, td_idx: int, max_ms_after: float = 120_000.0) -> int:
    t0 = float(df.iloc[td_idx]["Timestamp_ms"])
    n = len(df)
    j = td_idx
    while j < n:
        ts = float(df.iloc[j]["Timestamp_ms"])
        if ts - t0 > max_ms_after:
            return min(j, n - 1)
        og = float(df.iloc[j]["OnGround"])
        spd = df.iloc[j]["Airspeed_kts"]
        if og >= 0.5 and np.isfinite(spd) and float(spd) < 30.0:
            return j
        j += 1
    return n - 1


def wind_components_kt(wind_kts: float, wind_dir_deg: float, heading_deg: float) -> tuple[float, float]:
    """Składowe wiatru względem dzioba statku (deg). Cross = sin, head = cos."""
    if not (np.isfinite(wind_kts) and np.isfinite(wind_dir_deg) and np.isfinite(heading_deg)):
        return float("nan"), float("nan")
    rel = math.radians(wind_dir_deg - heading_deg)
    return (
        float(wind_kts * math.sin(rel)),
        float(wind_kts * math.cos(rel)),
    )


def compute_features_for_segment(seg: pd.DataFrame, td_rel_idx: int, source_file: str, landing_id: int) -> dict:
    """seg: ciągły fragment z reset_index; td_rel_idx — wiersz przyziemienia (OnGround przejście)."""
    row_td = seg.iloc[td_rel_idx]
    pre = seg.iloc[: td_rel_idx + 1]

    ra = seg["RadioAlt_ft"].to_numpy(dtype=float)
    loc = seg.get("Localizer_NAV1_CDI", pd.Series(np.nan, index=seg.index)).to_numpy(dtype=float)
    gs = seg.get("GlideSlope_deg", pd.Series(np.nan, index=seg.index)).to_numpy(dtype=float)
    asp = seg["Airspeed_kts"].to_numpy(dtype=float)
    tgt = seg.get("TargetAirspeed_kts", pd.Series(np.nan, index=seg.index)).to_numpy(dtype=float)
    gear = seg.get("Gear", pd.Series(0.0, index=seg.index)).to_numpy(dtype=float)
    flaps = seg.get("Flaps", pd.Series(0.0, index=seg.index)).to_numpy(dtype=float)

    mask_1000 = np.isfinite(ra) & (ra <= 1000.0)
    loc_m = np.abs(loc[mask_1000])
    gs_m = np.abs(gs[mask_1000])
    max_loc = float(np.nanmax(loc_m)) if loc_m.size else float("nan")
    max_gs = float(np.nanmax(gs_m)) if gs_m.size else float("nan")

    mask_band = np.isfinite(ra) & (ra <= 1000.0) & (ra >= 50.0)
    dev = np.abs(asp[mask_band] - tgt[mask_band])
    avg_asp_dev = float(np.nanmean(dev)) if dev.size else float("nan")

    final_gear = float(np.nanmax(gear[: td_rel_idx + 1]))
    final_flap = float(np.nanmax(flaps[: td_rel_idx + 1]))
    gear_th = max(0.85 * final_gear, 0.5) if np.isfinite(final_gear) else 0.5
    flap_th = max(0.85 * final_flap, 0.5) if np.isfinite(final_flap) else 0.5
    config_alt = float("nan")
    for k in range(td_rel_idx, -1, -1):
        if gear[k] >= gear_th and flaps[k] >= flap_th:
            config_alt = float(ra[k])
            break
    if np.isfinite(config_alt):
        late_pen = max(0.0, 500.0 - config_alt)
    else:
        late_pen = 1500.0

    cw, hw = wind_components_kt(
        float(row_td.get("Wind_kts", float("nan"))),
        float(row_td.get("Wind_dir_deg", float("nan"))),
        float(row_td.get("Heading_deg", float("nan"))),
    )

    td_vs = float(row_td.get("VerticalSpeed_fpm", float("nan")))
    td_pitch = float(row_td.get("Pitch_deg", float("nan")))
    td_dist = float(row_td.get("DistRunway_m", float("nan")))

    post = seg.iloc[td_rel_idx:].reset_index(drop=True)
    t0_ms = float(row_td["Timestamp_ms"])
    rev = post.get("ReverseNozzle1_pct", pd.Series(0.0, index=post.index)).to_numpy(dtype=float)
    ts_post = post["Timestamp_ms"].to_numpy(dtype=float)
    time_rev = float("nan")
    for j in range(len(post)):
        if rev[j] > 5.0:
            time_rev = (float(ts_post[j]) - t0_ms) / 1000.0
            break

    pre_row = pre.iloc[max(0, td_rel_idx - 1)]
    armed_before = float(pre_row.get("SpoilersArmed", 0.0) or 0.0) >= 0.5
    spoil_col = post.get("SpoilersLeft_pos", pd.Series(0.0, index=post.index))
    spoil_max_early = float(spoil_col.iloc[: min(len(spoil_col), 90)].max()) if len(spoil_col) else 0.0
    spoilers_ok = 1.0 if (armed_before and spoil_max_early > 0.08) else 0.0

    end_roll = len(post)
    for j in range(len(post)):
        ogj = float(post.iloc[j].get("OnGround", 0.0))
        spj = post.iloc[j].get("Airspeed_kts", float("nan"))
        if ogj >= 0.5 and np.isfinite(spj) and float(spj) < 30.0:
            end_roll = j + 1
            break
    roll_slice = post.iloc[:end_roll]
    if "Localizer_NAV1_CDI" in roll_slice.columns:
        centerline_max = float(roll_slice["Localizer_NAV1_CDI"].abs().max())
    else:
        centerline_max = float("nan")

    wslice = seg.iloc[max(0, td_rel_idx - 200) : td_rel_idx + 1]
    w_kg = float(wslice["Weight_kg"].mean()) if "Weight_kg" in seg.columns else float("nan")
    rwy_cond = float(row_td.get("RunwaySurfaceCondition", float("nan")))

    return {
        "source_file": os.path.basename(source_file),
        "landing_id": landing_id,
        "Max_Localizer_Deviation_Below_1000ft": max_loc,
        "Max_GlideSlope_Deviation_Below_1000ft": max_gs,
        "Avg_Airspeed_Deviation_1000_to_50ft": avg_asp_dev,
        "Late_Configuration_Penalty_ft": late_pen,
        "Crosswind_Component_kt": cw,
        "Headwind_Component_kt": hw,
        "TotalWeight_kg": w_kg,
        "RunwayCondition": rwy_cond,
        "Touchdown_VerticalSpeed_fpm": td_vs,
        "Touchdown_Pitch_deg": td_pitch,
        "Touchdown_Distance_m": td_dist,
        "Time_to_Reverse_Thrust_s": time_rev,
        "Spoilers_Deployed_Properly": spoilers_ok,
        "Centerline_Deviation_Rollout_max_CDI": centerline_max,
        "Ocena": float("nan"),
    }


def process_file(path: str, landing_id_start: int) -> tuple[pd.DataFrame, int]:
    df = _load_csv(path)
    for c in REQUIRED_BASE:
        if c not in df.columns:
            raise ValueError(f"Brak kolumny '{c}' w {path}")

    tds = find_touchdown_indices(df)
    if not tds:
        return pd.DataFrame(), landing_id_start

    rows: list[dict] = []
    prev_end = -1
    lid = landing_id_start
    for td_idx in tds:
        r_end = rollout_end_index(df, td_idx)
        start = prev_end + 1
        end = r_end + 1
        seg = df.iloc[start:end].reset_index(drop=True)
        td_rel = td_idx - start
        if td_rel < 0 or td_rel >= len(seg):
            prev_end = r_end
            continue
        rows.append(compute_features_for_segment(seg, td_rel, path, lid))
        lid += 1
        prev_end = r_end

    return pd.DataFrame(rows), lid


def main() -> None:
    ap = argparse.ArgumentParser(description="FOQA-like landing features from FlightDataRecorder CSV")
    ap.add_argument("--input", nargs="*", help="Pliki CSV (można użyć wildcardów w cudzysłowie)")
    ap.add_argument("--input-dir", help="Katalog z CSV")
    ap.add_argument("--glob", default="Lot_*.csv", help="Wzorzec w --input-dir")
    ap.add_argument("--output", required=True, help="Wyjściowy CSV (jeden wiersz / lądowanie)")
    args = ap.parse_args()

    paths: list[str] = []
    if args.input:
        for p in args.input:
            paths.extend(glob.glob(p) if ("*" in p or "?" in p) else ([p] if os.path.isfile(p) else []))
    if args.input_dir:
        paths.extend(glob.glob(os.path.join(args.input_dir, args.glob)))

    paths = sorted(set(p for p in paths if os.path.isfile(p)))
    if not paths:
        raise SystemExit("Brak plików wejściowych.")

    all_dfs: list[pd.DataFrame] = []
    lid = 0
    for p in paths:
        part, lid = process_file(p, lid)
        if not part.empty:
            all_dfs.append(part)

    if not all_dfs:
        raise SystemExit("Nie znaleziono przyziemień (OnGround 0→1) w podanych plikach.")

    out = pd.concat(all_dfs, ignore_index=True)
    out.to_csv(args.output, index=False, encoding="utf-8", lineterminator="\n")
    print(f"Zapisano {len(out)} lądowań -> {args.output}")


if __name__ == "__main__":
    main()
