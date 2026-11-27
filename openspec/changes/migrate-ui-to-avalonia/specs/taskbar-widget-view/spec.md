# Spec Delta

## ADDED Requirements

### Requirement: El widget de taskbar existe solo en Windows

El widget de taskbar SHALL estar disponible únicamente cuando la aplicación
corre en Windows. En cualquier otra plataforma, la aplicación SHALL NOT
mostrar el widget ni ofrecer ningún control para activarlo o configurarlo,
incluido el selector de monitor del widget en Configuración.

#### Scenario: Configuración en Linux

- **WHEN** el usuario abre Configuración en Linux
- **THEN** no aparece ningún control relacionado con el widget de taskbar

#### Scenario: Setting heredado de Windows

- **WHEN** la aplicación corre en Linux con un setting de minimizado que
  vale `Taskbar Widget` (por ejemplo, copiado de una instalación de Windows)
- **THEN** la aplicación lo trata como `Mini View`
