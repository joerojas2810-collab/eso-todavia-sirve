// [ETSV] Tests EditMode (NUnit) — verifican que el pipeline de testing del
// dominio puro funciona ANTES de portar las 73 aserciones del prototipo JS
// (prototype/tests/*.mjs). Cuando se porten las reglas, estos tests crecen
// con la paridad 1:1 como criterio de aceptación.

using NUnit.Framework;

namespace Etsv.Domain.Tests
{
    public class DomainSmokeTests
    {
        [Test]
        public void Title_IsConfigured()
        {
            Assert.AreEqual("¡ESO TODAVÍA SIRVE!", GameInfo.Title);
        }

        [Test]
        public void SaveVersion_MatchesPrototype()
        {
            // Paridad con prototype/js/state/gameState.js (VERSION = 3).
            Assert.AreEqual(3, GameInfo.SaveVersion);
        }

        [Test]
        public void MaxPlayers_IsSeven_PerGdd()
        {
            // GDD 1.0: sesiones cooperativas de 1 a 7 jugadores.
            Assert.AreEqual(7, GameInfo.MaxPlayers);
        }
    }
}
