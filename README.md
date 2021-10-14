# ElectricFridge
Attaches power requirements to the Rust refrigerator along with optional food decay.

 To power the fridge as of version 1.0.5, there is an attached electrical branch on the right side when looking at the front of the fridge.  The branch power out is assigned to the fridge.  You cannot remove the connection but the power out from the branch is still usable.

 In version 1.0.4 and below, you will need to locate a point around the middle of the fridge from the front to apply power.  Yes, the heater.

## Command
  - /fr -- Enable or disable spawning electric fridge by default.  This will reverse the setting for each player initially from the current default setting below.  Memory for this setting is lost on each plugin reload or server restart, etc.  You will be notified of current status via chat.

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
	"defaultEnabled": true
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
  - `defaultEnabled` -- If true, spawn an electric fridge when placed by default.  Each player can switch this on or off using /fr.
