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
│   │   ├── GameInfo.cs      identidad y constantes de diseño (existe hoy)
│   │   └── (próxima instrucción: port 1:1 desde prototype/js)
│   │       ├── Catalog/       ObjectBlueprint, Component, Fastener   ← catalog.js
│   │       ├── Disassembly/   DisassemblyInstance (estados, fijaciones, calidad) ← disassembly.js
│   │       ├── Pieces/        StoredPart + PieceRegistry (identidad, invariantes) ← pieces.js
│   │       ├── Warehouse/     zonas de componentes + acumulador de materiales ← warehouse.js
│   │       ├── Economy/       operaciones atómicas (extraer/procesar/vender/reservar) ← economy.js
│   │       ├── EconomyData/   materiales MAT-01..08, PART_DEFS, P1–P5, PROV ← economyData.js
│   │       └── Persistence/   esquema de save versionado ← gameState.js
│   └── Integration/       ← capa que CONECTA el dominio con Unity/NGO (Assembly-CSharp)
│       ├── SessionConfig.cs   parámetros de sesión (existe hoy; lo leen los scripts upstream)
│       └── (futuro) EconomyServerRpc, IInteractable de balanza/trituradora/contenedor/almacén,
│           DisassemblyView, generación de prefabs por blueprint
└── Tests/
    └── EditMode/          ← ETSV.Domain.Tests (NUnit; paridad con prototype/tests/*.mjs)
```

## Reglas de la capa Domain (innegociables)

1. **Sin `using UnityEngine`** — la asmdef lo impide físicamente (`noEngineReferences`).
2. **Sin MonoBehaviour, sin físicas, sin networking**: clases puras y structs.
3. Cada operación económica devuelve `{ok, reason}` como en el prototipo: la integración
   (ServerRpc del host) decide qué hacer con el resultado; el dominio solo valida y muta.
4. Los datos (materiales, precios, catálogos) entran como DTOs/structs; su versión
   ScriptableObject vive en Integration y se convierte al formato del dominio.
5. Los 73 tests JS (20 desmontaje + 53 economía) se portan a NUnit **1:1**: mismo
   nombre, misma aserción. Paridad verde = port terminado.

## Por qué Domain es una assembly separada

- Garantiza la separación reglas/presentación exigida por la Instrucción 02 §9.
- Permite correr los tests sin escena ni objetos de Unity (EditMode puros, rápidos).
- En multijugador, el host ejecuta el dominio autoritativo; los clientes solo piden ops.
