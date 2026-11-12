# Instrucción 07 — Recuperación, almacenamiento y economía cooperativa integrada

Estado: **implementado y verificado por tests en el dominio/Sim (62/62 bajo mono);
la capa Unity compila contra stubs. Pendiente de verificación en Unity Editor**
(físicas, RPCs reales y replicación entre clientes — ver §9).

Este documento describe cómo el desmontaje físico (Instrucción 06) se conecta con
la balanza, la trituradora, los contenedores de residuos y materiales, los
almacenes y la economía de `ETSV.Domain`, con autoridad del host y reutilizando
los sistemas existentes. No hay un segundo inventario ni estados duplicados:
**el dominio (`GameStateData` + `EconomyContext`) es el único estado económico**.

---

## 1. Sistemas reutilizados (sin modificar ni duplicar)

| Sistema existente | Uso en esta instrucción |
|---|---|
| `PickupItem` (U6-FPM, `Assets/Scripts`) | Transporte físico de piezas: `HeldBy` (NetworkVariable server-write), hold kinematic, auto-drop. **No se tocó.** |
| `PlayerInteraction` / `PlayerMovement` (U6-FPM) | Raycast `InteractRay`/`InteractRange` (5 m) y acción "Interact" (E) para apuntar y usar estaciones. **No se tocaron.** |
| `EconomyOps` (ETSV.Domain, Instr. 05) | Todas las operaciones económicas: `PickupPiece`, `DropPiece`, `PlaceOnScale`, `SellPiece`, `ProcessPiece`, `StorePiece`, `RetrievePiece`, `SellMaterial`, `DisposeSpecial`, `ReservePiece`. Se **extendió** (no reemplazó): `SessionOwner`, `CarryByPlayer` y overloads con `playerId` (Instr. 07 §2). El path legacy (`playerId = null`) conserva la paridad con el prototipo JS. |
| `WarehouseRules` (ETSV.Domain) | Zonas (estantería 8 / palé 6 / suelo 4), categorías, reservas, unidades y fracciones de materiales, capacidad de contenedores (120 u, PROV-05). Sin cambios. |
| `DisassemblySessionHost` (Sim, Instr. 06) | Cerebro del desmontaje. Cambios mínimos: las piezas nacen con `Owner = Ctx.SessionOwner` en vez de `"P1"`. |
| `ETSVSession` (Sim) | + `static ScrapyardSession Scrapyard` (acceso del host a la economía de la sesión). |
| `DisassemblyGate` (Sim) | `ValidatePresence` (distancia máxima 5 m, dt máximo 0.25 s) reutilizado como gate de las estaciones. |
| `CatalogIndex` (Sim) | Índices estables de blueprints/definiciones de pieza usados por las NetworkVariables. |
| Prefabs `Assets/_ETSV/Resources/ETSV/` | `ETSVSession.prefab` ahora porta la NetworkBehaviour de economía; `DisassemblyPiece.prefab` ya llevaba `PickupItem` (Instr. 06) ⇒ las piezas extraídas son físicamente transportables sin cambios. |
| Prototipo HTML + 5 GDDs | Intactos. Los GDDs siguen siendo la fuente de verdad de valores y reglas. |

## 2. Identidad y propiedad cooperativa (§2 de la instrucción)

Se eliminó la dependencia funcional del `"P1"` provisional distinguiendo cuatro
conceptos que antes se solapaban:

1. **Titularidad legal de las piezas** — `EconomyContext.SessionOwner =
   "chatarrera"` (constante `ScrapyardSession.OwnerTag`). Todas las piezas de la
   sesión nacen con `Owner = "chatarrera"`: la economía es compartida, conforme a
   los GDDs. `EconomyOps` valida propiedad contra `SessionOwner`, no contra un
   jugador concreto.
2. **Quién transporta** — `EconomyContext.CarryByPlayer[playerTag] = uid`
   (`playerTag = "c" + clientId`) + espejo `ScrapyardSession._carrying
   (clientId → uid)`. Manos independientes por jugador; imposible que dos
   transporten la misma pieza (un solo slot por jugador y la pieza queda
   `carried`).
3. **Autoridad de red** — `NetworkObject.OwnerClientId` sigue siendo solo
   autoridad NGO (quién puede enviar RPCs de su avatar). **No se confunde con
   propiedad económica**: una pieza en manos de J2 sigue siendo de la chatarrería.
4. **Trazabilidad por operación** — `ScrapyardSession.Log` (cap 64): cada
   transacción registra `{PlayerId (clientId), Op, Uid, Amount, Ok, Reason}`.
   Quién hizo cada venta/procesado/almacenado queda auditable.

El dominio conserva ids estables; las claves nuevas (`c{clientId}`) no rompen la
paridad con el prototipo (tests legacy con `playerId = null` siguen verdes).

## 3. Transporte físico ↔ dominio (§3): las prohibiciones, una por una

La sincronización es **host-observa-físico**: `DisassemblyPieceView` (solo en el
host, `IsServer`) se suscribe a `PickupItem.HeldBy.OnValueChanged` y refleja cada
cambio en `ScrapyardSession.OnPhysicalPickup/OnPhysicalDrop`:

- `HeldBy: MaxValue → N` ⇒ `EconomyOps.PickupPiece(ctx, uid, "cN")`. Si el
  dominio rechaza (manos ocupadas, pieza ajena, terminal, P3+ requiere carro…),
  el host **fuerza `HeldBy = MaxValue`**: la pieza vuelve al suelo físico.
- `HeldBy: N → MaxValue` ⇒ `OnPhysicalDrop(N, uid)`.
- Desconexión: `NetworkManager.OnClientDisconnectCallback` ⇒
  `OnPlayerDisconnected(clientId)`: la pieza pasa a `world` en el dominio; el
  NetworkObject físico persiste (el auto-drop de `PickupItem` lo deja en el
  suelo). **Sin duplicación de componentes.**

| Prohibición (§3) | Cómo se cumple |
|---|---|
| Dos jugadores transportando la misma pieza | `HeldBy` es single-value server-write; y en el dominio la pieza `carried` no pasa `HandsFree`/estado de otro jugador. Test E12. |
| Pieza almacenada activa en mundo físico | `StoreAt`/venta/procesado/entrega ⇒ el host **despawnea** el NetworkObject (`DespawnPiece`). Test E15 (almacenada/vendida no re-levantable: `pieza_terminal`/ausencia del físico). |
| Pieza vendida re-levantable | `SellPiece` la pasa a estado terminal `vendido`; `PickupPiece` rechaza con `pieza_terminal`. Test E15. |
| Pieza reservada procesable | `ProcessPiece`/`SellPiece` validan `Reserve` (rechazo `pieza_reservada`). Test E05. |
| Duplicación al desconectarse | Ver arriba + test E03 (desconexión durante transporte: pieza vuelve a `world`, sigue siendo una sola). |
| Restricciones de peso/transporte del GDD | `SpeedMul` por clase de peso (P1 ×1.0, P2 ×0.6…) ya vive en `EconomyOps.PickupPiece`; P3+ exige carro/grúa (`requiere_carro_o_grua`). |

El retiro desde almacén es el único camino que **crea** físico desde el dominio:
`StationUseRpc` (zona, manos libres) ⇒ `RetrieveFrom` (LIFO) ⇒ `PieceSpawner.Spawn`
con identidad en NetworkVariables **antes** de `Spawn()` ⇒ el que se incorpora
tarde recibe la pieza ya en manos del jugador correcto.

## 4. Estaciones físicas (§4–§7, §9)

`StationsCatalog` (Sim) define **14 estaciones con ids estables** (byte en red):

| id | Estación | Kind | Operación de dominio |
|---|---|---|---|
| 0 | Balanza | `Balanza` | `PlaceOnScale` + `SellPiece` (valor = VBase × Q/100 × mod comercial, PROV-03) |
| 1 | Trituradora básica | `Trituradora` | `ProcessPiece` (E = 0.9, PROV-01; receta del catálogo; commit atómico) |
| 2 | Residuos especiales | `ResiduosEspeciales` | `DisposeSpecial` (solo `Sealed`; coste unidades × $5; `fondos_insuficientes` si falta plata) |
| 3 | Estantería | zona `estanteria` (cap 8) | `StorePiece`/`RetrievePiece` (categorías A/B) |
| 4 | Palé | zona `pale` (cap 6) | idem (categorías C/D) |
| 5 | Suelo | zona `suelo` (cap 4) | idem (categoría E) |
| 6–13 | Contenedores MAT-01…08 | `ContenedorMaterial` | `SellMaterial` (todo el stock; MAT-08 no vendible, precio −5) |

- **Layout**: `ScrapyardLayout` (Unity) coloca las 14 estaciones en offsets
  deterministas respecto al ancla de la mesa (`Player 2 Spawn` + (1.8, 0, 1.2)),
  con primitivos + `BoxCollider` raíz + `StationRef` (id) + etiqueta `TextMesh`.
  Son decoración **idéntica en cada peer** (no se replica). Reemplazables por
  modelos de terceros: basta con sustituir `BuildStations()` respetando
  `StationPos(id)` y el `StationRef` — **las reglas no se tocan** (§9).
- **Interacción**: `DisassemblyPlayerController` apunta con el raycast upstream;
  mantener E sobre una estación ⇒ `StationUseRpc(stationId)`.
- **Autoridad**: el host valida presencia (gate: distancia ≤ 5 m a la estación),
  ejecuta la operación en el dominio, y **solo si tuvo éxito** muta el mundo
  físico (despawn de pieza consumida / spawn en retiro), sincroniza NVs
  (`SyncEconomy`) y emite el toast (`TransactionFeedRpc` a todos).
- **Semántica contextual de zonas**: con pieza en las manos ⇒ almacenar; con
  manos libres ⇒ retirar la última (LIFO).
- **Trituradora**: consume la pieza y acredita materiales + subproductos PART en
  una única operación atómica (`ProcessPiece` ya lo garantizaba en el dominio;
  test E02 procesado duplicado ⇒ la segunda solicitud encuentra la pieza
  `procesado` y falla sin mutar). Fragmentos/cintas siguen siendo **solo
  visuales** del prototipo, no recursos económicos.
- **Residuos especiales**: el módulo eléctrico `Sealed` no se vende
  (`pieza_protegida_no_vendible`) ni entra en la trituradora
  (`maquina_incompatible_sellado`); se entrega en el contenedor de residuos con
  coste (GDD §6.2). El rendimiento 4× MAT-08 sigue siendo **PROV-09 provisional**.
- **Almacenes**: identidad/calidad/origen/estado viajan en la pieza del dominio
  (`StoredPart`); materiales acumulables por tipo **sin perder fracciones**
  (`WarehouseRules.MaterialFraction`, fracción conservada en host; solo unidades
  enteras son vendibles).

## 5. Presentación mínima (§9)

- `DisassemblyHud` (UI Toolkit en código, `sortingOrder = 10` sobre el HUD base):
  - **Saldo compartido** arriba a la derecha (`Money` NV del host).
  - **Panel de objetivo**: nombre de estación/objeto, operación disponible,
    ocupación de zonas (`usado/capacidad`), unidades por contenedor, valor
    estimado de la pieza en manos, preview de receta, motivos de rechazo.
  - **Feed de transacciones** (toasts, RPC discreto `TransactionFeedRpc`):
    `✔ J1 vendió +$36` / `✖ J2: zona llena`, con motivos decodificados vía
    `ReasonCodes` (31 claves estables byte ↔ string).
- `DisassemblyPlayerController` expone la pieza que el jugador local transporta
  (`CarriedPiece`, derivada de `HeldBy` visible para todos) para el HUD.
- No hay gráficos definitivos ni animaciones: estaciones = primitivos coloreados
  con etiqueta.

## 6. Networking (§8)

- APIs NGO 2.12 actuales (verificadas contra el fuente del paquete):
  `NetworkVariable<int> Money` (server-write, Everyone-read), `NetworkList<byte>
  ZoneCounts[3]`, `NetworkList<ushort> MaterialUnits[8]`, `[Rpc(SendTo.Server)]
  StationUseRpc(byte, RpcParams = default)` y `[Rpc(SendTo.Everyone)]
  TransactionFeedRpc(...)`.
- **Toda transacción económica la autoriza y ejecuta el host.** Los clientes solo
  solicitan; reciben resultado por NVs (estado) + toast (evento).
- **Late join**: NVs/NetworkLists se replican automáticamente al conectar ⇒ el
  que entra tarde recibe dinero, ocupación de zonas y unidades de materiales
  vigentes; las piezas en el mundo (incluidas las transportadas) llegan como
  NetworkObjects spawn-on-join con su identidad. Test E11 (reconstrucción de
  inventario late-join contra `Snapshot()` autoritativo).
- **No se replica lo derivable continuamente**: los toasts son eventos discretos;
  la ocupación de zonas viaja como 3 bytes; no hay streaming de posiciones ni de
  catálogo (los ids estables viajan como bytes y se resuelven localmente con
  `CatalogIndex`/`StationsCatalog`).

## 7. Archivos creados / modificados

**Creados:**
- `Assets/_ETSV/Scripts/Integration/Sim/ScrapyardSession.cs` — catálogo de
  estaciones, `ReasonCodes`, log de transacciones, `EconomySnapshot`, sesión de
  chatarrería (transporte ↔ dominio, `UseStation`, operaciones, snapshot).
- `Assets/_ETSV/Scripts/Presentation/ScrapyardLayout.cs` — layout
  determinista + `BuildStations()` (primitivos reemplazables) + `StationRef`.
- `Assets/_ETSV/Scripts/Integration/Disassembly/PieceSpawner.cs` — helper del
  host: pieza del dominio → NetworkObject con identidad antes de `Spawn()`.
- `Assets/_ETSV/Tests/EditMode/HostEconomyTests.cs` — 16 tests E01–E16 (§8).

**Modificados:**
- `Domain/Economy/EconomyOps.cs` — `SessionOwner`, `CarryByPlayer`, overloads
  `playerId`, helpers `ClearCarry/HandsFree/SetCarry` (bug de recursión en
  `ClearCarry` corregido).
- `Sim/DisassemblySessionHost.cs` — piezas con `Owner = Ctx.SessionOwner`.
- `Sim/ETSVSession.cs` — + `static Scrapyard`.
- `Disassembly/ETSVSessionView.cs` — **reescrito**: NVs de economía, RPCs de
  herramienta/estación, despawn/spawn de piezas, `SyncEconomy`, feed.
- `Disassembly/DisassemblyPieceView.cs` — watcher host de `HeldBy` ↔ dominio
  (con reversa del físico si el dominio rechaza).
- `Disassembly/DisassemblyObjectView.cs` — spawn de pieza delega a `PieceSpawner`.
- `Disassembly/DisassemblyPlayerController.cs` — estaciones (uso + HUD rico),
  pieza transportada local.
- `Disassembly/DisassemblyHud.cs` — saldo compartido + toasts + motivo de rechazo.
- `ETSVBootstrap.cs` — crea `ScrapyardSession` antes de spawnear instancias,
  construye estaciones, engancha `OnClientDisconnectCallback`.

## 8. Verificaciones ejecutadas (§10)

Banco: `mcs`/`mono` (sin Unity Editor en el entorno). **62/62 tests C# PASS**
(46 previos — paridad y host de desmontaje — + 16 nuevos) y **JS 20/20 + 53/53
PASS** (prototipo congelado intacto).

Los 16 nuevos (`HostEconomyTests`): E01 venta simultánea (dos solicitudes a la
misma pieza: una gana, la otra falla sin doble pago) · E02 procesado duplicado ·
E03 desconexión durante transporte · E04 propiedad inválida (`propiedad_ajena`) ·
E05 pieza reservada · E06 capacidad insuficiente (zonas, categoría incompatible,
contenedor 120) · E07 residuos especiales ciclo completo (incluye
`fondos_insuficientes` → entrega $20) · E08 transacción fallida sin mutaciones
(snapshot idéntico) · E09 snapshot autoritativo + venta de material · E10
late-join reconstruye inventario · E11 sync de saldos tras operaciones · E12 dos
jugadores y la misma pieza (uno solo la lleva) · E13 manos independientes /
retiro con manos ocupadas · E14 retiro LIFO · E15 vendida/almacenada no
re-levantable · E16 distancia a estaciones (gate) + log de trazabilidad.

**Niveles de verificación (honestos):**
- *Ejecutado* (mono): dominio, Sim y ScrapyardSession completos — reglas,
  atomicidad, prohibiciones, late-join lógico.
- *Compilado contra stubs* (mcs): toda la capa Unity (`ETSV.UnitySide.dll`) —
  sintaxis y APIs según stubs calibrados contra el fuente real de NGO 2.12 y
  U6-FPM, **no** contra Unity Editor.
- *NO verificado*: físicas reales, RPCs en red real, replicación entre dos
  clientes, HUD en pantalla, interacción E en editor. Requieren Unity Editor
  (checklist §9).

## 9. Checklist para Unity Editor (pendiente)

1. Abrir `unity/` con Unity 6000.3.5f2; dejar que importe `Assets/_ETSV/`.
2. Escena `Multiplayer`: Host + 1 cliente (ParrelSync o build aparte).
3. Extraer un componente del microondas (Instr. 06) ⇒ levantarlo con E ⇒ llevarlo
   a la **balanza** ⇒ mantener E: dinero sube en ambos clientes, toast ✔, la pieza
   desaparece del mundo.
4. Trituradora con la carcasa ⇒ materiales en contenedores (unidades) ⇒ vender
   todo en el contenedor ⇒ dinero sync.
5. Módulo sellado: balanza y trituradora lo rechazan (toasts con motivo);
   contenedor de residuos lo entrega con coste.
6. Almacenes: guardar en estantería (pieza desaparece), retirar con manos libres
   (reaparece en las manos del que retiró, en ambos clientes).
7. Desconectar un cliente que transporta ⇒ la pieza queda en el suelo, no se
   duplica; late join ⇒ ve saldo/zonas/unidades vigentes y las piezas del mundo.
8. Teclas 1/2/3 siguen cambiando herramienta; el daño por herramienta incorrecta
   sigue funcionando (no regresión de Instr. 06).

## 10. Decisiones provisionales vigentes

- **PROV-01** eficiencia 0.9, **PROV-02** degradación por calidad, **PROV-03**
  modificador comercial 1.0, **PROV-05** capacidad de contenedor 120 u,
  **PROV-09** rendimiento 4× MAT-08 del módulo sellado: sin cambios, siguen
  provisionales hasta validación de diseño.
- `OwnerTag = "chatarrera"` y `MachineId = "trituradora_basica"`: constantes de
  sesión (una chatarrería, una máquina) — el día que haya progresión con
  múltiples máquinas/ sedes, pasan a datos.
- Layout de estaciones en fila junto a la mesa: provisional hasta arte (ver
  INTEGRACION.md §6; ithappy $89 en espera).
- Log de transacciones cap 64 en memoria: suficiente para trazabilidad de
  sesión; la persistencia de progreso sigue por `SaveJsonAdapter` (Instr. 05).
