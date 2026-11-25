# Tasks

## 1. Estado desde Jira hasta el modelo

- [ ] 1.1 Agregar `StatusCategory { Key }` a `StatusFields` y el enum `IssueStatusCategory { None, ToDo, InProgress, Done }`; verificar que el proyecto compila sin warnings.
- [ ] 1.2 Sumar `StatusName` y `StatusCategory` a `IssueSummaryResult` y mapearlos en `JiraClient.GetIssueSummary` (`new`/`indeterminate`/`done`, desconocida → `ToDo`, sin estado → `None`); verificar con casos nuevos en `JiraClientTest` para cada categoría, una desconocida y una respuesta sin `status`.
- [ ] 1.3 Exponer `StatusName` y `StatusCategory` en `IssueViewModel` con notificación de cambio, y asignarlos en `MainWindow.UpdateSummaryAsync` junto con `Summary`; verificar en `IssueViewModelTest` que cambiar cada uno notifica su propiedad.
- [ ] 1.4 Reenviar `StatusName` y `StatusCategory` en `MiniTimerRowViewModel`, incluida su notificación; verificar en `MiniTimerRowViewModelTest` que un cambio en el `IssueViewModel` se propaga.

## 2. Tema

- [ ] 2.1 Agregar `StatusToDo`, `StatusInProgress` y `StatusDone` a `Theme` (oscuro y claro) y registrarlos en `ThemeBrushes`; verificar que cambiar de tema en ejecución actualiza un brush `DynamicResource` de prueba.

## 3. Ícono en las filas

- [ ] 3.1 Agregar las tres geometrías y el estilo `StatusIcon` a `Styles.xaml` (geometría y brush por `StatusCategory`, colapsado con `None`, tooltip con `StatusName`); verificar que compila.
- [ ] 3.2 Insertar una columna de ancho fijo con el ícono entre el botón de abrir y el summary en `IssueRowCompact` e `IssueRowSpacious`, corriendo los `Grid.Column` posteriores; verificar en la app que el summary no se mueve al resolver el estado y que la spec `issue-list` sigue cumpliéndose (key y tiempo visibles al ancho mínimo).
- [ ] 3.3 Insertar la misma columna entre el key y el summary en la fila de `MiniTimerWindow`, corriendo los `Grid.Column` posteriores; verificar en la app con una y con varias filas.
- [ ] 3.4 Ajustar los tres colores sobre fila normal, seleccionada y con timer corriendo, en ambos temas, y revisar las formas en escala de grises; verificar contra el requisito "El indicador es legible en ambos temas".

## 4. Refresco tras la transición automática

- [ ] 4.1 En `ChangeIssueStateAsync`, tras un `DoTransition` exitoso, buscar la fila por key en el hilo de UI y llamar a `UpdateSummary`; verificar en la app con una transición de inicio configurada que el ícono cambia sin esperar el tick de 30 s.

## 5. Integración

- [ ] 5.1 Correr `dotnet build StopWatch.sln` y `dotnet test StopWatch.sln --settings .runsettings`; verificar que pasan sin warnings ni fallos.
