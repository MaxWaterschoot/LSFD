# Making your own LSFD callouts

LSFD loads callouts from any DLL in `GTA V/Plugins/LSFD/Callouts/`, the same way LSPDFR loads callout packs.
A working example lives in [`Examples/ExampleCallouts`](../Examples/ExampleCallouts).

## Project setup

Create a .NET Framework 4.8 class library (x64) and reference:

- `RagePluginHookSDK.dll` (from the RAGE Plugin Hook download, `SDK/` folder)
- `LSFD.dll` (keep `LSFD.xml` next to it for IntelliSense)

Set *Copy Local* to false for both; they are already in the GTA V folder.

## A callout

```csharp
using LSFD.API;
using Rage;

[CalloutInfo("Dumpster Fire", CalloutProbability.Medium, Category = CalloutCategory.Fire, Priority = CalloutPriority.Priority3)]
public class DumpsterFire : Callout
{
    private int fire;

    // Pick the location and texts. Return false to cancel (e.g. no good spot found).
    public override bool OnBeforeCalloutDisplayed()
    {
        CalloutPosition = Functions.GetRandomCalloutLocation();
        CalloutMessage = "Dumpster Fire";
        CalloutAdvisory = "A dumpster is on fire behind a building.";
        return true;
    }

    // The player is close: spawn the scene now, so it is streamed in.
    protected override void OnArrival()
    {
        fire = Fires.StartFire(CalloutPosition, 5);
    }

    // Every tick. End the callout yourself when the job is done.
    public override void Process()
    {
        if (HasArrived && Fires.CountFiresInRange(CalloutPosition, 20f) == 0)
            Complete();
    }

    // Clean up everything you created.
    protected override void OnEnd()
    {
        Fires.RemoveFire(fire);
    }
}
```

The class must not be abstract and needs a public parameterless constructor (the default one is fine).

### Life cycle

| Step | Method | Notes |
| --- | --- | --- |
| Dispatch | `OnBeforeCalloutDisplayed()` | Set `CalloutPosition`, `CalloutMessage`, `CalloutAdvisory`. Return `false` to cancel. |
| Offer | — | LSFD shows the notification and an area blip; the player presses the accept key. |
| Declined / ignored | `OnCalloutNotAccepted()` | |
| Accepted | `OnCalloutAccepted()` | LSFD adds a GPS route. Return `false` to abort. |
| Arrival | `OnArrival()` | Once, when the player is within `ArrivalDistance` (default 120 m). |
| Running | `Process()` | Every tick. Use `HasArrived` to tell where the player is. |
| End | `OnEnd()` | After `Complete()` (shows *Call completed*) or `End()`, or when the player goes off duty. |

You don't need to call `base` in any of these. Blips that LSFD made are removed for you.

If your callout throws an exception, LSFD logs it to `RagePluginHook.log`, ends the callout and keeps running.

### Category, priority and alarm level

| Setting | Values | Effect |
| --- | --- | --- |
| `Category` (attribute) | `Fire`, `Traffic`, `Medical`, `Rescue` | Players can turn categories off in `LSFD.ini`. Medical and traffic calls get an AI ambulance. |
| `Priority` (attribute, or set `Priority` in `OnBeforeCalloutDisplayed`) | `Priority1` (Code 3), `Priority2` (Code 3), `Priority3` (Code 2) | Shown as P1/P2/P3 in the dispatch notification and on the tablet. |
| `AlarmLevel` (set in `OnBeforeCalloutDisplayed`) | 0 (n/a), 1, 2, 3 | Every alarm above the first brings an extra AI engine company. `RollAlarmLevel()` picks one realistically. |
| `StagingPosition` (optional) | a street position | Where AI units drive to; defaults to `CalloutPosition`. |

### Probability

`CalloutProbability` is a weight: a `High` (8) callout is picked twice as often as a `Medium` (4) one. `Never` registers
the callout without dispatching it randomly; it can still be started with `Functions.StartCallout`.

### Incidents the player comes across

With `AllowAmbient = true`, the callout can also be an incident that happens on the road ahead while the player drives
around on duty, such as a crash or a car fire:

```csharp
[CalloutInfo("Car Fire", CalloutProbability.Medium, Category = CalloutCategory.Fire, AllowAmbient = true)]
```

- **Location:** LSFD picks a spot on the road ahead. Take your location from `Functions.GetRandomCalloutLocation()`;
  it returns that spot. If the callout picks a place of its own instead, LSFD doesn't use it for this.
- **Spawning:** `IsAmbient` is true, and `OnArrival` runs as soon as the player accepts, because the player is already
  close.
- **Requirement:** spawn things around `CalloutPosition` and don't rely on interiors.

## Useful API

| Member | What it does |
| --- | --- |
| `Functions.IsPlayerOnDuty()` | |
| `Functions.OnDutyStateChanged` | Event, `(bool onDuty)`. |
| `Functions.StartCallout(name)` | Offers a specific callout now. |
| `Functions.GetCurrentCallout()` / `StopCurrentCallout()` | |
| `Functions.GetRandomCalloutLocation(min, max)` | A street position around the player. |
| `Functions.DisplayNotification(subtitle, text)` | Notification in the LSFD dispatch style. |
| `Fires.StartFire(position, maxChildren, isGasFire)` | Returns a handle for `Fires.RemoveFire`. |
| `Fires.SetOnFire(entity)` | Sets a vehicle, ped or object on fire. |
| `Fires.CountFiresInRange(position, radius)` | Fires the player still has to put out. |
| `Fires.ExtinguishInRange(position, radius)` | |
| `StartFireIncident(origin, options)` (in your callout) | A fire that grows, spreads and escalates; see below. |

## Fire incidents: fires that grow

`Fires.StartFire` gives you a plain GTA fire: it may go out or move on its own and never grows. For a real fire, start
a `FireIncident` with `StartFireIncident` in `OnCalloutAccepted`, so it already burns while the player responds:

```csharp
private FireIncident fire;

public override bool OnCalloutAccepted()
{
    fire = StartFireIncident(CalloutPosition, new FireIncidentOptions
    {
        IsIndoor = true,          // stays on the floor, no spreading through walls, the interior fills with smoke
        InitialPoints = 1 + AlarmLevel,
        SpreadRadius = 10f,
        SmokeColumnHeight = 8f,   // where the smoke column rises, e.g. the roof
    });
    return true;
}

public override void Process()
{
    if (HasArrived && fire.IsOut) Complete();
}
```

LSFD removes the incident when the callout ends. What it does on its own:

- **Growth and spread**: every spot gets more intense over time and spreads to a new spot every `SpreadSeconds`
  (scaled by `GrowthMultiplier` in `LSFD.ini`), until water stops it.
- **Escalation**: when the fire burns as big as three quarters of `PointsPerAlarm` × the alarm level, it goes to the next
  alarm (up to `MaxAlarmLevel`), raises your `AlarmLevel` and dispatch sends another engine.
- **Status**: `fire.Status` goes Growing → Burning → UnderControl → Knockdown → Out. `fire.StatusChanged` tells you when.
- **Overhaul**: after knockdown the spots become hotspots that rekindle unless they are soaked. The fire is only `Out`
  when every hotspot is cold.
- **Smoke**: a column above the fire you can see from afar (black while it grows, white steam after knockdown) and,
  for indoor fires, smoke inside that hurts without an SCBA mask.

| Option | Default | |
| --- | --- | --- |
| `InitialPoints` / `InitialIntensity` | 2 / 0.5 | Spots burning at the start, intensity 0-1. |
| `SpreadRadius` | 8 | How far from the origin it can spread (m). |
| `SpreadSeconds` | 50 | Seconds between spreads. |
| `PointsPerAlarm` / `MaxAlarmLevel` | 6 / 3 | Escalation, see above. |
| `IsIndoor` / `IsGasFire` | false | |
| `SmokeColumnHeight` | 1 | Meters above the origin. |
| `Class` | `Ordinary` | `Flammable` (foam works, water barely), `Electrical` (water shocks until the power is isolated), `Battery` (EV: lots of water, keeps rekindling). |
| `PowerPanel` | null | For `Electrical`: where the player isolates the power. |

## Scene actions

Things the player does at a spot by holding the interact key, shown with a yellow arrow:

```csharp
AddSceneAction(new SceneAction(valvePosition, "close the ~y~gas valve~s~", () => valveClosed = true)
{
    RequiredEquipment = EquipmentType.Crowbar,   // optional: hand tool or weapon from an engine
    HoldSeconds = 6f,
    ProgressLabel = "CLOSING VALVE",
});
```

`CanUse` and `RequirementHint` add your own condition; `Repeatable` keeps the action after it was done. Actions added
with `AddSceneAction` are removed when the callout ends.

## Hazards and doors

| Member | What it does |
| --- | --- |
| `Hazards.AddToxicAtmosphere(position, radius, strength)` | Invisible gas (e.g. CO) that hurts without an SCBA. Lower `Strength` as it is ventilated, `Remove()` when clear. |
| `Interiors.LockDoors(position, radius, chance)` | Locks doors around a building; they must be forced with a halligan or axe. Unlocked when the call ends. |
| `Functions.IsPlayerCarrying(equipment)` | Whether the player has a tool, weapon or the thermal camera. |

## Vehicle rescue

```csharp
var wreck = new Vehicle("asea", CalloutPosition) { IsPersistent = true };
Rescue.CrashVehicle(wreck, new Vector3(0f, 1f, 0f), 0.9f);  // frontal impact, severity 0-1

var patient = new Ped(CalloutPosition);
Rescue.TrapInVehicle(patient, wreck, -1);                   // driver seat; doors are jammed

// In Process():
if (Rescue.IsExtricated(patient)) Complete();

// In OnEnd():
Rescue.Release(patient, wreck);
```

The player has to stabilize the vehicle with cribbing before the hydraulic tools can be used, then force or cut the
door next to the patient's seat and extricate them. `Rescue.OnPatientExtricated` is raised when that happens.

## Patients

```csharp
Ped patient = Patients.Spawn(position, PatientCondition.CardiacArrest);       // a civilian that needs CPR
Ped victim = Patients.SpawnTrappedInBuilding(insidePosition, streetPosition);  // has to be carried out first

// In Process():
if (Patients.AreAllHandled(patient, victim)) Complete();

// In OnEnd():
Patients.Release(patient);
Patients.Release(victim);
```

Conditions are `Injured`, `Unconscious`, `CardiacArrest`, `Stable` and `Deceased`. Without care a patient gets worse:
injured → unconscious after 5-7 minutes → cardiac arrest after 4-6 more → deceased 4 minutes later without CPR. Smoke and
heat around the patient (from LSFD fire incidents) speed this up and cause smoke inhalation and burns.

Pass an `InjuryType` (`Medical`, `Trauma`, `Burns`, `SmokeInhalation`) to `Patients.Spawn`; burns take longer to treat.
`Patients.SpawnWalkingWounded(position)` adds a green patient with minor injuries that doesn't get worse.

With three or more patients close together it is a **mass casualty incident**: patients are triaged (START: red,
yellow, green, black) by the player or the crew, the crew and the ambulances then go red first.
`Patients.AreAllTriaged(...)` and `Patients.GetTriage(ped)` let your callout wait for that. Stable patients are taken to
hospital by an AI ambulance before `Patients.AreAllHandled` is true; `Functions.RequestBackup(ApparatusType.Ambulance, position)`
sends extra ambulances. The player and
the AI crew treat, resuscitate and carry patients; `Patients.OnConditionChanged` tells you when something changes.
Extricated vehicle occupants become `Injured` patients automatically.

## Buildings and Enable All Interiors

Mark callouts that take place in interiors that are locked in the base game, so they are only dispatched when the
[Enable All Interiors](https://www.gta5-mods.com/scripts/enable-all-interiors-wip) mod is installed:

```csharp
[CalloutInfo("Apartment Fire", CalloutProbability.Medium, RequiresOpenInteriors = true)]
```

| Member | What it does |
| --- | --- |
| `Interiors.AreInteriorsUnlocked` | Whether Enable All Interiors is installed (or forced on in `LSFD.ini`). |
| `Interiors.PrepareInterior(position)` | Loads the interior and waits until it is ready; call it in `OnArrival` before spawning anything inside. |
| `Interiors.GetRandomBuilding(min, max, kinds...)` | A random building from `FireLocations.xml` (name, kind, position inside, staging position on the street), only using locked interiors when they are unlocked. Returns null when nothing fits. |

## Running code on load: `CalloutPack`

Callouts are registered automatically. If you also need code to run when LSFD loads your DLL, add one class that
inherits from `CalloutPack`:

```csharp
public class MyPack : CalloutPack
{
    // Baked in at compile time: tells LSFD which API version this DLL was built against.
    public override int ApiVersion => Functions.ApiVersion;

    public override void Initialize()
    {
        Functions.OnDutyStateChanged += OnDutyStateChanged;
        Events.OnCalloutEnded += OnCalloutEnded;
    }

    // Unsubscribe here, or your handlers keep running after a ReloadPlugin.
    public override void Finally()
    {
        Functions.OnDutyStateChanged -= OnDutyStateChanged;
        Events.OnCalloutEnded -= OnCalloutEnded;
    }

    private static void OnDutyStateChanged(bool onDuty) { /* ... */ }
    private static void OnCalloutEnded(Callout callout) { /* callout.IsCompleted: handled or cancelled */ }
}
```

### API version

`Functions.ApiVersion` is a constant, so its value is baked into your DLL when you compile. Return it from `ApiVersion` and LSFD knows which API your pack expects.

When LSFD loads your DLL:
- **Your pack is built for the same or an older API:** it is loaded. New LSFD versions only add to the API; they don't remove or change what is there, so a pack keeps working after an LSFD update.
- **Your pack needs a newer API than the installed LSFD:** it is not loaded. None of its callouts are registered, and the player gets a message that LSFD needs an update. Without this check, the pack would crash halfway with a `MissingMethodException`.
- **No `ApiVersion` override:** the pack counts as version 1.
- **No pack at all:** the callouts are loaded as before.

Two more checks keep a broken pack from taking LSFD down:
- **Callout fails to start:** a callout that can't be created (e.g. built against an incompatible LSFD) is taken out of the rotation, with the reason in `RagePluginHook.log`.
- **Event handler throws:** an exception in one of your event handlers is logged and doesn't affect LSFD or other packs.

To check at runtime, `Functions.InstalledApiVersion` gives the version of the running LSFD.

| Version | Added |
|---|---|
| 1 | The first public API. |
| 2 | `Events`, `CalloutPack.ApiVersion`, XML callouts, `CalloutInfo.AllowAmbient` and `Callout.IsAmbient`. |

### Events

The events are in the static class `Events`. They run on LSFD's game fiber, so you can call RPH and natives from them directly.

| Event | Arguments | When |
|---|---|---|
| `Functions.OnDutyStateChanged` | `bool onDuty` | The player goes on or off duty. |
| `Events.OnCalloutAccepted` | `Callout` | The player accepted a call; its `OnCalloutAccepted` has run. |
| `Events.OnCalloutEnded` | `Callout` | An accepted call ended. `callout.IsCompleted` tells handled from cancelled. |
| `Events.OnFireExtinguished` | `FireIncident` | A fire incident is completely out, hotspots included (any incident, also from other packs). |
| `Events.OnPatientRescued` | `Ped` | A victim was carried out of a building to safety. |
| `Events.OnPatientStabilized` | `Ped` | A patient became stable, by treatment or successful CPR. |
| `Events.OnPatientTransported` | `Ped` | An ambulance handed a patient over at a hospital; the ped is deleted right after. |
| `Events.OnApparatusSpawned` | `Vehicle, ApparatusType, bool isAiUnit` | An apparatus was spawned at a station, for the player or as an AI unit. |

Other events: `Patients.OnConditionChanged`, `Rescue.OnPatientExtricated` and `FireIncident.StatusChanged`.

## No programming: XML callouts

Simple calls (a fire, patients, vehicles, a few tasks) can also be made without C#, in an XML file. See [XML-CALLOUTS.md](XML-CALLOUTS.md).

## Testing

Put your DLL in `GTA V/Plugins/LSFD/Callouts/`, reload LSFD (`ReloadPlugin LSFD.dll` in the RPH console) and go on duty. Then:

- `LSFD_ListCallouts` lists every registered callout (check yours is there, otherwise read `RagePluginHook.log`).
- `LSFD_Dispatch "Dumpster Fire"` offers your callout right away.
