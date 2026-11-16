# Changes

## 8.2 (2026-11-16)

Charge groups with co-isolation notches. The model, the tree table, the reference rows and the reader are unchanged.

- `inputs_from_log.py`: a stage group that holds several ','-joined values (FLASHIda writes `charge`, `precursor_mz`
  and `isolation_width` that way when notches are co-isolated) gives its first value, the anchor. In 8.1 such a group
  did not parse and the input came out missing. No recorded run has such a group, so the check against the tables of
  the fit is unchanged.
- `MODEL_CARD.md`, `INPUTS.md`, `ENGINE.md`: `z_ms2` and `z_ms3` are the anchor charges; W was never fitted on an MS3
  that isolates several charge states.

## 8.1 (2026-11-16)

Exact definitions of the inputs, after questions from the implementation in FLASHIda. The model, the tree table, the
reference rows and the reader are unchanged.

- New: `inputs_from_log.py`, which rebuilds all raw inputs from the log tables of a run folder; it reproduces the tables
  of the fit for all 18,266 recorded MS3.
- `INPUTS.md`, `inputs_detail.tsv`: exact definitions of `n_ms2_prior`, `n_ms3_prior`, `x_ms1_plan`, `x_plan_age`,
  `x_queue_ahead`, `x_rank`, `has_shift`, `id_score`, `x_ms2_ions`; `ce1` and `cid3` are the first and last value of
  `collision_energy` of the MS3 command (package 8 said "method / scan filter").
- `ENGINE.md`: section 9.

## 8 (2026-11-16)

Documentation release. The model is unchanged.

- Byte for byte from package 7: `w_ext_v3.json`, `trees.tsv`, `inputs.tsv`, `reference_rows.tsv`.
- New: `MODEL_CARD.md`, `INPUTS.md`, `inputs_detail.tsv`, `ENGINE.md`, `reference_rows_ties.tsv`, this file.
- `w_gate.py`: `--selfcheck` runs every `reference_rows*.tsv`. The evaluation code is that of package 7; both readers give
  the same W for every recorded MS3.
- `README.md` and `MANIFEST.json` rewritten for this version.

## 7 (2026-11-16)

First package: the model file, the flat tree table, the input list, 300 reference rows, the reader.
