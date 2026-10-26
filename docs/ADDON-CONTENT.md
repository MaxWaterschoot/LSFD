# Add-on vehicles, props and peds

LSFD never ships game models. Like LSPDFR, it works with what the player has installed: vanilla GTA V models by default,
and any add-on model you point it to in the XML files.

## Where to find free models

- **[LCPDFR.com](https://www.lcpdfr.com/downloads/gta5mods/)**: the home of LSPDFR content, including many fire
  department vehicles (engines, ladders, rescues, brush units), liveries and equipment props. Free, but you need an account.
- **[GTA5-Mods.com](https://www.gta5-mods.com/)**: vehicles (search for "fire truck", "LAFD", "FDNY"), props and ped models.

Check each mod's license before using it. Most authors allow personal use but not re-uploading, so share links to
mods rather than the files themselves, and don't bundle them with LSFD.

## Installing an add-on vehicle

Most fire apparatus are released as *add-on* DLC packs:

1. Install the vehicle as its readme says (usually into `mods/update/x64/dlcpacks/` with OpenIV, plus an entry in `dlclist.xml`).
2. Find its spawn name in the readme (e.g. `ladder1`).
3. Add it to `Plugins/LSFD/Apparatus.xml`:

```xml
<Vehicle Name="Ladder 1" Model="ladder1" Type="Ladder" WaterCapacity="1500" PumpRate="25"
         HoseLength="30" SupplyLineLength="35" MinCrew="3" MaxCrew="4" Livery="0" Spawnable="true">
  <Equipment>
    <Item>AttackHose</Item>
    <Item>SupplyLine</Item>
    <Item>HydraulicSpreader</Item>
    <Item>HydraulicCutter</Item>
    <Item>Cribbing</Item>
  </Equipment>
</Vehicle>
```

If the model isn't installed, LSFD shows a warning instead of spawning it, so a shared `Apparatus.xml` won't break for
someone who doesn't have every vehicle.

## Tool props

Rescue tools are set in `Plugins/LSFD/Tools.xml`. To use an add-on spreader or rescue saw prop, set its model name and
how it sits in the hand:

```xml
<Tool Type="HydraulicSpreader" Prop="my_spreader_prop" Bone="57005"
      OffsetX="0.1" OffsetY="0" OffsetZ="0" Pitch="-90" Roll="0" Yaw="0"
      AnimDictionary="mini@repair" AnimName="fixing_a_player" UseSeconds="6" />
```

`Bone` is a ped bone ID (57005 is the right hand, 18905 the left hand). Adjust the offsets and rotation in-game until
the prop sits right, then use `ReloadPlugin LSFD.dll` to try again.

## Firefighter ped models

The player switches to `s_m_y_fireman_01` on duty, and crews use the same model. Add-on firefighter peds (EUP is a
popular option for uniforms) can replace it; set `ChangeModelOnDuty=false` in `LSFD.ini` if you dress your character yourself.
