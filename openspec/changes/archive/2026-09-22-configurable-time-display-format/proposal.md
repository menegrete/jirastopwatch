## Why

Hoy la Main Window siempre muestra el tiempo transcurrido de cada fila (y el
total) en notación Jira ("2h 15m"). Algunos usuarios prefieren leer ese mismo
dato como un reloj ("2:15"), que es más rápido de leer de un vistazo y más
parecido a lo que ya se ve en el Mini Timer. Hoy no hay forma de elegir: el
formato está fijo en el código.

## What Changes

- Nuevo setting `TimeDisplayFormat` (`Jira` / `Clock`), persistido igual que
  `ListDensity`, con su combo en Settings.
- El formato elegido determina cómo se presentan, en la Main Window, el
  tiempo transcurrido de cada fila y el total de la lista.
- Formato `Clock`: `H:mm` (horas:minutos, sin segundos), siempre con la parte
  de horas aunque sea `0` (ej. `0:45`, `2:15`). Distinto del `TimeSpanToClockTime`
  ya existente (que sí muestra segundos, para Mini Timer y Taskbar Widget).
- Formato `Jira`: sin cambios, sigue usando `TimeSpanToJiraTime` como hoy.
- El diálogo de edición de tiempo (`EditTimeWindow`) también sigue el formato
  elegido: precarga el campo con el valor en ese formato, y acepta tipear en
  cualquiera de las dos notaciones (nunca son ambiguas entre sí: la notación
  Jira siempre termina en d/h/m, la de reloj nunca).
- Puramente cosmético en todo lo demás: no afecta el valor posteado a Jira
  como worklog (`JiraApiRequestFactory` sigue usando notación Jira
  directamente), ni la cadencia de refresco de la Main Window (sigue en 30s /
  al iniciar-pausar-postear, sin ticker nuevo).

## Capabilities

### New Capabilities

(ninguna)

### Modified Capabilities

- `issue-list`: agrega el requisito de que el usuario puede elegir el formato
  en que se presenta el tiempo transcurrido de cada fila y el total de la
  lista.

## Impact

- `source/StopWatch/Settings/Settings.cs` (+ `Properties/Settings.settings`,
  `Settings.Designer.cs`): nuevo setting `TimeDisplayFormat`.
- `source/StopWatch/Helpers/JiraTimeHelpers.cs`: nueva función de formateo
  reloj sin segundos (`H:mm`) y su parser inverso, más un dispatcher de
  formateo/parseo que lee el setting.
- `source/StopWatch/Model/IssueViewModel.cs`: `TimeElapsedText` respeta el
  setting.
- `source/StopWatch/Model/IssueListViewModel.cs`: `TotalTimeText` respeta el
  setting.
- `source/StopWatch/UI/SettingsWindow.xaml(.cs)`: nuevo combo para elegir el
  formato.
- `source/StopWatch/UI/EditTimeWindow.xaml(.cs)`: precarga y parsea según el
  formato elegido, con fallback a la otra notación.
