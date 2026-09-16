## Purpose

Pone un tope configurable a la cantidad de timers que pueden correr al mismo
tiempo cuando la opción de permitir múltiples timers está activada, aplicado
por igual sin importar desde qué vista se arranca un timer.

## Requirements

### Requirement: El usuario configura el máximo de timers simultáneos

Cuando la opción de permitir múltiples timers está activada, la aplicación
SHALL ofrecer un ajuste de configuración con la cantidad máxima de timers que
pueden correr al mismo tiempo, con valor por defecto 3. Ese ajuste SHALL estar
deshabilitado cuando la opción de permitir múltiples timers está desactivada,
ya que en ese caso ya corre como máximo un solo timer.

#### Scenario: El ajuste está disponible

- **WHEN** el usuario activa la opción de permitir múltiples timers en la
  configuración
- **THEN** el campo del máximo de timers simultáneos queda habilitado, con 3
  como valor por defecto si no se configuró antes

#### Scenario: El ajuste no aplica sin múltiples timers

- **WHEN** la opción de permitir múltiples timers está desactivada
- **THEN** el campo del máximo de timers simultáneos aparece deshabilitado

### Requirement: Arrancar un timer respeta el máximo configurado

Con la opción de permitir múltiples timers activada, si la cantidad de timers
corriendo ya alcanzó el máximo configurado, arrancar o reanudar un timer
adicional SHALL NO tener efecto: ese timer permanece pausado y los que ya
estaban corriendo no cambian. Esta regla SHALL aplicarse por igual sin
importar si el intento de arrancarlo ocurre en la ventana principal o en la
vista mini.

La aplicación SHALL comunicar por qué el intento no tuvo efecto.

#### Scenario: Se alcanza el máximo desde la ventana principal

- **WHEN** el máximo configurado es 3, ya hay 3 timers corriendo, y el usuario
  intenta arrancar un cuarto desde la ventana principal
- **THEN** el cuarto timer no arranca
- **AND** los tres que ya corrían siguen corriendo sin cambios
- **AND** la aplicación indica que se alcanzó el máximo configurado

#### Scenario: Se alcanza el máximo desde la vista mini

- **WHEN** el máximo configurado es 3, ya hay 3 timers corriendo mostrados en
  la vista mini, y el usuario intenta reanudar un cuarto desde una fila
  pausada
- **THEN** el cuarto timer no arranca
- **AND** los tres que ya corrían siguen corriendo sin cambios

#### Scenario: Hay lugar bajo el máximo

- **WHEN** el máximo configurado es 3 y hay 2 timers corriendo
- **THEN** el usuario puede arrancar un tercero normalmente

#### Scenario: Se libera lugar pausando uno a mano

- **WHEN** el máximo configurado es 3, hay 3 timers corriendo, y el usuario
  pausa uno de ellos
- **THEN** el usuario puede arrancar otro timer distinto sin que se pause
  ninguno de los que quedaron corriendo
