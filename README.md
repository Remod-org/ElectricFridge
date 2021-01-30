# ElectricFridge
Attaches power requirements to the Rust refrigerator along with optional food decay.

## Configuration
```json
{
  "Settings": {
    "branding": "Frigidaire",
    "decay": false,
    "foodDecay": 0.98,
    "timespan": 600.0
  },
  "Version": {
    "Major": 1,
    "Minor": 0,
    "Patch": 1
  }
}
```

  - `branding` -- Fridge name, shown only in GUI upon opening fridge
  - `decay` -- Unused
  - `foodDecay` -- If true, food inside an unpowered fridge will decay over time
  - `timespan` -- How often to process food items in an unpowered fridge

