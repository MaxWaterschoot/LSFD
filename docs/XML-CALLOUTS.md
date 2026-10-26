# XML callouts: your own calls without programming

You can add calls to LSFD with a text file. Put an `.xml` file in `GTA V/Plugins/LSFD/Callouts/`. LSFD loads every such file when it starts.

`ExampleCallouts.xml` in that folder has three calls you can copy:
- a kitchen fire,
- a gas smell,
- a car crash with a trapped driver.

Need more than this (your own logic, conversations, special props)? Write a callout in C# instead, see [CALLOUTS.md](CALLOUTS.md).

## Trying a call

1. Save the file and run `LSFD_ReloadXmlCallouts` in the RPH console (F4). This reloads only the XML calls, so LSFD doesn't need a restart.
2. Go on duty.
3. Run `LSFD_ListCallouts` and check that your call is in the list.
4. Run `LSFD_Dispatch "Your Call Name"` to get it offered right away.

If your call isn't listed, open `RagePluginHook.log` and search for `LSFD`. The log says why the call was skipped, for example:
- a typo in the XML, with its line and column,
- a vehicle model that doesn't exist,
- a call with nothing to do.

## How a call works

1. **Offer:** LSFD picks a location and offers the call with your `Message` and `Advisory`.
2. **Accept:** the `Fire` starts burning and grows while you drive there, so a late response means a bigger fire. LSFD sends the usual units, plus the extra units you ask for with `Backup`.
3. **Arrival:** within about 120 m of the location, LSFD spawns the vehicles, patients, bystanders, toxic atmospheres and tasks, and shows `ArrivalHelp`.
4. **Done:** the call completes by itself once:
   - the fire is completely out,
   - every trapped driver is extricated,
   - every patient is handled,
   - every task is done.

   You can also end the call yourself from the tablet.

## A complete example

```xml
<?xml version="1.0" encoding="utf-8"?>
<Callouts>
  <Callout Name="Kitchen Fire" Probability="Low" Category="Fire" Priority="Priority1">
    <Message>Kitchen Fire</Message>
    <Advisory>Pan fire spreading to the cabinets.</Advisory>
    <ArrivalHelp>Put out the ~r~fire~s~ and treat the ~r~occupant~s~.</ArrivalHelp>
    <CompletedMessage>Fire out and the occupant is being cared for.</CompletedMessage>
    <Location MinDistance="300" MaxDistance="900" AreaRadius="30" />
    <Alarm Level="1" />
    <Fire Points="2" Intensity="0.5" Class="Flammable" />
    <Patient Condition="Injured" Injury="Burns" Distance="10" />
    <Bystander Count="2" />
  </Callout>
</Callouts>
```

A file can hold as many `<Callout>` elements as you like. Every element below is optional, but a call needs something to do: at least one `Fire`, `Patient` or `Task`, or a `Vehicle` with `OnFire` or `TrapDriver`.

You can put colours in all texts:
- `~r~` red
- `~o~` orange
- `~y~` yellow
- `~g~` green
- `~b~` blue
- `~s~` back to normal

## `<Callout>`

| Attribute | Values | Default |
|---|---|---|
| `Name` | Unique name of the call (also for `LSFD_Dispatch`) | required |
| `Probability` | `Never`, `VeryLow`, `Low`, `Medium`, `High`, `VeryHigh`. With `Never`, the call is only offered through `LSFD_Dispatch`. | `Medium` |
| `Category` | `Fire`, `Traffic`, `Medical`, `Rescue`, `Hazmat`. Players can turn off categories in `LSFD.ini`, and `Traffic` and `Medical` calls get an ambulance automatically. | `Fire` |
| `Priority` | `Priority1` (Code 3), `Priority2` (Code 3), `Priority3` (Code 2) | `Priority2` |
| `Ambient` | `true`: the call can also happen on the road ahead while you drive around on duty, as an incident you come across. It then needs a random location, without `Position` elements. | `false` |

| Child element | What it is |
|---|---|
| `<Message>` | Title of the dispatch notification; `Name` when left out |
| `<Advisory>` | Details in the dispatch notification |
| `<ArrivalHelp>` | Help text when you arrive |
| `<CompletedMessage>` | Notification when the call is done |

## `<Location>`

Without positions, LSFD picks a random street around the player:

```xml
<Location MinDistance="300" MaxDistance="900" AreaRadius="30" />
```

With positions, LSFD picks one of them at random:

```xml
<Location AreaRadius="40">
  <Position X="-561.2" Y="-213.5" Z="37.6" Heading="120" />
  <Position X="1204.8" Y="-1452.0" Z="34.9" Heading="0" />
</Location>
```

To find a position, stand on the spot and run `LSFD_PrintPosition` in the console. The position and heading are printed in the console and the log.

| Attribute | Meaning | Default |
|---|---|---|
| `MinDistance`, `MaxDistance` | Range for the random street, in meters | 300, 900 |
| `AreaRadius` | Size of the area on the map; vehicle fires are counted within this radius | 30 |

## `<Alarm>`

The alarm level decides how many engines respond: every alarm above the first sends an extra engine company.

```xml
<Alarm Level="2" />                                        <!-- always a 2nd alarm -->
<Alarm SecondAlarmChance="30" ThirdAlarmChance="10" />     <!-- random: 30% 2nd, 10% 3rd, otherwise 1st -->
```

Without `<Alarm>`, a call with a `Fire` is a 1st alarm, and any other call has no alarm level.

## `<Fire>`

A fire from the LSFD fire system:
- it grows and spreads,
- it moves to a higher alarm when it gets out of hand,
- after knockdown it leaves hotspots that you have to overhaul.

```xml
<Fire Points="3" Intensity="0.6" SpreadRadius="12" SpreadSeconds="40" PointsPerAlarm="6" MaxAlarm="3" Class="Ordinary" Gas="false" />
```

| Attribute | Meaning | Default |
|---|---|---|
| `Points` | Number of fire spots at the start | 2 |
| `Intensity` | 0 to 1, how hard the spots burn at the start | 0.5 |
| `SpreadRadius` | How far from the location the fire can spread, in meters | 10 |
| `SpreadSeconds` | Seconds between spreads to a new spot; lower spreads faster | 50 |
| `PointsPerAlarm` | Size of one alarm in fire spots, for escalation | 6 |
| `MaxAlarm` | Highest alarm it can escalate to | 3 |
| `Class` | `Ordinary` (water), `Flammable` (foam), `Electrical` (extinguisher, or water once the power is isolated), `Battery` (lots of water, rekindles) | `Ordinary` |
| `Gas` | Burns hotter and spreads faster | false |
| `PowerPanelDistance` | For `Electrical`: the electrical panel is this many meters from the fire. Hold E there to isolate the power. -1 means there is no panel. | -1 |

## `<Patient>`

```xml
<Patient Condition="Unconscious" Injury="SmokeInhalation" Count="2" Distance="8" />
<Patient Minor="true" Count="3" />
```

| Attribute | Values | Default |
|---|---|---|
| `Condition` | `Injured`, `Unconscious`, `CardiacArrest`, `Stable`, `Deceased` | `Injured` |
| `Injury` | `Medical`, `Trauma`, `Burns`, `SmokeInhalation`. Burns take longer to treat. | `Trauma` |
| `Count` | How many | 1 |
| `Minor` | Walking wounded: minor injuries, sitting, green at triage, does not get worse | false |
| `Distance` | Placed at random within this many meters of the location | 6 |

How patients behave in the game:
- **Without care, they get worse:** injured → unconscious → cardiac arrest → deceased. Smoke and heat speed this up.
- **Triage:** with 3 or more patients, triage (START) is used.
- **Transport:** when patient transport is on, an ambulance takes the stable patients away.

## `<Vehicle>`

```xml
<Vehicle Model="random" Crash="0.8" TrapDriver="true" />
<Vehicle Model="mule" Distance="8" OnFire="true" />
```

| Attribute | Meaning | Default |
|---|---|---|
| `Model` | Vehicle model name, or `random` for a random car | `random` |
| `Crash` | 0 is intact, 1 is a heavy crash | 0 |
| `TrapDriver` | A driver is trapped and has to be extricated with cribbing and the hydraulic tools, then treated | false |
| `OnFire` | The vehicle is on fire (GTA fire, put it out with water or an extinguisher) | false |
| `Distance` | Placed at random within this many meters of the location, on the road. 0 means exactly on the location. | 0 |

## `<Bystander>`

Witnesses standing around on the phone, as decoration.

```xml
<Bystander Count="2" Distance="12" />
```

## `<ToxicAtmosphere>`

An invisible toxic gas around the location, such as carbon monoxide or a gas leak. Breathing in it without the SCBA face piece costs health.

```xml
<ToxicAtmosphere Radius="10" Strength="0.7" />
```

`Strength` runs from 0 to 1. The gas stays until a task with `ClearsToxicAtmosphere="true"` is done, or until the call ends.

## `<Task>`

A job at a spot of the scene. The player holds E at a yellow marker for a few seconds. Examples:
- measure gas,
- close a valve,
- check the alarm panel.

```xml
<Task Text="close the ~y~gas valve" Seconds="5" OffsetX="-2" OffsetY="4" Equipment="" DoneMessage="Gas ~g~off~s~." ClearsToxicAtmosphere="true" />
```

| Attribute | Meaning | Default |
|---|---|---|
| `Text` | Ends "Hold E to ...", e.g. `check the ~y~alarm panel` | `check the ~y~scene` |
| `Seconds` | How long to hold E | 4 |
| `Equipment` | Something the player must carry. Empty means nothing is needed. | empty |
| `OffsetX`, `OffsetY`, `OffsetZ` | Meters from the location (X east, Y north, Z up) | 0 |
| `DoneMessage` | Subtitle when done | none |
| `ClearsToxicAtmosphere` | Removes the toxic atmospheres when done (e.g. "ventilate") | false |

Values for `Equipment`:
- `FireExtinguisher`, `Axe`, `Crowbar`, `Flashlight`
- `GasDetector`, `Absorbent`, `TrafficCones`
- `ThermalCamera`, `AED`

## `<Backup>`

Extra AI units, on top of the units LSFD sends by itself:
- an engine for every alarm above the first,
- an ambulance for `Traffic` and `Medical` calls.

```xml
<Backup Type="Ambulance" Count="2" />
<Backup Type="Ladder" />
```

`Type` must be a type from `Apparatus.xml`: `Engine`, `Ladder`, `Rescue`, `Ambulance` and so on.
