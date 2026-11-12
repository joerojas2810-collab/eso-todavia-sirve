# FIX COMPILACIÓN UNITY — 26 errores de la primera importación real (2026-11-12)

El propietario abrió el proyecto en **Unity 6000.3.5f2 (Windows)** por primera
vez y la importación produjo 26 errores de compilación. Este documento registra
la causa raíz de cada grupo, las correcciones y qué fue verificado contra qué.

**Importante: la compilación real NO se declara corregida hasta que el
propietario reimporte y Unity muestre 0 errores.** Lo que sí se hizo acá es
verificar cada archivo contra las **DLLs reales del Editor 6000.3.5f2** y el
**fuente real de los paquetes** (NGO 2.12.0, InputSystem 1.17.0, URP 17.3.0),
no contra suposiciones.

---

## 1. Causas raíz y correcciones

### Grupo A — `ModelsSceneryTests.cs` CS0103 (`ETSVModels`, `ETSVArt`, `ETSVScenery`, `ScrapyardLayout`)

**Causa raíz**: esos scripts vivían en `Assets/_ETSV/Scripts/Integration/
Disassembly/`, carpeta **sin assembly definition** ⇒ compilaban a
`Assembly-CSharp`. Un assembly definido por asmdef (`ETSV.Domain.Tests`)
**no puede referenciar** `Assembly-CSharp` (la dependencia va solo en la otra
dirección). Los namespaces y la visibilidad de las clases eran correctos; el
problema era de **frontera de assemblies**.

**Corrección** (sin eliminar ningún test):
- Nueva carpeta `Assets/_ETSV/Scripts/Presentation/` con `ETSV.Presentation.asmdef`
  (refs: `ETSV.Domain`, `ETSV.Sim`; `autoReferenced: true`). Los GUIDs de los
  `.meta` se conservaron al mover ⇒ las referencias de escena/prefabs no se rompen.
- Archivos movidos (todos verificados **sin** dependencias a NGO/InputSystem/
  upstream/vistas): `ETSVArt.cs`, `ETSVModels.cs`, `ETSVScenery.cs`,
  `ScrapyardLayout.cs`, `BlueprintBuilder.cs`, `DisassemblyLayouts.cs`,
  `DisassemblyRefs.cs`, `SessionConfig.cs`.
- `ETSV.Domain.Tests.asmdef` ahora referencia `["ETSV.Domain","ETSV.Sim","ETSV.Presentation"]`.
- Los archivos que **sí** tocan upstream (`ETSVBootstrap.cs`,
  `ETSVSessionView.cs`, `DisassemblyPieceView.cs`, `DisassemblyPlayerController.cs`
  — usan `PlayerMovement`/`PickupItem`, que viven en `Assembly-CSharp` del
  proyecto base y no se pueden mover) y los que dependen de ellos
  (`DisassemblyHud.cs`, `DisassemblyObjectView.cs`, `PieceSpawner.cs`,
  `PersistenceHub.cs`) **quedan en `Assembly-CSharp` como antes**. Desde ahí
  ven `ETSV.Presentation` automáticamente (`autoReferenced: true`), igual que
  ya veían `ETSV.Domain`/`ETSV.Sim`.

### Grupo B — `DisassemblyHud.cs:74` CS0117 (`Length.Pixel`)

**Causa raíz**: `UnityEngine.UIElements.Length` **no tiene** el método estático
`Pixel(float)` en la API real (sí tiene `Percent(float)`, `Auto()` y conversión
implícita desde `float`). El stub local del banco había inventado `Length.Pixel`
y ocultó el error.

**Corrección**: `new Translate(Length.Percent(-50), Length.Pixel(0))` →
`new Translate(Length.Percent(-50), 0f)` (conversión implícita float→Length =
píxeles, misma semántica). Stub corregido: `Length.Pixel` eliminado de
`ci/stubs/UIElementsStub.cs` para que el banco no vuelva a enmascarar este error.
Revisadas todas las demás llamadas `Length.*` del proyecto: solo queda
`Length.Percent` (líneas 72/108/239 del HUD), que sí existe.

### Grupo C — `ETSVBootstrap.cs:274` CS0029 (`NetworkObject` → `GameObject`)

**Causa raíz**: en NGO real 2.12.0, `NetworkClient.PlayerObject` es
**`NetworkObject`** (verificado: fuente real `Runtime/Connection/NetworkClient.cs:60`).
El stub local lo declaraba como `GameObject`, ocultando la incompatibilidad.
`_attachedPlayer` es `GameObject` ⇒ la asignación directa no compila; además
`NetworkObject` no tiene `AddComponent` (CS1061 en la línea 276).

**Corrección**: se toma el `NetworkObject` y se usa explícitamente
`localNo.gameObject` para la referencia `GameObject` y para
`GetComponent/AddComponent`. El flujo de red **no cambia** (misma lógica, mismo
orden). Stub alineado a la firma real (`public NetworkObject PlayerObject;`).

### Grupo D — error adicional encontrado por auditoría (no reportado explícitamente)

**`ETSVBootstrap.cs:167`**: `_nm.ConnectedClients.TryGetValue(...)` — en NGO
real, `NetworkManager.ConnectedClients` es `IReadOnlyDictionary<ulong,
NetworkClient>` (`Runtime/Core/NetworkManager.cs:599`), que **no expone**
`TryGetValue` ⇒ CS1061 en Unity real. El stub lo declaraba `Dictionary<,>`.

**Corrección**: `ContainsKey(clientId)` + indexador (ambos sí existen en
`IReadOnlyDictionary`). Lógica idéntica. Stub alineado a la firma real.

## 2. Auditoría del resto de la superficie de API (los 26 errores)

Con los grupos A–D corregidos (A explica ~14-15 CS0103/CS0246 del archivo de
tests, B 1, C 2-3, D 1), se auditó **todo** el resto del código usado contra
fuentes reales, para cazar cualquier error restante de los 26:

| Superficie | Método de verificación | Resultado |
|---|---|---|
| Todo `Scripts/Presentation` (8 archivos) | **Compilación real** con mcs contra las DLLs del Editor 6000.3.5f2 (`Managed/UnityEngine/*.dll` + netstandard 2.1 + shims netfx oficiales) | 0 errores |
| `ETSVBootstrap` + vistas + HUD + spawners (`Integration/`) | Compilación contra DLLs reales + stubs NGO/InputSystem **con firmas alineadas al fuente real** + Newtonsoft net45 real | 0 errores |
| `Sim/` (host, sync, persistencia) | Compilación contra DLLs reales + stub NGO alineado | 0 errores |
| `Domain/` | Compilación contra netstandard 2.1 real | 0 errores |
| `Editor/CaptureSceneryShots.cs` | Compilación contra DLLs reales **incl. UnityEditor** + mini-stub URP con firmas copiadas del fuente real | 0 errores, 0 warnings |
| NGO 2.12.0 miembros usados (`NetworkVariable<T>(value, readPerm, writePerm)`, `NetworkList<T>` Add/Clear/Contains/Count con `T: unmanaged, IEquatable<T>` — `CompSync`/byte/ushort cumplen, `OnValueChanged`, `Spawn()/Despawn(true)`, `IsSpawned`, `IsServer`, `OnNetworkSpawn/Despawn`, `INetworkSerializeByMemcpy`, permisos `Everyone`/`Server`, `LocalClient`) | Grep al **fuente real** del tarball oficial `com.unity.netcode.gameobjects-2.12.0` | Todo existe con esas firmas |
| InputSystem 1.17.0 (`new InputAction(name, InputActionType.Button)`, `AddBinding("<Keyboard>/f5")` — extensión real en `InputActionSetupExtensions.cs:305`, `Enable()` :907, `WasPressedThisFrame()`) | Grep al **fuente real** del tarball oficial | Todo existe |
| URP 17.3.0 (`UniversalAdditionalCameraData` no-obsolete, `renderPostProcessing` :789, `antialiasing` :799, `AntialiasingMode.SubpixelMorphologicalAntiAliasing` :61) | Grep al **fuente real** (BuiltInPackages del Editor descargado — misma versión 17.3.0 del manifest) | Todo existe |

Los únicos 3 grupos de API que no se compilaron contra binarios oficiales
(NGO, InputSystem, URP — paquetes que se resuelven al importar y no están
precompilados en ninguna parte accesible sin licencia) se verificaron contra
el **fuente exacto de las versiones del `Packages/manifest.json`**.

## 3. Cambios en el banco local (`unity/ci/`)

- `stubs/NetcodeStub.cs`: `PlayerObject` → `NetworkObject`; `ConnectedClients`
  → `IReadOnlyDictionary<ulong, NetworkClient>` (firmas reales; el banco ahora
  reproduce estos errores en vez de ocultarlos).
- `stubs/UIElementsStub.cs`: `Length.Pixel` eliminado (no existe en la API real).
- `build-and-test.sh`: compila también `Scripts/Presentation/` (nueva carpeta).
- Banco: **84/84 C#** + **20/20** y **53/53** JS tras todos los cambios.

## 4. Archivos modificados/movidos (resumen)

- Movidos a `Assets/_ETSV/Scripts/Presentation/` (GUIDs intactos): ETSVArt,
  ETSVModels, ETSVScenery, ScrapyardLayout, BlueprintBuilder,
  DisassemblyLayouts, DisassemblyRefs, SessionConfig (+ sus .meta).
- Nuevos: `ETSV.Presentation.asmdef` (+.meta), meta de carpeta.
- Editados: `DisassemblyHud.cs` (Grupo B), `ETSVBootstrap.cs` (Grupos C+D),
  `ETSV.Domain.Tests.asmdef` (referencia), `CaptureSceneryShots.cs` (warning),
  stubs y banco (sección 3), docs con rutas viejas (INTEGRACION.md,
  INTEGRACION_ECONOMIA.md, MODELOS_3D.md).
- **No tocados**: los 5 GDD, contenido upstream, reglas de juego, física, red
  (la lógica de los flujos es idéntica; solo cambian expresiones de tipos y
  fronteras de assembly).

## 5. Pendiente de confirmar en Unity Editor (propietario)

1. Reimportar el proyecto (Unity detectará los .meta movidos por GUID; no hace
   falta reasignar nada en la escena).
2. Confirmar **0 errores** en la Console. Si quedara algún error de los 26 no
   reproducido acá, enviar el texto exacto (código CS + archivo + línea).
3. Recién entonces: Play Mode, y opcionalmente ejecutar
   `Etsv.EditorTools.CaptureSceneryShots.CaptureAll` (requiere licencia
   activada; ver MODELOS_3D.md §4) para las capturas reales.
