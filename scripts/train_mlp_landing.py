#!/usr/bin/env python3
"""
Przykład uczenia MLP na cechach lądowania — StandardScaler przed siecią (jak w materiale o iris).

  pip install -r requirements-ml.txt
  python train_mlp_landing.py --features landings_features.csv
  python train_mlp_landing.py --features landings_features.csv --dummy-labels
"""

from __future__ import annotations

import argparse

import numpy as np
import pandas as pd
from sklearn.metrics import classification_report
from sklearn.model_selection import train_test_split
from sklearn.neural_network import MLPClassifier
from sklearn.pipeline import Pipeline
from sklearn.preprocessing import StandardScaler

FEATURE_COLUMNS = [
    "Max_Localizer_Deviation_Below_1000ft",
    "Max_GlideSlope_Deviation_Below_1000ft",
    "Avg_Airspeed_Deviation_1000_to_50ft",
    "Late_Configuration_Penalty_ft",
    "Crosswind_Component_kt",
    "Headwind_Component_kt",
    "TotalWeight_kg",
    "RunwayCondition",
    "Touchdown_VerticalSpeed_fpm",
    "Touchdown_Pitch_deg",
    "Touchdown_Distance_m",
    "Time_to_Reverse_Thrust_s",
    "Spoilers_Deployed_Properly",
    "Centerline_Deviation_Rollout_max_CDI",
]


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--features", required=True, help="CSV z build_landing_features.py")
    ap.add_argument("--dummy-labels", action="store_true", help="Sztuczne Ocena z progu VS (test potoku)")
    ap.add_argument("--test-size", type=float, default=0.25)
    args = ap.parse_args()

    df = pd.read_csv(args.features)
    missing = [c for c in FEATURE_COLUMNS if c not in df.columns]
    if missing:
        raise SystemExit(f"Brak kolumn: {missing}")

    work = df[FEATURE_COLUMNS].replace("", np.nan).astype(np.float64)
    work = work.replace([np.inf, -np.inf], np.nan)

    if args.dummy_labels:
        vs = df["Touchdown_VerticalSpeed_fpm"].astype(float)
        y_series = pd.Series(np.zeros(len(df), dtype=int), index=df.index)
        y_series.loc[vs < -900] = 0
        y_series.loc[(vs >= -900) & (vs < -600)] = 1
        y_series.loc[(vs >= -600) & (vs < -400)] = 2
        y_series.loc[vs >= -400] = 3
        print("UWAGA: --dummy-labels to tylko test potoku, nie zastępuje etykiet eksperckich Ocena.")
    else:
        if "Ocena" not in df.columns:
            raise SystemExit("Brak kolumny Ocena — uzupełnij w CSV lub użyj --dummy-labels.")
        y_series = pd.to_numeric(df["Ocena"], errors="coerce")

    work["__y__"] = y_series
    work = work.dropna(subset=FEATURE_COLUMNS + ["__y__"])
    X = work[FEATURE_COLUMNS]
    y = work["__y__"].astype(int)

    if len(y) < 8:
        raise SystemExit("Za mało kompletnych próbek (min. ~8 wierszy z cechami i Ocena).")

    if len(np.unique(y)) < 2:
        raise SystemExit("Potrzebne są co najmniej 2 różne klasy w Ocena.")

    try:
        X_train, X_test, y_train, y_test = train_test_split(
            X,
            y,
            test_size=args.test_size,
            random_state=42,
            stratify=y,
        )
    except ValueError:
        X_train, X_test, y_train, y_test = train_test_split(
            X, y, test_size=args.test_size, random_state=42
        )

    clf = Pipeline(
        [
            ("scaler", StandardScaler()),
            (
                "mlp",
                MLPClassifier(
                    hidden_layer_sizes=(40, 20),
                    max_iter=2000,
                    random_state=42,
                    early_stopping=True,
                    validation_fraction=0.15,
                    n_iter_no_change=30,
                ),
            ),
        ]
    )
    clf.fit(X_train, y_train)
    y_pred = clf.predict(X_test)
    print(classification_report(y_test, y_pred, digits=3))
    print("Zastosowano: Pipeline(StandardScaler, MLPClassifier) — cechy są standaryzowane przed uczeniem.")


if __name__ == "__main__":
    main()
