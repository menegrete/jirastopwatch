## Purpose

Define cómo se ve la aplicación en cada tema: qué color semántico corresponde a
cada estado de la interfaz, qué nivel de legibilidad se garantiza, qué se espera de
los elementos que dibuja el sistema operativo y no la aplicación, y cómo el usuario
elige entre los temas disponibles.

## Requirements

### Requirement: Temas disponibles

La aplicación SHALL ofrecer dos temas: oscuro y claro. El tema oscuro SHALL ser el
default en una instalación nueva.

#### Scenario: Se ejecuta la aplicación por primera vez

- **WHEN** el usuario ejecuta la aplicación sin configuración previa
- **THEN** la interfaz se presenta con el tema oscuro

### Requirement: Superficie consistente en toda la aplicación

Todas las ventanas de la aplicación SHALL presentarse con los colores del tema
activo. Ningún panel, campo de entrada, borde o separador SHALL quedar con los
colores del otro tema.

#### Scenario: Se abren todas las ventanas con el tema oscuro activo

- **WHEN** el usuario abre la ventana principal y luego cada diálogo (settings,
  submit worklog, edit timer, about)
- **THEN** todas presentan fondo oscuro y texto claro
- **AND** ningún elemento queda como un rectángulo claro sobre el fondo oscuro

#### Scenario: Se abren todas las ventanas con el tema claro activo

- **WHEN** el usuario abre la ventana principal y luego cada diálogo
- **THEN** todas presentan fondo claro y texto oscuro
- **AND** ningún elemento queda como un rectángulo oscuro sobre el fondo claro

#### Scenario: Un campo de entrada queda deshabilitado

- **WHEN** un campo de texto pasa a estado deshabilitado
- **THEN** se distingue visualmente de un campo habilitado
- **AND** sigue perteneciendo a la gama del tema activo

### Requirement: Legibilidad del texto

El texto SHALL mantener un contraste mínimo de 4.5:1 contra su fondo inmediato en
cualquiera de los dos temas. El texto secundario o atenuado SHALL mantener un mínimo
de 3:1.

#### Scenario: Se verifica el contraste de cada par texto/fondo

- **WHEN** se mide el contraste de cada combinación de color de texto y fondo que la
  aplicación puede producir, en ambos temas
- **THEN** ninguna combinación de texto principal cae por debajo de 4.5:1
- **AND** ninguna combinación de texto atenuado cae por debajo de 3:1

### Requirement: Los estados de la interfaz siguen siendo distinguibles

Los estados que hoy se comunican por color SHALL seguir siendo distinguibles entre
sí en el tema activo: timer corriendo, issue seleccionado, validación fallida,
conexión establecida y conexión fallida.

#### Scenario: Un timer está corriendo

- **WHEN** el usuario inicia un timer
- **THEN** el campo de tiempo de ese issue se distingue de los timers detenidos
- **AND** el indicador no destaca por brillo excesivo respecto del fondo

#### Scenario: Un issue es el seleccionado

- **WHEN** el usuario selecciona una fila de issue
- **THEN** esa fila se distingue de las no seleccionadas
- **AND** el texto de la fila seleccionada sigue cumpliendo el contraste mínimo

#### Scenario: Una entrada de tiempo es inválida

- **WHEN** el usuario ingresa un valor de tiempo que no se puede parsear
- **THEN** el campo señala el error de forma perceptible en el tema activo
- **AND** el texto ingresado sigue siendo legible

#### Scenario: Cambia el estado de conexión con Jira

- **WHEN** la conexión con Jira se establece, y luego falla
- **THEN** los dos estados se distinguen entre sí
- **AND** ambos textos cumplen el contraste mínimo

### Requirement: Los iconos permanecen visibles

Todo icono de la interfaz SHALL ser perceptible sobre la superficie del tema activo
sobre la que se dibuja.

#### Scenario: Se inspecciona cada icono en ambos temas

- **WHEN** se observa cada icono en su ubicación real, con el tema oscuro y con el
  claro
- **THEN** ninguno queda indistinguible del fondo en ninguno de los dos

### Requirement: Los elementos dibujados por el sistema acompañan el tema

Los elementos que dibuja el sistema operativo y no la aplicación —barra de título,
barras de desplazamiento, bordes de campos y glifos de casillas y radios— SHALL
presentarse en la gama del tema activo. Donde el sistema no lo permita, la
aplicación SHALL dibujar el elemento por su cuenta.

#### Scenario: La ventana principal tiene contenido desplazable

- **WHEN** la lista de issues excede el alto visible y aparece la barra de
  desplazamiento, con el tema oscuro activo
- **THEN** la barra no se presenta como una banda clara

#### Scenario: Se observa la barra de título de cada ventana

- **WHEN** el usuario abre cada ventana con el tema oscuro activo
- **THEN** la barra de título se presenta en oscuro, no en claro

#### Scenario: Un formulario tiene casillas de verificación y radios

- **WHEN** el usuario abre el diálogo de settings con el tema oscuro activo
- **THEN** los glifos de las casillas no se presentan como cuadrados claros
- **AND** el estado marcado se distingue del no marcado

### Requirement: El tema sobrevive a los cambios de estado en ejecución

Cuando la aplicación cambia el color de un control en respuesta a una acción del
usuario o a un evento, el color resultante SHALL pertenecer al tema activo. Ningún
cambio de estado SHALL dejar un control con colores del otro tema.

#### Scenario: Un campo pasa por un ciclo de validación

- **WHEN** el usuario ingresa un valor inválido y luego lo corrige
- **THEN** el campo vuelve a su apariencia normal dentro del tema activo
- **AND** no queda con colores del otro tema

#### Scenario: Un timer arranca y se detiene

- **WHEN** el usuario inicia un timer y luego lo detiene
- **THEN** el campo de tiempo vuelve a su apariencia normal dentro del tema activo

#### Scenario: Se recrean las filas de issues

- **WHEN** el usuario cambia la cantidad de issues en settings y las filas se
  vuelven a construir
- **THEN** las filas nuevas se presentan con el tema activo

### Requirement: Los controles interactivos comunican sus estados

Los botones y demás controles interactivos SHALL comunicar visualmente los estados
normal, con el puntero encima, presionado y deshabilitado, dentro del tema activo.

#### Scenario: El usuario interactúa con un botón

- **WHEN** el usuario posa el puntero sobre un botón y luego lo presiona
- **THEN** el botón responde visualmente en ambos casos
- **AND** cada respuesta se distingue del estado normal

### Requirement: El usuario elige el tema

El diálogo de settings SHALL permitir al usuario elegir entre los temas
disponibles. Al confirmar el diálogo, el tema elegido SHALL aplicarse a todas las
ventanas abiertas sin requerir reinicio.

#### Scenario: El usuario cambia el tema

- **WHEN** el usuario elige el otro tema en settings y confirma el diálogo
- **THEN** la ventana principal y las filas de issues se presentan con el tema
  elegido
- **AND** no hace falta reiniciar la aplicación

#### Scenario: El usuario cancela el diálogo de settings

- **WHEN** el usuario cambia la selección de tema y luego cancela el diálogo
- **THEN** la aplicación sigue presentándose con el tema anterior

### Requirement: La selección de tema persiste

El tema elegido por el usuario SHALL persistir entre ejecuciones de la aplicación.

#### Scenario: El usuario reinicia la aplicación

- **WHEN** el usuario elige un tema, cierra la aplicación y la vuelve a abrir
- **THEN** la aplicación se presenta con el tema que había elegido

#### Scenario: Se actualiza una instalación existente

- **WHEN** un usuario que ya tenía la aplicación instalada la actualiza a una
  versión con temas
- **THEN** la aplicación se presenta con el tema oscuro
- **AND** el resto de su configuración se conserva
