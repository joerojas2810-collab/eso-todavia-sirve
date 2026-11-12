# `Assets/_ETSV` — código y estructura propios de ¡ESO TODAVÍA SIRVE!

Todo lo que vive acá es **nuestro**; lo que está fuera (Assets/Scripts, Assets/Prefabs,
Assets/Scenes, etc.) proviene de la base **U6-First-Person-Multiplayer** (Unity Companion
License — ver `unity/ATTRIBUTION.md`) y se modifica lo menos posible, siempre con
comentarios `// [ETSV]` para poder actualizar la base sin perder los cambios.

## Estructura

```
Assets/_ETSV/
├── Scripts/
│   ├── Domain/            ← assembly ETSV.Domain (C# PURO: noEngineReferences = true)
│   │   ├── GameInfo.cs        identidad y constantes de diseño
│   │   ├── Support/           JsMath (rounding estilo JS), OpResult {Ok, Reason}
│   │   ├── Data/              CatalogData (blueprints, fijaciones, herramientas, recetas)
│   │   │                      EconomyData (MAT-01..08, PART_DEFS, P1–P5, zonas, PROV-01..11)
│   │   ├── Disassembly/       DisassemblyRules (estados, accesibilidad, fijaciones, calidad)
│   │   ├── Pieces/            StoredPart + PieceRegistry (identidad única, reservas)
│   │   ├── Warehouse/         ComponentWarehouse (zonas) + MaterialWarehouse (fracciones)
│   │   ├── Economy/           EconomyOps (operaciones atómicas con validación de propiedad)
│   │   └── Persistence/       SaveGame v3 (DTO) + SaveSerializer + SaveValidator
│   └── Integration/
│       ├── SessionConfig.cs   parámetros de sesión (Assembly-CSharp; la leen los upstream)
│       ├── Sim/             ← assembly ETSV.Sim (C# PURO: noEngineReferences = true)
│       │   ├── ETSVSession.cs / CatalogIndex.cs    cerebro del host + índices de red
│       │   ├── DisassemblyGate.cs                  validadores (identidad/distancia/conflictos)
│       │   ├── DisassemblySync.cs                  CompSync + pack/unpack (NetworkList NGO)
│       │   ├── DisassemblySessionHost.cs           solicitudes de red → ops del dominio
│       │   ├── DomainHost.cs / SaveJsonAdapter.cs  estado agregado + DTO↔JSON (Newtonsoft)
│       ├── Disassembly/     ← cáscaras Unity delgadas (Assembly-CSharp)
│       │   ├── DisassemblyObjectView / PieceView / ETSVSessionView (NetworkBehaviour)
│       │   ├── BlueprintBuilder + DisassemblyLayouts (visuales genéricos por blueprint)
│       │   ├── DisassemblyPlayerController (input → RPCs) + DisassemblyHud (UI Toolkit)
│       └── ETSVBootstrap.cs   arranque runtime SIN tocar escena/prefabs de la base
├── Resources/ETSV/          ← prefabs de red (ETSVSession/DisassemblyObject/DisassemblyPiece)
└── Tests/
    └── EditMode/          ← ETSV.Domain.Tests (NUnit)
        ├── DomainSmokeTests.cs          3 smoke (Instrucción 04)
        ├── DisassemblyParityTests.cs    20 asserts JS → 20 [Test] con [Order]
        ├── EconomyParityTests.cs        53 asserts JS → 10 [Test] por bloque
        └── HostSessionTests.cs          13 tests de la Instrucción 06 §10 (capa Sim)
        Matriz completa: unity/docs/MATRIZ_PARIDAD_TESTS.md
        Integración física/red: unity/docs/INTEGRACION_DESARMADO.md
```

## Reglas de la capa Domain (innegociables)

1. **Sin `using UnityEngine`** — la asmdef lo impide físicamente (`noEngineReferences`).
2. **Sin MonoBehaviour, sin físicas, sin networking**: clases puras y structs.
3. Cada operación económica devuelve `{ok, reason}` como en el prototipo: la integración
   (ServerRpc del host) decide qué hacer con el resultado; el dominio solo valida y muta.
4. Los datos (materiales, precios, catálogos) entran como DTOs/structs; su versión
   ScriptableObject vive en Integration y se convierte al formato del dominio.
5. Los 73 tests JS (20 desmontaje + 53 economía) están portados a NUnit **1:1** y
   verificados: 33 tests / 76 aserciones PASS bajo Mono (ver MATRIZ_PARIDAD_TESTS.md).
   Pendiente: ejecutarlos en el Test Runner del Unity Editor.

## Por qué Domain es una assembly separada

- Garantiza la separación reglas/presentación exigida por la Instrucción 02 §9.
- Permite correr los tests sin escena ni objetos de Unity (EditMode puros, rápidos).
- En multijugador, el host ejecuta el dominio autoritativo; los clientes solo piden ops.
