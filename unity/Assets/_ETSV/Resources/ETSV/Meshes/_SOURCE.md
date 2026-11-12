# Modelos 3D incorporados — fuentes y licencias

Todos los modelos de estas carpetas son **CC0 1.0** (dominio público), con sus
archivos de licencia originales copiados junto a los modelos. Solo se copiaron
los FBX (las texturas originales NO se usan: el proyecto aplica sus propios
materiales URP de `Resources/ETSV/Materials` — regla §7.1 de la biblia visual).

| Carpeta | Pack | Fuente | Descarga |
|---|---|---|---|
| KenneyCity | City Kit Industrial 2.0 (Kenney) | https://kenney.nl/assets/city-kit-industrial | 2026-11-12, link firmado oficial del sitio |
| KenneyFurniture | Furniture Kit 2.0 (Kenney) | https://kenney.nl/assets/furniture-kit | 2026-11-12, link firmado oficial del sitio |
| Quaternius | Modular Sci-Fi MegaKit [Standard] (@Quaternius) | https://quaternius.com/packs/modularscifimegakit.html (vía OpenGameArt: https://opengameart.org/content/modular-sci-fi-megakit) | 2026-11-12, zip `modular_scifi_megakitstandard.zip` (48.6 MB, CC0 según License_Standard.txt) |

## Modelos y uso en el juego

KenneyCity (escala del pack: 1 unidad ≈ 7 m; en el código se aplica ×7):
- shipping-container-a/b/c → contenedores apilados del patio
- building-c, building-m → galpones de fondo
- water-tower, chimney-medium, detail-tank-large → hitos industriales del perímetro

KenneyFurniture (escala real en metros, ×1):
- bookcaseOpen / bookcaseOpenLow → estación estantería y almacén
- cardboardBoxClosed / Open → cajas en pilas de chatarra y bajo la mesa
- kitchenMicrowave / televisionVintage → electrodomésticos en la chatarra
  (OJO: kitchenMicrowave es SOLO decoración; el microondas desmontable sigue
  siendo el sistema modular de 6 componentes — restricción del GDD)
- trashcan → tacho junto a la mesa
- lampSquareCeiling → artefactos de las lámparas colgantes
- bench → banco de descanso decorativo

Quaternius (escala real en metros, ×1):
- Prop_Barrel_Large → tambor de residuos especiales y contenedores de material
- Prop_Crate3 / Prop_Crate4 → cajones de las pilas de chatarra
- Prop_Computer → terminal/visor de la balanza y de la trituradora
- Prop_Fan_Small → extractor de la trituradora
- Prop_Chest → caja de herramientas junto a la mesa
- Prop_Light_Wide → artefacto de iluminación del galpón
- Column_MetalSupport → postes estructurales del galpón
- Prop_Vent_Small → rejilla de ventilación en la pared
- Decal_Sign → placa base de la señalización

## Descartados en esta instrucción (documentado)
- KayKit (mirror de GitHub sin archivo de licencia dentro del zip → fuente no
  verificable en sí misma; se prefiere no incorporar).
- poly.pizza (requiere API key).
- Palés: ningún pack descargado trae modelo de palé ⇒ el palé sigue
  procedural (tablones de madera) y queda pendiente (matriz P5).
- Herramientas 3D: no encontradas en CC0 con descarga directa; el HUD sigue
  con iconos (matriz P9).
