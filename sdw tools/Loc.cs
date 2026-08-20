using System;
using System.Collections.Generic;
using System.IO;

namespace SdwEditor
{
    internal static class Loc
    {
        public const int Ru = 0;
        public const int En = 1;

        public static int Id { get; private set; }
        public static event EventHandler Changed;

        private static readonly Dictionary<string, string[]> Map = Build();

        public static void Load()
        {
            try
            {
                string p = PathFile();
                if (File.Exists(p))
                {
                    string s = File.ReadAllText(p).Trim().ToLowerInvariant();
                    if (s == "en" || s == "en-us" || s == "english") Id = En;
                    else Id = Ru;
                    return;
                }
            }
            catch
            {
            }
            Id = Ru;
        }

        public static void Set(int id)
        {
            if (id != Ru && id != En) id = Ru;
            if (id == Id) return;
            Id = id;
            try
            {
                string dir = Path.GetDirectoryName(PathFile());
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(PathFile(), id == En ? "en" : "ru");
            }
            catch
            {
            }
            EventHandler h = Changed;
            if (h != null) h(null, EventArgs.Empty);
        }

        public static string T(string key)
        {
            string[] row;
            if (Map.TryGetValue(key, out row) && row != null && row.Length > Id)
            {
                return row[Id];
            }
            return key;
        }

        public static string F(string key, params object[] args)
        {
            return string.Format(T(key), args);
        }

        private static string PathFile()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SdwEditor", "ui-lang.txt");
        }

        private static Dictionary<string, string[]> Build()
        {
            Dictionary<string, string[]> d = new Dictionary<string, string[]>(StringComparer.Ordinal);
            Add(d, "app_title", "SDW Workshop", "SDW Workshop");
            Add(d, "ui_language", "Язык", "Language");
            Add(d, "open_folder", "Папка уровней", "Levels folder");
            Add(d, "status_pick", "  Открой папку Levels и выбери уровень", "  Open a Levels folder and pick a level");
            Add(d, "levels_header", "  УРОВНИ", "  LEVELS");
            Add(d, "tab_dav", "DAV  текстуры", "DAV  textures");
            Add(d, "tab_war", "WAR  модели", "WAR  models");
            Add(d, "tab_mlt", "MLT  тексты", "MLT  text");
            Add(d, "tab_snd", "SND  звуки", "SND  sounds");
            Add(d, "folder_levels", "Папка Levels", "Levels folder");
            Add(d, "folder_missing", "  Папка не найдена: {0}", "  Folder not found: {0}");
            Add(d, "levels_in", "  {0} уровней в {1}", "  {0} levels in {1}");
            Add(d, "langs_short", "{0} яз.", "{0} langs");
            Add(d, "status_loaded", "  {0}  ·  DAV {1} tex  ·  WAR {2} res  ·  MLT {3}  ·  SND {4}", "  {0}  ·  DAV {1} tex  ·  WAR {2} res  ·  MLT {3}  ·  SND {4}");
            Add(d, "open_level_fail", "Не удалось открыть уровень", "Could not open level");

            Add(d, "export_png", "Экспорт PNG", "Export PNG");
            Add(d, "col_texture", "Текстура", "Texture");
            Add(d, "grp_pages", "Страницы-атласы", "Atlas pages");
            Add(d, "grp_textures", "Текстуры", "Textures");
            Add(d, "atlas_info", "  Атлас {0} · {1}×{2}", "  Atlas {0} · {1}×{2}");
            Add(d, "no_image", "Нет изображения", "No image");
            Add(d, "err_dav", "Это не DAV (нужна магия VDX7).", "Not a DAV file (VDX7 magic required).");

            Add(d, "war_geom", "Геометрия", "Geometry");
            Add(d, "war_actors", "Актёры / скелеты", "Actors / skeletons");
            Add(d, "war_scene", "Сценарий", "Scenario");
            Add(d, "war_sky", "Скайбокс", "Skybox");
            Add(d, "war_coll", "Коллизии", "Collision");
            Add(d, "war_other", "Прочее", "Other");
            Add(d, "war_all", "Весь уровень", "Whole level");
            Add(d, "level_hint", "уровень · объекты {0} · небо {1}", "level · objects {0} · sky {1}");
            Add(d, "level_props", "Объединённый меш уровня\r\nСтатичных треугольников: {0}\r\nОбъекты/персонажи: {1}\r\nАнимированные актёры: {2}\r\nСкайбокс: {3}\r\nТуман: {4}", "Combined level mesh\r\nStatic triangles: {0}\r\nObjects/characters: {1}\r\nAnimated actors: {2}\r\nSkybox: {3}\r\nFog: {4}");
            Add(d, "res_props", "Индекс: {0}\r\nТип: {1}\r\nFlags: 0x{2:X2}\r\nУказатель: 0x{3:X}\r\nВершин: {4}\r\nЧанков: {5}\r\nКостей: {6}\r\nТреугольников: {7}\r\nКлипов: {8}\r\nАнимации: {9}\r\nClassID: {10} ({11})\r\nМодель: {12}\r\nПозиция: {13:0.00}, {14:0.00}, {15:0.00}\r\nПоворот: pitch {16}  yaw {17}  roll {18}  (1024 = 90°)\r\nВерсия WAR: {19}", "Index: {0}\r\nType: {1}\r\nFlags: 0x{2:X2}\r\nPointer: 0x{3:X}\r\nVertices: {4}\r\nChunks: {5}\r\nBones: {6}\r\nTriangles: {7}\r\nClips: {8}\r\nAnimations: {9}\r\nClassID: {10} ({11})\r\nModel: {12}\r\nPosition: {13:0.00}, {14:0.00}, {15:0.00}\r\nRotation: pitch {16}  yaw {17}  roll {18}  (1024 = 90°)\r\nWAR version: {19}");
            Add(d, "label_skel", "#{0}  скелет  v{1}  b{2}  a{3}", "#{0}  skeleton  v{1}  b{2}  a{3}");
            Add(d, "label_actor", "актёр", "actor");
            Add(d, "label_geom", "геометрия", "geometry");
            Add(d, "label_mesh", "#{0}  {1}  v{2}", "#{0}  {1}  v{2}");
            Add(d, "label_sky", "#{0}  скайбокс  v{1}", "#{0}  skybox  v{1}");
            Add(d, "label_collmap", "#{0}  карта коллизий", "#{0}  collision map");
            Add(d, "label_collpoly", "#{0}  полигоны коллизий", "#{0}  collision polys");
            Add(d, "err_war_short", "WAR слишком короткий.", "WAR file is too short.");

            Add(d, "pick_object", "Выбери объект слева", "Select an object on the left");
            Add(d, "stats", "статы", "stats");
            Add(d, "hud_help", "стрелки — ходить   СКМ — смотреть   колесо — вперёд", "arrows — walk   MMB — look   wheel — forward");
            Add(d, "tex_stats", "   |   с tex {0}/{1}", "   |   tex {0}/{1}");
            Add(d, "anim_level", "{0}   ·   {1} актёров  {2}    пробел — пауза", "{0}   ·   {1} actors  {2}    space — pause");
            Add(d, "paused", "пауза", "paused");
            Add(d, "playing", "играет", "playing");
            Add(d, "anim_clip", "{0}   ·   {1}  [{2}/{3}]  {4}    пробел — пауза   N/P — клип", "{0}   ·   {1}  [{2}/{3}]  {4}    space — pause   N/P — clip");
            Add(d, "no_geom", "Нет геометрии", "No geometry");
            Add(d, "stat_frame", "кадр  ", "frame ");
            Add(d, "stat_actors", "актёры ", "actors ");
            Add(d, "stat_groups", "групп ", "groups ");

            Add(d, "mlt_lang", "Язык", "Language");
            Add(d, "mlt_section", "Секция", "Section");
            Add(d, "mlt_save", "Сохранить MLT", "Save MLT");
            Add(d, "mlt_col", "Строка", "String");
            Add(d, "mlt_saved", "MLT сохранён.", "MLT saved.");
            Add(d, "mlt_save_err", "Ошибка сохранения", "Save error");

            Add(d, "col_sound", "Звук", "Sound");
            Add(d, "play", "Играть", "Play");
            Add(d, "stop", "Стоп", "Stop");
            Add(d, "export_wav", "Экспорт WAV", "Export WAV");
            Add(d, "export_all", "Экспорт все", "Export all");
            Add(d, "replace_wav", "Заменить WAV", "Replace WAV");
            Add(d, "snd_item", "#{0:000}  id {1}  {2:N0} байт  flags {3}", "#{0:000}  id {1}  {2:N0} bytes  flags {3}");
            Add(d, "snd_info", "  RIFF WAV · id {0} · двойной клик — играть", "  RIFF WAV · id {0} · double-click to play");
            Add(d, "snd_replaced", "Звук заменён и SND сохранён.", "Sound replaced and SND saved.");
            Add(d, "error", "Ошибка", "Error");
            Add(d, "no_wave", "Нет волны", "No waveform");
            Add(d, "err_wav", "Нужен обычный WAV (RIFF).", "Need a standard RIFF WAV.");
            return d;
        }

        private static void Add(Dictionary<string, string[]> d, string key, string ru, string en)
        {
            d[key] = new string[] { ru, en };
        }
    }
}
