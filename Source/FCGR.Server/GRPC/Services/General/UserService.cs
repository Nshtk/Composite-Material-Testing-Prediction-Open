using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Grpc.Core;

using FCGR.Proto.Common;
using FCGR.Proto.General;

namespace FCGR.Server.GRPC.Services.General;

public class ServiceUser : UserService.UserServiceBase
{
	public class User
	{
		public uint id;
		public string? name;
		public DateTime last_time_connected;
	}

	private uint _id_counter = 0;
	public static Dictionary<uint, User?> Users = new Dictionary<uint, User?>();

	public override Task<AuthorizeUserReply> authorizeUser(AuthorizeUserRequest request, ServerCallContext context)
	{
		AuthorizeUserReply reply = new AuthorizeUserReply
		{
			BasicReply = new BasicReply
			{
				Result = true,
				Message = new Message
				{
					Text = $"Connected as {request.UserName} #{_id_counter}.",
					Severity = Message.Types.MESSAGE_SEVERITY.Common
				}
			},
			Id = _id_counter
		};
		Users.Add(_id_counter, new User
		{
			id = _id_counter,
			name = request.UserName,
			last_time_connected = DateTime.Now
		});
		_id_counter++;

		return Task.FromResult(reply);
	}
	public override Task<BasicReply> keepAlive(BasicRequest request, ServerCallContext context)
	{
		if (Users[request.Id] == null)
		{
			return Task.FromResult(new BasicReply
			{
				Result = false,
				Message = new Message
				{
					Text = "Connection blocked, user is not authorized.",
					Severity = Message.Types.MESSAGE_SEVERITY.Critical
				}
			});
		}
		if (!Users.ContainsKey(request.Id))
		{
			Users.Add(_id_counter++, null);
			return Task.FromResult(new BasicReply
			{
				Result = false,
				Message = new Message
				{
					Text = "Connection blocked, user id not found.",
					Severity = Message.Types.MESSAGE_SEVERITY.Critical
				}
			});
		}

		Users[request.Id].last_time_connected = DateTime.Now;
		return Task.FromResult(new BasicReply
		{
			Result = true,
			Message = new Message
			{
				Text = "",
				Severity = Message.Types.MESSAGE_SEVERITY.Common
			}
		});
	}
}
