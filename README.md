Local LogiX Registers
=====================

A [NeosModLoader](https://github.com/zkxs/NeosModLoader) mod for [Neos VR](https://neos.com/) that adds the ability to create LogiX registers and boolean latches with localized values. This can be toggled on and off with a new context menu entry while having the tool equipped.  
It now adds the ability to toggle the localization/synchronization of any held register or all registers in a whole hierarchy,
as long as they all have the same state. Individual fields can also be localized by grabbing them with the LogiX tip and using the context menu entry.

The icons for the context menu can be changed in the settings.  
Localisation state is toggled per session, with the default config option determining what it starts as.

## Installation
1. Install [NeosModLoader](https://github.com/zkxs/NeosModLoader).
2. Place [LocalLogixRegisters.dll](https://github.com/Banane9/NeosLocalLogixRegisters/releases/latest/download/LocalLogixRegisters.dll) into your `nml_mods` folder. This folder should be at `C:\Program Files (x86)\Steam\steamapps\common\NeosVR\nml_mods` for a default install. You can create it if it's missing, or if you launch the game once with NeosModLoader installed it will create the folder for you.
3. Add Cyro's [Nodentify](https://github.com/RileyGuy/Nodentify) mod to have the changed names restored when unpacking the created LogiX nodes again.
4. Start the game. If you want to verify that the mod is working you can check your Neos logs.

## In Game

![LogiX Tip Context Menu when creating regular synchronized Registers](https://github.com/Banane9/NeosLocalLogixRegisters/raw/master/screenshots/MenuSynchronizing.png)

![LogiX Tip Context Menu when creating localized Registers](https://github.com/Banane9/NeosLocalLogixRegisters/raw/master/screenshots/MenuLocalizing.png)

![Different kinds of localized Registers](https://github.com/Banane9/NeosLocalLogixRegisters/raw/master/screenshots/Registers.png)

![How the localization works](https://github.com/Banane9/NeosLocalLogixRegisters/raw/master/screenshots/Localization.png)

![Localizing all Registers in a whole hierarchy](https://github.com/Banane9/NeosLocalLogixRegisters/raw/master/screenshots/LocalizeAllRegisters.png)

![Synchronizing all Registers in a whole hierarchy](https://github.com/Banane9/NeosLocalLogixRegisters/raw/master/screenshots/SynchronizeAllRegisters.png)

![Localizing held Register](https://github.com/Banane9/NeosLocalLogixRegisters/raw/master/screenshots/LocalizeRegister.png)

![Synchronize held Register](https://github.com/Banane9/NeosLocalLogixRegisters/raw/master/screenshots/SynchronizeRegister.png)

![Localize individual Field](https://github.com/Banane9/NeosLocalLogixRegisters/raw/master/screenshots/LocalizeField.png)

![Localized individual Field](https://github.com/Banane9/NeosLocalLogixRegisters/raw/master/screenshots/LocalizedField.png)
