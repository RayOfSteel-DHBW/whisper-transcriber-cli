### Remaining ToDo: 
1. Introduce new setting/button "Output Mode", with two possible values:
- OutputMode.InPlace : Subtitles are put right next to the transcribed file, so media players can automatically find them.
- OutputMode.OutputDir : Current behaviour that places the resulting subtitle files in a separate directory.
2. Implement Settings Dialog:
- Move Output Dir to settings dialog
- New setting Default Acceleration: CPU/GPU . If CPU is selected the program will default to CPU Acceleration even if GPU is available. GPU is only selectable if Hardware Requirements are met (like it happens currently for the dropdown)
- New setting Default Model: Keep existing Model Dropdown on the main page, but add this setting to pre-select one of the existing models on startup in that dropdown.
- (Obviously) don't show the "No Models Found" dialog anymore when opening the settings dialog. Instead add a way to re-select the models directory from there.
3. Simplify Queue Logic:
- No need to manually save/load the queue.
- All of this behaviour can be implicit, i.e. persist queue every time something changes, load it on program start.
- As a consequence the queue-related buttons can be removed from the UI, except for "Clear Done" and "Delete Queue".
4. UI Adjustments:
- Move existing buttons at the bottom into the line where the now removed Output Dir label was. (NOT the grey message bar)
- Move button description to a on-hover tool-tip instead.
- But then the buttons' icons need to be more intuitive so increase their size a bit and change the Icons for "Clear Done" and "Delete Queue": Clear Done gets a broom, Delete Queue gets the trash bin (currently ClearDone's icon).
- Add a minimum Window width so the window cant be reduced so much that parts of our menu at the top are no longer visible.
5. Multi-Select Queue Items:
- Should be possible to select multiple items in the queue (Default Windows Behaviour: Ctrl for individual selection, Shift for Range selection, Ctrl+A for all selection)
- Add entries to the right-click context menu for switching model or language (if not already processing or finished).
- If multiple entries are selected then multiple entries should be edited by these context-menu changes.
- Use nested context menus to provide the options for selecting, so on rightclick you now see "Model>" which expands to show available models on click.
