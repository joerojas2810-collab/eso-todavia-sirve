# Matriz de paridad de tests — JS → NUnit (Instrucción 05)

Equivalencia entre las **73 verificaciones del prototipo JS** y los tests NUnit
EditMode portados a `unity/Assets/_ETSV/Tests/EditMode/`. Los tests JS originales
(`prototype/tests/*.test.mjs`) se conservan intactos y siguen verdes.

## Estado de ejecución (2026-11-12)

| Nivel | Herramienta | Resultado |
|---|---|---|
| Código escrito | — | ✅ 9 archivos de dominio + 2 de integración + 3 de tests |
| Sintaxis verificada | Mono mcs 6.12 (`-langversion:latest`) | ✅ sin errores |
| Compilación real | mcs → ETSV.Domain.dll, ETSV.Integration.dll, tests.exe | ✅ |
| Pruebas ejecutadas | Mono JIT 6.12 + shim NUnit local | ✅ **33/33 tests (73 aserciones) PASS** |
| Test Runner de Unity | Unity Editor 6000.3.5f2 (no disponible en este entorno) | ⏳ PENDIENTE — ejecutar `Window > General > Test Runner > EditMode` |

> La ejecución bajo Mono usa el mismo código fuente del repositorio compilado
> con un shim mínimo de `NUnit.Framework` y un runner que respeta `[Test]` y
> `[Order]` (el shim y el runner son locales, **no** están en el repo). En
> Unity los mismos archivos compilan contra el NUnit real del paquete
> `com.unity.test-framework`. Los asserts son semánticamente idénticos a los `ok()`
> del JS: comparan resultados y transiciones reales, no la creación de clases.

## A) Desarmado — `disassembly.test.mjs` (20 asserts) → `DisassemblyParityTests` (20 `[Test]`, `[Order]` 1–20)

Paridad estructural total: el JS ejecuta una secuencia única sobre una instancia;
el port replica la secuencia con una instancia compartida y un test por aserción,
ordenados con `[Order]` para preservar la dependencia temporal del original.

| # | Assert JS (mensaje) | Test NUnit | Resultado esperado |
|---|---|---|---|
| 1 | accesibles iniciales | `A01_AccesiblesIniciales` | carcasa/puerta/panel = Accesible |
| 2 | bloqueados por dependencia | `A02_BloqueadosPorDependencia` | plato/motor/modulo = Bloqueado |
| 3 | herramienta incorrecta rechazada | `A03_HerramientaIncorrectaRechazada` | CanWork(llave) → !Ok, "herramienta" |
| 4 | destornillador 3s por tornillo | `A04_DestornilladorTresSegundosPorTornillo` | CanWork → Ok, Time = 3.0 |
| 5 | progreso interrumpido se conserva | `A05_ProgresoInterrumpidoSeConserva` | 1.5 s → progress = 0.5 |
| 6 | tornillo retirado tras 3s acumulados | `A06_TornilloRetiradoTrasTresSegundosAcumulados` | 1.5 + 1.5 → Removed |
| 7 | no se extrae pieza aún sujeta | `A07_NoSeExtraePiezaAunSujeta` | CanExtract(carcasa) = false |
| 8 | carcasa liberada (4 tornillos) | `A08_CarcasaLiberadaCuatroTornillos` | State = Liberado |
| 9 | motor sigue tapado hasta extraer carcasa | `A09_MotorSigueTapadoHastaExtraerCarcasa` | motor = Bloqueado |
| 10 | motor y módulo accesibles al retirar carcasa | `A10_MotorYModuloAccesiblesAlRetirarCarcasa` | ambos = Accesible |
| 11 | plato: extracción directa al retirar puerta | `A11_PlatoExtraccionDirectaAlRetirarPuerta` | puerta fuera → plato Liberado → Extraido |
| 12 | trabajo correcto no daña | `A12_TrabajoCorrectoNoDana` | calidad del motor intacta |
| 13 | golpe baja calidad a 88 | `A13_GolpeBajaCalidadA88` | Damage(12) → Quality = 88 |
| 14 | no se daña lo ya extraído | `A14_NoSeDanaLoYaExtraido` | Damage(carcasa) = 0 |
| 15 | no se procesa sin extraer antes | `A15_NoSeProcesaSinExtraerAntes` | SetTerminal(panel, Procesado) = false |
| 16 | módulo sellado → residuo especial | `A16_ModuloSelladoResiduoEspecial` | SetTerminal(Procesado, "residuo especial") = true |
| 17 | doble recuperación imposible (módulo) | `A17_DobleRecuperacionImposibleModulo` | SetTerminal(Vendido) = false |
| 18 | motor vendido | `A18_MotorVendido` | SetTerminal(Vendido, "balanza") = true |
| 19 | doble recuperación imposible (motor) | `A19_DobleRecuperacionImposibleMotor` | SetTerminal(Procesado) = false |
| 20 | objeto cerrado cuando todo fue vendido/procesado | `A20_ObjetoCerradoCuandoTodoFueVendidoOProcesado` | AllSettled = true |

**Subtotal: 20/20 ✅**

## B) Economía — `economy.test.mjs` (53 asserts, 10 bloques `{}`) → `EconomyParityTests` (10 `[Test]`)

Cada `[Test]` replica un bloque independiente del JS (mismo setup con helpers
`MakeCtx`/`MkPiece` ↔ `makeCtx`/`mkPiece`) y contiene exactamente las mismas
aserciones, en el mismo orden, con los mismos valores esperados.

| Bloque JS | Asserts | Test NUnit | Verifica |
|---|---|---|---|
| fórmula de valor (GDD 5.0 §6.3) | 1 | `B01_Valor_FormulaGdd` | V = 90 × 0.88 = 79.2 |
| identidad y exclusividad de estados | 10 | `B02_Identidad_Exclusividad` | world→carried→stored→vendido; una pieza en manos (GDD 8.1); no doble venta/almacenado |
| capacidades y categorías por zona (GDD 5.0 §8.5) | 5 | `B03_Zonas_CapacidadYCategorias` | P2 ∉ estantería; palé cap 6 → zona_llena; fallo no muta estado |
| rendimiento con fracciones (GDD 5.0 §4.2) | 6 | `B04_Rendimiento_FraccionesEntreLotes` | Y = B×E×P; floor(3.6)=3 + fracción 0.6; lote 2 → 7; no doble proceso |
| subensambles motor → motor_pequenio (GDD 3.0) | 5 | `B05_Subensamble_MotorPequenio` | Vb $90, calidad heredada 88 (PROV-10), va al palé, venta = 79 |
| sellado: gestión con coste | 7 | `B06_Sellado_GestionConCoste` | no tritura, no vende; entrega 4×$5=$20; fondos_insuficientes con $5 |
| reservas (GDD 5.0 §8.4) | 6 | `B07_Reservas_BloqueanVentaYProceso` | reservada bloquea venta/proceso; doble reserva no; liberar restaura |
| propiedad ajena | 1 | `B08_PropiedadAjena_Bloqueada` | pieza de P2 → propiedad_ajena |
| venta y consumo de materiales | 7 | `B09_Materiales_VentaYConsumoAtomico` | 5 Cu × $18 = $90; lote atómico todo-o-nada |
| persistencia (capa de estado) | 5 | `B10_Persistencia_RoundTrip` | dinero/pieza/fracción/zona persisten; pieza en world no (PROV-11) |

**Subtotal: 53/53 ✅** (1+10+5+6+5+7+6+1+7+5)

## C) Smoke tests previos (Instrucción 04, conservados)

| Test | Verifica |
|---|---|
| `DomainSmokeTests.MaxPlayers_IsSeven_PerGdd` | GameInfo.MaxPlayers = 7 |
| `DomainSmokeTests.SaveVersion_MatchesPrototype` | Version = 3 (gameState.js) |
| `DomainSmokeTests.Title_IsConfigured` | título configurado |

## Total: 33 tests NUnit / 76 aserciones (73 de paridad + 3 smoke) — 100% PASS bajo Mono

## Tests no migrables

Ninguno. Los 73 asserts JS tienen equivalente NUnit ejecutado. Única diferencia
de forma: el runner de Unity agrupa por método (33 tests) mientras el JS cuenta
asserts planos (73); la cobertura de comportamiento es idéntica.

## Verificación adicional del adaptador (fuera del set de paridad)

`DomainHost` + `SaveJsonAdapter` (capa de integración) fueron compilados y
ejecutados bajo Mono con Newtonsoft.Json 13.0.3: round-trip
DTO → JSON → `SaveValidator` → `DomainHost.Load()` restituye dinero, piezas
almacenadas y fracciones de material; un save corrupto (dinero negativo, pieza
en "world") es **detectado por el validador** y rechazado. Este chequeo es
local (no forma parte del repo) y documenta que el adaptador funciona; en Unity
se usará el Newtonsoft del paquete `com.unity.nuget.newtonsoft-json` 3.2.1.
