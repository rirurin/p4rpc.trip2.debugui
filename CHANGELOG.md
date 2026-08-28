# Changelog

## 0.2.0

### p4rpc.trip2.debugui
- Added box-drawing characters (U+2500 to U+257F) to font range for Noto Sans
- Save the current theme and window size and position into the Reloaded-II config. Updating any of these properties in the window edits the config values and vice versa
- Updated riri.yamlscans.ReloadedII to 1.2.1

### p4rpc.trip2.debug.reloadedconsole

- Use UTF-8 for logger

### p4rpc.trip2.debug.testtoolkit

- Added test for blueprint invokable method (ProcessEvent)
- Updated UE.Toolkit to 1.10.4

### p4rpc.trip2.debug.uobjectviewer

- Edit TArray and TMap properties in objects
- View and edit rows in DataTables
- Create new tab in UObject window to view a list of functions and invoke functions with set parameters
- When trying to open a UObject that already has a window open, focus the existing window instead of crashing
- Fixed duplicate IDs being listed for some object properties
- Updated UE.Toolkit to 1.10.4

## 0.1.0

Initial release