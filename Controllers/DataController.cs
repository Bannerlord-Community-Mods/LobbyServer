using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LobbyServer.Db;
using Messages.FromClient.ToLobbyServer;
using Messages.FromLobbyServer.ToClient;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using TaleWorlds.Diamond;
using TaleWorlds.Diamond.Rest;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade.Diamond;
using JsonConverter = Newtonsoft.Json.JsonConverter;

namespace LobbyServer.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class DataController : Controller
    {
       

        private readonly ILogger<DataController> _logger;

        public DataController(ILogger<DataController> logger, ApiContext context)
        {
            _logger = logger;
            _context = context;
            _context.Status = new ServerStatus(false, true, true, false,
                new TextObject("Welcome to MisterOutofTimes Master Server"));
        }


        private RestDataJsonConverter _restDataJsonConverter = new RestDataJsonConverter();
        private ApiContext _context;

        [HttpPost("ProcessMessage")]
        public async Task<JsonResult> ProcessMessage()
        {
            RestResponse response = new RestResponse();
            byte[] byteJson = await Request.GetRawBodyBytesAsync();
            string json = Encoding.Unicode.GetString(byteJson);
            RestRequestMessage request =
                JsonConvert.DeserializeObject<RestRequestMessage>(json,
                    new JsonConverter[] {this._restDataJsonConverter});
            
            
            Console.WriteLine("NEW MESSAGE");
            response.UserCertificate = Guid.NewGuid().ToByteArray();
            switch (request)
            {
                case RestDataRequestMessage message:
                    Console.WriteLine($"NEW MESSAGE OF TYPE:${message.MessageType}");
                    switch (message.MessageType)
                    {
                        case MessageType.Login:
                            Console.Write("New Login");
                            HandleLogins(message, ref response,request);

                            break;
                        case MessageType.Message:
                            Console.Write("New Message Message");
                            HandleMessage(message, ref response);

                            break;
                        case MessageType.Function:
                            HandleFunctions(message, ref response);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                    if (message.SessionCredentials!=null )
                    {
                        
                        response.UserCertificate = message.SessionCredentials.SessionKey.ToByteArray();
                        var user = _context.Users.Find(user => user.Id.SessionKey == message.SessionCredentials.SessionKey);
                        if (user != null)
                        {
                            RestResponseMessage result;
                            var hasMessage = user.QueuedMessages.TryDequeue(out result);
                            if(hasMessage) {
                    
                                response.EnqueueMessage(result);
                            }
                        }
                    }

                    break;
                case AliveMessage message:
                    if (message.SessionCredentials != null)
                    {

                        response.UserCertificate = message.SessionCredentials.SessionKey.ToByteArray();
                    }

                    Console.WriteLine($"NEW MESSAGE OF TYPE:${message.GetType().Name}");
                    response.SetSuccessful(true, "Alive");
                    if (message.SessionCredentials!=null )
                    {
                        var user = _context.Users.Find(user => user.Id.SessionKey == message.SessionCredentials.SessionKey);
                        if (user != null)
                        {
                            RestResponseMessage result;
                            var hasMessage = user.QueuedMessages.TryDequeue(out result);
                            if(hasMessage) {
                    
                                response.EnqueueMessage(result);
                            }
                        }
                    }
                    break;
                case ConnectMessage message:
                    
                    Console.WriteLine($"NEW MESSAGE OF TYPE:${message.GetType().Name}");
                    response.SetSuccessful(true, "Alive");
                    break;
                default:
                    Console.WriteLine($"Unhandled Type: ${request.GetType().Name}");
                    break;
            }

            
            
            //  response.FunctionResult = new RestObjectFunctionResult();new ServerStatus(new ServerStatus(true,true,true,true,new TextObject("hello world")));
            var settings = new JsonSerializerSettings
            {
                Converters = new JsonConverter[]
                {
                    this._restDataJsonConverter
                },
                ContractResolver = new DefaultContractResolver {NamingStrategy = new CamelCaseNamingStrategy()}
            };
            return new JsonResult(response, settings);
        }


        private void HandleFunctions(RestDataRequestMessage message, ref RestResponse response)
        {
            var rawMsg = message.GetMessage();
            Console.WriteLine($"Handle ${rawMsg.GetType().Name}");

            switch (rawMsg)
            {
                case GetAnotherPlayerStateMessage msg:
                    HandleGetAnotherPlayerStateMessage(msg,ref response, message);
                    break;
                case GetPlayerBadgesMessage msg:
                    HandleGetPlayerBadgesMessage(msg, ref response);

                    break;
                case RequestCustomGameServerListMessage msg:
                    HandleRequestCustomGameServerListMessage(msg, ref response);
                    break;
                case RegisterCustomGameMessage msg:
                    HandleRegisterCustomGameMessage(msg, ref response,message);
                    break;
                case EndHostingCustomGameMessage msg:
                    HandleEndHostingCustomGameMessage(msg, ref response,message);
                    break;
                default:
                    Console.WriteLine($"Unhandled MessageType Function: ${message.GetMessage().GetType().Name}");
                    break;
            }
        }

        private void HandleGetAnotherPlayerStateMessage(GetAnotherPlayerStateMessage msg,ref RestResponse response,
            RestDataRequestMessage message)
        {
            response.FunctionResult =
                new RestDataFunctionResult(new GetAnotherPlayerStateMessageResult(AnotherPlayerState.NotFound,0));
            response.SetSuccessful(true, "ResultFromServerTask");
        }

        private void HandleEndHostingCustomGameMessage(EndHostingCustomGameMessage msg, ref RestResponse response,
            RestDataRequestMessage message)
        {
            try
            {
                var user = _context.Users.Find(x => message.SessionCredentials.SessionKey == x.Id.SessionKey);
                if (user != null)
                {
                    user.Server = null;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            response.FunctionResult =
                new RestDataFunctionResult(new EndHostingCustomGameResult());
            response.SetSuccessful(true, "ResultFromServerTask");
        }

        private void HandleRegisterCustomGameMessage(RegisterCustomGameMessage msg, ref RestResponse response,
            RestDataRequestMessage message)
        {
            var user = this._context.Users.Find(x => x.Id.SessionKey == message.SessionCredentials.SessionKey);
            user.Server = new GameServerEntry( CustomBattleId.NewGuid(), msg.ServerName,
                this.HttpContext.Connection.RemoteIpAddress.ToString(), msg.Port, "EU", msg.GameModule, msg.GameType,
                msg.Map, 0, msg.MaxPlayerCount, true, false);
            Console.WriteLine($"New Server: {this.HttpContext.Connection.RemoteIpAddress.ToString()} {msg.Port}");
            response.FunctionResult =
                new RestDataFunctionResult(new RegisterCustomGameResult(true));
            response.SetSuccessful(true, "ResultFromServerTask");
            
        }

        private void HandleLogins(RestDataRequestMessage message, ref RestResponse response, RestRequestMessage request)
        {
            var loginMessage = message.GetMessage() as InitializeSession;
            
            var session = new SessionCredentials(loginMessage.PeerId, SessionKey.NewGuid());
  
            var playerdata = new PlayerData();
            playerdata.FillWithNewPlayer(loginMessage.PlayerId,new []{"FreeForAll","Captain","Siege","Duel","TeamDeathMatch","FreeForAll"});
            playerdata.LastPlayerName = loginMessage.PlayerName;
            playerdata.LastGameTypes = new[] {"Captain"};
            var user = new User {Id = session, QueuedMessages = new Queue<RestResponseMessage>(),PlayerData=playerdata};
            this._context.Users.Add(user);
            var initializeSessionResponse = new InitializeSessionResponse(playerdata,
                _context.Status);

            response.FunctionResult =
                new RestDataFunctionResult(new LoginResult(session.PeerId, session.SessionKey,
                    initializeSessionResponse));
            response.UserCertificate = session.SessionKey.ToByteArray();
            response.SetSuccessful(true, "ResultFromServerTask");
        }

        private void HandleMessage(RestDataRequestMessage message, ref RestResponse response)
        {
            var rawMessage = message.GetMessage();
            Console.WriteLine($"Handle ${rawMessage.GetType().Name}");
            switch (rawMessage)
            {
                case ChangeRegionMessage msg:
                    HandleChangeRegion(msg, ref response);
                    break;
                case ChangeGameTypesMessage msg:
                    HandleChangeGameTypesMessage(msg, ref response);
                    break;
                case GetServerStatusMessage msg:
                    response.EnqueueMessage(new RestDataResponseMessage(
                        new ServerStatusMessage(_context.Status)));
                    response.SetSuccessful(true,"yes");
                    break;
                case ResponseCustomGameClientConnectionMessage msg:
                    HandleResponseCustomGameClientConnectionMessage(msg, ref response, message);
                    break;
                case RequestJoinCustomGameMessage msg:
                    HandleRequestJoinCustomGameMessage(msg,ref response, message);
                    break;
                default:
                    Console.WriteLine($"Unhandled MessageType Message: ${message.GetMessage().GetType().Name}");
                    break;
            }
        }

        private void HandleResponseCustomGameClientConnectionMessage(ResponseCustomGameClientConnectionMessage msg, ref RestResponse response, RestDataRequestMessage message)
        {
            var Server = _context.Users.Find(x => x.Id.SessionKey == message.SessionCredentials.SessionKey).Server;
            foreach (var joindata in msg.PlayerJoinData)
            {
                var users = this._context.Users.FindAll(usr => joindata.PlayerId.ConvertToPeerId() == usr.Id.PeerId && usr.Server == null);
                switch (msg.Response)
                {
                    case CustomGameJoinResponse.Success:
                        users.ForEach(u=>u.QueuedMessages.Enqueue(new RestDataResponseMessage(JoinCustomGameResultMessage.CreateSuccess(new JoinGameData(new GameServerProperties(Server.ServerName,Server.Address,Server.Port,Server.Region,Server.GameModule,Server.GameType,Server.Map,"","",Server.MaxPlayerCount,Server.IsOfficial), joindata.PeerIndex,joindata.SessionKey) ))));

                        break;
                    case CustomGameJoinResponse.IncorrectPlayerState:
                    case CustomGameJoinResponse.ServerCapacityIsFull:
                    case CustomGameJoinResponse.ErrorOnGameServer:
                    case CustomGameJoinResponse.GameServerAccessError:
                    case CustomGameJoinResponse.CustomGameServerNotAvailable:
                    case CustomGameJoinResponse.CustomGameServerFinishing:
                    case CustomGameJoinResponse.IncorrectPassword:
                    case CustomGameJoinResponse.PlayerBanned:
                    case CustomGameJoinResponse.HostReplyTimedOut:
                    case CustomGameJoinResponse.NoPlayerDataFound:
                    case CustomGameJoinResponse.UnspecifiedError:
                    case CustomGameJoinResponse.NoPlayersCanJoin:
                    case CustomGameJoinResponse.AlreadyRequestedWaitingForServerResponse:
                    case CustomGameJoinResponse.RequesterIsNotPartyLeader:
                    case CustomGameJoinResponse.NotAllPlayersReady:
                    default:
                        users.ForEach(u=>u.QueuedMessages.Enqueue(new RestDataResponseMessage(JoinCustomGameResultMessage.CreateFailed(msg.Response))));
                    
                        break;
                }
            }
            response.SetSuccessful(true, "ResultFromServerTask");
        }

        // @TODO:  ClientWantsToConnectCustomGameMessage
        private void HandleRequestJoinCustomGameMessage(RequestJoinCustomGameMessage msg, ref RestResponse response, RestDataRequestMessage message)
        {
            User ServerOwner = _context.Users.Find(user => user.Server?.Id.Guid == msg.CustomBattleId.Guid);
            User JoiningUser = _context.Users.Find(user => user.Id.PeerId == message.SessionCredentials.PeerId);
            if(ServerOwner.Server != null) {
                
                var properties = new GameServerProperties(ServerOwner.Server.ServerName,ServerOwner.Server.Address,ServerOwner.Server.Port,ServerOwner.Server.Region,ServerOwner.Server.GameModule,ServerOwner.Server.GameType,ServerOwner.Server.Map,"","",ServerOwner.Server.MaxPlayerCount,ServerOwner.Server.IsOfficial);
                /*@TODO:
                //var result =  JoinCustomGameResultMessage.CreateSuccess(new JoinGameData(properties,0,0 ));
                ServerOwner.QueuedMessages.Add(new ClientWantsToConnectCustomGameMessage(new PlayerJoinGameData[]{new PlayerJoinGameData(JoiningUser.PlayerData,JoiningUser.PlayerData.LastPlayerName) },msg.Password ));
                
                response.EnqueueMessage(new RestDataResponseMessage(result));
                */
                ServerOwner.QueuedMessages.Enqueue(new RestDataResponseMessage(new ClientWantsToConnectCustomGameMessage(new PlayerJoinGameData[]{new PlayerJoinGameData(JoiningUser.PlayerData,JoiningUser.PlayerData.LastPlayerName) },msg.Password )));

                response.SetSuccessful(true, "ResultFromServerTask");
            }

            
            
        }

        private void HandleRequestCustomGameServerListMessage(RequestCustomGameServerListMessage msg,
            ref RestResponse response)
        {
            var serverList = new AvailableCustomGames();
            foreach (var server in this._context.Users.Select(u=>u.Server))
            {
                if(server != null)
                serverList.CustomGameServerInfos.Add(server);  // serverlist.CustomGameServerInfos.Add(new GameServerEntry());
            }
            
            var resp = new CustomGameServerListResponse(serverList);
            response.FunctionResult = new RestDataFunctionResult(resp);
            response.SetSuccessful(true, "ResultFromServerTask");
        }

        private void HandleGetPlayerBadgesMessage(GetPlayerBadgesMessage msg, ref RestResponse response)
        {
            response.FunctionResult =
                new RestDataFunctionResult(new GetPlayerBadgesMessageResult(new string[]
                    {"badge_taleworlds_primary_dev"}));
            response.SetSuccessful(true, "ResultFromServerTask");
        }

        private void HandleChangeGameTypesMessage(ChangeGameTypesMessage msg, ref RestResponse response)
        {
            response.SetSuccessful(true, "ResultFromServerTask");
        }

        private void HandleChangeRegion(ChangeRegionMessage message, ref RestResponse response)
        {
            response.SetSuccessful(true, "ResultFromServerTask");
        }
    }
}