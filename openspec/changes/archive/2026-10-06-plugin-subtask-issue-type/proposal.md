# Proposal

## Why

`IJiraApi.CreateSubtaskAsync(parentKey, summary)` no permite elegir el tipo de la subtarea: el host usa siempre el primer tipo de subtarea que ofrece el proyecto. En proyectos con varios tipos (por ejemplo un "Sub-task" genérico junto a uno propio del equipo) un plugin no puede crear el tipo que necesita, y como `PluginSubtask` solo expone `Key` y `Summary`, tampoco puede reconocer sus propias subtareas al reutilizarlas por summary. Lo necesita un plugin que reparte un worklog confirmado en una subtarea por modelo de IA y debe crearlas y reutilizarlas sin duplicar. Ver issue #52.

## What Changes

- Nueva sobrecarga `IJiraApi.CreateSubtaskAsync(parentKey, summary, issueTypeName)`: resuelve el nombre contra los tipos de subtarea del proyecto del padre (sin distinguir mayúsculas ni espacios en los extremos). Un nombre `null` o vacío se comporta exactamente como la sobrecarga actual (primer tipo del proyecto). Si el proyecto no ofrece ese tipo, devuelve `null`, no crea nada y lo registra en el log del host, sin caer silenciosamente en otro tipo.
- `PluginSubtask` gana `IssueType` (nombre del tipo, vacío si se desconoce) y un constructor nuevo que lo recibe; el constructor actual se mantiene. `GetSubtasksAsync` lo completa.
- Nuevo `IJiraApi.GetSubtaskTypesAsync(projectKey)`: nombres de los tipos de subtarea del proyecto, lista vacía si no tiene, `null` si no se pudieron leer. Permite que un plugin ofrezca un selector en sus ajustes.
- El decorador que cuenta las escrituras (`CountingJiraApi`) cuenta la sobrecarga nueva igual que la actual.
- **Contrato 1.2** (aditivo): `ContractVersion` y el `<Version>` del csproj de Abstractions suben a 1.2; un plugin compilado contra 1.1 sigue cargando, uno que declare 1.2 no carga en un host 1.1. Se documentan los miembros nuevos en `docs/plugins.md`.
- Fuera de alcance: cambiar la sobrecarga de dos argumentos, crear subtareas con campos extra, identificar tipos por id.

## Capabilities

### New Capabilities

### Modified Capabilities
- `plugin-contract`: la fachada de Jira suma la elección del tipo de subtarea por nombre, la lectura de los tipos de subtarea de un proyecto y el tipo en la descripción pública de una subtarea; `ContractVersion` pasa de 1.1 a 1.2.

## Impact

- `source/StopWatch.Plugin.Abstractions/`: `IJiraApi` (dos miembros nuevos), `PluginSubtask`, `PluginContract.ContractVersion` y `<Version>` del csproj a 1.2.
- `source/StopWatch/Plugins/PluginHostAdapters.cs` (`PluginJiraApi`): resolución por nombre, mapeo de `IssueType`, `GetSubtaskTypesAsync`.
- `source/StopWatch/Model/CountingJiraApi.cs`: sobrecarga nueva con conteo y paso directo de `GetSubtaskTypesAsync`.
- Tests: `PluginJiraApiTest`, `TimeLoadPluginPartsTest`, `PluginLoaderTest` (versión del contrato). Implementadores de `IJiraApi` en tests y en `Samples/` deben compilar con los miembros nuevos.
- `docs/plugins.md` (tabla de versiones y facade).
