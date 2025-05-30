using System;
using System.Net;
using System.Net.Sockets;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;

using FCGR.Server.GRPC.Services.General;
using FCGR.Server.GRPC.Services.Streaming;
using FCGR.Common.Libraries.Net.Networks;
using FCGR.Server.GRPC.Services.Processing;
using FCGR.Common.Utilities;

namespace FCGR.Server;

public class Program
{
	public static void Main(string[] args)
	{
		WebApplication app;
		WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
		AddressFamily address_family=AddressFamily.InterNetwork;
		ushort port_http = 5001;

		if(args.Length<0 || (args.Length==1 && (args[0]=="--help" || args[0]=="-help")))
		{
			Tracer.traceMessage("Please provide -port <number> and ip_type [v4|v6] arguments!", MESSAGE_SEVERITY.CRITICAL);
			return;
		}
		else
		{
			for(int i = 0, i_original; i<args.Length; i++)
			{
				var throwAndExit = (string argument_name) =>
				{
					Tracer.traceMessage($"Wrong value provided for argument {argument_name}", MESSAGE_SEVERITY.ERROR);
					Environment.Exit(1);
				};
				try
				{
					i_original=i;
					switch(args[i])
					{
						case "-ip_version":
							if(args[++i]=="6")
								address_family=AddressFamily.InterNetworkV6;
							else if(args[i]!="4")
								throwAndExit("-ip_version");
							break;
						case "-port":
							if(!UInt16.TryParse(args[++i], out port_http))
								throwAndExit("-port");
							break;
						default:
							break;
					}
				}
				catch(IndexOutOfRangeException)
				{
					string[] args_tmp = new string[args.Length+4];
					args.CopyTo(args_tmp, 0);
					for(int ii = args.Length; ii<args_tmp.Length; ii++)
						args_tmp[ii]="-";
					i--;
				}
			}
		}

		//server_udp = new UDPServer(port_http, address_family);	//TODO make UDPServer listen on different port

		builder.WebHost.ConfigureKestrel(options =>
		{
			IPAddress address = address_family==AddressFamily.InterNetworkV6 ? IPAddress.IPv6Loopback : IPAddress.Loopback;
			int port_https = port_http+1;
			options.Listen(address, port_http, options =>
			{
				options.Protocols = HttpProtocols.Http2;
			});
			options.Listen(address, port_https, options =>
			{
				options.Protocols = HttpProtocols.Http2;
				options.UseHttps();
			});
		});
		builder.Services.AddGrpc((options =>
		{
			options.MaxSendMessageSize = TCPNetwork.Buffer_size_max;
			options.MaxReceiveMessageSize = TCPNetwork.Buffer_size_max;
		}));
		builder.Services.AddSingleton(new ServiceUser());
		builder.Services.AddSingleton(new ServiceFrameStreaming());
		builder.Services.AddSingleton(new ServiceFrameProcessing());

		app = builder.Build(); 
		app.MapGrpcService<ServiceUser>();
		app.MapGrpcService<ServiceFrameStreaming>();
		app.MapGrpcService<ServiceFrameProcessing>();
		app.Run();
	}
}