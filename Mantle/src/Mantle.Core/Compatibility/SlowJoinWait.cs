// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// The queue rule matches ValheimMods Charter c059968f (FamilyRpc quiet wait and ceiling). The wording is Meadworks'.
using System;
using System.Globalization;

namespace Mantle.Compatibility
{
    /// <summary>
    /// How long a join waits for the other side's mod list. While the send queue is still above
    /// <see cref="BacklogBytes"/> the quiet wait has not started, so it restarts. The wait never runs past
    /// <see cref="CeilingSeconds"/> from the greeting. A peer that never answers is still given up.
    /// </summary>
    public static class SlowJoinWait
    {
        public const float QuietSeconds = 60f;
        public const float CeilingSeconds = 300f;
        /// <summary>The fixed Charter timeout this replaced. One warning is written once the wait passes it.</summary>
        public const float FormerTimeoutSeconds = 20f;
        public const int BacklogBytes = 64 * 1024;

        public readonly struct Step
        {
            public Step(float deadline, bool holding, bool giveUp, bool logSlow)
            {
                Deadline = deadline;
                Holding = holding;
                GiveUp = giveUp;
                LogSlow = logSlow;
            }

            public float Deadline { get; }
            /// <summary>The queue is still busy and the ceiling has not been reached: do not queue another greeting.</summary>
            public bool Holding { get; }
            public bool GiveUp { get; }
            public bool LogSlow { get; }
        }

        public static Step Advance(float greeted, float deadline, float now, int queuedBytes, bool slowLogged)
        {
            if (queuedBytes > BacklogBytes && now < greeted + CeilingSeconds)
            {
                float next = Math.Max(deadline, now + QuietSeconds);
                bool log = !slowLogged && now - greeted > FormerTimeoutSeconds;
                return new Step(next, true, false, log);
            }
            bool giveUp = now >= deadline || now >= greeted + CeilingSeconds;
            return new Step(deadline, false, giveUp, false);
        }

        /// <summary>The refusal lines for a player that never answered, one line per language, in the suite's language order.</summary>
        public static string[] NoAnswerLines(float seconds)
        {
            string n = seconds.ToString("0", CultureInfo.InvariantCulture);
            return new[]
            {
                "Your game did not answer this server's mod check in " + n + " seconds (mods missing, or a very slow connection).",
                "Tu juego no respondió a la comprobación de mods de este servidor en " + n + " segundos (faltan mods, o la conexión es muy lenta).",
                "Votre jeu n'a pas répondu à la vérification des mods de ce serveur en " + n + " secondes (mods manquants, ou connexion très lente).",
                "Dein Spiel hat die Mod-Prüfung dieses Servers innerhalb von " + n + " Sekunden nicht beantwortet (Mods fehlen, oder die Verbindung ist sehr langsam).",
                "Твоя игра не ответила на проверку модов этого сервера за " + n + " с (моды отсутствуют или соединение очень медленное).",
                "你的游戏未在 " + n + " 秒内回应此服务器的模组检查（缺少模组，或连接非常慢）。",
                "게임이 " + n + "초 안에 이 서버의 모드 확인에 응답하지 않았습니다(모드가 없거나 연결이 매우 느립니다)."
            };
        }

        public static string ServerRuns(string title, string version)
        {
            string name = title + " " + version;
            return "This server runs " + name + ". / Este servidor ejecuta " + name + ". / Ce serveur utilise " + name + ". / Dieser Server verwendet " + name + ". / Этот сервер использует " + name + ". / 此服务器运行 " + name + "。 / 이 서버는 " + name + "을(를) 실행합니다.";
        }
    }
}
