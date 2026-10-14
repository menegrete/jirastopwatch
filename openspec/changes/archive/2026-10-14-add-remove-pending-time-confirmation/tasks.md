# Tasks

## 1. Ventana de confirmación

- [x] 1.1 Crear `UI/ConfirmRemoveWindow.xaml(.cs)` siguiendo el patrón de `EditTimeWindow` (fondo del tema, `SetTitleBarDarkMode`, `CenterOwner`, `ShowInTaskbar=False`), con texto de key + tiempo sin cargar (+ aviso de timer en curso), botones Eliminar y Cancelar, Cancelar como `IsDefault` e `IsCancel`; verificar que `dotnet build StopWatch.sln` compila sin warnings
- [x] 1.2 Cubrir el caso de fila sin key en el texto; verificar abriendo la ventana con key vacío y con key, en tema oscuro y claro (sin elementos del otro tema)

## 2. Integración en MainWindow

- [x] 2.1 En `MainWindow.RemoveIssue`, si `issue.CanPost` mostrar `ConfirmRemoveWindow` (`Owner = this`) y solo llamar a `issues.Remove` cuando `ShowDialog() == true`; sin `CanPost`, quitar de inmediato. Verificar manualmente con botón de la fila, Ctrl+Delete y Delete en el campo del key: fila con tiempo pide confirmación, fila en 0 no
- [x] 2.2 Verificar que Cancelar, Esc y Enter conservan la fila y el estado del timer (corriendo y en pausa), y que `dotnet test StopWatch.sln --settings .runsettings` sigue pasando

## 3. Specs y cierre

- [x] 3.1 Revisar los escenarios de `specs/issue-removal/spec.md` contra el comportamiento real y ejecutar `openspec validate add-remove-pending-time-confirmation`
- [x] 3.2 Commit `feat:` y PR con `Closes #55` al inicio del cuerpo
