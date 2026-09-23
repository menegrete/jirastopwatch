## MODIFIED Requirements

### Requirement: La actualización se aplica sin interrumpir una sesión en curso

La app SHALL dejar la actualización verificada lista para aplicar sin
modificar los archivos de instalación mientras la app está corriendo, y
SHALL aplicarla recién en el próximo cierre normal de la app (nunca
forzando el cierre de la app ni interrumpiendo un timer en ejecución para
instalarla).

#### Scenario: Actualización lista mientras la app sigue en uso
- **WHEN** una actualización terminó de descargarse y verificarse
- **THEN** la app sigue funcionando con la versión actual sin interrupción,
  y muestra en rojo y negrita que hay una actualización lista para
  aplicarse al reiniciar, junto con un link "What's new" hacia la página
  del release en GitHub correspondiente a esa versión

#### Scenario: El usuario abre las notas de la versión desde el aviso
- **WHEN** el usuario hace clic en el link "What's new" del aviso de
  actualización lista
- **THEN** la app abre la página de ese release en GitHub en el navegador
  por defecto del usuario, sin afectar la actualización staged ni la
  sesión en curso

#### Scenario: El usuario cierra la app normalmente con una actualización lista
- **WHEN** el usuario cierra la app y hay una actualización verificada y
  lista para aplicar
- **THEN** la nueva versión reemplaza a la instalación actual y la app se
  vuelve a abrir automáticamente en la nueva versión

#### Scenario: La app se cierra sin que la actualización se aplique
- **WHEN** el proceso de la app termina de una forma que el proceso de
  aplicación no llega a detectar (timeout esperando el cierre)
- **THEN** la instalación actual queda intacta y sin aplicar, y la
  actualización se reintenta en un próximo cierre
