# Tasks

## 1. Versionado de metadatos con sufijo de prerelease

- [x] 1.1 Modificar `scripts/set-assembly-version.js` para que `AssemblyVersion` y `AssemblyFileVersion` reciban la parte numérica `X.Y.Z` (antes del primer `-`) y `AssemblyInformationalVersion` la versión completa. Verificar corriendo `node scripts/set-assembly-version.js 3.8.0-rc.1` y `node scripts/set-assembly-version.js 3.8.0`, revisando con `git diff` los tres atributos en cada caso, y restaurando `AssemblyInfo.cs` después.
- [x] 1.2 Con `AssemblyInfo.cs` en `3.8.0-rc.1`, verificar que `dotnet build StopWatch.sln` compila sin warnings (`TreatWarningsAsErrors`), que `dotnet test StopWatch.sln --settings .runsettings` pasa, y que `AppInfo.Version`, el título de la ventana y el User-Agent de `GitHubReleaseSource` se comportan bien con el sufijo (revisar el código que los consume; documentar en el PR cualquier ajuste necesario).

## 2. Configuración de semantic-release

- [x] 2.1 Reemplazar `.releaserc.json` por `release.config.js` con la misma lista de plugins y opciones, agregando `{ "name": "rc*", "prerelease": "rc" }` a `branches`, y omitiendo `@semantic-release/changelog` y `@semantic-release/git` cuando `GITHUB_REF_NAME` no es `main`. Verificar con `npx semantic-release --dry-run --no-ci` (o equivalente) simulando `GITHUB_REF_NAME=main` y `GITHUB_REF_NAME=rc1`, confirmando la lista de plugins efectiva en cada caso y que `main` conserva los mismos plugins que antes.
- [x] 2.2 Confirmar que los globs de assets de `@semantic-release/github` y `scripts/publish-release-artifacts.js` generan y matchean nombres con sufijo, ejecutando `node scripts/publish-release-artifacts.js 3.8.0-rc.1` y verificando que `release-artifacts/` contiene los 4 archivos `JiraStopWatch-v3.8.0-rc.1-*` (limpiar `release-artifacts/` y revertir `AssemblyInfo.cs` después).

## 3. Workflow de CI

- [x] 3.1 En `.github/workflows/build.yml`, agregar `rc*` a `on.push.branches` y ampliar el `if` del job `release` para incluir `startsWith(github.ref, 'refs/heads/rc')`, manteniendo `needs: build` y el `paths-filter`. Verificar con un linter de workflows (`actionlint`) o revisión del YAML, y que `pull_request` queda intacto.
- [x] 3.2 Revisar que `RELEASE_TOKEN`, reglas de protección de ramas y rulesets (incluida la config de `.github/CODEOWNERS`) permitan crear tags y GitHub Releases desde ramas `rc*` y no se apliquen mal a ellas; anotar en el PR lo revisado y cualquier ajuste manual requerido en la configuración del repo.

## 4. Documentación

- [x] 4.1 Actualizar `CLAUDE.md` (sección "Changelog and versioning": canal rc, una sola rama `rc*` activa, sin commits de vuelta, referencia a `release.config.js`) y el README (cómo obtener un beta y aviso de que es inestable). Verificar que no queden referencias a `.releaserc.json`.
- [x] 4.2 Verificar con `openspec validate add-beta-rc-releases --strict` que el change es válido.

## 5. Verificación de integración

- [ ] 5.1 Tras mergear a `main`, crear una rama `rc-test` desde `main` con un commit `feat:` de prueba y confirmar: se publica `vX.Y.0-rc.1` como pre-release con los 4 assets, la rama no recibe commits automáticos, y `gh api repos/menegrete/jirastopwatch/releases/latest` sigue devolviendo la última estable.
- [ ] 5.2 En esa rama, agregar un segundo commit release-able y confirmar `-rc.2`. Descargar el `.exe` framework-dependent, verificar su checksum y que arranca mostrando la versión rc. Luego borrar la rama de prueba y las pre-releases/tags de prueba.
- [ ] 5.3 Confirmar que el siguiente push a `main` con cambios en `source/StopWatch/**` sigue produciendo un release estable con commit `chore(release)`, sin regresiones.
