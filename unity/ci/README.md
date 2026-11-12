# CI y verificación automática — ¡ESO TODAVÍA SIRVE!

**Cómo se activan los workflows**: los YAML viven en `unity/ci/workflows/`
porque el token de push actual no tiene el scope `workflow` de GitHub (rechaza
crear `.github/workflows/`). Para activarlos, el propietario copia los archivos
a `.github/workflows/` (un commit manual o la web UI alcanzan); el contenido ya
está listo tal cual.

Estado real (Instrucción 09, 2026-11-12): **no se ejecutó ninguna compilación
real de Unity** (el entorno de trabajo no tiene Unity Editor ni licencia). Lo
que sí se ejecuta y está verde: el **banco de verificación C# puro** (76 tests)
y el **prototipo JS congelado** (20 + 53 tests). Este documento describe ambos
niveles y qué hace falta exactamente para activar la compilación real.

## Nivel 1 — Banco C# puro (ACTIVO, sin secretos, ya en CI)

- Workflow: `unity/ci/workflows/verify-domain.yml` (correrá en cada push/PR
  una vez copiado a `.github/workflows/`).
- Script local: `bash unity/ci/build-and-test.sh` (requiere `mono-mcs`).
- Qué hace: compila `ETSV.Domain`, `ETSV.Sim`, la capa de integración Unity
  (`_ETSV/Scripts/Integration/**`) contra **stubs** (`unity/ci/stubs/`, firmas
  copiadas del fuente real de NGO 2.12.0 y de la API pública de UnityEngine /
  InputSystem / UIElements) y ejecuta los tests EditMode (`unity/ci/shim/`
  aporta un shim mínimo de NUnit + runner; no se bindea NUnit al repo).
- Qué **NO** verifica (limitación explícita):
  - El compilador real de Unity y su import de assets (prefabs YAML, .meta,
    GUIDs, escenas). Eso se audita estáticamente (ver `unity/docs/INTEGRACION.md`
    y el informe de la Instrucción 09) pero sin Editor no es ejecutable.
  - El **ILPP de NGO** (generación de serializadores de NetworkVariable y
    tablas de RPC). Por eso la auditoría estática contra el fuente real de
    NGO 2.12.0 es parte obligada del proceso: fue la que detectó, p. ej., que
    `CompSync` necesitaba `INetworkSerializeByMemcpy`.
  - Físicas (PhysX), replicación real por red y `Application.persistentDataPath`
    real.
- Descarga en tiempo de build: Newtonsoft.Json 13.0.3 (MIT) desde nuget.org
  (misma versión base que redistribuye `com.unity.nuget.newtonsoft-json` 3.2.x;
  no se bindea la DLL al repo).

## Nivel 2 — Compilación real de Unity (PREPARADO, DESACTIVADO)

Workflow: `unity/ci/workflows/unity-compile.yml` (copiar a `.github/workflows/`
para habilitarlo) — solo corre con
`workflow_dispatch` y **falla de forma explícita** si no están los secretos.
Usa [game-ci](https://game.ci) (`unity-builder@v4` / `unity-test-runner@v4`),
la vía estándar y reutilizable: no modifica licencias ni exige cuentas de
pago; la licencia **Personal (gratis)** alcanza.

### Qué necesita exactamente el propietario del repo

1. **Una cuenta Unity** (email + contraseña) con licencia Personal activada.
2. **Activar la licencia una vez de forma manual** (fuera de CI):
   - Con Unity Hub: `Unity Hub > Preferences > Licencias` permite exportar el
     archivo de licencia (`.ulf`). Alternativa sin Hub:
     `unity -batchmode -quit -createManualActivationFile` genera un `.alf`, se
     sube a https://license.unity3d.com/manual y se descarga el `.ulf`.
   - game-ci documenta el flujo completo: https://game.ci/docs/github/activation
3. **Configurar secretos en GitHub** (Settings > Secrets and variables > Actions):
   | Secreto | Contenido |
   |---|---|
   | `UNITY_EMAIL` | email de la cuenta Unity |
   | `UNITY_PASSWORD` | contraseña de la cuenta |
   | `UNITY_LICENSE` | contenido ÍNTEGRO del archivo `.ulf` (licencia Personal) |
   | `UNITY_SERIAL` | (solo Plus/Pro) número de serie en vez de `.ulf` |
4. Ejecutar el workflow manualmente (Actions > "Unity (compilación real…)" >
   Run workflow). Con licencia Personal, los términos de Unity permiten CI con
   activación manual como la descrita; no se comparten credenciales con
   terceros (los secretos solo los consume el runner oficial de game-ci).

### Qué aportaría el Nivel 2 cuando se active

- Compilación real de los 3 assemblies + Assembly-CSharp con el ILPP de NGO
  (serializadores `NetworkList<CompSync>`, hashes de RPC privados, etc.).
- Los 76 tests EditMode corriendo bajo Unity Test Framework real.
- Import real de los 3 prefabs YAML manuales y de la escena (validación de
  GUIDs/fileIDs que hoy solo está auditada estáticamente).
- Build StandaloneLinux64 (detectaría, p. ej., problemas de stripping).

### Costo

- Runners públicos de GitHub: gratis para repos públicos (minutos limitados
  por mes; una corrida de builder ~20-40 min la primera vez, menos con cache
  de `Library/`).
- Licencia: Unity Personal, gratis (ingresos < USD 200k/año).

## Reproducibilidad local completa

```bash
# Nivel 1 (C# puro):
sudo apt-get install -y mono-mcs mono-runtime libmono-system-data4.0-cil \
  libmono-system-runtime-serialization4.0-cil unzip
bash unity/ci/build-and-test.sh          # ⇒ "76 passed, 0 failed"

# Prototipo JS congelado:
cd prototype && node tests/disassembly.test.mjs && node tests/economy.test.mjs

# Nivel 2 (Unity real), si se tiene Editor + licencia en la máquina:
Unity -batchmode -quit -projectPath unity -runTests -testPlatform EditMode \
      -testResults resultados.xml -logFile -
```
