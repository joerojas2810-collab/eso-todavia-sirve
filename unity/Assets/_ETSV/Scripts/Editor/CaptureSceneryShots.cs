// [ETSV] Instrucción 11 — capturas automatizadas reproducibles desde Unity.
// Solo Editor (carpeta especial "Editor" ⇒ Assembly-CSharp-Editor).
//
// Uso (requiere entorno gráfico virtual; ver unity/docs/MODELOS_3D.md §4):
//   xvfb-run -a Unity -batchmode -projectPath <repo>/unity \
//       -executeMethod Etsv.EditorTools.CaptureSceneryShots.CaptureAll \
//       -logFile -
//
// Genera Captures/*.png en la raíz del repo (fuera de Assets/ para no
// ensuciar el proyecto): patio general, mesa de desmontaje, microondas,
// zona de maquinaria (trituradora) y almacén (estantería + contenedores).
//
// NO se usa en el banco headless: este archivo solo compila dentro de Unity.

#if UNITY_EDITOR
using System.IO;
using Etsv.Domain;
using Etsv.Integration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Etsv.EditorTools
{
    public static class CaptureSceneryShots
    {
        private const int W = 1280, H = 720;

        public static void CaptureAll()
        {
            // 1) Escena real del juego (sol/niebla/Volume de la Instrucción 10).
            EditorSceneManager.OpenScene("Assets/Scenes/Multiplayer.unity", OpenSceneMode.Single);

            // 2) Construcción determinista del patio y las estaciones (misma vía
            //    que el bootstrap del juego; idempotente).
            ETSVScenery.Build();
            ScrapyardLayout.BuildStations();

            // 3) Microondas del catálogo sobre la mesa (vista del blueprint, los
            //    6 componentes con sus materiales por pieza).
            var anchor = ScrapyardLayout.TableAnchor();
            var mw = new GameObject("ETSV_Capture_Microondas");
            mw.transform.position = anchor + new Vector3(0f, 0.95f, 0f);
            BlueprintBuilder.Build(GameCatalog.Microondas, mw.transform);

            // 4) Cámara temporal (no toca la cámara de juego).
            var camGO = new GameObject("ETSV_CaptureCam");
            var cam = camGO.AddComponent<Camera>();
            var data = camGO.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true; // Volume global de la Instrucción 10
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 220f;
            cam.allowHDR = true;
            cam.allowMSAA = true;

            string outDir = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                "..", "Captures");
            Directory.CreateDirectory(outDir);

            Shot(cam, outDir, "01-patio-general",
                anchor + new Vector3(0.5f, 4.2f, 12.5f), Quaternion.Euler(14f, 180f, 0f));
            Shot(cam, outDir, "02-mesa-desmontaje",
                anchor + new Vector3(0f, 1.9f, 2.6f), Quaternion.Euler(12f, 180f, 0f));
            Shot(cam, outDir, "03-microondas",
                anchor + new Vector3(0.6f, 1.35f, 1.2f), Quaternion.Euler(8f, 200f, 0f));
            Shot(cam, outDir, "04-zona-maquinaria",
                ScrapyardLayout.StationPos(1) + new Vector3(0f, 1.6f, 2.4f),
                Quaternion.Euler(10f, 180f, 0f));
            Shot(cam, outDir, "05-almacen",
                ScrapyardLayout.StationPos(3) + new Vector3(0f, 1.7f, 2.6f),
                Quaternion.Euler(10f, 180f, 0f));

            Object.DestroyImmediate(camGO);
            Debug.Log("[ETSV] Capturas completadas en " + Path.GetFullPath(outDir));
        }

        private static void Shot(Camera cam, string outDir, string name,
            Vector3 pos, Quaternion rot)
        {
            cam.transform.SetPositionAndRotation(pos, rot);
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            rt.antiAliasing = 4;
            cam.targetTexture = rt;
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;
            File.WriteAllBytes(Path.Combine(outDir, name + ".png"), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(rt);
            Debug.Log("[ETSV] captura: " + name + ".png");
        }
    }
}
#endif
