using System.Collections.Generic;
using TaleWorlds.MountAndBlade.Diamond;

namespace LobbyServer.Db
{
    public class ApiContext 
    {
        
        public List<User> Users { get; set; } = new List<User>();
        public ServerStatus Status;
    }
}