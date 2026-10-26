# Making your own LSFD callouts

LSFD loads callouts from any DLL in `GTA V/Plugins/LSFD/`, the same way LSPDFR loads callout packs.
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

Conditions are `Injured`, `Unconscious`, `CardiacArrest`, `Stable` and `Deceased`. A cardiac arrest without CPR dies
after about four minutes; a victim left in a burning building goes into cardiac arrest after about five. The player and
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
    public override void Initialize() => Functions.OnDutyStateChanged += onDuty => { /* ... */ };
    public override void Finally() { }
}
```

## Testing

Put your DLL in `GTA V/Plugins/LSFD/`, reload LSFD (`ReloadPlugin LSFD.dll` in the RPH console) and go on duty. Then:

- `LSFD_ListCallouts` lists every registered callout (check yours is there, otherwise read `RagePluginHook.log`).
- `LSFD_Dispatch "Dumpster Fire"` offers your callout right away.
