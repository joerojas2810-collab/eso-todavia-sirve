#!/usr/bin/env bash
# =============================================================================
# ¡ESO TODAVÍA SIRVE! — Banco de verificación C# puro (mono/mcs)
# =============================================================================
# Compila ETSV.Domain + ETSV.Sim + capa de integración Unity (contra STUBS de
# UnityEngine/NGO/InputSystem/UIElements) + los 76+ tests EditMode, y los
# ejecuta SIN Unity Editor. Es el mismo banco que se usa localmente; vivir en
# el repo lo hace reproducible para cualquiera y para CI (GitHub Actions).
#
# AVISO DE HONESTIDAD (Instrucción 09): esto NO es una compilación real de
# Unity. Verifica tipos/firmas/lógica contra stubs cuyas firmas se copiaron
# del fuente real de NGO 2.12.0 y de la documentación de UnityEngine, pero
# no sustituye al compilador de Unity ni al ILPP de NGO (p. ej. no ejercita
# la generación de serializadores). La compilación real queda documentada en
# unity/ci/README.md (requiere licencia Unity del propietario).
#
# Requisitos: mono-mcs + mono-runtime (apt: mono-mcs mono-runtime
#   libmono-system-data4.0-cil libmono-system-runtime-serialization4.0-cil)
# Uso: bash unity/ci/build-and-test.sh   (desde la raíz del repo o cualquier cwd)
# =============================================================================
set -euo pipefail

CI_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$CI_DIR/../.." && pwd)"
ETSV="$REPO_ROOT/unity/Assets/_ETSV"
BUILD="$CI_DIR/.build"

MCS="${MCS:-mcs}"
MONO="${MONO:-mono}"

# Newtonsoft.Json (MIT) — la misma versión que resuelve Unity (3.2.x, lib/net45).
# No se bindea al repo: se descarga del feed oficial de NuGet.
# 13.0.3 (MIT) es la misma versión base que redistribuye el paquete de Unity
# com.unity.nuget.newtonsoft-json 3.2.x; se usa el perfil lib/net45 para mono.
NJSON_VERSION="13.0.3"
NJSON_URL="https://www.nuget.org/api/v2/package/Newtonsoft.Json/$NJSON_VERSION"
NJSON_DLL="$BUILD/lib/net45/Newtonsoft.Json.dll"

echo "== Banco de verificación C# puro (mono) =="
command -v "$MCS" >/dev/null || { echo "Falta mcs (apt-get install mono-mcs)"; exit 1; }
command -v "$MONO" >/dev/null || { echo "Falta mono (apt-get install mono-runtime)"; exit 1; }

rm -rf "$BUILD"
mkdir -p "$BUILD/lib/net45"

if [ ! -f "$NJSON_DLL" ]; then
  echo "-- Descargando Newtonsoft.Json $NJSON_VERSION (MIT) desde nuget.org…"
  TMP_NJSON="$BUILD/njson.zip"
  if command -v curl >/dev/null; then curl -sSL -o "$TMP_NJSON" "$NJSON_URL"
  else wget -q -O "$TMP_NJSON" "$NJSON_URL"; fi
  (cd "$BUILD" && unzip -o -q njson.zip "lib/net45/Newtonsoft.Json.dll" -d njson_tmp \
    || python3 -c "import zipfile;zipfile.ZipFile('$TMP_NJSON').extract('lib/net45/Newtonsoft.Json.dll','$BUILD/njson_tmp')")
  mv "$BUILD/njson_tmp/lib/net45/Newtonsoft.Json.dll" "$NJSON_DLL"
  rm -rf "$TMP_NJSON" "$BUILD/njson_tmp"
fi

CFLAGS="-langversion:latest -codepage:utf8 -nowarn:0067,0108,0114,0414,0649 -warn:1"

echo "-- 1/5 ETSV.Domain (C# puro, sin referencias)"
"$MCS" -target:library $CFLAGS -out:"$BUILD/ETSV.Domain.dll" \
  $(find "$ETSV/Scripts/Domain" -name '*.cs')

echo "-- 2/5 stubs (UnityEngine/NGO/InputSystem/UIElements — firmas del fuente real)"
(cd "$CI_DIR/stubs" && "$MCS" -target:library $CFLAGS -out:"$BUILD/stubs.dll" \
  -r:"$BUILD/ETSV.Domain.dll" *.cs)

echo "-- 3/5 ETSV.Sim (C# puro + Newtonsoft + tag NGO)"
"$MCS" -target:library $CFLAGS -out:"$BUILD/ETSV.Sim.dll" \
  -r:"$BUILD/ETSV.Domain.dll" -r:"$BUILD/stubs.dll" -r:"$NJSON_DLL" \
  $(find "$ETSV/Scripts/Integration/Sim" -name '*.cs')

echo "-- 4/5 Capa de integración Unity (contra stubs)"
"$MCS" -target:library $CFLAGS -out:"$BUILD/ETSV.UnitySide.dll" \
  -r:"$BUILD/ETSV.Domain.dll" -r:"$BUILD/ETSV.Sim.dll" -r:"$BUILD/stubs.dll" \
  $(find "$ETSV/Scripts/Integration/Disassembly" -name '*.cs') \
  "$ETSV/Scripts/Integration/ETSVBootstrap.cs" \
  "$ETSV/Scripts/Integration/SessionConfig.cs"

echo "-- 5/5 Tests EditMode (shim NUnit + runner propio)"
"$MCS" -target:exe $CFLAGS -out:"$BUILD/tests.exe" \
  -r:"$BUILD/ETSV.Domain.dll" -r:"$BUILD/ETSV.Sim.dll" -r:"$BUILD/ETSV.UnitySide.dll" \
  -r:"$BUILD/stubs.dll" -r:"$NJSON_DLL" \
  "$CI_DIR/shim/NUnitShim.cs" "$CI_DIR/shim/Runner.cs" \
  "$ETSV"/Tests/EditMode/*.cs

echo "-- Ejecutando…"
cp "$NJSON_DLL" "$BUILD/" 2>/dev/null || true
"$MONO" "$BUILD/tests.exe"
