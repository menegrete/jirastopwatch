## 1. Propagar el parent key hasta el ViewModel

- [x] 1.1 Definir `IssueSummaryResult` (o tipo equivalente) con `Summary` y
      `ParentKey`, y cambiar `JiraClient.GetIssueSummary` para devolverlo en
      vez de un `string` (source/StopWatch/Jira/JiraClient.cs)
- [x] 1.2 Actualizar `IJiraOperations` y cualquier otra implementación/mock
      de la interfaz para el nuevo tipo de retorno
- [x] 1.3 Actualizar `IssueJiraService.GetSummaryAsync` para propagar el
      nuevo tipo estructurado (source/StopWatch/Model/IssueJiraService.cs)
- [x] 1.4 Agregar `ParentKey` (y `HasParent`) a `IssueViewModel`, limpiando
      ambos cuando cambia `IssueKey`, igual que `Summary`
      (source/StopWatch/Model/IssueViewModel.cs)
- [x] 1.5 Actualizar el código que consume `GetSummaryAsync`/`GetSummaryAsync`
      para asignar `Summary` y `ParentKey` del resultado al `IssueViewModel`

## 2. Ícono de copiar el key propio

- [x] 2.1 Agregar el ícono de copia superpuesto sobre el `TextBox` del
      `IssueKey` en `IssueRowCompact` y `IssueRowSpacious`
      (source/StopWatch/UI/MainWindow.xaml)
- [x] 2.2 Atar su `Visibility` al estado de hover de la fila (mismo patrón
      que ya usa `IsCurrent`)
- [x] 2.3 Implementar el handler de click que copia `IssueKey` al
      portapapeles (source/StopWatch/UI/MainWindow.xaml.cs)
- [x] 2.4 Agregar `ToolTip` descriptivo al ícono

## 3. Ícono de copiar el parent key

- [x] 3.1 Agregar el ícono de copia superpuesto al inicio de la columna del
      summary en ambos `DataTemplate`
- [x] 3.2 Atar su `Visibility` a hover de la fila **y** `HasParent`
- [x] 3.3 Implementar el handler de click que copia `ParentKey` al
      portapapeles
- [x] 3.4 Agregar `ToolTip` descriptivo al ícono

## 4. Feedback visual de copiado

- [x] 4.1 Implementar el cambio temporal de glifo a check (~1s) al copiar,
      reusable por ambos íconos (propio y parent)
- [x] 4.2 Verificar que el estado de confirmación es puramente visual (no
      persiste en el `ViewModel` ni sobrevive a un refresh de la fila)

## 5. Specs y tests

- [x] 5.1 Actualizar `JiraClientTest.cs` para el nuevo tipo de retorno de
      `GetIssueSummary`, incluyendo casos con y sin parent key
- [x] 5.2 Actualizar `IssueViewModelTest.cs` para cubrir `ParentKey` /
      `HasParent`, incluyendo que se limpian al cambiar `IssueKey`
- [x] 5.3 Correr `openspec validate add-copy-issue-key --strict` y
      confirmar que la delta de `parent-issue-summary` es coherente con el
      spec existente

## 6. Verificación manual

- [x] 6.1 Probar en la app: hover sobre una fila muestra el ícono de copiar
      el key propio; click copia y muestra el check
- [x] 6.2 Probar con un issue subtask: aparece también el ícono de copiar
      el parent; click copia el key correcto
- [x] 6.3 Probar con un issue que no es subtask: el ícono de copiar el
      parent no aparece nunca
- [x] 6.4 Confirmar que el mini timer no cambió de comportamiento

## 7. Fix encontrado en verificación manual

- [x] 7.1 Pegar un link vía Ctrl+V funcionaba, pero el menú contextual del
      `TextBox` del key invocaba el `Paste` nativo de WPF y pegaba el texto
      crudo sin pasar por `JiraKeyHelpers.ParseUrlToKey`. Se agregó un
      `CommandBinding` de `ApplicationCommands.Paste` en el `TextBox` (ambas
      densidades) que redirige a `PasteKey`
      (source/StopWatch/UI/MainWindow.xaml,
      source/StopWatch/UI/MainWindow.xaml.cs)
