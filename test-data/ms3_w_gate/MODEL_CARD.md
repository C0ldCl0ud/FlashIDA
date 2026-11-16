# W, model card (package ms3_w_gate 8.2, 2026-11-16)

## What W is

W is the probability that the MS3 of a planned target fragment **shows at least one bond of that fragment**: at least one
product ion that is a terminal piece of the target fragment is found in the MS3 spectrum. The bond may already be covered
by the MS2 or by an earlier MS3. W knows nothing about coverage and does not say how many bonds will be seen or whether
they are new.

- Label: on the offline deconvolution of the MS3 (FLASHDeconv, `-SD:min_cos 0.85 0.85 0.5`, cosine 0.5 for MS3), a
  deconvolved mass matches a product of the target fragment within 10 ppm after a per-run mass offset, the mass also tried
  one isotope spacing lower and higher. Products as the engine's MS3 matcher builds them (a and b from the fragment's
  N-terminal side; y from its C-terminal side for a y-type fragment, the two internal types for a b-type fragment).
- One evaluation per candidate MS3, made when the engine plans it. All 36 inputs exist at that moment; nothing measured
  on the MS3 itself is used.

## The model

`w_ext_v3.json`, SHA-256 `47f0babed8f470e242510765cfc3e49c35dcfce5d035efc149bf025a81ba425b`. Unchanged since model version 3 (2026-11-15).

- Gradient-boosted trees (XGBoost 3.4.1), objective binary:logistic: 84 trees, 3,398 leaves,
  3,314 splits, the deepest path has 6 splits. Base score 0.4159396.
- Settings of the fit: depth 6, learning rate 0.05, 80% of the rows and 50% of the inputs per tree, minimum child weight
  5, L2 1.0. Number of trees = the median of five fits that were stopped early, each on a development
  fold it was not fitted on; then one fit on all development proteins. Nothing was tuned on the test proteins.
- The inputs with the largest share of the model's gain: log10_intensity 21%, id_score 7%, x_ms2_fragment_count 7%, x_ms2_mass_count 5%, x_ms2_ions 5%, x_charge_cos 4%, mass_ms3 4%, x_rt_frac 4%.

## Training data

8,482 MS3 of 965 E. coli proteins (863 sequence clusters; the "development" proteins of the split
`data/splits.json`), 3,528 of them show a bond (41.6%). E. coli K12 lysate, reduced and alkylated, LC, Orbitrap: MS2 HCD, MS3 CID
in the Orbitrap at 240k with 4 microscans.

| Run | MS3 in the fit | show a bond | application mode | recorded with a live gate | HCD energy | MS3 CID energy |
|---|---|---|---|---|---|---|
| ecoli1110_run1 | 903 | 47% | Intact Protein | no | 29 | 35 |
| ecoli1110_run2 | 915 | 43% | Intact Protein | no | 29 | 35 |
| ecoli1110_run3 | 887 | 41% | Intact Protein | no | 29 | 35 |
| ecoli1110_run4 | 873 | 39% | Intact Protein | no | 29 | 35 |
| ecoli1110_run5 | 855 | 38% | Intact Protein | no | 29 | 35 |
| ecoli1112_run1 | 165 | 56% | Intact Protein | yes | 29 | 35 |
| ecoli1112_run2 | 444 | 65% | Intact Protein | yes | 29 | 35 |
| ecoli1112_run3 | 904 | 33% | Intact Protein | no | 29 | 25/30/35 |
| ecoli1112_run4 | 928 | 34% | Intact Protein | no | 29 | 25/30/35 |
| ecoli1112_run5 | 909 | 34% | Intact Protein | no | 29 | 25/30/35 |
| ecoli1112_run6 | 221 | 53% | Peptide | yes | 29 | 35 |
| ecoli1112_run7 | 478 | 53% | Peptide | yes | 29 | 35 |

Not in the fit: the 1,802 MS3 of the 224 E. coli test proteins (held out by sequence cluster), the two Caco-2
runs, and the three infusion runs.

## How it was checked

AUROC and calibration against the label above. Development proteins are predicted by models that never saw their
protein (out of fold); everything else by this model.

| Data | MS3 | Show a bond | Mean W | AUROC | Calibration slope | Show a bond with the live labels (cosine 0.85) |
|---|---|---|---|---|---|---|
| E. coli development proteins, all 12 runs, out of fold | 8,482 | 41.6% | 40.7% | 0.857 | 0.96 | 37.7% |
| E. coli development proteins, 8 runs in intact mode without a live gate, out of fold | 7,174 | 38.7% | 36.6% | 0.869 | 1.02 | 34.9% |
| E. coli test proteins (never used for fitting) | 1,802 | 39.0% | 38.1% | 0.892 | 1.19 | 34.5% |
| E. coli 1112 runs 6, 7, peptide mode (development proteins out of fold) | 828 | 53.1% | 64.5% | 0.774 | 0.95 | 49.2% |
| Caco-2 (human, 2 runs) | 2,156 | 27.4% | 36.7% | 0.856 | 0.99 | 24.1% |
| Myoglobin infusion | 555 | 64.0% | 74.1% | 0.622 | 0.46 | 60.0% |
| Cytochrome c infusion 1111 | 1,940 | 65.9% | 78.6% | 0.744 | 1.04 | 61.2% |
| Cytochrome c infusion 1112 | 3,331 | 63.0% | 68.5% | 0.859 | 1.49 | 62.9% |

Among the MS3 a gate on W would send (candidates with at least one uncovered bond; E. coli development proteins in the
eight runs in intact mode without a live gate, out of fold): at W >= 0.5 the mean W is 74% and 78%
show a bond; at W >= 0.835 it is 90% and 91%.

## Where it holds and where it does not

- **Holds:** E. coli lysate, LC, application mode Intact Protein, the settings above. Mean W and the observed share agree
  for development and test proteins.
- **Too high elsewhere.** Caco-2: 36.7% predicted, 27.4% observed. Peptide mode: 64.5% against 53.1%. The infusion
  runs are too high as well. The order of the MS3 still carries (AUROC 0.86 in Caco-2), the level does not.
- **The label is the offline deconvolution at cosine 0.5.** FLASHIda's live deconvolution uses 0.85 for MS3 and sees a
  bond in fewer MS3 (37.7% against 41.6% of the training MS3). Read against the live engine, W is high by that margin.
- **No coverage.** W ranks by "shows any bond". A gate that should keep new bonds at a strict threshold does better with
  the bond part multiplied in (`results/REPORT_v7.md` section 4.3, `results/REPORT_v8.md` part 3).
- **The collision energies hardly varied in the fit.** `ce1` was 29 in every run, so the model holds no information about
  another first-stage energy. `cid3` took three values (25 to 35), the two lower ones only in 3 runs. Outside the ranges
  of `INPUTS.md` a tree model returns the value of the nearest range; it does not extrapolate.
- **One charge state per stage.** Every recorded MS3 isolated a single charge state of the precursor and of the fragment
  (no co-isolation notches in any run). For a multiplexed MS3 the inputs describe the anchor charge only; W was never
  fitted on such MS3 and should not be trusted for them without new data.
- **No input was ever missing** in the 18,266 recorded MS3. The trees say where a missing value goes, but those
  branches were never fitted. An engine that cannot fill an input should not evaluate W for that MS3.
- **`x_rt_frac` needs the length of the run.** The fit used the last retention time of the finished run (67 min
  acquisitions). The engine has to use the planned length; other run lengths are untested.
- **Nothing here is a release of a gate.** Two thresholds are held as variants (0.5 and 0.835); neither is chosen, and
  the test plan for a run is a draft (`results/v7/test_plan_w_draft.json` of the repository).

## Versions

| | Date | What |
|---|---|---|
| W of model version 1 | 2026-11-14 | first W (labels at cosine 0.85); superseded |
| W of model version 3 | 2026-11-15 | this model: the same inputs and settings, refitted on labels at cosine 0.5 |
| package 7 | 2026-11-16 | the model as a standalone package with a reader and 300 reference rows |
| package 8 | 2026-11-16 | this documentation release; the model file, the tree table, the input list and the 300 reference rows are byte for byte those of package 7 |
