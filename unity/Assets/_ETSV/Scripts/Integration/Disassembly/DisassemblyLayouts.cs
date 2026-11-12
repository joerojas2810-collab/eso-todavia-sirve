// [ETSV] Integración — DATOS VISUALES (solo presentación, nunca reglas).
// Geometría sencilla por blueprint: posición/tamaño/primitiva/color de cada
// componente y offsets de sus fijaciones. El dominio (ETSV.Domain) no contiene
// geometría a propósito; esta tabla es el único lugar donde vive.
// Los 30 objetos del GDD se incorporan agregando entradas acá (o dejando que
// BlueprintBuilder use el layout genérico en rejilla), sin tocar controladores.

using System.Collections.Generic;
using Etsv.Domain;
using UnityEngine;

namespace Etsv.Integration
{
    public class CompLayout
    {
        public Vector3 Pos;             // posición local dentro del objeto
        public Vector3 Scale;           // tamaño local
        public string Primitive;        // "cube" | "cylinder" | "sphere"
        public Color Color;
        public Vector3[] FastenerOffsets; // posiciones locales de cada fijación (en orden del blueprint)
    }

    public static class DisassemblyLayouts
    {
        // Microondas (~0.60 × 0.40 × 0.45 m, apoyado sobre su base local y=0).
        private static readonly Dictionary<string, CompLayout[]> Tables =
            new Dictionary<string, CompLayout[]>();

        static DisassemblyLayouts()
        {
            Tables["microondas"] = new[]
            {
                // carcasa: cuerpo principal
                new CompLayout
                {
                    Pos = new Vector3(0f, 0.20f, 0f), Scale = new Vector3(0.60f, 0.40f, 0.45f),
                    Primitive = "cube", Color = new Color(0.62f, 0.64f, 0.67f),
                    FastenerOffsets = new[]
                    {
                        new Vector3(-0.28f, 0.34f, 0.20f), new Vector3(0.28f, 0.34f, 0.20f),
                        new Vector3(-0.28f, 0.06f, 0.20f), new Vector3(0.28f, 0.06f, 0.20f),
                    },
                },
                // puerta: frente izquierdo
                new CompLayout
                {
                    Pos = new Vector3(-0.14f, 0.20f, 0.235f), Scale = new Vector3(0.30f, 0.32f, 0.03f),
                    Primitive = "cube", Color = new Color(0.20f, 0.24f, 0.30f),
                    FastenerOffsets = new[]
                    {
                        new Vector3(-0.16f, 0.12f, 0.0f), new Vector3(-0.16f, -0.12f, 0.0f),
                    },
                },
                // panel de control: frente derecho
                new CompLayout
                {
                    Pos = new Vector3(0.18f, 0.20f, 0.235f), Scale = new Vector3(0.16f, 0.32f, 0.03f),
                    Primitive = "cube", Color = new Color(0.12f, 0.13f, 0.15f),
                    FastenerOffsets = new[]
                    {
                        new Vector3(-0.05f, 0.12f, 0.02f), new Vector3(0.05f, 0.12f, 0.02f),
                        new Vector3(0.0f, -0.12f, 0.02f),
                    },
                },
                // plato giratorio: interior, visible al sacar la puerta
                new CompLayout
                {
                    Pos = new Vector3(-0.12f, 0.06f, 0.05f), Scale = new Vector3(0.26f, 0.02f, 0.26f),
                    Primitive = "cylinder", Color = new Color(0.85f, 0.88f, 0.90f),
                    FastenerOffsets = new Vector3[0], // extracción directa
                },
                // motor de giro: interior trasero
                new CompLayout
                {
                    Pos = new Vector3(0.10f, 0.12f, -0.10f), Scale = new Vector3(0.12f, 0.12f, 0.14f),
                    Primitive = "cube", Color = new Color(0.72f, 0.45f, 0.20f),
                    FastenerOffsets = new[]
                    {
                        new Vector3(-0.05f, 0.05f, 0.08f), new Vector3(0.05f, 0.05f, 0.08f),
                    },
                },
                // módulo eléctrico sellado: interior trasero, amarillo de precaución
                new CompLayout
                {
                    Pos = new Vector3(-0.05f, 0.30f, -0.12f), Scale = new Vector3(0.20f, 0.10f, 0.14f),
                    Primitive = "cube", Color = new Color(0.85f, 0.70f, 0.10f),
                    FastenerOffsets = new[]
                    {
                        new Vector3(-0.08f, 0.0f, 0.08f), new Vector3(0.08f, 0.0f, 0.08f),
                        new Vector3(-0.08f, 0.0f, -0.08f), new Vector3(0.08f, 0.0f, -0.08f),
                    },
                },
            };
        }

        /// <summary>Layout del blueprint, o null si no tiene tabla (→ genérico).</summary>
        public static CompLayout[] For(ObjectBlueprint bp)
        {
            CompLayout[] table;
            if (bp != null && Tables.TryGetValue(bp.Id, out table) && table.Length == bp.Components.Length)
                return table;
            return null;
        }

        /// <summary>
        /// Layout genérico en rejilla para blueprints sin tabla: permite incorporar
        /// los 30 objetos del GDD SIN programar nada (Instrucción 05 §2 / 06 §2).
        /// </summary>
        public static CompLayout[] Generic(ObjectBlueprint bp)
        {
            var layouts = new CompLayout[bp.Components.Length];
            int cols = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(bp.Components.Length)));
            for (int i = 0; i < bp.Components.Length; i++)
            {
                var def = bp.Components[i];
                int nf = def.Fasteners != null ? def.Fasteners.Length : 0;
                var offs = new Vector3[nf];
                for (int f = 0; f < nf; f++)
                {
                    float ang = nf == 1 ? 0f : (Mathf.PI * 2f * f) / nf;
                    offs[f] = new Vector3(Mathf.Cos(ang) * 0.13f, 0.13f, Mathf.Sin(ang) * 0.13f);
                }
                layouts[i] = new CompLayout
                {
                    Pos = new Vector3((i % cols) * 0.5f - (cols - 1) * 0.25f, 0.15f, (i / cols) * 0.5f),
                    Scale = new Vector3(0.3f, 0.3f, 0.3f),
                    Primitive = "cube",
                    Color = def.Sealed ? new Color(0.85f, 0.70f, 0.10f) : new Color(0.55f, 0.58f, 0.62f),
                    FastenerOffsets = offs,
                };
            }
            return layouts;
        }

        /// <summary>Color/material aproximado para una pieza suelta (por definición).</summary>
        public static Color PieceColor(string defId, string bpIdHint)
        {
            var bp = bpIdHint != null ? GameCatalog.ObjectById(bpIdHint) : null;
            var table = bp != null ? For(bp) : null;
            if (table != null && bp != null)
            {
                for (int i = 0; i < bp.Components.Length; i++)
                    if (bp.Components[i].Id == defId) return table[i].Color;
            }
            // subensambles (PART_DEFS): color por material dominante del catálogo
            if (defId == "motor_pequenio") return new Color(0.72f, 0.45f, 0.20f);
            return new Color(0.55f, 0.58f, 0.62f);
        }

        public static Vector3 PieceScale(string defId)
        {
            // Tamaños de pieza suelta derivados de la clase de peso (presentación).
            var partDef = EconomyData.PartDefs.ContainsKey(defId) ? EconomyData.PartDefs[defId] : null;
            string wc = partDef != null ? partDef.WeightClass : "P1";
            switch (wc)
            {
                case "P1": return new Vector3(0.22f, 0.22f, 0.22f);
                case "P2": return new Vector3(0.35f, 0.30f, 0.35f);
                default: return new Vector3(0.5f, 0.4f, 0.5f);
            }
        }
    }
}
