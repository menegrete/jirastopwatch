# Design

## Context

Ver `proposal.md` - Why. Estado actual: `.releaserc.json` declara
`branches: ["main"]`, y el job `release` de `.github/workflows/build.yml` solo
corre en `refs/heads/main`. Los plugins `@semantic-release/changelog` y
`@semantic-release/git` commitean `CHANGELOG.md` y `AssemblyInfo.cs` de
vuelta a la rama. `scripts/set-assembly-version.js` escribe la misma versión
en `AssemblyVersion`, `AssemblyFileVersion` y `AssemblyInformationalVersion`.
`@semantic-release/github` marca como pre-release automáticamente las
versiones publicadas desde una rama configurada como prerelease, y el
auto-updater de la app usa `releases/latest`, que excluye pre-releases.

## Goals / Non-Goals

**Goals:**
- Reusar semantic-release y el pipeline de artifacts existentes; el canal rc
  es configuración, no un pipeline paralelo.
- Cero cambios de comportamiento para `main`.

**Non-Goals:**
- Que la app detecte o instale betas (issue #39).
- Múltiples ramas rc en paralelo.

## Decisions

**1. Rama de prerelease con id fijo: `{ "name": "rc*", "prerelease": "rc" }`.**
Da versiones `X.Y.Z-rc.N` con orden semver trivial. Alternativa: `prerelease:
true` (nombre de rama como id) permite ramas paralelas pero genera versiones
como `rc1.1` y `rc-foo.1`, cuyo orden entre ramas es impredecible para el
futuro updater. Se acepta la restricción de una sola rama rc activa.

**2. Config dinámico: reemplazar `.releaserc.json` por `release.config.js`.**
semantic-release no permite condicionar un plugin por rama; el config JS
arma la lista de plugins según `process.env.GITHUB_REF_NAME`: en `main`
incluye `changelog` y `git`; en `rc*` los omite. El resto (commit-analyzer,
release-notes-generator, exec, github) es idéntico para ambos. Alternativas:
dos archivos de config elegidos con `--extends` desde el workflow (duplica la
lista de plugins) o mantener `git` en rc y revertir (frágil, deja commits).
Se mueve la configuración existente tal cual para que `main` quede
equivalente en comportamiento.

**3. `set-assembly-version.js` separa versión numérica de sufijo.**
Corre en `prepareCmd` también en rc, porque el build de los artifacts necesita
el informational version correcto. Usa la parte numérica `X.Y.Z` (antes del
primer `-`) para `AssemblyVersion` y `AssemblyFileVersion`, y la versión
completa para `AssemblyInformationalVersion`. Los cambios quedan solo en el
working tree del runner, sin commitear.

**4. Workflow.** Agregar `rc*` a `on.push.branches` y ampliar el `if` del job
`release` a `refs/heads/main` o `startsWith(github.ref, 'refs/heads/rc')`.
`pull_request` queda igual. El `paths-filter` y el `needs: build` (gate de
tests) se mantienen.

**5. `publish-release-artifacts.js` sin cambios**, solo se verifica: usa la
versión únicamente para nombrar los assets, y los globs de
`@semantic-release/github` (`JiraStopWatch-v*-...`) ya matchean un sufijo.

## Risks / Trade-offs

- [`RELEASE_TOKEN` o reglas/rulesets de rama pueden bloquear tags o push desde
  `rc*`] → Verificar en la primera corrida real y ajustar permisos; es una
  tarea explícita.
- [`AssemblyInformationalVersion` con sufijo rompe algún consumidor
  (`AppInfo.Version`, User-Agent, título de ventana)] → Se verifica con un
  build local con versión `-rc.1`; el parseo del updater es del issue #39.
- [Dos ramas rc simultáneas colisionan en el contador] → Convención
  documentada de una sola rama rc activa.
- [Config JS más difícil de leer que JSON] → Mantenerlo mínimo: una lista
  base de plugins y un único condicional.
- [Commits `docs`/`ci` en rc no son release-ables] → Comportamiento esperado,
  igual que en `main`.

## Migration Plan

1. Mergear el change a `main` (sin efecto sobre los releases estables).
2. Crear una rama `rc*` de prueba desde `main` con un commit `feat:` y
   confirmar la pre-release `-rc.1`, sus 4 assets y que `releases/latest` no
   cambia.
3. Rollback: revertir el PR; las pre-releases y tags ya publicados se pueden
   borrar a mano.
