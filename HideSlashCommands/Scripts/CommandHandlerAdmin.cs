using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PlayerStorage = DMChatTeleport.PlayerDataStore.PlayerStorage;

namespace DMChatTeleport
{
    public static class CommandHandlerAdmin
    {
        public static bool TryHandle(string callerPlayerId, int callerEntityId, string cmd)
        {
            if (string.IsNullOrWhiteSpace(cmd))
                return false;

            string raw = cmd.Trim();
            if (raw.StartsWith("/"))
                raw = raw.Substring(1);

            string[] parts = raw.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return false;

            bool isConsole = callerEntityId <= 0 || string.IsNullOrWhiteSpace(callerPlayerId);
            string lang = isConsole ? "en" : PlayerStorage.GetLanguage(callerPlayerId, "en");

            if (!isConsole && !IsAdmin(callerEntityId))
                return false;

            string verb = parts[0];

            if (verb.Equals("reloadconfig", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    // Reload config.json
                    ConfigManager.Load();

                    // Reload StarterKitConfig.json
                    StarterKitManager.Load();

                    if (!isConsole)
                        lang = PlayerStorage.GetLanguage(callerPlayerId, "en");

                    Reply(isConsole, callerEntityId, lang, L.Get(lang, "cmd.reloadconfig.ok"));
                }
                catch (Exception ex)
                {
                    Debug.LogError("[DMChatTeleport] reloadconfig failed: " + ex);

                    if (!isConsole)
                        Reply(false, callerEntityId, lang, "[DMChatTeleport] Reload failed. Check server logs.");
                    else
                        Debug.Log("[DMChatTeleport] Reload failed. Check logs.");
                }

                return true;
            }

            if (verb.Equals("addrp", StringComparison.OrdinalIgnoreCase))
            {
                HandleAddRp(isConsole, callerEntityId, lang, parts);
                return true;
            }

            if (verb.Equals("setrp", StringComparison.OrdinalIgnoreCase))
            {
                HandleSetRp(isConsole, callerEntityId, lang, parts);
                return true;
            }

            if (verb.Equals("getrp", StringComparison.OrdinalIgnoreCase) || verb.Equals("rpof", StringComparison.OrdinalIgnoreCase))
            {
                HandleRpOf(isConsole, callerEntityId, lang, parts);
                return true;
            }

            if (verb.Equals("dmplayers", StringComparison.OrdinalIgnoreCase) ||
                verb.Equals("dmlistplayers", StringComparison.OrdinalIgnoreCase))
            {
                HandleListPlayers(isConsole, callerEntityId, lang);
                return true;
            }

            return false;
        }

        private static void HandleAddRp(bool isConsole, int callerEntityId, string lang, string[] parts)
        {
            if (parts.Length < 3)
            {
                Reply(isConsole, callerEntityId, lang, L.Get(lang, "admin.addrp.usage"));
                return;
            }

            string targetText = parts[1];

            if (!int.TryParse(parts[2], out int amount))
            {
                Reply(isConsole, callerEntityId, lang, L.Get(lang, "admin.amount.invalid"));
                return;
            }

            if (!TryResolveTarget(targetText, out var target))
            {
                Reply(isConsole, callerEntityId, lang, L.Format(lang, "admin.target.not_found", ("target", targetText)));
                return;
            }

            bool created;
            var pd = PlayerStorage.GetOrCreate(target.PlayerId, out created);

            int before = Math.Max(0, pd.RewardPoints);
            int after = Math.Max(0, before + amount);

            if (after < before)
            {
                int spent = before - after;
                if (spent > 0)
                    pd.TotalRewardPointsSpent += spent;
            }

            pd.RewardPoints = after;
            PlayerStorage.Save();

            Reply(isConsole, callerEntityId, lang, L.Format(lang, "admin.rp.updated",
                ("name", target.DisplayName),
                ("amount", amount),
                ("wallet", after)
            ));

            if (target.EntityId > 0)
            {
                string targetLang = PlayerStorage.GetLanguage(target.PlayerId, "en");
                SayPlayer(target.EntityId, L.Format(targetLang, "admin.rp.updated_self",
                    ("amount", amount),
                    ("wallet", after)
                ));
            }
        }

        private static void HandleSetRp(bool isConsole, int callerEntityId, string lang, string[] parts)
        {
            if (parts.Length < 3)
            {
                Reply(isConsole, callerEntityId, lang, L.Get(lang, "admin.setrp.usage"));
                return;
            }

            string targetText = parts[1];

            if (!int.TryParse(parts[2], out int amount))
            {
                Reply(isConsole, callerEntityId, lang, L.Get(lang, "admin.amount.invalid"));
                return;
            }

            amount = Math.Max(0, amount);

            if (!TryResolveTarget(targetText, out var target))
            {
                Reply(isConsole, callerEntityId, lang, L.Format(lang, "admin.target.not_found", ("target", targetText)));
                return;
            }

            bool created;
            var pd = PlayerStorage.GetOrCreate(target.PlayerId, out created);
            pd.RewardPoints = amount;
            PlayerStorage.Save();

            Reply(isConsole, callerEntityId, lang, L.Format(lang, "admin.rp.set",
                ("name", target.DisplayName),
                ("wallet", amount)
            ));

            if (target.EntityId > 0)
            {
                string targetLang = PlayerStorage.GetLanguage(target.PlayerId, "en");
                SayPlayer(target.EntityId, L.Format(targetLang, "admin.rp.set_self",
                    ("wallet", amount)
                ));
            }
        }

        private static void HandleRpOf(bool isConsole, int callerEntityId, string lang, string[] parts)
        {
            if (parts.Length < 2)
            {
                Reply(isConsole, callerEntityId, lang, L.Get(lang, "admin.rpof.usage"));
                return;
            }

            string targetText = parts[1];

            if (!TryResolveTarget(targetText, out var target))
            {
                Reply(isConsole, callerEntityId, lang, L.Format(lang, "admin.target.not_found", ("target", targetText)));
                return;
            }

            int rp = PlayerStorage.GetRP(target.PlayerId);
            Reply(isConsole, callerEntityId, lang, L.Format(lang, "admin.rp.of",
                ("name", target.DisplayName),
                ("wallet", rp)
            ));
        }

        private static void HandleListPlayers(bool isConsole, int callerEntityId, string lang)
        {
            var clients = ConnectionManager.Instance?.Clients?.List;
            if (clients == null || clients.Count == 0)
            {
                Reply(isConsole, callerEntityId, lang, L.Get(lang, "admin.players.none"));
                return;
            }

            var names = new List<string>();
            foreach (var c in clients)
            {
                if (c == null) continue;
                string name = !string.IsNullOrWhiteSpace(c.playerName) ? c.playerName : $"Entity:{c.entityId}";
                names.Add(name);
            }

            Reply(isConsole, callerEntityId, lang, L.Format(lang, "admin.players.list", ("list", string.Join(", ", names))));
        }

        private static bool IsAdmin(int entityId)
        {
            try
            {
                var ci = ConnectionManager.Instance?.Clients?.ForEntityId(entityId);
                if (ci == null) return false;
                int perm = GameManager.Instance.adminTools.Users.GetUserPermissionLevel(ci);
                return perm == 0;
            }
            catch
            {
                return false;
            }
        }

        private struct TargetInfo
        {
            public string PlayerId;
            public int EntityId;
            public string DisplayName;
        }

        private static bool TryResolveTarget(string text, out TargetInfo target)
        {
            target = default;

            if (string.IsNullOrWhiteSpace(text))
                return false;

            text = text.Trim();

            if (int.TryParse(text, out int entityId) && entityId > 0)
            {
                var ci = ConnectionManager.Instance?.Clients?.ForEntityId(entityId);
                if (ci != null)
                {
                    string pid = PlayerIdUtil.GetPersistentIdOrNull(ci);
                    if (!string.IsNullOrWhiteSpace(pid))
                    {
                        target = new TargetInfo
                        {
                            PlayerId = pid,
                            EntityId = entityId,
                            DisplayName = !string.IsNullOrWhiteSpace(ci.playerName) ? ci.playerName : pid
                        };
                        return true;
                    }
                }
            }

            var clients = ConnectionManager.Instance?.Clients?.List;
            if (clients != null && clients.Count > 0)
            {
                var exact = clients.FirstOrDefault(c => c != null &&
                    !string.IsNullOrWhiteSpace(c.playerName) &&
                    c.playerName.Equals(text, StringComparison.OrdinalIgnoreCase));

                if (exact != null)
                {
                    string pid = PlayerIdUtil.GetPersistentIdOrNull(exact);
                    if (!string.IsNullOrWhiteSpace(pid))
                    {
                        target = new TargetInfo
                        {
                            PlayerId = pid,
                            EntityId = exact.entityId,
                            DisplayName = exact.playerName
                        };
                        return true;
                    }
                }

                var partialMatches = clients
                    .Where(c => c != null && !string.IsNullOrWhiteSpace(c.playerName) &&
                                c.playerName.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();

                if (partialMatches.Count == 1)
                {
                    var c = partialMatches[0];
                    string pid = PlayerIdUtil.GetPersistentIdOrNull(c);
                    if (!string.IsNullOrWhiteSpace(pid))
                    {
                        target = new TargetInfo
                        {
                            PlayerId = pid,
                            EntityId = c.entityId,
                            DisplayName = c.playerName
                        };
                        return true;
                    }
                }
            }

            try
            {
                var pd = PlayerStorage.Get(text);
                if (pd != null)
                {
                    target = new TargetInfo
                    {
                        PlayerId = text,
                        EntityId = pd.entityId,
                        DisplayName = text
                    };
                    return true;
                }
            }
            catch { }

            return false;
        }

        private static void Reply(bool isConsole, int callerEntityId, string lang, string msg)
        {
            if (string.IsNullOrWhiteSpace(msg))
                return;

            if (isConsole)
            {
                //Debug.Log("[DMChatTeleport] " + msg);  
                SdtdConsole.Instance.Output("[DMChatTeleport] " + msg);
                return;
            }

            SayPlayer(callerEntityId, msg);
        }

        private static void SayPlayer(int entityId, string msg)
        {
            if (entityId <= 0 || string.IsNullOrWhiteSpace(msg))
                return;

            msg = msg.Replace("\"", "\\\"");
            SdtdConsole.Instance.ExecuteSync($"sayplayer {entityId} \"{msg}\"", null);
        }
    }
}