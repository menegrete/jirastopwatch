## 1. Setting

- [x] 1.1 Agregar el enum `TimeDisplayFormat { Jira = 0, Clock = 1 }` en
      `Settings.cs`, junto a los demás enums de settings (`ListDensity`, etc.).
- [x] 1.2 Agregar el setting `TimeDisplayFormat` en `Properties/Settings.settings`
      (Type `System.Int32`, Scope `User`, default `0`), y regenerar
      `Settings.Designer.cs`.
- [x] 1.3 Agregar la propiedad `TimeDisplayFormat` en `Settings.cs` (miembro,
      `ReadSettings`, `Save`), siguiendo el patrón de `ListDensity`.

## 2. Formateo

- [x] 2.1 Agregar en `JiraTimeHelpers.cs` una función de formato reloj sin
      segundos (`H:mm`, con la parte de horas siempre presente incluso en
      cero) - distinta de `TimeSpanToClockTime`, que muestra segundos.
- [x] 2.2 Agregar en `JiraTimeHelpers.cs` (o donde corresponda) una forma de
      elegir entre `TimeSpanToJiraTime` y la nueva función reloj según el
      `TimeDisplayFormat` vigente.

## 3. Consumo en el modelo

- [x] 3.1 `IssueViewModel.TimeElapsedText` usa el formato elegido. Terminó
      leyendo `JiraTimeHelpers.TimeDisplayFormat` (un static, igual que
      `Configuration`) en lugar de `Settings.Instance` directo, porque
      `IssueViewModel` no tiene referencia a `Settings`; `Settings.ReadSettings`/
      `Save` mantienen ese static sincronizado.
- [x] 3.2 `IssueListViewModel.TotalTimeText` usa el mismo formato elegido.
- [x] 3.3 Confirmado: `JiraApiRequestFactory` (el POST del worklog) sigue
      usando `TimeSpanToJiraTime` directo, sin pasar por el setting.

## 3b. EditTimeWindow sigue el formato

- [x] 3b.1 Agregar en `JiraTimeHelpers.cs` un parser de notación reloj
      (`ClockHoursMinutesToTimeSpan`, inverso de `TimeSpanToClockHoursMinutes`)
      y un dispatcher de parseo (`DisplayTimeToTimeSpan`) que prueba primero
      el formato elegido y cae al otro - nunca son ambiguos entre sí.
- [x] 3b.2 `EditTimeWindow` precarga `tbTime` con `TimeSpanToDisplayTime` y
      valida con `DisplayTimeToTimeSpan` en vez de usar `TimeSpanToJiraTime`/
      `JiraTimeToTimeSpan` directos.
- [x] 3b.3 Actualizar el texto de ayuda de `EditTimeWindow.xaml` para mencionar
      también la notación reloj.

## 4. Settings UI

- [x] 4.1 Agregar el combo `cbTimeDisplayFormat` en `SettingsWindow.xaml`,
      junto al de `cbListDensity`.
- [x] 4.2 Cablear el combo en `SettingsWindow.xaml.cs`: `Fill` con las dos
      opciones (Jira / Clock) y guardarlo al aceptar, siguiendo el patrón de
      `cbListDensity`.

## 5. Refresco al cambiar el setting

- [x] 5.1 Verificado: al volver de Settings con un `TimeDisplayFormat`
      distinto, `MainWindow.EditSettings()` llama
      `issues.NotifyTimeDisplayFormatChanged()` (nuevo método, mismo patrón que
      `NotifyDensityChanged`), que refresca filas y total sin esperar al
      próximo tick de 30s.

## 6. Pruebas

- [x] 6.1 Tests unitarios de la nueva función de formato reloj en
      `JiraTimeHelpers` (casos: 0 horas, horas exactas, minutos con y sin
      cero a la izquierda, sin segundos) y del dispatcher `TimeSpanToDisplayTime`.
- [x] 6.2 Tests de `IssueViewModel.TimeElapsedText` / `IssueListViewModel.TotalTimeText`
      respetando el setting elegido, y de los métodos `NotifyTimeDisplayFormatChanged`.

## 7. Specs

- [x] 7.1 Corrido `openspec archive` (con merge manual del delta spec por un
      EPERM de Windows al mover la carpeta del change).
