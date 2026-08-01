# Early Work Maturity validation

`RC_Evolution_AcceleratedBroodMaturity` restores the tier-1 evolution from the balance table.

Runtime behavior:

- age-restricted work types unlock at the previous positive work-age threshold;
- HSK thresholds therefore shift from 7 to 3, 10 to 7, and 13 to 10;
- the earliest age-3 work types are not shifted into the baby stage;
- childhood aging speed is not modified.

Validation entry point:

```bash
python RedCrow_Insector_Tweaks/Source/validate_early_work_maturity.py
```
