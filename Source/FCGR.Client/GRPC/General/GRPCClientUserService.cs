using System;
using System.Threading.Tasks;

using Grpc.Core;

using FCGR.Proto;
using FCGR.Proto.General;
using System.Net;
using FCGR.Client.Clients;

namespace FCGR.Client.GRPC.General;

public class GRPCClientUser : GRPCClient<UserService.UserServiceClient>
{
    private AuthorizeUserRequest _authorize_user_request = new AuthorizeUserRequest();

    public GRPCClientUser(IPEndPoint address_connected_to) : base(address_connected_to)
    {

    }

    public AuthorizeUserReply authorizeUser(string username)
    {
        _authorize_user_request.UserName = username;

        return _client.authorizeUser(_authorize_user_request);
    }
    public async Task<AuthorizeUserReply> authorizeUserAsync(string username)
    {
        _authorize_user_request.UserName = username;

        return await _client.authorizeUserAsync(_authorize_user_request);
    }
}
