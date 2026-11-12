# Atribuciones y licencias

## Base técnica: U6-First-Person-Multiplayer

- **Obra**: "U6-First-Person-Multiplayer" © 2026 Unity Technologies
- **Fuente**: https://github.com/Unity-Technologies/U6-First-Person-Multiplayer
- **Commit incorporado**: `a69cb8d70da855fa8396f0d37068a8470430f640` (2026-08-20, rama `main`)
- **Licencia**: Unity Companion License for Unity-dependent projects —
  https://unity3d.com/legal/licenses/unity_companion_license
  (texto completo en `LICENSE_UPSTREAM.md`). Permite uso comercial en proyectos
  creados con Unity; se distribuye "AS IS", sin garantía.
- **Incorporación**: copia de `Assets/`, `Packages/`, `ProjectSettings/`,
  `UIElementsSchema/` y `.gitignore` en esta carpeta. Se excluyó `.github/media`
  (GIFs de demo, 84 MB) por peso; el README upstream se conserva en
  `docs/UPSTREAM_README.md`.
- **Archivos upstream modificados** (todos marcados con comentarios `// [ETSV]`):
  - `Assets/Scripts/RelayManager.cs` — capacidad de sesión 4 → `SessionConfig.MaxPlayers` (7); login/logout Vivox condicionado a `SessionConfig.VoiceEnabled`.
  - `Assets/Scripts/LobbyController.cs` — cleanup Vivox condicionado.
  - `Assets/Scripts/ProximityVoice.cs` — no inicializa Vivox con la voz desactivada.
  - `Assets/Scripts/PlayerSpawnManager.cs` — aprueba hasta 7 jugadores; posiciones de spawn 5–7 con fallback procedural hasta editar la escena.
  - `Assets/Scripts/PlayerColour.cs` — la paleta de 4 materiales cicla para jugadores 5–7.
  - `ProjectSettings/ProjectSettings.asset` — `productName` / `companyName`.
- Todo lo demás de la base permanece **sin modificar** para facilitar la adopción de
  futuras actualizaciones upstream (procedimiento en `docs/INTEGRACION.md` §5).

## Código propio

- `Assets/_ETSV/**` — © 2026 colaboradores de ¡ESO TODAVÍA SIRVE!, licenciado bajo la
  MIT License del repositorio (`../LICENSE`).

## Herramientas/dependencias de paquete

Todas las dependencias de `Packages/manifest.json` provienen del manifest upstream
(paquetes oficiales de Unity bajo sus licencias estándar; Vivox bajo Unity Gaming
Services Terms). No se agregó ni se quitó ningún paquete.

## Referencias no incorporadas

- `stevenharmongames/Unity-FirstPersonInteractionToolkit` (MPL-2.0): **solo referencia
  de diseño** por decisión de proyecto (Instrucción 04 #5) — no se copió código, por lo
  que no hay obligaciones MPL en este repositorio.
- `bababuyyy/unity-fps-gameplay-systems` (MIT): adopción selectiva pendiente de una
  instrucción futura; si se incorpora, su aviso de copyright MIT se agregará acá.
- Assets CC0 previstos (Kenney, Quaternius): aún no incorporados; CC0 no exige
  atribución, pero se registrará el origen cuando se importen.
