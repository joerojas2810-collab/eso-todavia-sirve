// [ETSV] Instrucción 11 — capa de modelos 3D reutilizables (ETSVModels) y
// robustez headless de la escenografía/estaciones.
//
// En el banco (stubs, sin Resources reales) los modelos NO están disponibles:
// estas pruebas fijan el CONTRATO de fallback — nada del juego depende de que
// el arte exista: Load→null, TryCreate→null, y Build()/BuildStations() deben
// completarse por la vía primitiva sin excepciones. La validación visual con
// modelos reales es exclusiva del Unity Editor (ver unity/docs/MODELOS_3D.md).

using Etsv.Integration;
using NUnit.Framework;
using UnityEngine;

namespace Etsv.Domain.Tests
{
    [TestFixture]
    public class ModelsSceneryTests
    {
        [SetUp]
        public void LimpiarCache()
        {
            ETSVModels.Reset();
            ETSVArt.Reset();
        }

        [Test]
        public void M01_Load_SinRecursos_DevuelveNullYNuloSeCachea()
        {
            Assert.IsNull(ETSVModels.Load(ETSVModels.KenneyCity, "shipping-container-a"),
                "Headless no hay Resources: Load debe devolver null (fallback primitivo).");
            // Segunda llamada: cacheada (null también), sin excepción.
            Assert.IsNull(ETSVModels.Load(ETSVModels.KenneyCity, "shipping-container-a"));
        }

        [Test]
        public void M02_TryCreate_SinModelo_DevuelveNull()
        {
            var root = new GameObject("raiz");
            var go = ETSVModels.TryCreate(ETSVModels.Quaternius, "Prop_Barrel_Large",
                "barril", root.transform, Vector3.zero, 1f, 0f, ETSVArt.Tinted(ETSVArt.MetalPainted, Color.white));
            Assert.IsNull(go, "Sin FBX importado el llamador debe recibir null y usar su primitivo.");
            Assert.AreEqual(0, root.transform.childCount, "TryCreate fallido no debe dejar hijos huérfanos.");
        }

        [Test]
        public void M03_EscalaCityKit_SieteMetros()
        {
            // Medida sobre los OBJ del pack: shipping-container 0.823 u → 5.76 m reales.
            Assert.AreEqual(7f, ETSVModels.KenneyCityScale, 1e-4f);
        }

        [Test]
        public void M04_Scenery_Build_Headless_SinExcepcion_FallbackPrimitivo()
        {
            Assert.DoesNotThrow(() => ETSVScenery.Build());
        }

        [Test]
        public void M05_Estaciones_BuildStations_Headless_SinExcepcion()
        {
            Assert.DoesNotThrow(() => ScrapyardLayout.BuildStations());
        }

        [Test]
        public void M06_Reset_VaciaCache_SinEfectosLaterales()
        {
            ETSVModels.Load(ETSVModels.KenneyFurniture, "bookcaseOpen");
            Assert.DoesNotThrow(() => ETSVModels.Reset());
            Assert.IsNull(ETSVModels.Load(ETSVModels.KenneyFurniture, "bookcaseOpen"));
        }
    }
}
