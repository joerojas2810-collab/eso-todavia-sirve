// [ETSV] Integración — constructor GENÉRICO de visuales desde blueprints.
// Un solo builder para los 30 objetos del GDD: lee ObjectBlueprint + tabla de
// layout (o layout genérico en rejilla) y crea GameObjects primitivos por
// componente y por fijación. Sin prefabs por objeto, sin controladores por
// objeto. Solo presentación: no toca el dominio.

using System.Collections.Generic;
using Etsv.Domain;
using UnityEngine;

namespace Etsv.Integration
{
    /// <summary>Handles de los GameObjects construidos, indexados como el blueprint.</summary>
    public class ObjectVisuals
    {
        public GameObject[] ComponentGOs;
        public GameObject[][] FastenerGOs; // [compIdx][fIdx]; puede ser vacío
        public Renderer[] ComponentRenderers;
        public Renderer[][] FastenerRenderers;
        public Color[] BaseColors;
    }

    public static class BlueprintBuilder
    {
        private const string UrpLit = "Universal Render Pipeline/Lit";
        private const string FallbackShader = "Standard";

        public static Material MakeMaterial(Color c)
        {
            var shader = Shader.Find(UrpLit);
            if (shader == null) shader = Shader.Find(FallbackShader);
            var m = new Material(shader);
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            return m;
        }

        private static GameObject MakePrimitive(string kind, string name, Transform parent,
            Vector3 localPos, Vector3 localScale, Color color)
        {
            PrimitiveType t = kind == "cylinder" ? PrimitiveType.Cylinder
                            : kind == "sphere" ? PrimitiveType.Sphere
                            : PrimitiveType.Cube;
            var go = GameObject.CreatePrimitive(t); // trae su collider
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            var rend = go.GetComponent<Renderer>();
            rend.sharedMaterial = MakeMaterial(color);
            return go;
        }

        /// <summary>
        /// Construye el objeto completo bajo `root`. Los colliders de componentes y
        /// fijaciones quedan en el layer de root (Default) para los raycasts de
        /// interacción.
        /// </summary>
        public static ObjectVisuals Build(ObjectBlueprint bp, Transform root)
        {
            var layout = DisassemblyLayouts.For(bp) ?? DisassemblyLayouts.Generic(bp);
            var vis = new ObjectVisuals
            {
                ComponentGOs = new GameObject[bp.Components.Length],
                FastenerGOs = new GameObject[bp.Components.Length][],
                ComponentRenderers = new Renderer[bp.Components.Length],
                FastenerRenderers = new Renderer[bp.Components.Length][],
                BaseColors = new Color[bp.Components.Length],
            };

            for (int ci = 0; ci < bp.Components.Length; ci++)
            {
                var def = bp.Components[ci];
                var lay = layout[ci];
                var go = MakePrimitive(lay.Primitive, "Comp_" + def.Id, root, lay.Pos, lay.Scale, lay.Color);
                go.AddComponent<ComponentRef>().Init(ci);
                vis.ComponentGOs[ci] = go;
                vis.ComponentRenderers[ci] = go.GetComponent<Renderer>();
                vis.BaseColors[ci] = lay.Color;

                int nf = def.Fasteners != null ? def.Fasteners.Length : 0;
                var fgo = new GameObject[nf];
                var frend = new Renderer[nf];
                for (int fi = 0; fi < nf; fi++)
                {
                    Vector3 off = fi < lay.FastenerOffsets.Length
                        ? lay.FastenerOffsets[fi]
                        : new Vector3(0f, 0.15f, 0f);
                    // Fijación = esfera pequeña en la superficie del componente (hija del
                    // componente: se oculta/mueve con él). Sobresale para que el raycast
                    // del jugador pueda apuntarle.
                    var f = MakePrimitive("sphere", "Fastener_" + ci + "_" + fi, go.transform,
                        off, new Vector3(0.05f, 0.05f, 0.05f), new Color(0.85f, 0.25f, 0.2f));
                    f.AddComponent<FastenerRef>().Init(ci, fi);
                    fgo[fi] = f;
                    frend[fi] = f.GetComponent<Renderer>();
                }
                vis.FastenerGOs[ci] = fgo;
                vis.FastenerRenderers[ci] = frend;
            }
            return vis;
        }
    }
}
