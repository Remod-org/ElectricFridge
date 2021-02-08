# ElectricFridge
Attaches power requirements to the Rust refrigerator along with optional food decay.

## Configuration
```json
{
  "Settings": {
    "branding": "Frigidaire",
    "decay": false,
    "foodDecay": 0.98,
    "timespan": 600.0,
	"blockPickup": true,
	"blockLooting": false
  },
  "Version": {
    "Major": 1,
    "Minor": 0,
    "Patch": 2
  }
}
```

  - `branding` -- Fridge name, shown only in GUI upon opening fridge
  - `decay` -- If true, food inside an unpowered fridge will decay over time.  If false, the next two configs will be unimportant.
  - `foodDecay` -- Percentage of food left behind on each stack at each timespan.  The default of 0.98 should decrement by 2% on each run.
  - `timespan` -- How often to process food items in an unpowered fridge
  - `blockPickup` -- If true, block picking up fridge if powered.
  - `blockLooting` -- If true, block looting when fridge is NOT powered.
