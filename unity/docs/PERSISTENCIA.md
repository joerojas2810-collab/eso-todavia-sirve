# Instrucción 08 — Persistencia integral del mundo y progreso cooperativo

Estado: **implementado y verificado por tests en dominio/Sim (76/76 bajo mono:
62 previos + 14 nuevos de persistencia)**; la capa Unity compila contra stubs.
**Pendiente de verificación en Unity Editor** (escritura real en
`persistentDataPath`, reconstrucción física de NetworkObjects y replicación —
ver §10).

Fuente de verdad: **GDD 5.0 §16** (datos persistentes, cuándo guardar, guardado
seguro, restauración y versionado). No hay un segundo sistema económico ni un
registro paralelo de piezas: el save es una **captura del dominio existente**
(`GameStateData` + instancias de `DisassemblySessionHost`), y la restauración
vuelca el archivo de vuelta en ese mismo dominio.

---

## 1. Arquitectura — cinco capas separadas (§7 de la instrucción)

| Capa | Dónde | Responsabilidad |
|---|---|---|
| **Serialización** | `Domain/Persistence/SaveFull.cs` (`SaveSerializer.SerializeFull/DeserializeFull`) | Estado vivo ↔ DTO v4. Trabaja sobre clones: **la captura no muta el estado vivo**. |
| **Validación** | `Domain/Persistence/SaveGame.cs` + `SaveFull.cs` (`SaveValidator.ValidateFull`) | Integridad: ids únicos, referencias cruzadas pieza↔pose↔zona, estados legales, huellas de catálogo, capacidades. |
| **Migración** | `Domain/Persistence/SaveFull.cs` (`SaveMigrations`) | v3 → v4 explícita y documentada; versiones desconocidas se rechazan (nunca se inventa). |
| **Reconstrucción lógica** | `Sim/SessionPersistence.cs` + `Sim/SaveRepository.cs` | Punto de consistencia, captura del patio/poses/contadores, restauración sobre un host, escritura segura de archivos (System.IO, sin UnityEngine). |
| **Reconstrucción física + red** | `Disassembly/PersistenceHub.cs` + `ETSVBootstrap.cs` (Unity) | Ruta real (`Application.persistentDataPath`), poses desde los transforms, re-materialización de NetworkObjects al cargar, disparadores (arranque/autoguardado/desconexión/cierre/F5). La sync viaja por la infraestructura existente (NVs/NetworkLists/spawn de NGO). |

**Autoridad cooperativa (§5)**: solo el host guarda y restaura
(`PersistenceHub` vive en el host y `SaveNow` verifica `IsServer`). Los clientes
reciben el estado restaurado por los canales existentes de la Instrucción
06/07: `NetworkVariable`/`NetworkList` del `ETSVSessionView` (dinero, zonas,
materiales) y spawn de NetworkObjects con identidad antes de `Spawn()` (piezas y
objetos). Un cliente **solo puede solicitar** un guardado (`RequestSaveRpc`,
tecla F5): el host decide y ejecuta; jamás un cliente reemplaza estado económico
del anfitrión. Identidad de jugador (`clientId`/`playerTag "c{id}"`), propiedad
económica (`Owner`/`SessionOwner="chatarrería"`) y autoridad de red
(`OwnerClientId` NGO) siguen siendo tres conceptos distintos (Instrucción 07 §2).

## 2. Formato de guardado v4

Un único DTO (`SaveGame`, el mismo de la paridad v3 + campos nuevos; un archivo
v3 trae los campos nuevos en null y la migración los completa):

```jsonc
{
  "Version": 4,
  "SaveId": "9f2c…",              // identificador único de partida (GUID, estable entre guardados)
  "SavedAtUtc": "2026-11-12T…",   // ISO-8601
  "Money": 1250,                  // saldo compartido de la chatarrería
  "Pieces": [ … ],                // TODAS las piezas: identidad completa (uid, defId,
                                  // origen, calidad, kg, clase, vbase, owner, storage,
                                  // reserva, terminal, sealed, receta)
  "PiecePoses": [                 // posición/orientación de las piezas FÍSICAS
    { "Uid": "p_7", "X": 9.5, "Y": 1.2, "Z": 9.5, "Yaw": 0,
      "CarriedBy": "c1" }         // quién la transportaba al guardar (trazabilidad)
  ],
  "Warehouses": {                 // almacenes con fracciones completas
    "Zones": { "estanteria": ["p_3"], "pale": [], "suelo": [] },
    "Acc":   { "MAT-03": 7.2 }    // unidades + fracción acumulada (nunca se pierde)
  },
  "Yard": [                       // patio: objetos recibidos y su desmontaje
    { "DomainId": "microondas_1", "BlueprintId": "microondas",
      "BpFingerprint": -123456,   // huella del catálogo al crearse (GDD §16.5)
      "X": 10, "Y": 1, "Z": 10, "Yaw": 30,
      "Components": [
        { "Id": "carcasa", "State": 2, "Quality": 100, "TerminalTag": null,
          "Fasteners": [ { "Removed": true, "Progress": 1 },
                         { "Removed": false, "Progress": 0.5 } ] }  // double completo,
                                                                 // NO cuantizado a 7 bits
        …
      ] }
  ],
  "Counters": { "NextInstance": 2 },   // anti-colisión de identidades
  "Progress": {                        // progreso económico/tecnológico
    "ToolsOwned": ["llave","destornillador","manos"],
    "Upgrades": [],                    // mejoras adquiridas (sistema futuro)
    "Contracts": [],                   // RESERVADO: contratos (no implementado)
    "NarrativeFlags": []               // RESERVADO: progresión narrativa (no implementado)
  }
}
```

Cobertura de §2 de la instrucción: id único ✓, versión ✓, saldo ✓, inventario y
almacenes ✓, materiales y fracciones ✓, piezas y subensambles ✓ (los PART de
`ProcessPiece` se registran como piezas con `DefId` de subensamble), propiedad/
reservas/terminales ✓, objetos del patio ✓, componentes instalados/extraídos ✓,
fijaciones y progreso ✓, calidad y daño ✓, posiciones y orientaciones ✓,
herramientas y mejoras ✓ (estructura; las compras aún no existen como sistema),
progreso económico/tecnológico ✓ (estructura extensible). Contratos y narrativa:
**estructuras reservadas vacías, sin implementar** (§9 de la instrucción).

**No se persiste** (derivable/episódico, documentado): el log de transacciones
de la sesión (`ScrapyardSession.Log`, trazabilidad episódica), el log de eventos
de desmontaje (`DisassemblyInstance.Events`) y las sesiones de trabajo activas
(ver política §3). La red no replica el archivo: los clientes reciben el estado
por las NVs/NetworkLists existentes.

## 3. Políticas deterministas

- **Transporte al guardar** (GDD §16.4: "las interacciones manuales incompletas
  pueden cancelarse sin penalización"): una pieza `carried`/`scale` se persiste
  como `world` con la pose del portador y `CarriedBy` como trazabilidad. Al
  cargar aparece **en el suelo**, con identidad/calidad/origen intactos: no se
  pierde, no se duplica, nadie reaparece transportando. La captura **no muta el
  estado vivo**: la política aplica solo al archivo (test S12).
  Esto **reemplaza a PROV-11 en el proyecto Unity** (el prototipo JS congelado
  conserva su comportamiento y su serializer v3 sigue intacto para la paridad).
- **Operaciones activas al guardar** (GDD §16.3 pasos 1–2): `PruneStaleWorkers`
  + `StopAllWork` = punto lógico de consistencia. El progreso de fijaciones ya
  vive en el dominio y se conserva exacto (double, no cuantizado): interrumpir
  no penaliza (tests S02/S12).
- **Piezas terminales/almacenadas**: no tienen pose ni físico; al cargar no
  reaparecen en el patio (test S03). Solo las `world` no terminales se
  re-materializan.
- **Pose de respaldo**: si una pieza/objeto lógico no tiene físico visible al
  guardar (caso excepcional), se usa una pose determinista (hash FNV del id ⇒
  anillo alrededor de la mesa): misma entrada ⇒ misma salida, sin azar.
- **Contadores de identidad**: `UidSeq` se deriva de los uids persistidos y
  `NextInstance` de los `DomainId`; tras cargar, nada colisiona (test S14). Los
  contadores de red (`netId`) no se reinician dentro del proceso.

## 4. Guardado seguro (GDD §16.3, `SaveRepository`)

1. Validación del DTO **antes** de escribir.
2. Escritura en `*.json.tmp`.
3. **Re-lectura e íntegra re-validación** del temporal (parseo + `ValidateFull`).
4. Copia del archivo actual a `*.json.bak` (respaldo del guardado anterior).
5. Reemplazo del principal; el temporal se descarta siempre al final.

Si cualquier paso falla, **el principal y el .bak quedan intactos** (test S09a).
La carga prueba el principal y, si está corrupto/inválido, el respaldo
(`FromBackup`, test S08); archivos v3 se migran al vuelo (`Migrated`, test S10).
El `.bak` conserva el guardado **anterior** al último exitoso (semántica GDD
§16.3 paso 7: respaldo de lo que se sustituye).

**Cuándo se guarda** (GDD §16.2, adaptado a lo que existe hoy): al iniciar el
host no (se *carga*); autoguardado periódico cada **60 s** (valor elegido, no
del GDD — documentado como decisión); al desconectarse un cliente (checkpoint
de sesión); al cerrar la aplicación (`OnApplicationQuit`); y manual con **F5**
(cualquier peer lo solicita, el host ejecuta). Los disparadores del GDD que aún
no existen como sistemas (completar contrato, comprar ampliación, terminar
construcción) quedan cableables al mismo `SaveNow()`.

## 5. Versionado y migraciones (GDD §16.5)

- `Version = 3`: formato del prototipo (paridad congelada; descarta físicos,
  PROV-11). `Version = 4`: formato completo de producción.
- **Migración explícita v3→v4** (`SaveMigrations.Migrate3To4`, reglas
  documentadas en el código): dinero/piezas/almacenes se conservan; `SaveId`
  nuevo; patio y poses vacíos (la v3 no los persistía); contadores y progreso
  por defecto. Una partida migrada arranca sin objetos en el patio, igual que
  una partida nueva, pero con su economía intacta.
- **Versiones desconocidas se rechazan** (no se adivina hacia atrás ni adelante).
- **Huella de catálogo** (`SaveFormat.BlueprintFingerprint`, FNV-1a sobre ids,
  componentes, dependencias, fijaciones con herramienta/tiempo/verbo y recetas):
  cada objeto del patio guarda la huella del catálogo con el que fue creado. Si
  el catálogo cambia (p. ej. el microondas pasa de 6 a 9 componentes), la carga
  detecta `blueprint_incompatible` y **exige una regla de migración explícita**
  en vez de reinterpretar datos viejos (test S09c). Hoy no hay migraciones de
  blueprint registradas porque el catálogo no cambió desde la Instrucción 05.

## 6. Validación de integridad (`SaveValidator.ValidateFull`)

Detecta, con claves estables: `saveid_vacio`, `dinero_negativo`,
`uid_duplicado_o_nulo`, `defid_nulo`, `calidad_fuera_rango`, `clase_invalida`,
`terminal_invalido`, `terminal_con_almacenamiento`, `sin_terminal_storage_none`,
`transporte_en_save` (carried/scale nunca llegan al archivo),
`reserva_desconocida`, `pose_duplicada_o_nula`, `pose_uid_inexistente`,
`pose_pieza_terminal`, `pose_pieza_no_world`, `world_sin_pose`,
`zona_desconocida/uid_inexistente/storage_inconsistente/categoria_invalida/
pieza_terminal/sobre_capacidad`, `stored_sin_zona`, `material_desconocido`,
`acumulador_negativo/sobre_capacidad`, `yard_id_duplicado_o_nulo`,
`yard_blueprint_desconocido`, `blueprint_incompatible`, `yard_comp_desconocido`,
`yard_estado_invalido`, `yard_calidad_invalida`, `yard_fijaciones_count`,
`yard_progreso_rango`, `contador_instancias_invalido`. La restauración con
problemas **no toca el host** (transacción fallida sin mutaciones).

## 7. Restauración física y de red (§3, capa Unity)

`PersistenceHub.TryRestore` (host, al arrancar): `SaveRepository.Load` →
`SessionPersistence.RestoreInto` (lógica) → `SpawnPhysicalWorld`:

- **Objetos del patio**: se instancia el prefab `DisassemblyObject` en la pose
  guardada con `NetInstanceId`/`BlueprintIdx` fijados **antes** de `Spawn()`;
  el `OnNetworkSpawn` existente puebla la `NetworkList<CompSync>` desde el
  snapshot del dominio ⇒ estados, fijaciones, progreso cuantizado y calidad se
  replican a todos (incluido late-join) sin protocolo nuevo.
- **Piezas world**: `PieceSpawner.Spawn(piece, pose, yaw)` (identidad en NVs
  antes de `Spawn()`).
- **Economía**: `ETSVSessionView.SyncEconomy()` copia el snapshot restaurado a
  `Money`/`ZoneCounts`/`MaterialUnits`; el late-join las recibe con el spawn del
  objeto de sesión. El que se incorpora después ve el mismo mundo restaurado
  (test S11 verifica la fuente autoritativa; la replicación real queda para el
  editor).
- Si no hay partida (o es inutilizable), el bootstrap spawnea la partida nueva
  (microondas sobre la mesa) como antes. Un archivo corrupto **no se
  sobrescribe** al cargar; solo el próximo guardado válido lo reemplaza (con
  .bak).

## 8. Archivos creados / modificados

**Creados:**
- `Domain/Persistence/SaveFull.cs` — DTOs v4, huella de catálogo,
  `SerializeFull/DeserializeFull`, `SaveMigrations`, `ValidateFull`, codec de
  instancia de desmontaje (`DisassemblyRules.SerializeInstance/DeserializeInstance`).
- `Sim/SessionPersistence.cs` — `IPoseSource`/`SavePose`, punto de consistencia,
  `Capture`, `RestoreInto`, poses de respaldo deterministas.
- `Sim/SaveRepository.cs` — escritura segura (temp+verificación+reemplazo+
  respaldo) y carga tolerante con fallback al .bak y migración.
- `Disassembly/PersistenceHub.cs` — host: ruta real, restauración+reconstrucción
  física, autoguardado, guardado al cerrar, poses desde transforms.
- `Tests/EditMode/SavePersistenceTests.cs` — 14 tests S01–S14.

**Modificados:**
- `Domain/Persistence/SaveGame.cs` — DTO único con campos v4; `Validate` rutea
  por versión (la rama v3 queda intacta para la paridad).
- `Domain/GameInfo.cs` — `SaveVersionFull = 4`.
- `Domain/Data/CatalogData.cs` — helper `FastenerTypeById`.
- `Domain/Disassembly/DisassemblyRules.cs` — clase `partial` (codec en SaveFull).
- `Sim/DisassemblySessionHost.cs` — `AllInstances`, `StopAllWork`,
  `ClearInstances`, `RestoreInstance`, `ApplyState`.
- `Sim/ScrapyardSession.cs` — motivos nuevos `guardado_fallido`/`partida_invalida`.
- `Disassembly/ETSVSessionView.cs` — `RequestSaveRpc`, `FeedSaveResult`,
  `FeedRestored`, `TxKind.Guardado/Restaurado`.
- `Disassembly/DisassemblyObjectView.cs` — registro estático netId→view (poses).
- `Disassembly/PieceSpawner.cs` — overload con yaw persistido.
- `Disassembly/DisassemblyPlayerController.cs` — F5 solicita guardado.
- `Disassembly/DisassemblyHud.cs` — toasts de guardado/restauración + motivos.
- `ETSVBootstrap.cs` — flujo de arranque: hub de persistencia, restaurar o
  partida nueva, checkpoint en desconexión (con drop físico del que se fue).

## 9. Verificaciones ejecutadas (§8 de la instrucción)

Banco: `mcs`/`mono` (sin Unity Editor). **76/76 tests C# PASS** (62 previos +
14 nuevos) y **JS 20/20 + 53/53 PASS** (prototipo congelado intacto; su
serializer v3 y sus tests de persistencia siguen verdes — la paridad no se tocó).

Los 14 nuevos (`SavePersistenceTests`): **S01** round-trip completo (dinero,
piezas, fracciones, patio, daño, fijaciones, SaveId estable) · **S02** guardado
durante desmontaje parcial (punto de consistencia, progreso exacto, el trabajo
continúa tras cargar) · **S03** vendida/procesada/almacenada no reaparecen;
solo world tiene pose · **S04** cadena guardar→cargar→guardar→cargar sin
duplicar ni resucitar (+.bak creado) · **S05** fracciones de material exactas
(7.2 → 7 u + 0.2; 0.4 → 0 u + 0.4) · **S06** calidad y reserva sobreviven y
siguen bloqueando la venta · **S07** desconexión durante transporte → pieza al
suelo, sin duplicar · **S08** principal corrupto → recuperación desde el
respaldo · **S09** corrupción detectada (7 variantes) y escritura inválida NO
sobrescribe la copia válida · **S10** migración v3→v4 explícita (directa y a
través del repositorio; versión desconocida rechazada) · **S11** late-join:
snapshot económico y CompSync idénticos al original tras restaurar · **S12** la
captura no muta el estado vivo · **S13** política de transporte con
trazabilidad `CarriedBy` y pieza re-levantable por otro jugador · **S14**
contadores sin colisión de identidades.

**Niveles de verificación (honestos):**
- *Ejecutado* (mono): formato v4, validación, migración, escritura segura con
  archivos reales (System.IO), restauración lógica completa y todas las
  políticas.
- *Compilado contra stubs* (mcs): capa Unity (`PersistenceHub`, bootstrap,
  RPCs) — sintaxis y APIs contra stubs calibrados con NGO 2.12/U6-FPM, **no**
  contra Unity Editor.
- *NO verificado* (requiere Unity Editor): escritura real en
  `Application.persistentDataPath`, reconstrucción física de NetworkObjects al
  cargar, replicación del mundo restaurado a clientes y late-join real,
  `OnApplicationQuit`, F5 en juego.

## 10. Checklist para Unity Editor (pendiente)

1. Abrir `unity/` con Unity 6000.3.5f2; Host en la escena `Multiplayer`.
2. Desarmar parcialmente el microondas, vender/procesar algo, dejar una pieza en
   el suelo y otra en la estantería; pulsar **F5** (toast "✔ … guardó la
   partida"); verificar `etsv_save.json` en `Application.persistentDataPath`.
3. Detener Play y volver a entrar como Host: el patio debe reconstruirse (objeto
   con su progreso, pieza en el suelo en su pose, almacenes/zonas/dinero en el
   HUD) y aparecer el toast "✔ Partida restaurada".
4. Conectar un cliente: ve el mismo mundo restaurado (NVs/NetworkLists/spawns).
5. Late-join tras la restauración: recibe dinero/zonas/materiales y las piezas
   del mundo.
6. Corromper a mano el `etsv_save.json`: al arrancar debe cargar el `.bak` (log
   "recuperada desde la copia de respaldo") y NO sobrescribirlo hasta un guardado
   válido.
7. Desconectar un cliente que transporta: checkpoint guardado; la pieza queda en
   el suelo.

## 11. Decisiones provisionales / pendientes

- **Autoguardado 60 s**: valor elegido (el GDD pide "periódicamente" sin
  número). Ajustable en `PersistenceHub.AutoSaveInterval`.
- **Un solo slot de guardado** (`etsv_save.json` + `.bak`): el GDD no define
  slots múltiples; la estructura lo permite a futuro (SaveId distinto por ruta).
- **PROV-11 queda reemplazada** en Unity por la política de transporte §3 (el
  registro PROVISORIO del prototipo no se modifica: sigue describiendo el
  comportamiento congelado de la v3). El resto de las PROV (01–10) no se tocó.
- `Progress.ToolsOwned` lista las herramientas del catálogo actual (todas
  disponibles); cuando exista el sistema de compras/mejoras, este campo es su
  punto de persistencia (ya preparado).
- Contratos y narrativa: **solo estructuras reservadas** (`Contracts`,
  `NarrativeFlags`), vacías y sin lógica — como pide la instrucción.
- La migración de blueprints (GDD §16.5, cambio de componentes/recetas) hoy
  solo **detecta** incompatibilidad (`blueprint_incompatible`); las reglas de
  migración por blueprint se escribirán cuando el catálogo cambie de verdad
  (objetos 02–30).
