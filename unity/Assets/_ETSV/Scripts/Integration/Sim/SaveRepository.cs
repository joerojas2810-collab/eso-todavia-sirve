// [ETSV] Capa Sim (C# puro, System.IO — sin UnityEngine) — ESCRITURA SEGURA
// del save (GDD 5.0 §16.3 pasos 4–7; Instrucción 08 §6):
//   4. escribe un archivo temporal,
//   5. verifica su integridad RE-LEYÉNDOLO (parseo + validación completa),
//   6. sustituye el guardado anterior (reemplazo atómico en lo posible),
//   7. conserva una copia de respaldo (.bak).
// Ante una partida inválida (DTO corrupto, JSON roto, integridad fallida) NO se
// sobrescribe la última copia válida: el temporal se descarta y el archivo
// existente (y su .bak) quedan intactos.
// La ruta concreta (Application.persistentDataPath u otra) la decide la capa
// Unity; en tests se usan rutas temporales del sistema.

using System.Collections.Generic;
using System.IO;
using Etsv.Domain;

namespace Etsv.Integration
{
    public class SaveWriteResult
    {
        public bool Ok;
        public List<string> Problems = new List<string>(); // por qué se rechazó la escritura
        public string Path;                                // ruta final efectiva
    }

    public class SaveLoadResult
    {
        public SaveGame Save;               // DTO v4 listo para RestoreInto (null = sin partida utilizable)
        public bool FromBackup;             // se recuperó desde la copia de respaldo
        public bool Migrated;               // venía en v3 y se migró explícitamente
        public List<string> Problems = new List<string>(); // problemas del archivo principal (diagnóstico)
        public string Path;                 // ruta de la que se cargó (o la principal si falló todo)
    }

    public static class SaveRepository
    {
        public static string BackupPath(string path) { return path + ".bak"; }
        private static string TempPath(string path) { return path + ".tmp"; }

        /// <summary>Guardado seguro completo. Acepta un SaveGame (se valida ANTES de
        /// escribir) o JSON crudo nunca: la única entrada es el DTO capturado por
        /// SessionPersistence.Capture.</summary>
        public static SaveWriteResult SafeWrite(string path, SaveGame save)
        {
            var res = new SaveWriteResult { Path = path };

            // Validación previa del DTO (nunca escribir una partida inválida).
            var migrated = SaveMigrations.MigrateToLatest(save);
            if (migrated == null)
            {
                res.Problems.Add(save == null ? "save_nulo" : "version_desconocida:" + save.Version);
                return res;
            }
            var problems = SaveValidator.ValidateFull(migrated);
            if (problems.Count > 0) { res.Problems.AddRange(problems); return res; }

            string json;
            try { json = SaveJsonAdapter.ToJson(migrated); }
            catch (System.Exception e) { res.Problems.Add("serializacion:" + e.Message); return res; }

            string tmp = TempPath(path);
            try
            {
                // 4. archivo temporal
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
                File.WriteAllText(tmp, json);

                // 5. verificación de integridad RE-LEYENDO el temporal
                var readBack = SaveJsonAdapter.FromJson(File.ReadAllText(tmp));
                if (readBack == null) { res.Problems.Add("relectura_json_fallida"); return res; }
                var recheck = SaveValidator.ValidateFull(SaveMigrations.MigrateToLatest(readBack));
                if (recheck.Count > 0) { res.Problems.AddRange(recheck); return res; }

                // 7. copia de respaldo del guardado anterior (si existe y es distinto)
                if (File.Exists(path))
                {
                    try { File.Copy(path, BackupPath(path), true); }
                    catch (System.Exception) { /* sin respaldo posible: no bloquea el guardado */ }
                }

                // 6. sustitución
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
                res.Ok = true;
                return res;
            }
            catch (System.Exception e)
            {
                res.Problems.Add("io:" + e.Message);
                return res;
            }
            finally
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch (System.Exception) { }
            }
        }

        /// <summary>Carga tolerante: prueba el archivo principal y, si está corrupto o
        /// es inválido, la copia de respaldo (GDD §16.3 paso 7 / Instrucción 08 §6:
        /// "recuperar una copia de respaldo"). Devuelve SIEMPRE un DTO ya migrado a
        /// v4 y validado (Save == null si ninguna fuente es utilizable).</summary>
        public static SaveLoadResult Load(string path)
        {
            var res = new SaveLoadResult { Path = path };
            TryReadOne(path, res, false);
            if (res.Save != null) return res;

            string bak = BackupPath(path);
            if (File.Exists(bak))
            {
                var firstProblems = new List<string>(res.Problems);
                TryReadOne(bak, res, true);
                if (res.Save != null)
                {
                    res.Problems = firstProblems; // diagnóstico del principal
                    res.Path = bak;
                    return res;
                }
                res.Problems = firstProblems;
                res.Path = path;
            }
            return res;
        }

        private static void TryReadOne(string file, SaveLoadResult res, bool backup)
        {
            res.Save = null;
            res.FromBackup = backup;
            res.Migrated = false;
            if (!File.Exists(file)) { res.Problems.Add("archivo_inexistente:" + file); return; }
            try
            {
                var dto = SaveJsonAdapter.FromJson(File.ReadAllText(file));
                if (dto == null) { res.Problems.Add("json_invalido:" + file); return; }
                if (dto.Version != GameInfo.SaveVersionFull)
                {
                    var mig = SaveMigrations.MigrateToLatest(dto);
                    if (mig == null) { res.Problems.Add("version_desconocida:" + dto.Version); return; }
                    dto = mig;
                    res.Migrated = true;
                }
                var problems = SaveValidator.ValidateFull(dto);
                if (problems.Count > 0) { res.Problems.AddRange(problems); return; }
                res.Save = dto;
            }
            catch (System.Exception e)
            {
                res.Problems.Add("io:" + e.Message);
            }
        }
    }
}
