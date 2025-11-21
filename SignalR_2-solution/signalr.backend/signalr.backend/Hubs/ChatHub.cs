using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using signalr.backend.Data;
using signalr.backend.Models;

namespace signalr.backend.Hubs
{
    // On garde en mémoire les connexions actives
    // Note: Ce n'est pas nécessaire dans le TP
    public static class UserHandler
    {
        public static Dictionary<string, string> UserConnections { get; set; } = new Dictionary<string, string>();
    }

    // L'annotation Authorize fonctionne de la même façon avec SignalR qu'avec Web API
    [Authorize]
    // Le Hub est le type de base des "contrôleurs" de web sockets
    public class ChatHub : Hub
    {
        public ApplicationDbContext _context;


        public IdentityUser CurentUser
        {
            get
            {
                // TODO on récupère le userid à partir du Cookie qui devrait être envoyé automatiquement
                string userid = Context.UserIdentifier!;

                var user = _context.Users.Single(u => u.Id == userid);

                return user;
            }

        }

        public ChatHub(ApplicationDbContext context)
        {
            _context = context;
        }

        public async override Task OnConnectedAsync()
        {
            await JoinChat();
        }

        public async override Task OnDisconnectedAsync(Exception? exception)
        {
            // TODO Lors de la fermeture de la connexion, on met à jour notre dictionnary d'utilisateurs connectés
            KeyValuePair<string, string> entrie = UserHandler.UserConnections.SingleOrDefault(uc => uc.Value == Context.UserIdentifier);
            UserHandler.UserConnections.Remove(entrie.Key);
            await UserList();
        }

        private async Task JoinChat()
        {
            // TODO Context.ConnectionId est l'identifiant de la connection entre le web socket et l'utilisateur
            // TODO Ce sera utile pour créer des groups
            UserHandler.UserConnections.Add(CurentUser.Email!, Context.UserIdentifier);
            
            await UserList();
            await Clients.Caller.SendAsync("ChannelsList", _context.Channel.ToList());
        }

        public async Task CreateChannel(string title)
        {
            _context.Channel.Add(new Channel { Title = title });
            await _context.SaveChangesAsync();

            await Clients.All.SendAsync("ChannelsList", await _context.Channel.ToListAsync());
        }

        public async Task DeleteChannel(int channelId)
        {
            Channel channel = _context.Channel.Find(channelId);

            if(channel != null)
            {
                _context.Channel.Remove(channel);
                await _context.SaveChangesAsync();
            }
            string groupName = CreateChannelGroupName(channelId);
            await Clients.Group(groupName).SendAsync("NewMessage", "[" + channel.Title + "] a été détruit");
            await Clients.Group(groupName).SendAsync("LeaveChannel");
            await Clients.All.SendAsync("ChannelsList", await _context.Channel.ToListAsync());
        }

        public async Task UserList()
        {
            // TODO On envoie un évènement de type UserList à tous les Utilisateurs
            // TODO On peut envoyer en paramètre tous les types que l'om veut,
            // ici serHandler.UserConnections.Keys correspond à la liste de tous les emails des utilisateurs connectés
            await Clients.All.SendAsync("UsersList", UserHandler.UserConnections.ToList());
        }

        public async Task JoinChannel(int oldChannelId, int newChannelId)
        {
            string userTag = "[" + CurentUser.Email! + "]";

            if(oldChannelId > 0)
            {
                string oldGroupName = CreateChannelGroupName(oldChannelId);
                Channel channel = _context.Channel.Find(oldChannelId);
                string message = userTag + " quitte: " + channel.Title;
                await Clients.Group(oldGroupName).SendAsync("NewMessage", message);
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, oldGroupName);
            }
            
            if(newChannelId > 0)
            {
                string newGroupName = CreateChannelGroupName(newChannelId);
                await Groups.AddToGroupAsync(Context.ConnectionId, newGroupName);

                Channel channel = _context.Channel.Find(newChannelId);
                string message = userTag + " a rejoint : " + channel.Title;
                await Clients.Group(newGroupName).SendAsync("NewMessage", message);
            }
        }

        public async Task SendMessage(string message, int channelId, string userId)
        {
            if (userId != null)
            {
                string messageWithTag = "[De: " + CurentUser.Email! + "] " + message;
                await Clients.User(userId).SendAsync("NewMessage", messageWithTag);
            }
            else if (channelId != 0)
            {
                string groupName = CreateChannelGroupName(channelId);
                Channel channel = _context.Channel.Find(channelId);
                channel.NbMessages++;
                await _context.SaveChangesAsync();
                await Clients.Group(groupName).SendAsync("NewMessage", "[" + channel.Title + "] " + message);
            }
            else
            {
                await Clients.All.SendAsync("NewMessage", "[Tous] " + message);
            }
        }

        private static string CreateChannelGroupName(int channelId)
        {
            return "Channel" + channelId;
        }

        
    }
}