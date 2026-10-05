# Proposal

## Why

Hoy la carga de tiempo es un camino cerrado: `MainWindow.PostWorklogAsync` llama directo a `IssueJiraService.PostWorklogAsync` y los plugins no pueden reaccionar ni intervenir. Hay casos reales que lo necesitan, como repartir el tiempo de un issue entre varias subtareas, y no se pueden resolver sin abrir ese punto de forma controlada, con un fallback seguro al comportamiento original cuando el plugin falla. Las dependencias ya están: el plugin-host (#35) y la búsqueda/creación de subtareas en el cliente de Jira (#36) están archivados. Ver issue #37.

## What Changes

- Nuevo `TimeLoadPipeline` en `Model/`, independiente de UI y testeable con fakes: `InsteadOf → (original) → After`. `MainWindow.PostWorklogAsync` pasa a llamar al pipeline en lugar de a `IssueJiraService`, preservando la semántica actual: solo se resetea el timer si la carga tuvo éxito.
- Evento `InsteadOf`: un plugin puede declinar, manejar, cancelar o fallar. El host decide si corre el original, resetea el timer o avisa, según el resultado y la cantidad de escrituras que el handler hizo (fallback solo si no escribió nada). Sin timeout; varios handlers se consultan en orden alfabético por id y gana el primero que no declina; un plugin nunca es consultado por sus propias cargas.
- En `Handled`, el plugin se hace cargo de todo, comentario incluido: el host no postea nada por su cuenta. `Handled` lleva `TimeLoaded`, el tiempo que el plugin declara haber cargado, y el host lo **valida** contra el total confirmado: si no coinciden, no se resetea el timer y se avisa como una carga parcial (igual que un fallo con escrituras), en vez de perder tiempo sin aviso. El plugin no decide si se resetea el timer: eso sigue siendo del host.
- Evento `After`: observador sin poder de veto, aislado por observador, que recibe qué se cargó, quién lo manejó, el resultado y la cantidad de escrituras hechas.
- Un `IJiraApi` **con alcance** por invocación de `InsteadOf`, que cuenta las escrituras (`AddWorklogAsync`, `CreateSubtaskAsync`) para aplicar la tabla de resultados.
- Dos entradas a la carga: `IJiraApi.AddWorklogAsync` queda como operación **cruda** (sin eventos ni `InsteadOf`) y gana una sobrecarga que acepta método y valor de estimate (hoy siempre usa `Auto`), para que un plugin que maneja la carga pueda respetar el estimate que el usuario eligió en el diálogo; y un nuevo `ITimeLoader.LoadAsync` entra al pipeline con `Source = plugin:<id>`.
- Registro explícito: cada plugin registra a lo sumo un `InsteadOf` con un método del host; `After` se expone como evento.
- **Contrato 1.1** (aditivo): `IJiraApi` suma `GetSubtasksAsync` y `CreateSubtaskAsync` con tipos públicos de Abstractions. No rompe plugins compilados contra 1.0.
- Plugin de ejemplo `Samples/SplitTime` (no se empaqueta en el release) que reparte el tiempo entre subtareas y ejercita todo el contrato, y ampliación de `docs/plugins.md`.
- Fuera de alcance: evento `Before`, eventos de timers, timeouts, prioridades en el manifiesto, dependencias entre plugins, sugerencia automática de reparto, cargas parciales intencionales y timer parcial (el plugin no puede pedir "no resetear" ni descontar tiempo). `IssueAdded`/`IssueRemoved` ya existen en el contrato 1.0.

## Capabilities

### New Capabilities
- `time-load-pipeline`: comportamiento del host al cargar tiempo: orden `InsteadOf → original → After`, tabla de resultados del handler, regla de fallback, reseteo del timer, consulta de varios handlers y fuente de la carga.
- `plugin-time-load`: superficie del contrato para que un plugin participe: registro del `InsteadOf`, evento `After`, `ITimeLoader`, `Source`, `IJiraApi` con alcance y su contador de escrituras.

### Modified Capabilities
- `plugin-contract`: la fachada de Jira suma lectura y creación de subtareas, y `ContractVersion` pasa de 1.0 a 1.1.

## Impact

- `source/StopWatch.Plugin.Abstractions/`: `IJiraApi` (dos métodos nuevos), tipos de subtarea, de request/resultado del handler y de `After`, `ITimeLoader`, y la entrada de registro en `IPluginHost`. `PluginContract.ContractVersion` y `<Version>` del csproj suben a 1.1.
- `source/StopWatch/Model/`: `TimeLoadPipeline` nuevo; `IssueJiraService` sin cambios de comportamiento.
- `source/StopWatch/Plugins/`: adaptadores del host (registro por plugin, decorator contador, `ITimeLoader`) y exposición de subtareas desde `IJiraOperations`.
- `source/StopWatch/UI/MainWindow.xaml.cs`: `PostWorklogAsync` usa el pipeline y muestra el aviso no modal de fallback o fallo parcial.
- `source/StopWatchTest/`: tests del pipeline sin UI, uno por fila de la tabla de resultados.
- `source/Samples/SplitTime/` nuevo y `docs/plugins.md` ampliado.
- Sin plugins instalados no cambia nada visible.
