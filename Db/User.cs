using System.Collections.Generic;
using TaleWorlds.Diamond;
using TaleWorlds.Diamond.Rest;
using TaleWorlds.MountAndBlade.Diamond;

namespace LobbyServer.Db
{
    public class User
    {
        public SessionCredentials Id { get; set; }
 
        public Queue<RestResponseMessage> QueuedMessages { get; set; }
        public PlayerData PlayerData { get; set; }

        public GameServerEntry Server;
    }
}