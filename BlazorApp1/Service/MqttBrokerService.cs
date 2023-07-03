using BlazorApp1.Service.IService;
using Microsoft.AspNetCore.Hosting;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Server;

namespace BlazorApp1.Service
{
	public class MqttBrokerService : IMqttBrokerService
	{
		public async ValueTask ConnectAsync(IMqttClient client, string host, int port, string id)
		{
			var options = new MqttClientOptionsBuilder()
				.WithTcpServer(host, port)
				.WithCleanSession()
				.WithClientId(id)
				.Build();

			// 재연결
			client.DisconnectedAsync += async (e) =>
			{
				if (e.ClientWasConnected)
				{
					await client.ConnectAsync(client.Options);
				}
			};

			await client.ConnectAsync(options);
		}

		public void RegisterHandler(IMqttClient client, Func<MqttApplicationMessageReceivedEventArgs, Task> handler)
		{
			client.ApplicationMessageReceivedAsync += handler;
		}
	}
}
