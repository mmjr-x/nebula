﻿#region

using System;
using System.Collections.Generic;
using System.Linq;
using NebulaModel.DataStructures.Chat;
using NebulaModel.Logger;
using NebulaModel.Packets.Chat;
using NebulaWorld.MonoBehaviours.Local.Chat;

#endregion

namespace NebulaWorld.Chat.Commands;

public class GiftCommandHandler : IChatCommandHandler
{
    private struct UserInfo
    {
        public ushort id;
        public string name;
    }

    public void Execute(ChatWindow window, string[] parameters)
    {
        if (parameters.Length < 3)
        {
            throw new ChatCommandUsageException("Not enough arguments!".Translate());
        }

        UserInfo sender;
        {
            if (
                Multiplayer.Session?.LocalPlayer?.Data?.PlayerId is ushort senderUserId
                && Multiplayer.Session?.LocalPlayer?.Data?.Username is string senderUsername
            )
            {
                sender = new UserInfo
                {
                    id = senderUserId,
                    name = senderUsername
                };
            }
            else
            {
                window.SendLocalChatMessage("Invalid sender (not connected), can't send gift".Translate(), ChatMessageType.CommandErrorMessage);
                return;
            };
        }

        UserInfo recipient;
        {
            var userIdOrNameParameter = parameters[0];
            var couldParseUserId = ushort.TryParse(userIdOrNameParameter, out var recipientUserId);

            UserInfo? recipientOrNull = null;
            if (couldParseUserId)
            {
                recipientOrNull = getUserInfoById(recipientUserId);
            }

            if (recipientOrNull is UserInfo recipientNotNull)
            {
                recipient = recipientNotNull;
            }
            else
            {
                var recipientsByUsername = getUserInfosByUsername(userIdOrNameParameter);

                if (recipientsByUsername.Count == 0)
                {
                    window.SendLocalChatMessage("Invalid recipient (user id or username not found), can't send gift".Translate(), ChatMessageType.CommandErrorMessage);
                    return;
                }

                if (recipientsByUsername.Count > 1)
                {
                    window.SendLocalChatMessage("Ambiguous recipient (multiple recipients with same username), can't send gift".Translate(), ChatMessageType.CommandErrorMessage);
                    return;
                }

                recipient = recipientsByUsername.First();
            }
        }

        if (sender.id == recipient.id)
        {
            window.SendLocalChatMessage("Invalid recipient (self), can't send gift".Translate(), ChatMessageType.CommandErrorMessage);
            return;
        }

        ChatCommandGiftType type;
        switch (parameters[1])
        {
            case "soil":
            case "sand":
            case "s":
                type = ChatCommandGiftType.Soil;
                break;
            // TODO: Implement Item and Energy variants.
            default:
                window.SendLocalChatMessage("Invalid gift type, can't send gift".Translate(), ChatMessageType.CommandErrorMessage);
                return;
        }

        // Add support for scientific notation and other notation types
        if (!long.TryParse(parameters[2], out var quantity) || quantity == 0)
        {
            window.SendLocalChatMessage("Invalid gift quantity, can't send gift".Translate(), ChatMessageType.CommandErrorMessage);
            return;
        }

        Action<ChatCommandGiftPacket> sendPacket;
        if (Multiplayer.Session.LocalPlayer.IsHost)
        {
            // If you are the host, you can directly send the packet to the recipient
            var recipientConnection = Multiplayer.Session.Network.PlayerManager.GetPlayerById(recipient.id);
            if (recipientConnection == null)
            {
                window.SendLocalChatMessage("Invalid recipient (no connection), can't send gift".Translate(), ChatMessageType.CommandErrorMessage);
                return;
            }

            sendPacket = (packet) =>
            {
                recipientConnection.SendPacket(packet);
            };
        }
        else
        {
            // Else send it to the host who can relay it
            sendPacket = (packet) =>
            {
                Multiplayer.Session.Network.SendPacket(packet);
            };
        }

        // Validate that you actually have the required soil/items/energy to gift
        switch (type)
        {
            case ChatCommandGiftType.Soil:
                var packet = new ChatCommandGiftPacket(sender.id, recipient.id, type, quantity);
                var mainPlayer = GameMain.data.mainPlayer;
                bool sufficient;
                lock (mainPlayer)
                {
                    var remainingSand = mainPlayer.sandCount - quantity;
                    sufficient = remainingSand >= 0;
                    if (sufficient)
                    {
                        sendPacket(packet);
                        mainPlayer.SetSandCount(remainingSand);
                        // TODO: Do we need to do something with soil sync?
                    }
                }

                if (!sufficient)
                {
                    window.SendLocalChatMessage("You dont have enough soil to send, can't send gift".Translate(), ChatMessageType.CommandErrorMessage);
                    return;
                }
                // TODO: I don't think this is translatable since it contains dynamic data, look into this
                window.SendLocalChatMessage($"[{DateTime.Now:HH:mm}] You gifted [{recipient.id}] {recipient.name} soil ({quantity})".Translate(), ChatMessageType.SystemInfoMessage);
                break;
                // TODO: Implement Item and Energy variants.
        }
    }

    public string GetDescription()
    {
        return string.Format("Send gift to player. Use /who for valid user names. Valid types are soil (s), item (i), energy (e)".Translate());
    }

    public string[] GetUsage()
    {
        return ["<player name|id> <soil|item|energy> <quantity>"];
    }

    // TODO: We should add logic here that acctually adds the gifted materials (and devise something to substract the materials)
    //public static void SendWhisperToLocalPlayer(string sender, string mesageBody)
    //{
    //    ChatManager.Instance.SendChatMessage($"[{DateTime.Now:HH:mm}] [{sender} whispered] : {mesageBody}",
    //        ChatMessageType.PlayerMessagePrivate);
    //}

    private UserInfo? getUserInfoById(ushort userId)
    {
        using (Multiplayer.Session.World.GetRemotePlayersModels(out var remotePlayersModels))
        {
            // TODO: This does not include self, perhaps we should include this
            var result = remotePlayersModels
                .Select(remotePlayerModel => remotePlayerModel.Value.Movement)
                .Where(movement => movement.PlayerID == userId)
                .Select(movement => new UserInfo
                {
                    id = movement.PlayerID,
                    name = movement.Username
                });

            return result.Any() ? result.First() : null;
        }
    }

    private List<UserInfo> getUserInfosByUsername(string username)
    {
        using (Multiplayer.Session.World.GetRemotePlayersModels(out var remotePlayersModels))
        {
            // TODO: This does not include self, perhaps we should include this
            return remotePlayersModels
                .Select(remotePlayerModel => remotePlayerModel.Value.Movement)
                .Where(movement => movement.Username == username)
                .Select(movement => new UserInfo
                {
                    id = movement.PlayerID,
                    name = movement.Username
                })
                .ToList();
        }
    }
}