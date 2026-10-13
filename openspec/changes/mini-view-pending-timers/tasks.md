# Tasks

## 1. Modelo: filas que sigue la vista mini

- [x] 1.1 En `ActiveTimerViewModel`, agregar el parámetro `Func<bool> allowMultiple` al constructor (con una sobrecarga de un argumento equivalente a `false` para los llamadores existentes) y verificar que `dotnet build StopWatch.sln` compila sin tocar `PluginLoaderTest`.
- [x] 1.2 Agregar `TrackedSources`, calculada en `Refresh()` según la regla del design (modo apagado: corriendo o `ActiveSource`; modo activado: corriendo o con `TimeElapsed > 0`, en orden de la lista, con fallback a `ActiveSource`), y verificar que `RunningSources` no cambió.
- [x] 1.3 Cablear `() => settings.AllowMultipleTimers` en `AppComposition` y verificar que el modelo lee el valor actual en cada `Refresh()` (test que cambia el flag entre dos llamadas).
- [x] 1.4 Pruebas en `ActiveTimerViewModelTest` para los escenarios de la spec: corriendo + pausado con tiempo conserva el orden, pausado en cero no aparece, timer que vuelve a cero sale, modo apagado no lista pausados con tiempo, ninguno corrió → fila del issue seleccionado. Verificar con `dotnet test StopWatch.sln --settings .runsettings --filter "FullyQualifiedName~ActiveTimerViewModelTest"`.

## 2. Vista mini

- [x] 2.1 Hacer que `MiniTimerWindow.ResolveSources()` devuelva `TrackedSources` y eliminar el fallback propio, y verificar que `SyncRows` sigue sin recrear filas sin cambios (revisión de código y prueba manual con dos timers).
- [x] 2.2 Atenuar la fila pausada en `MiniTimerWindow.xaml` con un `DataTrigger` sobre `IsRunning`, y verificar manualmente que la fila pausada se ve atenuada, conserva el botón de reanudar y no cambia de lugar al pausar o reanudar.
- [x] 2.3 Verificar manualmente el recorrido completo con "permitir varios" activado y apagado: dos timers corriendo → pausar uno (queda atenuado) → reanudarlo → pausarlo y cargar su tiempo a Jira desde la ventana completa (la fila sale al volver a la vista mini) → con la opción apagada, cambiar de issue no deja una segunda fila.

## 3. Cierre

- [x] 3.1 Correr `dotnet build StopWatch.sln` y `dotnet test StopWatch.sln --settings .runsettings` completos y verificar que pasan sin warnings (`TreatWarningsAsErrors` está activo).
