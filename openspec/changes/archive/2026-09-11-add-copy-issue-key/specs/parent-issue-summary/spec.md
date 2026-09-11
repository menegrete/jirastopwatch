## MODIFIED Requirements

### Requirement: Sin interacción sobre el parent más allá de copiar su key

La información del parent SHALL mostrarse como texto plano dentro del summary
existente. La única interacción permitida sobre esa información es el ícono
de copia del parent key definido por la capability `copy-issue-key`; ningún
otro control, tooltip o acción (como abrir el parent en el navegador o
seleccionarlo) SHALL agregarse.

#### Scenario: Usuario interactúa con el summary de un subtask fuera del ícono de copia

- **WHEN** el usuario hace click o hover sobre el texto del summary de un
  issue que es subtask, sin interactuar con el ícono de copia del parent key
- **THEN** no ocurre ninguna acción relacionada al parent (no se abre el
  parent, no aparece un tooltip distinto al que ya existe para el summary)
