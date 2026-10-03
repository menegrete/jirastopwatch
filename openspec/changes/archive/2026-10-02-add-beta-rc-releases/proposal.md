# Proposal

## Why

Hoy solo `main` publica releases, así que cualquier cambio llega a los
usuarios directamente como versión estable. No hay forma de distribuir un
build candidato para probarlo antes de mergearlo. Queremos publicar betas
(release candidates) automáticamente desde ramas `rc*`, sin afectar a quienes
usan el canal estable. Es el primer paso del issue #38; el opt-in en la app
para recibirlos es un change aparte (issue #39).

## What Changes

- Un push a una rama `rc*` corre build + tests y, si pasan, publica una
  pre-release de GitHub con versión `X.Y.Z-rc.N` (id de prerelease fijo `rc`),
  calculada con semantic-release a partir de Conventional Commits.
- La pre-release adjunta los mismos artifacts que un release estable
  (self-contained, framework-dependent y sus checksums SHA256), con la versión
  completa (incluyendo `-rc.N`) en el nombre.
- En ramas `rc*` **no se commitea nada de vuelta** (ni `CHANGELOG.md` ni
  `AssemblyInfo.cs`): evita commits de versión y conflictos al mergear a
  `main`. El tag y la release igual se crean.
- `scripts/set-assembly-version.js` deja de escribir versiones con sufijo en
  `AssemblyVersion`/`AssemblyFileVersion` (solo aceptan numéricos); el sufijo
  va únicamente en `AssemblyInformationalVersion`.
- El workflow de CI se dispara también en push a `rc*` y habilita el job de
  release para esas ramas.
- Los releases estables desde `main` no cambian.
- Convención: una sola rama `rc*` activa a la vez, para no colisionar en el
  contador `-rc.N`.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `automated-releases`: se agrega el canal de pre-releases desde ramas `rc*`
  (disparo, versionado `-rc.N`, marcado como pre-release, sin commits de
  vuelta), y se acota el requisito de metadatos de versión para que el
  sufijo de prerelease solo se registre en `AssemblyInformationalVersion`.

## Impact

- `.releaserc.json` (o su reemplazo por un `release.config.js` si hace falta
  configurar plugins según la rama), `package.json` si se agrega
  dependencia.
- `.github/workflows/build.yml`: triggers y condición del job `release`.
- `scripts/set-assembly-version.js` (y verificar
  `scripts/publish-release-artifacts.js`).
- Reglas de protección de ramas / rulesets y permisos de `RELEASE_TOKEN`
  sobre `rc*`.
- Docs: `CLAUDE.md` (sección Changelog and versioning), spec
  `automated-releases`, README.
- No cambia código de la app ni el comportamiento del auto-updater:
  `releases/latest` sigue ignorando pre-releases.
